using Automais.Core.Entities;
using Automais.Core.Interfaces;
using Automais.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Automais.Infrastructure.Repositories;

/// <summary>
/// Implementação do repositório de Router Config Logs com EF Core
/// </summary>
public class RouterConfigLogRepository : IRouterConfigLogRepository
{
    private readonly ApplicationDbContext _context;

    public RouterConfigLogRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RouterConfigLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<RouterConfigLog>()
            .Include(l => l.Router)
            .Include(l => l.Tenant)
            .Include(l => l.PortalUser)
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<RouterConfigLog>> GetByRouterIdAsync(Guid routerId, DateTime? since = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<RouterConfigLog>()
            .Include(l => l.PortalUser)
            .Where(l => l.RouterId == routerId);

        if (since.HasValue)
        {
            query = query.Where(l => l.Timestamp >= since.Value);
        }

        return await query
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<RouterConfigLog>> GetByTenantIdAsync(Guid tenantId, DateTime? since = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<RouterConfigLog>()
            .Include(l => l.Router)
            .Include(l => l.PortalUser)
            .Where(l => l.TenantId == tenantId);

        if (since.HasValue)
        {
            query = query.Where(l => l.Timestamp >= since.Value);
        }

        return await query
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<RouterConfigLog>> GetByUserIdAsync(Guid userId, DateTime? since = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<RouterConfigLog>()
            .Include(l => l.Router)
            .Where(l => l.PortalUserId == userId);

        if (since.HasValue)
        {
            query = query.Where(l => l.Timestamp >= since.Value);
        }

        return await query
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task<RouterConfigLog> CreateAsync(RouterConfigLog log, CancellationToken cancellationToken = default)
    {
        _context.Set<RouterConfigLog>().Add(log);
        await _context.SaveChangesAsync(cancellationToken);
        return log;
    }

    public async Task<int> CountByRouterIdAsync(Guid routerId, DateTime? since = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<RouterConfigLog>()
            .Where(l => l.RouterId == routerId);

        if (since.HasValue)
        {
            query = query.Where(l => l.Timestamp >= since.Value);
        }

        return await query.CountAsync(cancellationToken);
    }
}

