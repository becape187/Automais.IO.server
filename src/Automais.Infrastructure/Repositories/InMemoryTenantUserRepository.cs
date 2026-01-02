using Automais.Core.Entities;
using Automais.Core.Interfaces;

namespace Automais.Infrastructure.Repositories;

public class InMemoryTenantUserRepository : ITenantUserRepository
{
    private readonly List<TenantUser> _users = new();
    private readonly object _lock = new();

    public Task<TenantUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var user = _users.FirstOrDefault(u => u.Id == id);
            return Task.FromResult(user);
        }
    }

    public Task<IEnumerable<TenantUser>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var result = _users
                .Where(u => u.TenantId == tenantId)
                .OrderBy(u => u.Name)
                .ToList();

            return Task.FromResult<IEnumerable<TenantUser>>(result);
        }
    }

    public Task<TenantUser> CreateAsync(TenantUser user, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _users.Add(user);
            return Task.FromResult(user);
        }
    }

    public Task<TenantUser> UpdateAsync(TenantUser user, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var index = _users.FindIndex(u => u.Id == user.Id);
            if (index >= 0)
            {
                _users[index] = user;
            }

            return Task.FromResult(user);
        }
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var user = _users.FirstOrDefault(u => u.Id == id);
            if (user != null)
            {
                _users.Remove(user);
            }
        }

        return Task.CompletedTask;
    }

    public Task<bool> EmailExistsAsync(Guid tenantId, string email, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var exists = _users.Any(u =>
                u.TenantId == tenantId &&
                u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(exists);
        }
    }
}


