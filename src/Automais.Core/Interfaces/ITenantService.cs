using Automais.Core.DTOs;

namespace Automais.Core.Interfaces;

/// <summary>
/// Contrato para lógica de negócio de Tenants
/// </summary>
public interface ITenantService
{
    Task<TenantDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TenantDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<IEnumerable<TenantDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<TenantDto> CreateAsync(CreateTenantDto dto, CancellationToken cancellationToken = default);
    Task<TenantDto> UpdateAsync(Guid id, UpdateTenantDto dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

