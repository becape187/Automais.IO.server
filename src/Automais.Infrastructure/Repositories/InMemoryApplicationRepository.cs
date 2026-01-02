using Automais.Core.Entities;
using Automais.Core.Interfaces;

namespace Automais.Infrastructure.Repositories;

public class InMemoryApplicationRepository : IApplicationRepository
{
    private readonly List<Application> _applications = new();
    private readonly object _lock = new();

    public Task<Application?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var app = _applications.FirstOrDefault(a => a.Id == id);
            return Task.FromResult(app);
        }
    }

    public Task<IEnumerable<Application>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var apps = _applications
                .Where(a => a.TenantId == tenantId)
                .OrderBy(a => a.Name)
                .ToList();

            return Task.FromResult<IEnumerable<Application>>(apps);
        }
    }

    public Task<Application> CreateAsync(Application application, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _applications.Add(application);
            return Task.FromResult(application);
        }
    }

    public Task<Application> UpdateAsync(Application application, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var index = _applications.FindIndex(a => a.Id == application.Id);
            if (index >= 0)
            {
                _applications[index] = application;
            }

            return Task.FromResult(application);
        }
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var app = _applications.FirstOrDefault(a => a.Id == id);
            if (app != null)
            {
                _applications.Remove(app);
            }
        }

        return Task.CompletedTask;
    }

    public Task<bool> NameExistsAsync(Guid tenantId, string name, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var exists = _applications.Any(a =>
                a.TenantId == tenantId &&
                a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(exists);
        }
    }
}


