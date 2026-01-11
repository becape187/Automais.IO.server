using Automais.Core.DTOs;

namespace Automais.Core.Interfaces;

/// <summary>
/// Cliente HTTP para comunicação com o serviço RouterOS Python
/// </summary>
public interface IRouterOsServiceClient
{
    /// <summary>
    /// Adiciona rota estática no RouterOS
    /// </summary>
    Task<bool> AddRouteAsync(
        Guid routerId,
        RouterStaticRouteDto route,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove rota estática do RouterOS
    /// </summary>
    Task<bool> RemoveRouteAsync(
        Guid routerId,
        string routerOsRouteId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista interfaces WireGuard do RouterOS
    /// </summary>
    Task<List<RouterOsWireGuardInterfaceDto>> ListWireGuardInterfacesAsync(
        Guid routerId,
        CancellationToken cancellationToken = default);
}
