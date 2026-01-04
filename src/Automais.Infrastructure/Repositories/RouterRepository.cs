using Automais.Core.Entities;
using Automais.Core.Interfaces;
using Automais.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Automais.Infrastructure.Repositories;

/// <summary>
/// Implementação do repositório de Routers com EF Core
/// </summary>
public class RouterRepository : IRouterRepository
{
    private readonly ApplicationDbContext _context;

    public RouterRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Router?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Router>()
            .Include(r => r.Tenant)
            .Include(r => r.VpnNetwork)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<Router?> GetBySerialNumberAsync(string serialNumber, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Router>()
            .Include(r => r.Tenant)
            .Include(r => r.VpnNetwork)
            .FirstOrDefaultAsync(r => r.SerialNumber == serialNumber, cancellationToken);
    }

    public async Task<IEnumerable<Router>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Router>()
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Router>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Set<Router>()
            .Include(r => r.Tenant)
            .Include(r => r.VpnNetwork)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Router> CreateAsync(Router router, CancellationToken cancellationToken = default)
    {
        _context.Set<Router>().Add(router);
        await _context.SaveChangesAsync(cancellationToken);
        return router;
    }

    public async Task<Router> UpdateAsync(Router router, CancellationToken cancellationToken = default)
    {
        _context.Set<Router>().Update(router);
        await _context.SaveChangesAsync(cancellationToken);
        return router;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var router = await GetByIdAsync(id, cancellationToken);
        if (router != null)
        {
            _context.Set<Router>().Remove(router);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> SerialNumberExistsAsync(string serialNumber, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Router>()
            .AnyAsync(r => r.SerialNumber == serialNumber, cancellationToken);
    }
}

