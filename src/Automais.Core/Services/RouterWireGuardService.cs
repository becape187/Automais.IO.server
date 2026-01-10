using Automais.Core.Configuration;
using Automais.Core.DTOs;
using Automais.Core.Entities;
using Automais.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

namespace Automais.Core.Services;

/// <summary>
/// Serviço para gerenciamento de WireGuard dos Routers
/// Delega operações de WireGuard para o serviço Python via IVpnServiceClient
/// </summary>
public class RouterWireGuardService : IRouterWireGuardService
{
    private readonly IRouterWireGuardPeerRepository _peerRepository;
    private readonly IRouterRepository _routerRepository;
    private readonly IVpnNetworkRepository _vpnNetworkRepository;
    private readonly IVpnServiceClient _vpnServiceClient;
    private readonly WireGuardSettings _wireGuardSettings;
    private readonly ILogger<RouterWireGuardService>? _logger;

    public RouterWireGuardService(
        IRouterWireGuardPeerRepository peerRepository,
        IRouterRepository routerRepository,
        IVpnNetworkRepository vpnNetworkRepository,
        IOptions<WireGuardSettings> wireGuardSettings,
        IVpnServiceClient vpnServiceClient,
        ILogger<RouterWireGuardService>? logger = null)
    {
        _peerRepository = peerRepository;
        _routerRepository = routerRepository;
        _vpnNetworkRepository = vpnNetworkRepository;
        _wireGuardSettings = wireGuardSettings.Value;
        _vpnServiceClient = vpnServiceClient;
        _logger = logger;
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

        // Chamar serviço Python para provisionar peer
        // O serviço Python gerencia toda a lógica de WireGuard (chaves, IPs, interfaces, etc.)
        var allowedNetworks = new List<string>();
        string? manualIp = null;
        
        if (!string.IsNullOrWhiteSpace(dto.AllowedIps))
        {
            // Se AllowedIps foi fornecido, pode conter múltiplas redes separadas por vírgula
            // O primeiro elemento é o IP do router (manual), os demais são redes permitidas
            var networks = dto.AllowedIps.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            
            if (networks.Length > 0)
            {
                // Primeiro elemento é o IP do router (manual)
                manualIp = networks[0];
                
                // Demais elementos são redes permitidas
                if (networks.Length > 1)
                {
                    allowedNetworks.AddRange(networks.Skip(1));
                }
            }
        }

        _logger?.LogInformation("Chamando serviço VPN Python para provisionar peer: Router={RouterId}, VPN={VpnNetworkId}", 
            routerId, dto.VpnNetworkId);

        // Chamar serviço Python
        var result = await _vpnServiceClient.ProvisionPeerAsync(
            routerId,
            dto.VpnNetworkId,
            allowedNetworks,
            manualIp, // IP manual (primeiro elemento do AllowedIps) ou null para alocação automática
            cancellationToken);

        // Salvar peer no banco de dados
        var peer = new RouterWireGuardPeer
        {
            Id = Guid.NewGuid(),
            RouterId = routerId,
            VpnNetworkId = dto.VpnNetworkId,
            PublicKey = result.PublicKey,
            PrivateKey = result.PrivateKey, // Chave privada gerada pelo serviço Python
            AllowedIps = result.AllowedIps,
            Endpoint = null, // Endpoint vem da VpnNetwork
            ListenPort = dto.ListenPort ?? 51820,
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
        // Endpoint não é atualizado aqui - ele vem da VpnNetwork
        peer.ListenPort = dto.ListenPort;
        peer.UpdatedAt = DateTime.UtcNow;

        var updated = await _peerRepository.UpdateAsync(peer, cancellationToken);
        return MapToDto(updated);
    }

    public async Task UpdatePeerStatsAsync(Guid id, UpdatePeerStatsDto dto, CancellationToken cancellationToken = default)
    {
        var peer = await _peerRepository.GetByIdAsync(id, cancellationToken);
        if (peer == null)
        {
            throw new KeyNotFoundException($"Peer WireGuard com ID {id} não encontrado.");
        }

        // Atualizar apenas estatísticas (não configuração)
        if (dto.LastHandshake.HasValue)
        {
            peer.LastHandshake = dto.LastHandshake.Value;
        }
        if (dto.BytesReceived.HasValue)
        {
            peer.BytesReceived = dto.BytesReceived.Value;
        }
        if (dto.BytesSent.HasValue)
        {
            peer.BytesSent = dto.BytesSent.Value;
        }
        if (dto.PingSuccess.HasValue)
        {
            peer.PingSuccess = dto.PingSuccess.Value;
        }
        if (dto.PingAvgTimeMs.HasValue)
        {
            peer.PingAvgTimeMs = dto.PingAvgTimeMs.Value;
        }
        if (dto.PingPacketLoss.HasValue)
        {
            peer.PingPacketLoss = dto.PingPacketLoss.Value;
        }
        
        peer.UpdatedAt = DateTime.UtcNow;

        await _peerRepository.UpdateAsync(peer, cancellationToken);
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

        // Buscar configuração do serviço Python
        _logger?.LogInformation("Chamando serviço VPN Python para obter config: Router={RouterId}", peer.RouterId);
        
        var config = await _vpnServiceClient.GetConfigAsync(peer.RouterId, cancellationToken);
        
        return config;
    }

    public async Task<RouterWireGuardPeerDto> RegenerateKeysAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var peer = await _peerRepository.GetByIdAsync(id, cancellationToken);
        if (peer == null)
        {
            throw new KeyNotFoundException($"Peer WireGuard com ID {id} não encontrado.");
        }

        // ⚠️ ATENÇÃO: Regenerar chaves requer remover o peer antigo do servidor e adicionar o novo
        // Por enquanto, lançar exceção informando que precisa deletar e recriar
        throw new NotImplementedException(
            "Regeneração de chaves ainda não está implementada. " +
            "Para regenerar chaves, delete o peer e crie novamente via ProvisionRouterAsync. " +
            "Isso garantirá que as chaves sejam geradas corretamente usando 'wg genkey' do sistema Linux.");
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
            PingSuccess = peer.PingSuccess,
            PingAvgTimeMs = peer.PingAvgTimeMs,
            PingPacketLoss = peer.PingPacketLoss,
            IsEnabled = peer.IsEnabled,
            CreatedAt = peer.CreatedAt,
            UpdatedAt = peer.UpdatedAt
        };
    }

    /// <summary>
    /// Sanitiza o nome do arquivo removendo caracteres inválidos
    /// </summary>
    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "router";

        // Remover caracteres inválidos para nomes de arquivo (sem usar Path para manter na camada Core)
        var invalidChars = new[] { '"', '<', '>', '|', ':', '*', '?', '\\', '/' };
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
}

