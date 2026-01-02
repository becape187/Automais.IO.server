using Automais.Core.Entities;
using Automais.Core.Interfaces;
using Automais.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Automais.Infrastructure.Repositories;

/// <summary>
/// Implementação EF Core para Applications.
/// </summary>
public class ApplicationRepository : IApplicationRepository
{
    private readonly ApplicationDbContext _context;

    public ApplicationRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Application?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Applications
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Application>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.Applications
            .Where(a => a.TenantId == tenantId)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Application> CreateAsync(Application application, CancellationToken cancellationToken = default)
    {
        _context.Applications.Add(application);
        await _context.SaveChangesAsync(cancellationToken);
        return application;
    }

    public async Task<Application> UpdateAsync(Application application, CancellationToken cancellationToken = default)
    {
        _context.Applications.Update(application);
        await _context.SaveChangesAsync(cancellationToken);
        return application;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var application = await _context.Applications.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (application == null)
        {
            return;
        }

        _context.Applications.Remove(application);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> NameExistsAsync(Guid tenantId, string name, CancellationToken cancellationToken = default)
    {
        return await _context.Applications
            .AnyAsync(a => a.TenantId == tenantId && a.Name == name, cancellationToken);
    }
}


