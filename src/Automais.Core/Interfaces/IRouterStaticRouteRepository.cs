using Automais.Core.Entities;

namespace Automais.Core.Interfaces;

/// <summary>
/// Interface do repositório de Router Static Routes
/// </summary>
public interface IRouterStaticRouteRepository
{
    Task<RouterStaticRoute?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<RouterStaticRoute>> GetByRouterIdAsync(Guid routerId, CancellationToken cancellationToken = default);
    Task<RouterStaticRoute?> GetByRouterIdAndDestinationAsync(Guid routerId, string destination, CancellationToken cancellationToken = default);
    Task<RouterStaticRoute> CreateAsync(RouterStaticRoute route, CancellationToken cancellationToken = default);
    Task<RouterStaticRoute> UpdateAsync(RouterStaticRoute route, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

