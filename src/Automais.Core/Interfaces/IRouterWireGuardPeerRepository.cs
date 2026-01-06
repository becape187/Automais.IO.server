using Automais.Core.Entities;

namespace Automais.Core.Interfaces;

/// <summary>
/// Interface para repositório de Router WireGuard Peers
/// </summary>
public interface IRouterWireGuardPeerRepository
{
    Task<RouterWireGuardPeer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RouterWireGuardPeer?> GetByRouterIdAndNetworkIdAsync(Guid routerId, Guid vpnNetworkId, CancellationToken cancellationToken = default);
    Task<IEnumerable<RouterWireGuardPeer>> GetByRouterIdAsync(Guid routerId, CancellationToken cancellationToken = default);
    Task<IEnumerable<RouterWireGuardPeer>> GetByVpnNetworkIdAsync(Guid vpnNetworkId, CancellationToken cancellationToken = default);
    Task<IEnumerable<RouterWireGuardPeer>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<RouterWireGuardPeer> CreateAsync(RouterWireGuardPeer peer, CancellationToken cancellationToken = default);
    Task<RouterWireGuardPeer> UpdateAsync(RouterWireGuardPeer peer, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Busca todos os IPs alocados em uma rede VPN (para alocação de novos IPs)
    /// </summary>
    Task<IEnumerable<string>> GetAllocatedIpsByNetworkAsync(Guid vpnNetworkId, CancellationToken cancellationToken = default);
}

