using Automais.Core.Entities;
using Automais.Core.Interfaces;
using Automais.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Automais.Infrastructure.Repositories;

/// <summary>
/// Implementação do repositório de Router Static Routes com EF Core
/// </summary>
public class RouterStaticRouteRepository : IRouterStaticRouteRepository
{
    private readonly ApplicationDbContext _context;

    public RouterStaticRouteRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RouterStaticRoute?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<RouterStaticRoute>()
            .Include(r => r.Router)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<RouterStaticRoute>> GetByRouterIdAsync(Guid routerId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<RouterStaticRoute>()
            .Include(r => r.Router)
            .Where(r => r.RouterId == routerId)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<RouterStaticRoute?> GetByRouterIdAndDestinationAsync(Guid routerId, string destination, CancellationToken cancellationToken = default)
    {
        return await _context.Set<RouterStaticRoute>()
            .FirstOrDefaultAsync(r => r.RouterId == routerId && r.Destination == destination, cancellationToken);
    }

    public async Task<RouterStaticRoute> CreateAsync(RouterStaticRoute route, CancellationToken cancellationToken = default)
    {
        _context.Set<RouterStaticRoute>().Add(route);
        await _context.SaveChangesAsync(cancellationToken);
        return route;
    }

    public async Task<RouterStaticRoute> UpdateAsync(RouterStaticRoute route, CancellationToken cancellationToken = default)
    {
        _context.Set<RouterStaticRoute>().Update(route);
        await _context.SaveChangesAsync(cancellationToken);
        return route;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var route = await GetByIdAsync(id, cancellationToken);
        if (route != null)
        {
            _context.Set<RouterStaticRoute>().Remove(route);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

