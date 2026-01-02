using Automais.Core.Entities;
using Automais.Core.Interfaces;

namespace Automais.Infrastructure.Repositories;

/// <summary>
/// Implementação em memória do repositório de Tenants (sem banco de dados)
/// </summary>
public class InMemoryTenantRepository : ITenantRepository
{
    private readonly List<Tenant> _tenants = new();
    private readonly object _lock = new();

    public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var tenant = _tenants.FirstOrDefault(t => t.Id == id);
            return Task.FromResult(tenant);
        }
    }

    public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var tenant = _tenants.FirstOrDefault(t => t.Slug == slug);
            return Task.FromResult(tenant);
        }
    }

    public Task<IEnumerable<Tenant>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var result = _tenants
                .Where(t => t.Status != TenantStatus.Deleted)
                .OrderBy(t => t.Name)
                .ToList();
            return Task.FromResult<IEnumerable<Tenant>>(result);
        }
    }

    public Task<Tenant> CreateAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _tenants.Add(tenant);
            return Task.FromResult(tenant);
        }
    }

    public Task<Tenant> UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var existing = _tenants.FirstOrDefault(t => t.Id == tenant.Id);
            if (existing != null)
            {
                _tenants.Remove(existing);
                _tenants.Add(tenant);
            }
            return Task.FromResult(tenant);
        }
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var tenant = _tenants.FirstOrDefault(t => t.Id == id);
            if (tenant != null)
            {
                _tenants.Remove(tenant);
            }
            return Task.CompletedTask;
        }
    }

    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var exists = _tenants.Any(t => t.Slug == slug);
            return Task.FromResult(exists);
        }
    }
}

