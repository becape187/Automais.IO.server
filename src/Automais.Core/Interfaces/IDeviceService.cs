using Automais.Core.DTOs;

namespace Automais.Core.Interfaces;

public interface IDeviceService
{
    Task<IEnumerable<DeviceDto>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IEnumerable<DeviceDto>> GetByApplicationAsync(Guid applicationId, CancellationToken cancellationToken = default);
    Task<DeviceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DeviceDto> CreateAsync(Guid tenantId, CreateDeviceDto dto, CancellationToken cancellationToken = default);
    Task<DeviceDto> UpdateAsync(Guid id, UpdateDeviceDto dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}


