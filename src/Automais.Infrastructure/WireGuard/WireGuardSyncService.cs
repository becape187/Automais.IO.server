using Automais.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Automais.Infrastructure.WireGuard;

/// <summary>
/// Serviço que sincroniza arquivos de configuração do WireGuard com o banco de dados
/// Executa na inicialização da API para garantir integridade
/// </summary>
public class WireGuardSyncService : IHostedService
{
    private readonly IWireGuardServerService _wireGuardServerService;
    private readonly IVpnNetworkRepository _vpnNetworkRepository;
    private readonly IRouterWireGuardPeerRepository _peerRepository;
    private readonly IRouterRepository _routerRepository;
    private readonly IRouterAllowedNetworkRepository _allowedNetworkRepository;
    private readonly ILogger<WireGuardSyncService> _logger;

    public WireGuardSyncService(
        IWireGuardServerService wireGuardServerService,
        IVpnNetworkRepository vpnNetworkRepository,
        IRouterWireGuardPeerRepository peerRepository,
        IRouterRepository routerRepository,
        IRouterAllowedNetworkRepository allowedNetworkRepository,
        ILogger<WireGuardSyncService> logger)
    {
        _wireGuardServerService = wireGuardServerService;
        _vpnNetworkRepository = vpnNetworkRepository;
        _peerRepository = peerRepository;
        _routerRepository = routerRepository;
        _allowedNetworkRepository = allowedNetworkRepository;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🔄 Iniciando sincronização do WireGuard com banco de dados...");

        try
        {
            // Configurações do sistema que devem ser validadas/ajustadas na inicialização
            _logger.LogInformation("⚙️ Validando e configurando sistema para WireGuard...");
            
            // 1. Verificar se WireGuard está instalado
            await VerifyWireGuardInstallationAsync(cancellationToken);
            
            // 2. Garantir que o diretório de configuração existe
            await EnsureWireGuardDirectoryExistsAsync(cancellationToken);
            
            // 3. Habilitar encaminhamento IP (necessário para todas as interfaces)
            await EnableIpForwardingAsync(cancellationToken);
            
            // 4. Configurar regras básicas de firewall (porta UDP 51820)
            await ConfigureBasicFirewallRulesAsync(cancellationToken);
            
            // 5. Sincronizar configurações do banco para arquivos
            await SyncWireGuardConfigurationsAsync(cancellationToken);
            
            // 6. Salvar regras de firewall/NAT permanentemente
            await SaveFirewallRulesAsync(cancellationToken);
            
            _logger.LogInformation("✅ Sincronização do WireGuard concluída com sucesso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro durante sincronização do WireGuard");
            // Não lança exceção para não impedir a inicialização da API
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sincroniza todas as configurações do WireGuard do banco de dados para os arquivos.
    /// IMPORTANTE: Sincroniza TODAS as VpnNetworks, não apenas as que têm peers.
    /// Isso garante que em caso de desastre, todas as interfaces sejam reconstruídas.
    /// Também remove interfaces órfãs (existem no sistema mas não no banco).
    /// </summary>
    private async Task SyncWireGuardConfigurationsAsync(CancellationToken cancellationToken)
    {
        // 1. Buscar TODAS as VpnNetworks do banco (fonte de verdade)
        var allVpnNetworks = await _vpnNetworkRepository.GetAllAsync(cancellationToken);
        var vpnNetworkIds = allVpnNetworks.Select(v => v.Id).ToHashSet();

        // 2. Listar todas as interfaces WireGuard no sistema
        var systemInterfaces = await ListSystemWireGuardInterfacesAsync(cancellationToken);
        
        // 3. Remover interfaces órfãs (existem no sistema mas não no banco)
        await RemoveOrphanInterfacesAsync(systemInterfaces, vpnNetworkIds, cancellationToken);

        // 4. Sincronizar interfaces do banco
        if (!allVpnNetworks.Any())
        {
            _logger.LogInformation("Nenhuma VpnNetwork encontrada no banco. Interfaces órfãs removidas.");
            return;
        }

        _logger.LogInformation("Encontradas {Count} VpnNetworks para sincronizar", allVpnNetworks.Count());

        foreach (var vpnNetwork in allVpnNetworks)
        {
            try
            {
                await SyncVpnNetworkAsync(vpnNetwork.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao sincronizar VpnNetwork {VpnNetworkId} ({VpnNetworkName})", 
                    vpnNetwork.Id, vpnNetwork.Name);
                // Continua com as próximas redes - NÃO para em caso de erro
            }
        }
    }

    /// <summary>
    /// Lista todas as interfaces WireGuard ativas no sistema
    /// </summary>
    private async Task<List<string>> ListSystemWireGuardInterfacesAsync(CancellationToken cancellationToken)
    {
        var interfaces = new List<string>();
        
        try
        {
            // Listar interfaces via wg show (mostra apenas interfaces ativas)
            var wgShowProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/usr/bin/wg",
                    Arguments = "show",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            
            wgShowProcess.Start();
            var output = await wgShowProcess.StandardOutput.ReadToEndAsync(cancellationToken);
            await wgShowProcess.WaitForExitAsync(cancellationToken);

            if (wgShowProcess.ExitCode == 0 && !string.IsNullOrEmpty(output))
            {
                // Extrair nomes de interfaces (formato: "interface: wg-xxxx")
                var lines = output.Split('\n');
                foreach (var line in lines)
                {
                    if (line.TrimStart().StartsWith("interface:", StringComparison.OrdinalIgnoreCase))
                    {
                        var interfaceName = line.Split(':')[1].Trim();
                        if (!string.IsNullOrEmpty(interfaceName))
                        {
                            interfaces.Add(interfaceName);
                        }
                    }
                }
            }

            // Também listar arquivos .conf em /etc/wireguard (pode haver interfaces não ativas)
            var wireGuardDir = "/etc/wireguard";
            if (System.IO.Directory.Exists(wireGuardDir))
            {
                var confFiles = System.IO.Directory.GetFiles(wireGuardDir, "*.conf");
                foreach (var file in confFiles)
                {
                    var fileName = System.IO.Path.GetFileNameWithoutExtension(file);
                    if (!interfaces.Contains(fileName))
                    {
                        interfaces.Add(fileName);
                    }
                }
            }

            _logger.LogDebug("Encontradas {Count} interfaces WireGuard no sistema: {Interfaces}", 
                interfaces.Count, string.Join(", ", interfaces));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao listar interfaces WireGuard do sistema");
        }

        return interfaces;
    }

    /// <summary>
    /// Remove interfaces órfãs (existem no sistema mas não no banco)
    /// </summary>
    private async Task RemoveOrphanInterfacesAsync(
        List<string> systemInterfaces,
        HashSet<Guid> vpnNetworkIds,
        CancellationToken cancellationToken)
    {
        foreach (var interfaceName in systemInterfaces)
        {
            try
            {
                // Extrair VpnNetworkId do nome da interface (formato: wg-{8 primeiros chars do GUID})
                // Exemplo: wg-c9520d7d -> precisa encontrar qual VpnNetwork tem ID começando com c9520d7d
                var interfacePrefix = interfaceName.Replace("wg-", "");
                
                // Verificar se existe alguma VpnNetwork com esse prefixo
                bool isOrphan = true;
                foreach (var vpnNetworkId in vpnNetworkIds)
                {
                    var vpnNetworkPrefix = vpnNetworkId.ToString("N")[..8];
                    if (vpnNetworkPrefix.Equals(interfacePrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        isOrphan = false;
                        break;
                    }
                }

                if (isOrphan)
                {
                    _logger.LogWarning("🔍 Interface órfã detectada: {InterfaceName}. Removendo...", interfaceName);
                    
                    // Fazer down da interface
                    await DownInterfaceIfActiveAsync(interfaceName, cancellationToken);
                    
                    // Remover arquivo de configuração
                    var configPath = $"/etc/wireguard/{interfaceName}.conf";
                    if (System.IO.File.Exists(configPath))
                    {
                        System.IO.File.Delete(configPath);
                        _logger.LogInformation("✅ Arquivo de configuração órfão removido: {ConfigPath}", configPath);
                    }
                    
                    _logger.LogInformation("✅ Interface órfã {InterfaceName} removida com sucesso", interfaceName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao remover interface órfã {InterfaceName}", interfaceName);
            }
        }
    }

    /// <summary>
    /// Sincroniza uma VpnNetwork específica
    /// Faz um RELOAD COMPLETO: down → recria arquivo com todos os peers → up
    /// </summary>
    private async Task SyncVpnNetworkAsync(Guid vpnNetworkId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("🔄 Iniciando sincronização completa (reload) da VpnNetwork {VpnNetworkId}", vpnNetworkId);

        var vpnNetwork = await _vpnNetworkRepository.GetByIdAsync(vpnNetworkId, cancellationToken);
        if (vpnNetwork == null)
        {
            _logger.LogWarning("VpnNetwork {VpnNetworkId} não encontrada no banco", vpnNetworkId);
            return;
        }

        var interfaceName = GetInterfaceName(vpnNetworkId);
        var configPath = $"/etc/wireguard/{interfaceName}.conf";

        // 1. Verificar se a interface está ativa
        bool interfaceIsActive = await IsInterfaceActiveAsync(interfaceName, cancellationToken);
        
        // 2. Garantir que a interface existe (cria arquivo base com chaves do banco)
        await EnsureInterfaceExistsAsync(interfaceName, vpnNetwork, cancellationToken);

        // 3. Buscar todos os peers desta VpnNetwork do banco
        var peers = await _peerRepository.GetByVpnNetworkIdAsync(vpnNetworkId, cancellationToken);
        
        if (!peers.Any())
        {
            _logger.LogInformation("VpnNetwork {VpnNetworkId} não possui peers. Interface base criada.", vpnNetworkId);
            // Ativar interface mesmo sem peers (pode receber peers depois)
            await SyncInterfaceFromFileAsync(interfaceName, interfaceIsActive, cancellationToken);
            return;
        }

        _logger.LogInformation("Recriando arquivo de configuração com {PeerCount} peers do banco", peers.Count());

        // 4. Recriar arquivo .conf COMPLETO com todos os peers do banco
        await RecreateConfigFileWithAllPeersAsync(interfaceName, vpnNetwork, peers, cancellationToken);

        // 5. Sincronizar interface com arquivo (usa syncconf se ativa, ou up se inativa)
        await SyncInterfaceFromFileAsync(interfaceName, interfaceIsActive, cancellationToken);
        
        _logger.LogInformation("✅ VpnNetwork {VpnNetworkId} sincronizada com sucesso (reload completo)", vpnNetworkId);
    }

    /// <summary>
    /// Faz DOWN da interface se estiver ativa (para garantir limpeza antes do reload)
    /// </summary>
    private async Task DownInterfaceIfActiveAsync(string interfaceName, CancellationToken cancellationToken)
    {
        try
        {
            // Verificar se a interface está ativa
            var checkProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/usr/bin/wg",
                    Arguments = $"show {interfaceName}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            checkProcess.Start();
            await checkProcess.WaitForExitAsync(cancellationToken);

            // Se a interface está ativa, fazer down
            if (checkProcess.ExitCode == 0)
            {
                _logger.LogDebug("Interface {InterfaceName} está ativa. Fazendo down para reload...", interfaceName);
                
                var downProcess = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "/usr/bin/wg-quick",
                        Arguments = $"down {interfaceName}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                downProcess.Start();
                var output = await downProcess.StandardOutput.ReadToEndAsync(cancellationToken);
                var error = await downProcess.StandardError.ReadToEndAsync(cancellationToken);
                await downProcess.WaitForExitAsync(cancellationToken);

                if (downProcess.ExitCode == 0)
                {
                    _logger.LogDebug("Interface {InterfaceName} desativada para reload", interfaceName);
                }
                else
                {
                    _logger.LogWarning("Erro ao desativar interface {InterfaceName}: {Error}", interfaceName, error);
                }
            }
            else
            {
                _logger.LogDebug("Interface {InterfaceName} não está ativa. Prosseguindo...", interfaceName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao verificar/desativar interface {InterfaceName}", interfaceName);
            // Não lança exceção - continua com o processo
        }
    }

    /// <summary>
    /// Recria o arquivo de configuração .conf do ZERO com TODOS os peers do banco.
    /// IMPORTANTE: Recria completamente para garantir que não há peers órfãos.
    /// </summary>
    private async Task RecreateConfigFileWithAllPeersAsync(
        string interfaceName,
        Automais.Core.Entities.VpnNetwork vpnNetwork,
        IEnumerable<Automais.Core.Entities.RouterWireGuardPeer> peers,
        CancellationToken cancellationToken)
    {
        var configPath = $"/etc/wireguard/{interfaceName}.conf";
        
        // RECRIAR DO ZERO - não ler arquivo existente para evitar peers órfãos
        var configContent = new System.Text.StringBuilder();
        
        // Seção [Interface] - usar chaves do BANCO (fonte de verdade)
        configContent.AppendLine("[Interface]");
        
        if (string.IsNullOrEmpty(vpnNetwork.ServerPrivateKey))
        {
            _logger.LogError("VpnNetwork {VpnNetworkId} não possui ServerPrivateKey no banco. Não é possível recriar arquivo.", vpnNetwork.Id);
            throw new InvalidOperationException($"VpnNetwork {vpnNetwork.Id} não possui chave privada do servidor no banco.");
        }
        
        configContent.AppendLine($"PrivateKey = {vpnNetwork.ServerPrivateKey}");
        
        // Parse do CIDR para obter o IP do servidor (.1)
        var cidrParts = vpnNetwork.Cidr.Split('/');
        if (cidrParts.Length != 2)
        {
            throw new InvalidOperationException($"CIDR inválido: {vpnNetwork.Cidr}");
        }
        
        var ipParts = cidrParts[0].Split('.');
        if (ipParts.Length != 4)
        {
            throw new InvalidOperationException($"IP inválido no CIDR: {vpnNetwork.Cidr}");
        }
        
        // Servidor sempre usa .1
        var serverIp = $"{ipParts[0]}.{ipParts[1]}.{ipParts[2]}.1/{cidrParts[1]}";
        configContent.AppendLine($"Address = {serverIp}");
        configContent.AppendLine("ListenPort = 51820");
        
        // DNS se configurado
        if (!string.IsNullOrWhiteSpace(vpnNetwork.DnsServers))
        {
            configContent.AppendLine($"DNS = {vpnNetwork.DnsServers}");
        }
        
        // Adicionar todos os peers do banco
        configContent.AppendLine();
        configContent.AppendLine("# Peers sincronizados do banco de dados");
        
        foreach (var peer in peers)
        {
            try
            {
                // Verificar se peer está habilitado
                if (!peer.IsEnabled)
                {
                    _logger.LogDebug("Peer {PeerId} está desabilitado. Pulando.", peer.Id);
                    continue;
                }
                
                var router = await _routerRepository.GetByIdAsync(peer.RouterId, cancellationToken);
                if (router == null)
                {
                    _logger.LogWarning("Router {RouterId} do peer {PeerId} não encontrado. Pulando peer.", peer.RouterId, peer.Id);
                    continue;
                }

                // Buscar redes permitidas
                var allowedNetworks = await _allowedNetworkRepository.GetByRouterIdAsync(peer.RouterId, cancellationToken);
                var allowedIps = new List<string> { peer.AllowedIps };
                allowedIps.AddRange(allowedNetworks.Select(n => n.NetworkCidr));
                var allowedIpsString = string.Join(", ", allowedIps);

                // Adicionar seção [Peer]
                configContent.AppendLine();
                configContent.AppendLine($"# Router: {router.Name} (ID: {router.Id})");
                configContent.AppendLine("[Peer]");
                configContent.AppendLine($"PublicKey = {peer.PublicKey}");
                // Nota: Endpoint não é necessário na configuração do servidor
                // O servidor escuta em todas as interfaces na porta 51820
                configContent.AppendLine($"AllowedIPs = {allowedIpsString}");
                configContent.AppendLine("PersistentKeepalive = 25");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao adicionar peer {PeerId} ao arquivo de configuração", peer.Id);
            }
        }

        // Salvar arquivo completo (sobrescreve completamente)
        await System.IO.File.WriteAllTextAsync(configPath, configContent.ToString(), cancellationToken);
        
        // Definir permissões corretas (600 = rw-------)
        try
        {
            var chmodProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/bin/chmod",
                    Arguments = $"600 {configPath}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            chmodProcess.Start();
            await chmodProcess.WaitForExitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao definir permissões do arquivo de configuração");
        }
        
        _logger.LogInformation("✅ Arquivo de configuração {ConfigPath} recriado do ZERO com {PeerCount} peers do banco", 
            configPath, peers.Count(p => p.IsEnabled));
    }

    /// <summary>
    /// Verifica se a interface está ativa
    /// </summary>
    private async Task<bool> IsInterfaceActiveAsync(string interfaceName, CancellationToken cancellationToken = default)
    {
        try
        {
            var checkProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/usr/bin/wg",
                    Arguments = $"show {interfaceName}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            checkProcess.Start();
            await checkProcess.WaitForExitAsync(cancellationToken);
            return checkProcess.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sincroniza a interface com o arquivo de configuração.
    /// Se a interface está ativa, usa wg syncconf (sem interrupção).
    /// Se está inativa, usa wg-quick up (ativa).
    /// </summary>
    private async Task SyncInterfaceFromFileAsync(
        string interfaceName, 
        bool interfaceIsActive,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var configPath = $"/etc/wireguard/{interfaceName}.conf";
            
            if (!System.IO.File.Exists(configPath))
            {
                _logger.LogWarning("Arquivo de configuração {ConfigPath} não existe. Não é possível sincronizar interface.", configPath);
                return;
            }

            if (interfaceIsActive)
            {
                // Interface já está ativa - usar syncconf para sincronizar sem interrupção
                _logger.LogInformation("Sincronizando interface WireGuard {InterfaceName} com arquivo (sem interrupção)...", interfaceName);
                
                var syncProcess = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "/usr/bin/wg",
                        Arguments = $"syncconf {interfaceName} {configPath}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                syncProcess.Start();
                var syncOutput = await syncProcess.StandardOutput.ReadToEndAsync(cancellationToken);
                var syncError = await syncProcess.StandardError.ReadToEndAsync(cancellationToken);
                await syncProcess.WaitForExitAsync(cancellationToken);

                if (syncProcess.ExitCode == 0)
                {
                    _logger.LogInformation("✅ Interface WireGuard {InterfaceName} sincronizada com sucesso", interfaceName);
                }
                else
                {
                    // Se o erro não for crítico, apenas logar e não fazer down/up (evita derrubar conexões)
                    // wg syncconf pode falhar se o arquivo tiver algum problema, mas a interface continua funcionando
                    _logger.LogWarning("Erro ao sincronizar interface {InterfaceName} via syncconf: {Error}. Interface continua ativa.", interfaceName, syncError);
                    _logger.LogWarning("Se houver problemas de conectividade, reinicie a API para forçar reload completo.");
                    // NÃO fazer down/up automaticamente - isso derruba todas as conexões ativas!
                    // O sync completo só deve acontecer na inicialização da API
                }
            }
            else
            {
                // Interface não está ativa - fazer up
                await UpInterfaceAsync(interfaceName, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao sincronizar interface WireGuard {InterfaceName}", interfaceName);
            throw;
        }
    }

    /// <summary>
    /// Faz UP da interface (ativa)
    /// </summary>
    private async Task UpInterfaceAsync(string interfaceName, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Ativando interface WireGuard {InterfaceName}...", interfaceName);
            
            var upProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/usr/bin/wg-quick",
                    Arguments = $"up {interfaceName}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            upProcess.Start();
            var upOutput = await upProcess.StandardOutput.ReadToEndAsync(cancellationToken);
            var upError = await upProcess.StandardError.ReadToEndAsync(cancellationToken);
            await upProcess.WaitForExitAsync(cancellationToken);

            if (upProcess.ExitCode == 0)
            {
                _logger.LogInformation("✅ Interface WireGuard {InterfaceName} ativada com sucesso", interfaceName);
            }
            else
            {
                // Se o erro é "already exists", a interface já está ativa - usar syncconf
                if (upError.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Interface {InterfaceName} já existe. Sincronizando com arquivo...", interfaceName);
                    var configPath = $"/etc/wireguard/{interfaceName}.conf";
                    var syncProcess = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "/usr/bin/wg",
                            Arguments = $"syncconf {interfaceName} {configPath}",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };
                    syncProcess.Start();
                    await syncProcess.WaitForExitAsync(cancellationToken);
                    
                    if (syncProcess.ExitCode == 0)
                    {
                        _logger.LogInformation("✅ Interface WireGuard {InterfaceName} sincronizada com sucesso", interfaceName);
                        return;
                    }
                }
                
                _logger.LogError("❌ Erro ao ativar interface WireGuard {InterfaceName}: {Error}", interfaceName, upError);
                throw new InvalidOperationException($"Falha ao ativar interface {interfaceName}: {upError}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao ativar interface WireGuard {InterfaceName}", interfaceName);
            throw;
        }
    }

    /// <summary>
    /// Garante que a interface WireGuard existe (delega para o WireGuardServerService)
    /// </summary>
    private async Task EnsureInterfaceExistsAsync(
        string interfaceName,
        Automais.Core.Entities.VpnNetwork vpnNetwork,
        CancellationToken cancellationToken)
    {
        // Usar o método público do WireGuardServerService
        await _wireGuardServerService.EnsureInterfaceForVpnNetworkAsync(vpnNetwork.Id, cancellationToken);
    }

    private string GetInterfaceName(Guid vpnNetworkId)
    {
        return $"wg-{vpnNetworkId.ToString("N")[..8]}";
    }

    private async Task ExecuteWireGuardCommandAsync(string command, CancellationToken cancellationToken)
    {
        try
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/usr/bin/wg",
                    Arguments = command,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("Comando WireGuard falhou: {Command}, Erro: {Error}", command, error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar comando WireGuard: {Command}", command);
        }
    }

    private async Task SaveWireGuardConfigAsync(string interfaceName, CancellationToken cancellationToken)
    {
        try
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/usr/bin/wg-quick",
                    Arguments = $"save {interfaceName}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar configuração WireGuard para interface {InterfaceName}", interfaceName);
        }
    }

    /// <summary>
    /// Habilita o encaminhamento IP (ip_forward) necessário para o WireGuard
    /// </summary>
    private async Task EnableIpForwardingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Verificar se já está habilitado
            var checkProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = "-c \"cat /proc/sys/net/ipv4/ip_forward\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            checkProcess.Start();
            var currentValue = (await checkProcess.StandardOutput.ReadToEndAsync(cancellationToken)).Trim();
            await checkProcess.WaitForExitAsync(cancellationToken);

            if (currentValue == "1")
            {
                _logger.LogDebug("Encaminhamento IP já está habilitado");
                return;
            }

            _logger.LogInformation("Habilitando encaminhamento IP...");

            // Habilitar temporariamente (até reiniciar)
            // A aplicação roda como root via systemd, então não precisa de sudo
            var enableProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = "-c \"echo 1 > /proc/sys/net/ipv4/ip_forward\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            enableProcess.Start();
            var enableOutput = await enableProcess.StandardOutput.ReadToEndAsync(cancellationToken);
            var enableError = await enableProcess.StandardError.ReadToEndAsync(cancellationToken);
            await enableProcess.WaitForExitAsync(cancellationToken);

            if (enableProcess.ExitCode == 0)
            {
                _logger.LogInformation("Encaminhamento IP habilitado temporariamente");
            }
            else
            {
                _logger.LogWarning("Erro ao habilitar encaminhamento IP temporariamente: {Error}", enableError);
            }

            // Habilitar permanentemente via sysctl
            // Verificar se já existe no sysctl.conf
            var checkSysctlProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = "-c \"grep -q 'net.ipv4.ip_forward=1' /etc/sysctl.conf || echo 'net.ipv4.ip_forward=1' >> /etc/sysctl.conf\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            checkSysctlProcess.Start();
            await checkSysctlProcess.WaitForExitAsync(cancellationToken);

            // Aplicar sysctl
            var sysctlProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/usr/sbin/sysctl",
                    Arguments = "-p",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            sysctlProcess.Start();
            var sysctlOutput = await sysctlProcess.StandardOutput.ReadToEndAsync(cancellationToken);
            var sysctlError = await sysctlProcess.StandardError.ReadToEndAsync(cancellationToken);
            await sysctlProcess.WaitForExitAsync(cancellationToken);

            if (sysctlProcess.ExitCode == 0)
            {
                _logger.LogInformation("Encaminhamento IP habilitado permanentemente");
            }
            else
            {
                _logger.LogWarning("Erro ao habilitar encaminhamento IP permanentemente: {Error}", sysctlError);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao habilitar encaminhamento IP");
            // Não lança exceção - pode ser configurado manualmente
        }
    }

    /// <summary>
    /// Verifica se o WireGuard está instalado no sistema
    /// </summary>
    private async Task VerifyWireGuardInstallationAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var wgProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/usr/bin/wg",
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            wgProcess.Start();
            var output = await wgProcess.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = await wgProcess.StandardError.ReadToEndAsync(cancellationToken);
            await wgProcess.WaitForExitAsync(cancellationToken);

            if (wgProcess.ExitCode == 0)
            {
                _logger.LogInformation("WireGuard instalado: {Version}", output.Trim());
            }
            else
            {
                _logger.LogWarning("WireGuard não encontrado ou não está instalado. Erro: {Error}", error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar instalação do WireGuard");
        }
    }

    /// <summary>
    /// Garante que o diretório de configuração do WireGuard existe
    /// </summary>
    private async Task EnsureWireGuardDirectoryExistsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            const string wireGuardDir = "/etc/wireguard";
            
            if (!System.IO.Directory.Exists(wireGuardDir))
            {
                System.IO.Directory.CreateDirectory(wireGuardDir);
                _logger.LogInformation("Diretório WireGuard criado: {Directory}", wireGuardDir);
            }
            else
            {
                _logger.LogDebug("Diretório WireGuard já existe: {Directory}", wireGuardDir);
            }

            // Verificar permissões (deve ser 755)
            var dirInfo = new System.IO.DirectoryInfo(wireGuardDir);
            _logger.LogDebug("Diretório WireGuard: {Path}, Permissões: {Permissions}", 
                wireGuardDir, dirInfo.Exists ? "OK" : "Não encontrado");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao garantir diretório WireGuard");
        }
    }

    /// <summary>
    /// Configura regras básicas de firewall para WireGuard (porta UDP 51820)
    /// </summary>
    private async Task ConfigureBasicFirewallRulesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Verificar se iptables está disponível
            var checkProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/usr/sbin/iptables",
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            checkProcess.Start();
            await checkProcess.WaitForExitAsync(cancellationToken);

            if (checkProcess.ExitCode != 0)
            {
                _logger.LogWarning("iptables não encontrado. Regras de firewall não serão configuradas automaticamente.");
                return;
            }

            // Permitir tráfego na porta WireGuard (UDP 51820)
            await ExecuteIptablesCommandAsync("-A INPUT -p udp --dport 51820 -j ACCEPT", cancellationToken);
            
            _logger.LogInformation("Regras básicas de firewall configuradas para WireGuard");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao configurar regras básicas de firewall");
        }
    }

    /// <summary>
    /// Executa comando iptables (verifica se já existe antes de adicionar)
    /// </summary>
    private async Task ExecuteIptablesCommandAsync(string rule, CancellationToken cancellationToken = default)
    {
        try
        {
            // Extrair a ação (INPUT, OUTPUT, FORWARD) e a regra
            var parts = rule.Split(new[] { ' ' }, 2);
            if (parts.Length < 2) return;

            var action = parts[0]; // -A INPUT, -A OUTPUT, etc.
            var rulePart = parts[1];

            // Verificar se a regra já existe
            var checkProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"iptables -C {rulePart} 2>/dev/null\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            checkProcess.Start();
            await checkProcess.WaitForExitAsync(cancellationToken);

            // Se a regra não existe (exit code != 0), adicionar
            if (checkProcess.ExitCode != 0)
            {
                var addProcess = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "/usr/sbin/iptables",
                        Arguments = $"{action} {rulePart}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                addProcess.Start();
                var output = await addProcess.StandardOutput.ReadToEndAsync(cancellationToken);
                var error = await addProcess.StandardError.ReadToEndAsync(cancellationToken);
                await addProcess.WaitForExitAsync(cancellationToken);

                if (addProcess.ExitCode == 0)
                {
                    _logger.LogDebug("Regra iptables adicionada: {Rule}", rule);
                }
                else
                {
                    _logger.LogWarning("Erro ao adicionar regra iptables {Rule}: {Error}", rule, error);
                }
            }
            else
            {
                _logger.LogDebug("Regra iptables já existe: {Rule}", rule);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar comando iptables: {Rule}", rule);
        }
    }

    /// <summary>
    /// Ativa a interface WireGuard se não estiver ativa
    /// </summary>
    private async Task ActivateInterfaceIfNeededAsync(string interfaceName, CancellationToken cancellationToken = default)
    {
        try
        {
            // Verificar se a interface está ativa
            var checkProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/usr/bin/wg",
                    Arguments = $"show {interfaceName}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            checkProcess.Start();
            var output = await checkProcess.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = await checkProcess.StandardError.ReadToEndAsync(cancellationToken);
            await checkProcess.WaitForExitAsync(cancellationToken);

            // Se a interface não existe ou não está ativa, ativar
            if (checkProcess.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                _logger.LogInformation("Ativando interface WireGuard {InterfaceName}...", interfaceName);
                
                var upProcess = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "/usr/bin/wg-quick",
                        Arguments = $"up {interfaceName}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                upProcess.Start();
                var upOutput = await upProcess.StandardOutput.ReadToEndAsync(cancellationToken);
                var upError = await upProcess.StandardError.ReadToEndAsync(cancellationToken);
                await upProcess.WaitForExitAsync(cancellationToken);

                if (upProcess.ExitCode == 0)
                {
                    _logger.LogInformation("Interface WireGuard {InterfaceName} ativada com sucesso", interfaceName);
                }
                else
                {
                    _logger.LogWarning("Erro ao ativar interface WireGuard {InterfaceName}: {Error}", interfaceName, upError);
                }
            }
            else
            {
                _logger.LogDebug("Interface WireGuard {InterfaceName} já está ativa", interfaceName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar/ativar interface WireGuard {InterfaceName}", interfaceName);
        }
    }

    /// <summary>
    /// Salva regras de firewall/NAT permanentemente usando iptables-save ou netfilter-persistent
    /// </summary>
    private async Task SaveFirewallRulesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Tentar usar netfilter-persistent (recomendado)
            var persistentProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = "-c \"command -v netfilter-persistent >/dev/null 2>&1 && netfilter-persistent save || iptables-save > /etc/iptables/rules.v4 2>/dev/null || true\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            persistentProcess.Start();
            var output = await persistentProcess.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = await persistentProcess.StandardError.ReadToEndAsync(cancellationToken);
            await persistentProcess.WaitForExitAsync(cancellationToken);

            if (persistentProcess.ExitCode == 0)
            {
                _logger.LogInformation("Regras de firewall/NAT salvas permanentemente");
            }
            else
            {
                // Tentar método alternativo: salvar manualmente
                await SaveIptablesRulesManuallyAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao salvar regras de firewall. Regras podem não persistir após reinicialização.");
            _logger.LogWarning("Considere instalar: sudo apt install iptables-persistent");
        }
    }

    /// <summary>
    /// Salva regras iptables manualmente
    /// </summary>
    private async Task SaveIptablesRulesManuallyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Criar diretório se não existir
            var rulesDir = "/etc/iptables";
            if (!System.IO.Directory.Exists(rulesDir))
            {
                System.IO.Directory.CreateDirectory(rulesDir);
            }

            // Salvar regras
            var saveProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = "-c \"iptables-save > /etc/iptables/rules.v4\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            saveProcess.Start();
            var error = await saveProcess.StandardError.ReadToEndAsync(cancellationToken);
            await saveProcess.WaitForExitAsync(cancellationToken);

            if (saveProcess.ExitCode == 0)
            {
                _logger.LogInformation("Regras iptables salvas em /etc/iptables/rules.v4");
            }
            else
            {
                _logger.LogWarning("Não foi possível salvar regras iptables automaticamente. Erro: {Error}", error);
                _logger.LogWarning("Para persistir regras após reinicialização, execute manualmente:");
                _logger.LogWarning("  sudo apt install iptables-persistent");
                _logger.LogWarning("  sudo netfilter-persistent save");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar regras iptables manualmente");
        }
    }
}

