using Automais.Core.Entities;

namespace Automais.Core.Interfaces;

/// <summary>
/// Contrato para acesso a dados de Tenants
/// </summary>
public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<IEnumerable<Tenant>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Tenant> CreateAsync(Tenant tenant, CancellationToken cancellationToken = default);
    Task<Tenant> UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken = default);
}

