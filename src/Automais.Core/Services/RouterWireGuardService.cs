using Automais.Core.DTOs;
using Automais.Core.Entities;
using Automais.Core.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace Automais.Core.Services;

/// <summary>
/// Serviço para gerenciamento de WireGuard dos Routers
/// </summary>
public class RouterWireGuardService : IRouterWireGuardService
{
    private readonly IRouterWireGuardPeerRepository _peerRepository;
    private readonly IRouterRepository _routerRepository;
    private readonly IVpnNetworkRepository _vpnNetworkRepository;
    private readonly IWireGuardServerService? _wireGuardServerService;

    public RouterWireGuardService(
        IRouterWireGuardPeerRepository peerRepository,
        IRouterRepository routerRepository,
        IVpnNetworkRepository vpnNetworkRepository,
        IWireGuardServerService? wireGuardServerService = null)
    {
        _peerRepository = peerRepository;
        _routerRepository = routerRepository;
        _vpnNetworkRepository = vpnNetworkRepository;
        _wireGuardServerService = wireGuardServerService;
    }

    public async Task<IEnumerable<RouterWireGuardPeerDto>> GetByRouterIdAsync(Guid routerId, CancellationToken cancellationToken = default)
    {
        var peers = await _peerRepository.GetByRouterIdAsync(routerId, cancellationToken);
        return peers.Select(MapToDto);
    }

    public async Task<RouterWireGuardPeerDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var peer = await _peerRepository.GetByIdAsync(id, cancellationToken);
        return peer == null ? null : MapToDto(peer);
    }

    public async Task<RouterWireGuardPeerDto> CreatePeerAsync(Guid routerId, CreateRouterWireGuardPeerDto dto, CancellationToken cancellationToken = default)
    {
        var router = await _routerRepository.GetByIdAsync(routerId, cancellationToken);
        if (router == null)
        {
            throw new KeyNotFoundException($"Router com ID {routerId} não encontrado.");
        }

        var vpnNetwork = await _vpnNetworkRepository.GetByIdAsync(dto.VpnNetworkId, cancellationToken);
        if (vpnNetwork == null)
        {
            throw new KeyNotFoundException($"Rede VPN com ID {dto.VpnNetworkId} não encontrada.");
        }

        // Verificar se já existe peer para este router e network
        var existing = await _peerRepository.GetByRouterIdAndNetworkIdAsync(routerId, dto.VpnNetworkId, cancellationToken);
        if (existing != null)
        {
            throw new InvalidOperationException($"Já existe um peer WireGuard para este router e rede VPN.");
        }

        // Validar e alocar IP (se AllowedIps foi fornecido, validar; senão, alocar automaticamente)
        string routerIp;
        if (!string.IsNullOrWhiteSpace(dto.AllowedIps))
        {
            // IP manual foi especificado - validar
            if (_wireGuardServerService != null)
            {
                // Usar o método de alocação para validar o IP manual
                routerIp = await _wireGuardServerService.AllocateVpnIpAsync(dto.VpnNetworkId, dto.AllowedIps, cancellationToken);
            }
            else
            {
                // Se não tiver acesso ao serviço, apenas usar o IP fornecido (sem validação)
                routerIp = dto.AllowedIps;
            }
        }
        else
        {
            // IP não especificado - alocar automaticamente
            if (_wireGuardServerService != null)
            {
                routerIp = await _wireGuardServerService.AllocateVpnIpAsync(dto.VpnNetworkId, null, cancellationToken);
            }
            else
            {
                throw new InvalidOperationException("Não é possível alocar IP automaticamente. Especifique AllowedIps ou configure IWireGuardServerService.");
            }
        }

        // Gerar par de chaves WireGuard
        var (publicKey, privateKey) = GenerateWireGuardKeys();

        var peer = new RouterWireGuardPeer
        {
            Id = Guid.NewGuid(),
            RouterId = routerId,
            VpnNetworkId = dto.VpnNetworkId,
            PublicKey = publicKey,
            PrivateKey = privateKey, // TODO: Criptografar antes de salvar
            AllowedIps = routerIp, // Usar IP validado/alocado
            Endpoint = dto.Endpoint,
            ListenPort = dto.ListenPort,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await _peerRepository.CreateAsync(peer, cancellationToken);
        return MapToDto(created);
    }

    public async Task<RouterWireGuardPeerDto> UpdatePeerAsync(Guid id, CreateRouterWireGuardPeerDto dto, CancellationToken cancellationToken = default)
    {
        var peer = await _peerRepository.GetByIdAsync(id, cancellationToken);
        if (peer == null)
        {
            throw new KeyNotFoundException($"Peer WireGuard com ID {id} não encontrado.");
        }

        peer.AllowedIps = dto.AllowedIps;
        peer.Endpoint = dto.Endpoint;
        peer.ListenPort = dto.ListenPort;
        peer.UpdatedAt = DateTime.UtcNow;

        var updated = await _peerRepository.UpdateAsync(peer, cancellationToken);
        return MapToDto(updated);
    }

    public async Task DeletePeerAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var peer = await _peerRepository.GetByIdAsync(id, cancellationToken);
        if (peer == null)
        {
            return;
        }

        await _peerRepository.DeleteAsync(id, cancellationToken);
    }

    public async Task<RouterWireGuardConfigDto> GetConfigAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var peer = await _peerRepository.GetByIdAsync(id, cancellationToken);
        if (peer == null)
        {
            throw new KeyNotFoundException($"Peer WireGuard com ID {id} não encontrado.");
        }

        var router = await _routerRepository.GetByIdAsync(peer.RouterId, cancellationToken);
        var vpnNetwork = await _vpnNetworkRepository.GetByIdAsync(peer.VpnNetworkId, cancellationToken);

        if (router == null || vpnNetwork == null)
        {
            throw new InvalidOperationException("Router ou rede VPN não encontrados.");
        }

        // Gerar conteúdo do arquivo .conf
        var configContent = GenerateWireGuardConfig(peer, router, vpnNetwork);
        var fileName = $"router_{router.Name}_{peer.Id}.conf";

        return new RouterWireGuardConfigDto
        {
            ConfigContent = configContent,
            FileName = fileName
        };
    }

    public async Task<RouterWireGuardPeerDto> RegenerateKeysAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var peer = await _peerRepository.GetByIdAsync(id, cancellationToken);
        if (peer == null)
        {
            throw new KeyNotFoundException($"Peer WireGuard com ID {id} não encontrado.");
        }

        var (publicKey, privateKey) = GenerateWireGuardKeys();
        peer.PublicKey = publicKey;
        peer.PrivateKey = privateKey; // TODO: Criptografar
        peer.UpdatedAt = DateTime.UtcNow;

        var updated = await _peerRepository.UpdateAsync(peer, cancellationToken);
        return MapToDto(updated);
    }

    private static (string publicKey, string privateKey) GenerateWireGuardKeys()
    {
        // TODO: Implementar geração real de chaves WireGuard usando biblioteca apropriada
        // Por enquanto retorna chaves mockadas
        var random = new Random();
        var bytes = new byte[32];
        random.NextBytes(bytes);
        var privateKey = Convert.ToBase64String(bytes);
        random.NextBytes(bytes);
        var publicKey = Convert.ToBase64String(bytes);
        return (publicKey, privateKey);
    }

    private static string GenerateWireGuardConfig(RouterWireGuardPeer peer, Router router, VpnNetwork vpnNetwork)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[Interface]");
        sb.AppendLine($"PrivateKey = {peer.PrivateKey}");
        sb.AppendLine($"Address = {peer.AllowedIps}");
        sb.AppendLine();
        sb.AppendLine("[Peer]");
        sb.AppendLine($"PublicKey = {peer.PublicKey}");
        if (!string.IsNullOrWhiteSpace(peer.Endpoint))
        {
            sb.AppendLine($"Endpoint = {peer.Endpoint}:{peer.ListenPort ?? 51820}");
        }
        sb.AppendLine($"AllowedIPs = {vpnNetwork.Cidr}");
        sb.AppendLine("PersistentKeepalive = 25");
        return sb.ToString();
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

