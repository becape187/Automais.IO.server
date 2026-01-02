using Automais.Core.Entities;
using Automais.Core.Interfaces;
using Automais.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Automais.Infrastructure.Repositories;

/// <summary>
/// Implementação do repositório de Gateways com EF Core
/// </summary>
public class GatewayRepository : IGatewayRepository
{
    private readonly ApplicationDbContext _context;

    public GatewayRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Gateway?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Gateways
            .Include(g => g.Tenant)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
    }

    public async Task<Gateway?> GetByEuiAsync(string gatewayEui, CancellationToken cancellationToken = default)
    {
        return await _context.Gateways
            .Include(g => g.Tenant)
            .FirstOrDefaultAsync(g => g.GatewayEui == gatewayEui.ToUpper(), cancellationToken);
    }

    public async Task<IEnumerable<Gateway>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.Gateways
            .Where(g => g.TenantId == tenantId)
            .OrderBy(g => g.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Gateway>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Gateways
            .Include(g => g.Tenant)
            .OrderBy(g => g.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Gateway> CreateAsync(Gateway gateway, CancellationToken cancellationToken = default)
    {
        _context.Gateways.Add(gateway);
        await _context.SaveChangesAsync(cancellationToken);
        return gateway;
    }

    public async Task<Gateway> UpdateAsync(Gateway gateway, CancellationToken cancellationToken = default)
    {
        _context.Gateways.Update(gateway);
        await _context.SaveChangesAsync(cancellationToken);
        return gateway;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var gateway = await GetByIdAsync(id, cancellationToken);
        if (gateway != null)
        {
            _context.Gateways.Remove(gateway);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> EuiExistsAsync(string gatewayEui, CancellationToken cancellationToken = default)
    {
        return await _context.Gateways
            .AnyAsync(g => g.GatewayEui == gatewayEui.ToUpper(), cancellationToken);
    }
}

