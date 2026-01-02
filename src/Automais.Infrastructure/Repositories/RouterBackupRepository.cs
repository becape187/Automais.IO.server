using Automais.Core.Entities;
using Automais.Core.Interfaces;
using Automais.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Automais.Infrastructure.Repositories;

/// <summary>
/// Implementação do repositório de Router Backups com EF Core
/// </summary>
public class RouterBackupRepository : IRouterBackupRepository
{
    private readonly ApplicationDbContext _context;

    public RouterBackupRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RouterBackup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<RouterBackup>()
            .Include(b => b.Router)
            .Include(b => b.Tenant)
            .Include(b => b.CreatedBy)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<RouterBackup>> GetByRouterIdAsync(Guid routerId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<RouterBackup>()
            .Include(b => b.CreatedBy)
            .Where(b => b.RouterId == routerId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<RouterBackup>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<RouterBackup>()
            .Include(b => b.Router)
            .Include(b => b.CreatedBy)
            .Where(b => b.TenantId == tenantId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<RouterBackup> CreateAsync(RouterBackup backup, CancellationToken cancellationToken = default)
    {
        _context.Set<RouterBackup>().Add(backup);
        await _context.SaveChangesAsync(cancellationToken);
        return backup;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var backup = await GetByIdAsync(id, cancellationToken);
        if (backup != null)
        {
            _context.Set<RouterBackup>().Remove(backup);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<int> CountByRouterIdAsync(Guid routerId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<RouterBackup>()
            .Where(b => b.RouterId == routerId)
            .CountAsync(cancellationToken);
    }
}

