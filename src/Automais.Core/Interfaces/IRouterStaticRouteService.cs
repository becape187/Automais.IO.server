using Automais.Core.DTOs;

namespace Automais.Core.Interfaces;

/// <summary>
/// Interface do serviço de Router Static Routes
/// </summary>
public interface IRouterStaticRouteService
{
    Task<IEnumerable<RouterStaticRouteDto>> GetByRouterIdAsync(Guid routerId, CancellationToken cancellationToken = default);
    Task<RouterStaticRouteDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RouterStaticRouteDto> CreateAsync(Guid routerId, CreateRouterStaticRouteDto dto, CancellationToken cancellationToken = default);
    Task<RouterStaticRouteDto> UpdateAsync(Guid id, UpdateRouterStaticRouteDto dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task BatchUpdateStatusAsync(Guid routerId, BatchUpdateRoutesDto dto, CancellationToken cancellationToken = default);
    Task UpdateRouteStatusAsync(UpdateRouteStatusDto dto, CancellationToken cancellationToken = default);
}

