using Automais.Core.Entities;

namespace Automais.Core.Interfaces;

public interface ITenantUserRepository
{
    Task<TenantUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<TenantUser>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<TenantUser> CreateAsync(TenantUser user, CancellationToken cancellationToken = default);
    Task<TenantUser> UpdateAsync(TenantUser user, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(Guid tenantId, string email, CancellationToken cancellationToken = default);
}


