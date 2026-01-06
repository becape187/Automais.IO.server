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
    /// Sincroniza todas as configurações do WireGuard do banco de dados para os arquivos
    /// </summary>
    private async Task SyncWireGuardConfigurationsAsync(CancellationToken cancellationToken)
    {
        // Buscar todas as VpnNetworks através dos peers (peers só existem se houver VpnNetwork)
        var allPeers = await _peerRepository.GetAllAsync(cancellationToken);
        
        // Obter VpnNetworkIds únicos
        var vpnNetworkIds = allPeers
            .Select(p => p.VpnNetworkId)
            .Distinct()
            .ToList();

        if (!vpnNetworkIds.Any())
        {
            _logger.LogInformation("Nenhuma VpnNetwork com peers encontrada. Nada para sincronizar.");
            return;
        }

        _logger.LogInformation("Encontradas {Count} VpnNetworks para sincronizar", vpnNetworkIds.Count);

        foreach (var vpnNetworkId in vpnNetworkIds)
        {
            try
            {
                await SyncVpnNetworkAsync(vpnNetworkId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao sincronizar VpnNetwork {VpnNetworkId}", vpnNetworkId);
                // Continua com as próximas redes
            }
        }
    }

    /// <summary>
    /// Sincroniza uma VpnNetwork específica
    /// </summary>
    private async Task SyncVpnNetworkAsync(Guid vpnNetworkId, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Sincronizando VpnNetwork {VpnNetworkId}", vpnNetworkId);

        var vpnNetwork = await _vpnNetworkRepository.GetByIdAsync(vpnNetworkId, cancellationToken);
        if (vpnNetwork == null)
        {
            _logger.LogWarning("VpnNetwork {VpnNetworkId} não encontrada no banco", vpnNetworkId);
            return;
        }

        // Buscar todos os peers desta VpnNetwork
        var peers = await _peerRepository.GetByVpnNetworkIdAsync(vpnNetworkId, cancellationToken);
        
        // Garantir que a interface existe (independente de ter peers ou não)
        var interfaceName = GetInterfaceName(vpnNetworkId);
        await EnsureInterfaceExistsAsync(interfaceName, vpnNetwork, cancellationToken);
        
        if (!peers.Any())
        {
            _logger.LogDebug("VpnNetwork {VpnNetworkId} não possui peers. Interface garantida.", vpnNetworkId);
            return;
        }

        _logger.LogInformation("Sincronizando {PeerCount} peers da VpnNetwork {VpnNetworkId}", peers.Count(), vpnNetworkId);

        // Sincronizar cada peer
        foreach (var peer in peers)
        {
            try
            {
                await SyncPeerAsync(peer, vpnNetwork, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao sincronizar peer {PeerId}", peer.Id);
            }
        }

        // Salvar configuração persistente
        await SaveWireGuardConfigAsync(interfaceName, cancellationToken);
        
        // Ativar interface se não estiver ativa
        await ActivateInterfaceIfNeededAsync(interfaceName, cancellationToken);
        
        _logger.LogInformation("VpnNetwork {VpnNetworkId} sincronizada com sucesso", vpnNetworkId);
    }

    /// <summary>
    /// Sincroniza um peer específico
    /// </summary>
    private async Task SyncPeerAsync(
        Automais.Core.Entities.RouterWireGuardPeer peer,
        Automais.Core.Entities.VpnNetwork vpnNetwork,
        CancellationToken cancellationToken)
    {
        var router = await _routerRepository.GetByIdAsync(peer.RouterId, cancellationToken);
        if (router == null)
        {
            _logger.LogWarning("Router {RouterId} do peer {PeerId} não encontrado", peer.RouterId, peer.Id);
            return;
        }

        // Buscar redes permitidas
        var allowedNetworks = await _allowedNetworkRepository.GetByRouterIdAsync(peer.RouterId, cancellationToken);
        var allowedIps = new List<string> { peer.AllowedIps };
        allowedIps.AddRange(allowedNetworks.Select(n => n.NetworkCidr));

        var interfaceName = GetInterfaceName(vpnNetwork.Id);
        var allowedIpsString = string.Join(",", allowedIps);

        // Adicionar/atualizar peer na interface WireGuard
        await ExecuteWireGuardCommandAsync(
            $"set {interfaceName} peer {peer.PublicKey} allowed-ips {allowedIpsString}",
            cancellationToken);

        _logger.LogDebug("Peer {PeerId} sincronizado na interface {InterfaceName}", peer.Id, interfaceName);
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

