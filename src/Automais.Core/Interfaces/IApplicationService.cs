using Automais.Core.DTOs;

namespace Automais.Core.Interfaces;

public interface IApplicationService
{
    Task<IEnumerable<ApplicationDto>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<ApplicationDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ApplicationDto> CreateAsync(Guid tenantId, CreateApplicationDto dto, CancellationToken cancellationToken = default);
    Task<ApplicationDto> UpdateAsync(Guid id, UpdateApplicationDto dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}


