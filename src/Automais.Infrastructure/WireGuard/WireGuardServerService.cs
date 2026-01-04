using Automais.Core.DTOs;
using Automais.Core.Entities;
using Automais.Core.Interfaces;
using Microsoft.Extensions.Logging;
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

    public WireGuardServerService(
        IRouterRepository routerRepository,
        IRouterWireGuardPeerRepository peerRepository,
        IRouterAllowedNetworkRepository allowedNetworkRepository,
        IVpnNetworkRepository vpnNetworkRepository,
        ILogger<WireGuardServerService>? logger = null)
    {
        _routerRepository = routerRepository;
        _peerRepository = peerRepository;
        _allowedNetworkRepository = allowedNetworkRepository;
        _vpnNetworkRepository = vpnNetworkRepository;
        _logger = logger;
    }

    public async Task<RouterWireGuardPeerDto> ProvisionRouterAsync(
        Guid routerId,
        Guid vpnNetworkId,
        IEnumerable<string> allowedNetworks,
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
        var (publicKey, privateKey) = GenerateWireGuardKeys();

        // Alocar IP da VPN
        var routerIp = await AllocateVpnIpAsync(vpnNetworkId, cancellationToken);

        // Construir allowed-ips (IP do router + redes permitidas)
        var allowedIps = new List<string> { routerIp };
        allowedIps.AddRange(allowedNetworks);
        var allowedIpsString = string.Join(",", allowedIps);

        // Criar peer no banco
        var peer = new RouterWireGuardPeer
        {
            Id = Guid.NewGuid(),
            RouterId = routerId,
            VpnNetworkId = vpnNetworkId,
            PublicKey = publicKey,
            PrivateKey = privateKey, // Texto plano inicialmente
            AllowedIps = routerIp,
            Endpoint = GetServerPublicIp(),
            ListenPort = 51820, // TODO: Obter da configuração da VpnNetwork
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _peerRepository.CreateAsync(peer, cancellationToken);

        // Salvar redes permitidas
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

        // Aplicar no WireGuard server (Linux)
        var interfaceName = GetInterfaceName(vpnNetworkId);
        await ExecuteWireGuardCommandAsync(
            $"set {interfaceName} peer {publicKey} allowed-ips {allowedIpsString}"
        );

        // Salvar configuração persistente
        await SaveWireGuardConfigAsync(interfaceName);

        // Gerar e salvar arquivo .conf
        var config = await GenerateConfigContentAsync(router, peer, vpnNetwork, allowedNetworks);
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
        var config = await GenerateConfigContentAsync(router, peer, vpnNetwork!, allowedNetworks.Select(n => n.NetworkCidr));
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
        var config = await GenerateConfigContentAsync(router, peer, vpnNetwork, allowedNetworks.Select(n => n.NetworkCidr));

        // Salvar no banco
        peer.ConfigContent = config;
        await _peerRepository.UpdateAsync(peer, cancellationToken);

        return new RouterWireGuardConfigDto
        {
            ConfigContent = config,
            FileName = $"router_{router.Name}_{routerId}.conf"
        };
    }

    public async Task<RouterWireGuardConfigDto> GetConfigAsync(
        Guid routerId,
        CancellationToken cancellationToken = default)
    {
        var router = await _routerRepository.GetByIdAsync(routerId, cancellationToken);
        if (router == null)
            throw new KeyNotFoundException($"Router com ID {routerId} não encontrado.");

        var peer = (await _peerRepository.GetByRouterIdAsync(routerId, cancellationToken)).FirstOrDefault();
        if (peer == null)
            throw new InvalidOperationException("Router não possui peer WireGuard configurado.");

        // Se já tem config salva, retorna
        if (!string.IsNullOrEmpty(peer.ConfigContent))
        {
            return new RouterWireGuardConfigDto
            {
                ConfigContent = peer.ConfigContent,
                FileName = $"router_{router.Name}_{routerId}.conf"
            };
        }

        // Senão, gera e salva
        return await GenerateAndSaveConfigAsync(routerId, cancellationToken);
    }

    public async Task<string> AllocateVpnIpAsync(
        Guid vpnNetworkId,
        CancellationToken cancellationToken = default)
    {
        var vpnNetwork = await _vpnNetworkRepository.GetByIdAsync(vpnNetworkId, cancellationToken);
        if (vpnNetwork == null)
            throw new KeyNotFoundException("Rede VPN não encontrada.");

        // Parse do CIDR (ex: "10.100.1.0/24")
        var (networkIp, prefixLength) = ParseCidr(vpnNetwork.Cidr);

        // Buscar IPs já alocados
        var allocatedIps = await _peerRepository.GetAllocatedIpsByNetworkAsync(vpnNetworkId, cancellationToken);

        // Encontrar próximo IP disponível
        var availableIp = FindNextAvailableIp(networkIp, prefixLength, allocatedIps);

        return $"{availableIp}/{prefixLength}";
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
        // TODO: Implementar salvamento do arquivo de configuração
        // wg-quick save wg-tenant1
        await Task.CompletedTask;
        _logger?.LogDebug("Configuração WireGuard salva para interface {InterfaceName}", interfaceName);
    }

    private string GetInterfaceName(Guid vpnNetworkId)
    {
        // TODO: Buscar nome da interface da VpnNetwork ou usar padrão
        // Por enquanto usa padrão baseado no ID
        return $"wg-{vpnNetworkId.ToString("N")[..8]}";
    }

    private string GetServerPublicIp()
    {
        // TODO: Obter IP público do servidor da configuração
        // Por enquanto retorna placeholder
        return "srv01.automais.io"; // ou IP público real
    }

    private (string publicKey, string privateKey) GenerateWireGuardKeys()
    {
        // Geração de chaves WireGuard usando wg genkey e wg pubkey
        // Por enquanto usa implementação mockada - TODO: usar wg genkey via shell
        var random = new Random();
        var privateKeyBytes = new byte[32];
        random.NextBytes(privateKeyBytes);
        var privateKey = Convert.ToBase64String(privateKeyBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        // Para gerar a chave pública, precisaríamos executar: echo <privateKey> | wg pubkey
        // Por enquanto gera mockada
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
        IEnumerable<string> allowedNetworks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[Interface]");
        sb.AppendLine($"PrivateKey = {peer.PrivateKey}");
        sb.AppendLine($"Address = {peer.AllowedIps}");
        sb.AppendLine();
        sb.AppendLine("[Peer]");
        sb.AppendLine($"PublicKey = {GetServerPublicKey(peer.VpnNetworkId)}"); // TODO: Buscar chave pública do servidor
        if (!string.IsNullOrWhiteSpace(peer.Endpoint))
        {
            sb.AppendLine($"Endpoint = {peer.Endpoint}:{peer.ListenPort ?? 51820}");
        }

        // Adicionar todas as redes permitidas
        var allNetworks = new List<string> { vpnNetwork.Cidr };
        allNetworks.AddRange(allowedNetworks);
        sb.AppendLine($"AllowedIPs = {string.Join(", ", allNetworks)}");
        sb.AppendLine("PersistentKeepalive = 25");

        return sb.ToString();
    }

    private string GetServerPublicKey(Guid vpnNetworkId)
    {
        // TODO: Buscar chave pública do servidor da VpnNetwork ou configuração
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

        // Começar do .1 (primeiro IP utilizável)
        for (int i = 1; i <= maxHosts && i <= 254; i++)
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

