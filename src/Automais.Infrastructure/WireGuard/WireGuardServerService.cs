using Automais.Core.Configuration;
using Automais.Core.DTOs;
using Automais.Core.Entities;
using Automais.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Automais.Infrastructure.WireGuard;

/// <summary>
/// Serviço para gerenciamento do servidor WireGuard no Linux
/// Executa comandos wg e wg-quick via shell
/// </summary>
public class WireGuardServerService : IWireGuardServerService
{
    private readonly IRouterRepository _routerRepository;
    private readonly IRouterWireGuardPeerRepository _peerRepository;
    private readonly IRouterAllowedNetworkRepository _allowedNetworkRepository;
    private readonly IVpnNetworkRepository _vpnNetworkRepository;
    private readonly ILogger<WireGuardServerService>? _logger;
    private readonly WireGuardSettings _wireGuardSettings;

    public WireGuardServerService(
        IRouterRepository routerRepository,
        IRouterWireGuardPeerRepository peerRepository,
        IRouterAllowedNetworkRepository allowedNetworkRepository,
        IVpnNetworkRepository vpnNetworkRepository,
        IOptions<WireGuardSettings> wireGuardSettings,
        ILogger<WireGuardServerService>? logger = null)
    {
        _routerRepository = routerRepository;
        _peerRepository = peerRepository;
        _allowedNetworkRepository = allowedNetworkRepository;
        _vpnNetworkRepository = vpnNetworkRepository;
        _wireGuardSettings = wireGuardSettings.Value;
        _logger = logger;
    }

    public async Task<RouterWireGuardPeerDto> ProvisionRouterAsync(
        Guid routerId,
        Guid vpnNetworkId,
        IEnumerable<string> allowedNetworks,
        string? manualIp = null,
        CancellationToken cancellationToken = default)
    {
        var router = await _routerRepository.GetByIdAsync(routerId, cancellationToken);
        if (router == null)
            throw new KeyNotFoundException($"Router com ID {routerId} não encontrado.");

        var vpnNetwork = await _vpnNetworkRepository.GetByIdAsync(vpnNetworkId, cancellationToken);
        if (vpnNetwork == null)
            throw new KeyNotFoundException($"Rede VPN com ID {vpnNetworkId} não encontrada.");

        // Verificar se já existe peer
        var existing = await _peerRepository.GetByRouterIdAndNetworkIdAsync(routerId, vpnNetworkId, cancellationToken);
        if (existing != null)
            throw new InvalidOperationException($"Router já possui peer WireGuard para esta rede VPN.");

        // Gerar par de chaves WireGuard
        var (publicKey, privateKey) = await GenerateWireGuardKeysAsync(cancellationToken);

        // Alocar IP da VPN (manual ou automático)
        var routerIp = await AllocateVpnIpAsync(vpnNetworkId, manualIp, cancellationToken);

        // Construir allowed-ips (IP do router + redes permitidas opcionais)
        var allowedIps = new List<string> { routerIp };
        if (allowedNetworks != null && allowedNetworks.Any())
        {
            allowedIps.AddRange(allowedNetworks);
        }
        var allowedIpsString = string.Join(",", allowedIps);

        // Criar peer no banco
        // Nota: Endpoint não é salvo no peer, ele vem da VpnNetwork
        var peer = new RouterWireGuardPeer
        {
            Id = Guid.NewGuid(),
            RouterId = routerId,
            VpnNetworkId = vpnNetworkId,
            PublicKey = publicKey,
            PrivateKey = privateKey, // Texto plano inicialmente
            AllowedIps = routerIp,
            Endpoint = null, // Endpoint vem da VpnNetwork, não é armazenado no peer
            ListenPort = 51820, // TODO: Obter da configuração da VpnNetwork
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _peerRepository.CreateAsync(peer, cancellationToken);

        // Salvar redes permitidas (se houver)
        if (allowedNetworks != null && allowedNetworks.Any())
        {
            foreach (var network in allowedNetworks)
            {
                await _allowedNetworkRepository.CreateAsync(new RouterAllowedNetwork
                {
                    Id = Guid.NewGuid(),
                    RouterId = routerId,
                    NetworkCidr = network,
                    CreatedAt = DateTime.UtcNow
                }, cancellationToken);
            }
        }

        // Aplicar no WireGuard server (Linux)
        var interfaceName = GetInterfaceName(vpnNetworkId);
        
        // Garantir que a interface existe (criar arquivo de configuração inicial se necessário)
        await EnsureInterfaceExistsAsync(interfaceName, vpnNetwork, cancellationToken);
        
        // Adicionar peer à interface
        await ExecuteWireGuardCommandAsync(
            $"set {interfaceName} peer {publicKey} allowed-ips {allowedIpsString}"
        );

        // Salvar configuração persistente no arquivo
        await SaveWireGuardConfigAsync(interfaceName);

        // Gerar e salvar arquivo .conf
        var config = await GenerateConfigContentAsync(router, peer, vpnNetwork, allowedNetworks, cancellationToken);
        peer.ConfigContent = config;
        await _peerRepository.UpdateAsync(peer, cancellationToken);

        _logger?.LogInformation("Router {RouterId} provisionado na VPN {VpnNetworkId} com IP {RouterIp}",
            routerId, vpnNetworkId, routerIp);

        return MapToDto(peer);
    }

    public async Task AddNetworkToRouterAsync(
        Guid routerId,
        string networkCidr,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        var router = await _routerRepository.GetByIdAsync(routerId, cancellationToken);
        if (router == null)
            throw new KeyNotFoundException($"Router com ID {routerId} não encontrado.");

        // Verificar se já existe
        var existing = await _allowedNetworkRepository.GetByRouterIdAndCidrAsync(routerId, networkCidr, cancellationToken);
        if (existing != null)
            throw new InvalidOperationException($"Rede {networkCidr} já está configurada para este router.");

        // Salvar no banco
        await _allowedNetworkRepository.CreateAsync(new RouterAllowedNetwork
        {
            Id = Guid.NewGuid(),
            RouterId = routerId,
            NetworkCidr = networkCidr,
            Description = description,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        // Buscar peer e redes
        var peer = (await _peerRepository.GetByRouterIdAsync(routerId, cancellationToken)).FirstOrDefault();
        if (peer == null)
            throw new InvalidOperationException("Router não possui peer WireGuard configurado.");

        var allowedNetworks = await _allowedNetworkRepository.GetByRouterIdAsync(routerId, cancellationToken);

        // Reconstruir allowed-ips
        var allowedIps = new List<string> { peer.AllowedIps };
        allowedIps.AddRange(allowedNetworks.Select(n => n.NetworkCidr));

        // Atualizar no WireGuard
        var interfaceName = GetInterfaceName(peer.VpnNetworkId);
        await ExecuteWireGuardCommandAsync(
            $"set {interfaceName} peer {peer.PublicKey} allowed-ips {string.Join(",", allowedIps)}"
        );

        // Regenerar e salvar config
        var vpnNetwork = await _vpnNetworkRepository.GetByIdAsync(peer.VpnNetworkId, cancellationToken);
        var config = await GenerateConfigContentAsync(router, peer, vpnNetwork!, allowedNetworks.Select(n => n.NetworkCidr));
        peer.ConfigContent = config;
        await _peerRepository.UpdateAsync(peer, cancellationToken);

        _logger?.LogInformation("Rede {NetworkCidr} adicionada ao router {RouterId}", networkCidr, routerId);
    }

    public async Task RemoveNetworkFromRouterAsync(
        Guid routerId,
        string networkCidr,
        CancellationToken cancellationToken = default)
    {
        var router = await _routerRepository.GetByIdAsync(routerId, cancellationToken);
        if (router == null)
            throw new KeyNotFoundException($"Router com ID {routerId} não encontrado.");

        // Remover do banco
        await _allowedNetworkRepository.DeleteByRouterIdAndCidrAsync(routerId, networkCidr, cancellationToken);

        // Buscar peer e redes restantes
        var peer = (await _peerRepository.GetByRouterIdAsync(routerId, cancellationToken)).FirstOrDefault();
        if (peer == null)
            throw new InvalidOperationException("Router não possui peer WireGuard configurado.");

        var allowedNetworks = await _allowedNetworkRepository.GetByRouterIdAsync(routerId, cancellationToken);

        // Reconstruir allowed-ips
        var allowedIps = new List<string> { peer.AllowedIps };
        allowedIps.AddRange(allowedNetworks.Select(n => n.NetworkCidr));

        // Atualizar no WireGuard
        var interfaceName = GetInterfaceName(peer.VpnNetworkId);
        await ExecuteWireGuardCommandAsync(
            $"set {interfaceName} peer {peer.PublicKey} allowed-ips {string.Join(",", allowedIps)}"
        );

        // Regenerar e salvar config
        var vpnNetwork = await _vpnNetworkRepository.GetByIdAsync(peer.VpnNetworkId, cancellationToken);
        var config = await GenerateConfigContentAsync(router, peer, vpnNetwork!, allowedNetworks.Select(n => n.NetworkCidr), cancellationToken);
        peer.ConfigContent = config;
        await _peerRepository.UpdateAsync(peer, cancellationToken);

        _logger?.LogInformation("Rede {NetworkCidr} removida do router {RouterId}", networkCidr, routerId);
    }

    public async Task ReloadPeerConfigAsync(Guid peerId, CancellationToken cancellationToken = default)
    {
        var peer = await _peerRepository.GetByIdAsync(peerId, cancellationToken);
        if (peer == null)
            throw new KeyNotFoundException($"Peer com ID {peerId} não encontrado.");

        var allowedNetworks = await _allowedNetworkRepository.GetByRouterIdAsync(peer.RouterId, cancellationToken);
        var allowedIps = new List<string> { peer.AllowedIps };
        allowedIps.AddRange(allowedNetworks.Select(n => n.NetworkCidr));

        var interfaceName = GetInterfaceName(peer.VpnNetworkId);
        await ExecuteWireGuardCommandAsync(
            $"set {interfaceName} peer {peer.PublicKey} allowed-ips {string.Join(",", allowedIps)}"
        );

        _logger?.LogInformation("Configuração do peer {PeerId} recarregada", peerId);
    }

    public async Task<RouterWireGuardConfigDto> GenerateAndSaveConfigAsync(
        Guid routerId,
        CancellationToken cancellationToken = default)
    {
        var router = await _routerRepository.GetByIdAsync(routerId, cancellationToken);
        if (router == null)
            throw new KeyNotFoundException($"Router com ID {routerId} não encontrado.");

        var peer = (await _peerRepository.GetByRouterIdAsync(routerId, cancellationToken)).FirstOrDefault();
        if (peer == null)
            throw new InvalidOperationException("Router não possui peer WireGuard configurado.");

        var vpnNetwork = await _vpnNetworkRepository.GetByIdAsync(peer.VpnNetworkId, cancellationToken);
        if (vpnNetwork == null)
            throw new KeyNotFoundException("Rede VPN não encontrada.");

        var allowedNetworks = await _allowedNetworkRepository.GetByRouterIdAsync(routerId, cancellationToken);
        var config = await GenerateConfigContentAsync(router, peer, vpnNetwork, allowedNetworks.Select(n => n.NetworkCidr), cancellationToken);

        // Salvar no banco
        peer.ConfigContent = config;
        await _peerRepository.UpdateAsync(peer, cancellationToken);

        // Sanitizar o nome do router para o nome do arquivo
        var sanitizedRouterName = SanitizeFileName(router.Name);
        
        return new RouterWireGuardConfigDto
        {
            ConfigContent = config,
            FileName = $"router_{sanitizedRouterName}_{routerId}.conf"
        };
    }

    public async Task<RouterWireGuardConfigDto> GetConfigAsync(
        Guid routerId,
        CancellationToken cancellationToken = default)
    {
        var router = await _routerRepository.GetByIdAsync(routerId, cancellationToken);
        if (router == null)
        {
            _logger?.LogWarning("Router {RouterId} não encontrado ao tentar obter configuração VPN", routerId);
            throw new KeyNotFoundException($"Router com ID {routerId} não encontrado.");
        }

        var peer = (await _peerRepository.GetByRouterIdAsync(routerId, cancellationToken)).FirstOrDefault();
        if (peer == null)
        {
            _logger?.LogWarning("Router {RouterId} não possui peer VPN configurado. VpnNetworkId: {VpnNetworkId}", 
                routerId, router.VpnNetworkId);
            throw new InvalidOperationException(
                $"Router não possui peer VPN configurado. " +
                $"Certifique-se de que o router foi criado com uma rede VPN (vpnNetworkId) e que o peer foi provisionado corretamente.");
        }

        _logger?.LogDebug("Obtendo configuração VPN para router {RouterId}. Peer ID: {PeerId}, ConfigContent vazio: {IsEmpty}", 
            routerId, peer.Id, string.IsNullOrEmpty(peer.ConfigContent));

        // Se já tem config salva, retorna
        if (!string.IsNullOrEmpty(peer.ConfigContent))
        {
            _logger?.LogDebug("Retornando configuração VPN salva para router {RouterId}", routerId);
            
            // Sanitizar o nome do router para o nome do arquivo
            var sanitizedRouterName = SanitizeFileName(router.Name);
            
            return new RouterWireGuardConfigDto
            {
                ConfigContent = peer.ConfigContent,
                FileName = $"router_{sanitizedRouterName}_{routerId}.conf"
            };
        }

        // Senão, gera e salva
        _logger?.LogInformation("Gerando nova configuração VPN para router {RouterId}", routerId);
        return await GenerateAndSaveConfigAsync(routerId, cancellationToken);
    }

    public async Task<string> AllocateVpnIpAsync(
        Guid vpnNetworkId,
        string? manualIp = null,
        CancellationToken cancellationToken = default)
    {
        var vpnNetwork = await _vpnNetworkRepository.GetByIdAsync(vpnNetworkId, cancellationToken);
        if (vpnNetwork == null)
            throw new KeyNotFoundException("Rede VPN não encontrada.");

        // Parse do CIDR (ex: "10.100.1.0/24")
        var (networkIp, prefixLength) = ParseCidr(vpnNetwork.Cidr);

        // Se IP manual foi especificado, validar e usar
        if (!string.IsNullOrWhiteSpace(manualIp))
        {
            return await ValidateAndReserveManualIpAsync(vpnNetworkId, manualIp, networkIp, prefixLength, cancellationToken);
        }

        // Buscar IPs já alocados
        var allocatedIps = await _peerRepository.GetAllocatedIpsByNetworkAsync(vpnNetworkId, cancellationToken);

        // Encontrar próximo IP disponível (começando do .2, pois .1 é reservado para servidor)
        var availableIp = FindNextAvailableIp(networkIp, prefixLength, allocatedIps);

        return $"{availableIp}/{prefixLength}";
    }

    /// <summary>
    /// Valida e reserva um IP manual especificado pelo usuário
    /// </summary>
    private async Task<string> ValidateAndReserveManualIpAsync(
        Guid vpnNetworkId,
        string manualIp,
        IPAddress networkIp,
        int prefixLength,
        CancellationToken cancellationToken)
    {
        // Parse do IP manual
        var manualIpParts = manualIp.Split('/');
        if (manualIpParts.Length != 2)
            throw new ArgumentException($"IP manual inválido. Use o formato: IP/PREFIX (ex: 10.222.111.5/24)");

        if (!IPAddress.TryParse(manualIpParts[0], out var requestedIp))
            throw new ArgumentException($"IP manual inválido: {manualIpParts[0]}");

        if (!int.TryParse(manualIpParts[1], out var requestedPrefix) || requestedPrefix != prefixLength)
            throw new ArgumentException($"Prefix do IP manual ({requestedPrefix}) deve ser igual ao prefix da rede VPN ({prefixLength})");

        // Verificar se o IP está dentro da rede VPN
        var networkBytes = networkIp.GetAddressBytes();
        var requestedBytes = requestedIp.GetAddressBytes();
        
        // Verificar se os primeiros prefixLength bits são iguais
        var networkBits = prefixLength;
        var networkByte = networkBits / 8;
        var networkBit = networkBits % 8;

        bool isInNetwork = true;
        for (int i = 0; i < networkByte; i++)
        {
            if (networkBytes[i] != requestedBytes[i])
            {
                isInNetwork = false;
                break;
            }
        }
        
        if (networkByte < 4 && networkBit > 0)
        {
            var mask = (byte)(0xFF << (8 - networkBit));
            if ((networkBytes[networkByte] & mask) != (requestedBytes[networkByte] & mask))
            {
                isInNetwork = false;
            }
        }

        if (!isInNetwork)
            throw new ArgumentException($"IP manual {manualIp} não está na rede VPN {networkIp}/{prefixLength}");

        // Verificar se é o IP .1 (reservado para servidor)
        var serverIp = new IPAddress(new byte[]
        {
            networkBytes[0],
            networkBytes[1],
            networkBytes[2],
            (byte)(networkBytes[3] + 1)
        });

        if (requestedIp.Equals(serverIp))
            throw new ArgumentException($"IP {manualIp} é reservado para o servidor VPN (.1)");

        // Verificar se o IP já está alocado
        var allocatedIps = await _peerRepository.GetAllocatedIpsByNetworkAsync(vpnNetworkId, cancellationToken);
        var allocated = new HashSet<string>();
        foreach (var ip in allocatedIps)
        {
            var ipPart = ip.Split('/')[0];
            allocated.Add(ipPart);
        }

        if (allocated.Contains(requestedIp.ToString()))
            throw new InvalidOperationException($"IP {manualIp} já está alocado para outro router nesta rede VPN");

        // IP válido e disponível
        _logger?.LogInformation("IP manual {ManualIp} validado e reservado para router", manualIp);
        return manualIp;
    }

    // ===== Métodos Privados =====

    private async Task ExecuteWireGuardCommandAsync(string command)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
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
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                _logger?.LogError("Erro ao executar comando WireGuard: {Command}, Erro: {Error}", command, error);
                throw new InvalidOperationException($"Erro ao executar comando WireGuard: {error}");
            }

            _logger?.LogDebug("Comando WireGuard executado: {Command}, Output: {Output}", command, output);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Exceção ao executar comando WireGuard: {Command}", command);
            throw;
        }
    }

    private async Task SaveWireGuardConfigAsync(string interfaceName)
    {
        try
        {
            // Usar wg-quick save para salvar a configuração atual em arquivo
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
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
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                _logger?.LogWarning("Erro ao salvar configuração WireGuard: {Error}. Interface pode não estar ativa ainda.", error);
                // Não lança exceção - a interface pode não estar ativa ainda
            }
            else
            {
                _logger?.LogDebug("Configuração WireGuard salva para interface {InterfaceName}", interfaceName);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Exceção ao salvar configuração WireGuard para interface {InterfaceName}", interfaceName);
            // Não lança exceção - continua operação
        }
    }

    public string GetInterfaceName(Guid vpnNetworkId)
    {
        // TODO: Buscar nome da interface da VpnNetwork ou usar padrão
        // Por enquanto usa padrão baseado no ID
        return $"wg-{vpnNetworkId.ToString("N")[..8]}";
    }

    /// <summary>
    /// Garante que a interface WireGuard existe para uma VpnNetwork
    /// </summary>
    public async Task EnsureInterfaceForVpnNetworkAsync(Guid vpnNetworkId, CancellationToken cancellationToken = default)
    {
        var vpnNetwork = await _vpnNetworkRepository.GetByIdAsync(vpnNetworkId, cancellationToken);
        if (vpnNetwork == null)
            throw new KeyNotFoundException($"Rede VPN com ID {vpnNetworkId} não encontrada.");

        var interfaceName = GetInterfaceName(vpnNetworkId);
        await EnsureInterfaceExistsAsync(interfaceName, vpnNetwork, cancellationToken);
    }

    /// <summary>
    /// Garante que a interface WireGuard existe, criando o arquivo de configuração inicial se necessário
    /// </summary>
    private async Task EnsureInterfaceExistsAsync(
        string interfaceName,
        VpnNetwork vpnNetwork,
        CancellationToken cancellationToken = default)
    {
        var configPath = $"/etc/wireguard/{interfaceName}.conf";
        
        // Se o arquivo já existe, verificar se a chave pública está salva
        if (File.Exists(configPath))
        {
            _logger?.LogDebug("Interface WireGuard {InterfaceName} já existe", interfaceName);
            
            // Se a chave pública não está salva, tentar ler do arquivo e salvar
            if (string.IsNullOrEmpty(vpnNetwork.ServerPublicKey))
            {
                try
                {
                    var configLines = await File.ReadAllLinesAsync(configPath, cancellationToken);
                    foreach (var line in configLines)
                    {
                        if (line.TrimStart().StartsWith("PrivateKey", StringComparison.OrdinalIgnoreCase))
                        {
                            var privateKey = line.Split('=')[1].Trim();
                            var recoveredPublicKey = await GetPublicKeyFromPrivateKeyAsync(privateKey);
                            if (recoveredPublicKey != "SERVER_PUBLIC_KEY_PLACEHOLDER")
                            {
                                vpnNetwork.ServerPublicKey = recoveredPublicKey;
                                vpnNetwork.UpdatedAt = DateTime.UtcNow;
                                await _vpnNetworkRepository.UpdateAsync(vpnNetwork, cancellationToken);
                                _logger?.LogInformation("Chave pública do servidor recuperada e salva na VpnNetwork {VpnNetworkId}", vpnNetwork.Id);
                            }
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Erro ao tentar recuperar chave pública do arquivo de configuração existente");
                }
            }
            
            return;
        }

        // Gerar chaves do servidor para esta interface
        var (serverPublicKey, serverPrivateKey) = await GenerateWireGuardKeysAsync(cancellationToken);
        
        // Salvar a chave pública na VpnNetwork se ainda não estiver salva
        if (string.IsNullOrEmpty(vpnNetwork.ServerPublicKey))
        {
            vpnNetwork.ServerPublicKey = serverPublicKey;
            vpnNetwork.UpdatedAt = DateTime.UtcNow;
            await _vpnNetworkRepository.UpdateAsync(vpnNetwork, cancellationToken);
            _logger?.LogInformation("Chave pública do servidor salva na VpnNetwork {VpnNetworkId}", vpnNetwork.Id);
        }
        
        // Parse do CIDR para obter o IP da interface do servidor
        var (networkIp, prefixLength) = ParseCidr(vpnNetwork.Cidr);
        // O servidor usa o primeiro IP da rede (ex: 10.100.1.0/24 -> servidor usa 10.100.1.1)
        var serverIp = new IPAddress(new byte[]
        {
            networkIp.GetAddressBytes()[0],
            networkIp.GetAddressBytes()[1],
            networkIp.GetAddressBytes()[2],
            (byte)(networkIp.GetAddressBytes()[3] + 1)
        });

        // Criar conteúdo do arquivo de configuração
        var configContent = new StringBuilder();
        configContent.AppendLine("[Interface]");
        configContent.AppendLine($"PrivateKey = {serverPrivateKey}");
        configContent.AppendLine($"Address = {serverIp}/{prefixLength}");
        configContent.AppendLine($"ListenPort = 51820");
        
        // Adicionar DNS se configurado
        if (!string.IsNullOrWhiteSpace(vpnNetwork.DnsServers))
        {
            configContent.AppendLine($"DNS = {vpnNetwork.DnsServers}");
        }
        
        configContent.AppendLine();
        configContent.AppendLine("# Peers serão adicionados automaticamente pela API");

        // Criar diretório se não existir
        var configDir = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
        {
            Directory.CreateDirectory(configDir);
            _logger?.LogInformation("Diretório WireGuard criado: {ConfigDir}", configDir);
        }

        // Salvar arquivo de configuração
        await File.WriteAllTextAsync(configPath, configContent.ToString(), cancellationToken);
        
        // Definir permissões corretas (600 = rw-------)
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/chmod",
                    Arguments = $"600 {configPath}",
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
            _logger?.LogWarning(ex, "Erro ao definir permissões do arquivo de configuração WireGuard");
        }

        _logger?.LogInformation("Arquivo de configuração WireGuard criado: {ConfigPath}", configPath);
        
        // Configurar regras de firewall para esta interface específica
        await ConfigureFirewallRulesAsync(interfaceName, vpnNetwork.Cidr, cancellationToken);
        
        // Tentar ativar a interface usando wg-quick
        try
        {
            var upProcess = new Process
            {
                StartInfo = new ProcessStartInfo
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
            var output = await upProcess.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = await upProcess.StandardError.ReadToEndAsync(cancellationToken);
            await upProcess.WaitForExitAsync(cancellationToken);

            if (upProcess.ExitCode == 0)
            {
                _logger?.LogInformation("Interface WireGuard {InterfaceName} ativada com sucesso", interfaceName);
            }
            else
            {
                _logger?.LogWarning("Erro ao ativar interface WireGuard {InterfaceName}: {Error}", interfaceName, error);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Exceção ao ativar interface WireGuard {InterfaceName}", interfaceName);
            // Não lança exceção - a interface pode ser ativada manualmente depois
        }
    }

    /// <summary>
    /// Obtém o endpoint do servidor VPN a partir da VpnNetwork.
    /// O valor sempre deve vir do banco (salvo quando a VPN foi criada/atualizada pelo frontend).
    /// Se por algum motivo não estiver configurado, usa o valor padrão da configuração como fallback.
    /// </summary>
    private string GetServerEndpoint(VpnNetwork vpnNetwork)
    {
        if (!string.IsNullOrWhiteSpace(vpnNetwork.ServerEndpoint))
        {
            return vpnNetwork.ServerEndpoint;
        }
        // Fallback de segurança: se por algum motivo não tiver no banco, usa o valor da configuração
        // (mas isso não deveria acontecer se o frontend sempre enviar o valor)
        _logger?.LogWarning("ServerEndpoint não encontrado na VpnNetwork {VpnNetworkId}, usando valor padrão da configuração", vpnNetwork.Id);
        return _wireGuardSettings.DefaultServerEndpoint;
    }

    /// <summary>
    /// Habilita o encaminhamento IP (ip_forward) necessário para o WireGuard
    /// </summary>
    private async Task EnableIpForwardingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Verificar se já está habilitado
            var checkProcess = new Process
            {
                StartInfo = new ProcessStartInfo
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
                _logger?.LogDebug("Encaminhamento IP já está habilitado");
                return;
            }

            // Habilitar temporariamente (até reiniciar)
            var enableProcess = new Process
            {
                StartInfo = new ProcessStartInfo
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
            await enableProcess.WaitForExitAsync(cancellationToken);

            if (enableProcess.ExitCode == 0)
            {
                _logger?.LogInformation("Encaminhamento IP habilitado temporariamente");
            }

            // Habilitar permanentemente via sysctl
            // Verificar se já existe no sysctl.conf
            var checkSysctlProcess = new Process
            {
                StartInfo = new ProcessStartInfo
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
            var sysctlProcess = new Process
            {
                StartInfo = new ProcessStartInfo
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
                _logger?.LogInformation("Encaminhamento IP habilitado permanentemente");
            }
            else
            {
                _logger?.LogWarning("Erro ao habilitar encaminhamento IP permanentemente: {Error}", sysctlError);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Erro ao habilitar encaminhamento IP");
            // Não lança exceção - pode ser configurado manualmente
        }
    }

    /// <summary>
    /// Configura regras de firewall (iptables) para o WireGuard
    /// </summary>
    private async Task ConfigureFirewallRulesAsync(string interfaceName, string vpnCidr, CancellationToken cancellationToken = default)
    {
        try
        {
            // Verificar se iptables está disponível
            var checkProcess = new Process
            {
                StartInfo = new ProcessStartInfo
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
                _logger?.LogWarning("iptables não encontrado. Regras de firewall não serão configuradas automaticamente.");
                return;
            }

            // Permitir tráfego na porta WireGuard (UDP 51820)
            await ExecuteIptablesCommandAsync($"-A INPUT -p udp --dport 51820 -j ACCEPT", cancellationToken);

            // Permitir tráfego na interface WireGuard
            await ExecuteIptablesCommandAsync($"-A INPUT -i {interfaceName} -j ACCEPT", cancellationToken);
            await ExecuteIptablesCommandAsync($"-A OUTPUT -o {interfaceName} -j ACCEPT", cancellationToken);

            // Permitir forwarding para a rede VPN
            await ExecuteIptablesCommandAsync($"-A FORWARD -i {interfaceName} -j ACCEPT", cancellationToken);
            await ExecuteIptablesCommandAsync($"-A FORWARD -o {interfaceName} -j ACCEPT", cancellationToken);

            // NAT para tráfego da VPN (masquerade)
            // Isso permite que clientes VPN acessem a internet através do servidor
            // Detectar interface de rede principal (eth0, ens3, enp0s3, etc.)
            var mainInterface = await GetMainNetworkInterfaceAsync(cancellationToken);
            
            if (!string.IsNullOrEmpty(mainInterface))
            {
                var natProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $"-c \"iptables -t nat -C POSTROUTING -s {vpnCidr} -o {mainInterface} -j MASQUERADE 2>/dev/null || iptables -t nat -A POSTROUTING -s {vpnCidr} -o {mainInterface} -j MASQUERADE\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                natProcess.Start();
                var natOutput = await natProcess.StandardOutput.ReadToEndAsync(cancellationToken);
                var natError = await natProcess.StandardError.ReadToEndAsync(cancellationToken);
                await natProcess.WaitForExitAsync(cancellationToken);

                if (natProcess.ExitCode == 0)
                {
                    _logger?.LogInformation("Regras de firewall e NAT configuradas para interface {InterfaceName} (via {MainInterface})", interfaceName, mainInterface);
                }
                else
                {
                    _logger?.LogWarning("Erro ao configurar NAT: {Error}", natError);
                }
            }
            else
            {
                _logger?.LogWarning("Não foi possível detectar interface de rede principal. NAT não configurado.");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Erro ao configurar regras de firewall");
            // Não lança exceção - pode ser configurado manualmente
        }
    }

    /// <summary>
    /// Detecta a interface de rede principal (eth0, ens3, etc.)
    /// </summary>
    private async Task<string> GetMainNetworkInterfaceAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = "-c \"ip route | grep default | awk '{print $5}' | head -1\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var interfaceName = (await process.StandardOutput.ReadToEndAsync(cancellationToken)).Trim();
            await process.WaitForExitAsync(cancellationToken);

            if (!string.IsNullOrEmpty(interfaceName) && process.ExitCode == 0)
            {
                _logger?.LogDebug("Interface de rede principal detectada: {InterfaceName}", interfaceName);
                return interfaceName;
            }

            // Fallback: tentar interfaces comuns
            var commonInterfaces = new[] { "eth0", "ens3", "enp0s3", "enp0s8" };
            foreach (var iface in commonInterfaces)
            {
                var checkProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $"-c \"ip link show {iface} >/dev/null 2>&1\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                checkProcess.Start();
                await checkProcess.WaitForExitAsync(cancellationToken);
                
                if (checkProcess.ExitCode == 0)
                {
                    _logger?.LogDebug("Interface de rede detectada (fallback): {InterfaceName}", iface);
                    return iface;
                }
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Erro ao detectar interface de rede principal");
            return string.Empty;
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
            var checkProcess = new Process
            {
                StartInfo = new ProcessStartInfo
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
                var addProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
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
                    _logger?.LogDebug("Regra iptables adicionada: {Rule}", rule);
                }
                else
                {
                    _logger?.LogWarning("Erro ao adicionar regra iptables {Rule}: {Error}", rule, error);
                }
            }
            else
            {
                _logger?.LogDebug("Regra iptables já existe: {Rule}", rule);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Erro ao executar comando iptables: {Rule}", rule);
        }
    }

    private async Task<(string publicKey, string privateKey)> GenerateWireGuardKeysAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Gerar chave privada usando wg genkey
            var genkeyProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/usr/bin/wg",
                    Arguments = "genkey",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            genkeyProcess.Start();
            var privateKey = (await genkeyProcess.StandardOutput.ReadToEndAsync(cancellationToken)).Trim();
            var error = await genkeyProcess.StandardError.ReadToEndAsync(cancellationToken);
            await genkeyProcess.WaitForExitAsync(cancellationToken);

            if (genkeyProcess.ExitCode != 0 || string.IsNullOrEmpty(privateKey))
            {
                _logger?.LogWarning("Erro ao gerar chave privada WireGuard: {Error}. Usando método alternativo.", error);
                return GenerateWireGuardKeysFallback();
            }

            // Gerar chave pública a partir da privada: echo <privateKey> | wg pubkey
            var pubkeyProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"echo '{privateKey}' | /usr/bin/wg pubkey\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            pubkeyProcess.Start();
            var publicKey = (await pubkeyProcess.StandardOutput.ReadToEndAsync(cancellationToken)).Trim();
            var pubkeyError = await pubkeyProcess.StandardError.ReadToEndAsync(cancellationToken);
            await pubkeyProcess.WaitForExitAsync(cancellationToken);

            if (pubkeyProcess.ExitCode != 0 || string.IsNullOrEmpty(publicKey))
            {
                _logger?.LogWarning("Erro ao gerar chave pública WireGuard: {Error}. Usando método alternativo.", pubkeyError);
                return GenerateWireGuardKeysFallback();
            }

            return (publicKey, privateKey);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Exceção ao gerar chaves WireGuard. Usando método alternativo.");
            return GenerateWireGuardKeysFallback();
        }
    }

    private (string publicKey, string privateKey) GenerateWireGuardKeysFallback()
    {
        // Método fallback caso wg genkey não funcione (para desenvolvimento/testes)
        _logger?.LogWarning("Usando geração de chaves WireGuard mockada (fallback)");
        var random = new Random();
        var privateKeyBytes = new byte[32];
        random.NextBytes(privateKeyBytes);
        var privateKey = Convert.ToBase64String(privateKeyBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        random.NextBytes(privateKeyBytes);
        var publicKey = Convert.ToBase64String(privateKeyBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return (publicKey, privateKey);
    }

    private async Task<string> GenerateConfigContentAsync(
        Router router,
        RouterWireGuardPeer peer,
        VpnNetwork vpnNetwork,
        IEnumerable<string> allowedNetworks,
        CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        
        // Seção [Interface] - Configuração do cliente (router)
        sb.AppendLine("# Configuração VPN para Router");
        sb.AppendLine($"# Router: {router.Name}");
        sb.AppendLine($"# Gerado em: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        sb.AppendLine("[Interface]");
        sb.AppendLine($"PrivateKey = {peer.PrivateKey}");
        sb.AppendLine($"Address = {peer.AllowedIps}");
        sb.AppendLine();
        
        // Seção [Peer] - Configuração do servidor
        sb.AppendLine("[Peer]");
        var serverPublicKey = await GetServerPublicKeyAsync(peer.VpnNetworkId, cancellationToken);
        
        if (serverPublicKey == "SERVER_PUBLIC_KEY_PLACEHOLDER")
        {
            _logger?.LogWarning("Chave pública do servidor não encontrada para VpnNetwork {VpnNetworkId}. Usando placeholder.", peer.VpnNetworkId);
            // Tentar buscar do arquivo de configuração do servidor
            var interfaceName = GetInterfaceName(peer.VpnNetworkId);
            var configPath = $"/etc/wireguard/{interfaceName}.conf";
            if (File.Exists(configPath))
            {
                var configLines = await File.ReadAllLinesAsync(configPath, cancellationToken);
                foreach (var line in configLines)
                {
                    if (line.TrimStart().StartsWith("PrivateKey", StringComparison.OrdinalIgnoreCase))
                    {
                        var privateKey = line.Split('=')[1].Trim();
                        serverPublicKey = await GetPublicKeyFromPrivateKeyAsync(privateKey);
                        break;
                    }
                }
            }
        }
        
        sb.AppendLine($"PublicKey = {serverPublicKey}");
        
        // Endpoint sempre vem da VpnNetwork, não do peer
        var endpoint = GetServerEndpoint(vpnNetwork);
        sb.AppendLine($"Endpoint = {endpoint}:{peer.ListenPort ?? 51820}");

        // Adicionar todas as redes permitidas (se houver)
        var allNetworks = new List<string> { vpnNetwork.Cidr };
        if (allowedNetworks != null && allowedNetworks.Any())
        {
            allNetworks.AddRange(allowedNetworks);
        }
        sb.AppendLine($"AllowedIPs = {string.Join(", ", allNetworks)}");
        sb.AppendLine("PersistentKeepalive = 25");

        var configContent = sb.ToString();
        _logger?.LogDebug("Configuração VPN gerada para router {RouterId}. Tamanho: {Size} bytes", router.Id, configContent.Length);
        
        return configContent;
    }

    private async Task<string> GetServerPublicKeyAsync(Guid vpnNetworkId, CancellationToken cancellationToken = default)
    {
        // Buscar chave pública do servidor da VpnNetwork
        var vpnNetwork = await _vpnNetworkRepository.GetByIdAsync(vpnNetworkId, cancellationToken);
        if (vpnNetwork == null)
            throw new KeyNotFoundException("Rede VPN não encontrada.");

        // Primeiro, tentar usar a chave salva na VpnNetwork
        if (!string.IsNullOrEmpty(vpnNetwork.ServerPublicKey))
        {
            _logger?.LogDebug("Chave pública do servidor encontrada na VpnNetwork {VpnNetworkId}", vpnNetworkId);
            return vpnNetwork.ServerPublicKey;
        }

        // Se não estiver salva, tentar buscar do arquivo de configuração da interface
        var interfaceName = GetInterfaceName(vpnNetworkId);
        var configPath = $"/etc/wireguard/{interfaceName}.conf";
        
        if (File.Exists(configPath))
        {
            var configLines = await File.ReadAllLinesAsync(configPath, cancellationToken);
            foreach (var line in configLines)
            {
                if (line.TrimStart().StartsWith("PrivateKey", StringComparison.OrdinalIgnoreCase))
                {
                    var privateKey = line.Split('=')[1].Trim();
                    // Gerar chave pública a partir da privada
                    var publicKey = await GetPublicKeyFromPrivateKeyAsync(privateKey);
                    if (publicKey != "SERVER_PUBLIC_KEY_PLACEHOLDER")
                    {
                        // Salvar a chave na VpnNetwork para uso futuro
                        vpnNetwork.ServerPublicKey = publicKey;
                        vpnNetwork.UpdatedAt = DateTime.UtcNow;
                        await _vpnNetworkRepository.UpdateAsync(vpnNetwork, cancellationToken);
                        _logger?.LogInformation("Chave pública do servidor recuperada do arquivo e salva na VpnNetwork {VpnNetworkId}", vpnNetworkId);
                        return publicKey;
                    }
                }
            }
        }

        // Se não encontrou, retornar placeholder (será gerado quando criar interface)
        _logger?.LogWarning("Chave pública do servidor não encontrada para interface {InterfaceName}", interfaceName);
        return "SERVER_PUBLIC_KEY_PLACEHOLDER";
    }

    private async Task<string> GetPublicKeyFromPrivateKeyAsync(string privateKey)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"echo '{privateKey}' | /usr/bin/wg pubkey\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var publicKey = (await process.StandardOutput.ReadToEndAsync()).Trim();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && !string.IsNullOrEmpty(publicKey))
            {
                return publicKey;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Erro ao gerar chave pública a partir da privada");
        }

        return "SERVER_PUBLIC_KEY_PLACEHOLDER";
    }

    private (IPAddress networkIp, int prefixLength) ParseCidr(string cidr)
    {
        var parts = cidr.Split('/');
        if (parts.Length != 2)
            throw new ArgumentException($"CIDR inválido: {cidr}");

        if (!IPAddress.TryParse(parts[0], out var ip))
            throw new ArgumentException($"IP inválido no CIDR: {cidr}");

        if (!int.TryParse(parts[1], out var prefix) || prefix < 0 || prefix > 32)
            throw new ArgumentException($"Prefix length inválido no CIDR: {cidr}");

        return (ip, prefix);
    }

    private string FindNextAvailableIp(IPAddress networkIp, int prefixLength, IEnumerable<string> allocatedIps)
    {
        // Parse dos IPs alocados
        var allocated = new HashSet<string>();
        foreach (var ip in allocatedIps)
        {
            var ipPart = ip.Split('/')[0];
            allocated.Add(ipPart);
        }

        // Calcular range de IPs disponíveis
        var networkBytes = networkIp.GetAddressBytes();
        var hostBits = 32 - prefixLength;
        var maxHosts = (int)Math.Pow(2, hostBits) - 2; // -2 para network e broadcast

        // Reservar .1 para o servidor, começar do .2
        // Adicionar .1 aos IPs alocados para garantir que não seja usado
        var serverIp = new IPAddress(new byte[]
        {
            networkBytes[0],
            networkBytes[1],
            networkBytes[2],
            (byte)(networkBytes[3] + 1)
        });
        allocated.Add(serverIp.ToString());

        // Começar do .2 (primeiro IP utilizável para clientes, .1 é servidor)
        for (int i = 2; i <= maxHosts && i <= 254; i++)
        {
            var testIp = new IPAddress(new byte[]
            {
                networkBytes[0],
                networkBytes[1],
                networkBytes[2],
                (byte)(networkBytes[3] + i)
            });

            if (!allocated.Contains(testIp.ToString()))
            {
                return testIp.ToString();
            }
        }

        throw new InvalidOperationException("Não há IPs disponíveis na rede VPN.");
    }

    /// <summary>
    /// Sanitiza o nome do arquivo removendo caracteres inválidos
    /// </summary>
    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "router";

        // Remover caracteres inválidos para nomes de arquivo
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName
            .Where(c => !invalidChars.Contains(c))
            .ToArray());

        // Substituir espaços por underscores
        sanitized = sanitized.Replace(" ", "_");

        // Remover underscores múltiplos
        while (sanitized.Contains("__"))
        {
            sanitized = sanitized.Replace("__", "_");
        }

        // Remover underscores no início e fim
        sanitized = sanitized.Trim('_');

        // Se ficou vazio após sanitização, usar nome padrão
        if (string.IsNullOrWhiteSpace(sanitized))
            return "router";

        return sanitized;
    }

    private static RouterWireGuardPeerDto MapToDto(RouterWireGuardPeer peer)
    {
        return new RouterWireGuardPeerDto
        {
            Id = peer.Id,
            RouterId = peer.RouterId,
            VpnNetworkId = peer.VpnNetworkId,
            PublicKey = peer.PublicKey,
            AllowedIps = peer.AllowedIps,
            Endpoint = peer.Endpoint,
            ListenPort = peer.ListenPort,
            LastHandshake = peer.LastHandshake,
            BytesReceived = peer.BytesReceived,
            BytesSent = peer.BytesSent,
            IsEnabled = peer.IsEnabled,
            CreatedAt = peer.CreatedAt,
            UpdatedAt = peer.UpdatedAt
        };
    }
}

