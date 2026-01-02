using Automais.Core.Entities;

namespace Automais.Core.Interfaces;

/// <summary>
/// Interface para repositório de Routers
/// </summary>
public interface IRouterRepository
{
    Task<Router?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Router?> GetBySerialNumberAsync(string serialNumber, CancellationToken cancellationToken = default);
    Task<IEnumerable<Router>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Router>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Router> CreateAsync(Router router, CancellationToken cancellationToken = default);
    Task<Router> UpdateAsync(Router router, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> SerialNumberExistsAsync(string serialNumber, CancellationToken cancellationToken = default);
}

