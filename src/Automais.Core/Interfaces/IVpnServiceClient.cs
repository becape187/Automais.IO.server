using Automais.Core.DTOs;

namespace Automais.Core.Interfaces;

/// <summary>
/// Cliente HTTP para comunicação com o serviço VPN Python
/// </summary>
public interface IVpnServiceClient
{
    /// <summary>
    /// Provisiona um peer WireGuard para um router
    /// </summary>
    Task<ProvisionPeerResult> ProvisionPeerAsync(
        Guid routerId,
        Guid vpnNetworkId,
        IEnumerable<string> allowedNetworks,
        string? manualIp = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém a configuração WireGuard para um router
    /// </summary>
    Task<RouterWireGuardConfigDto> GetConfigAsync(
        Guid routerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adiciona uma rede permitida ao router
    /// </summary>
    Task AddNetworkToRouterAsync(
        Guid routerId,
        string networkCidr,
        string? description = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove uma rede permitida do router
    /// </summary>
    Task RemoveNetworkFromRouterAsync(
        Guid routerId,
        string networkCidr,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Garante que a interface WireGuard existe para uma VpnNetwork
    /// </summary>
    Task EnsureInterfaceAsync(
        Guid vpnNetworkId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove a interface WireGuard de uma VpnNetwork
    /// </summary>
    Task RemoveInterfaceAsync(
        Guid vpnNetworkId,
        CancellationToken cancellationToken = default);
}

