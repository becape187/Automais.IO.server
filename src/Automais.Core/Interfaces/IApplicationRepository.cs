using Automais.Core.Entities;

namespace Automais.Core.Interfaces;

public interface IApplicationRepository
{
    Task<Application?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Application>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Application> CreateAsync(Application application, CancellationToken cancellationToken = default);
    Task<Application> UpdateAsync(Application application, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> NameExistsAsync(Guid tenantId, string name, CancellationToken cancellationToken = default);
}


