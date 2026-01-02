using Automais.Core.DTOs;
using Automais.Core.Entities;
using Automais.Core.Interfaces;

namespace Automais.Core.Services;

public class ApplicationService : IApplicationService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IApplicationRepository _applicationRepository;
    private readonly IDeviceRepository _deviceRepository;

    public ApplicationService(
        ITenantRepository tenantRepository,
        IApplicationRepository applicationRepository,
        IDeviceRepository deviceRepository)
    {
        _tenantRepository = tenantRepository;
        _applicationRepository = applicationRepository;
        _deviceRepository = deviceRepository;
    }

    public async Task<IEnumerable<ApplicationDto>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var applications = await _applicationRepository.GetByTenantIdAsync(tenantId, cancellationToken);
        var result = new List<ApplicationDto>();

        foreach (var application in applications)
        {
            result.Add(await MapToDtoAsync(application, cancellationToken));
        }

        return result;
    }

    public async Task<ApplicationDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var application = await _applicationRepository.GetByIdAsync(id, cancellationToken);
        return application == null ? null : await MapToDtoAsync(application, cancellationToken);
    }

    public async Task<ApplicationDto> CreateAsync(Guid tenantId, CreateApplicationDto dto, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant == null)
        {
            throw new KeyNotFoundException($"Tenant com ID {tenantId} não encontrado.");
        }

        if (await _applicationRepository.NameExistsAsync(tenantId, dto.Name, cancellationToken))
        {
            throw new InvalidOperationException($"Já existe uma application chamada '{dto.Name}' para este tenant.");
        }

        var application = new Application
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = dto.Name,
            Description = dto.Description,
            Status = ApplicationStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await _applicationRepository.CreateAsync(application, cancellationToken);
        return await MapToDtoAsync(created, cancellationToken);
    }

    public async Task<ApplicationDto> UpdateAsync(Guid id, UpdateApplicationDto dto, CancellationToken cancellationToken = default)
    {
        var application = await _applicationRepository.GetByIdAsync(id, cancellationToken);
        if (application == null)
        {
            throw new KeyNotFoundException($"Application com ID {id} não encontrada.");
        }

        if (!string.IsNullOrWhiteSpace(dto.Name))
        {
            if (!application.Name.Equals(dto.Name, StringComparison.OrdinalIgnoreCase))
            {
                if (await _applicationRepository.NameExistsAsync(application.TenantId, dto.Name, cancellationToken))
                {
                    throw new InvalidOperationException($"Já existe uma application chamada '{dto.Name}' para este tenant.");
                }
            }

            application.Name = dto.Name;
        }

        if (dto.Description != null)
        {
            application.Description = dto.Description;
        }

        if (dto.Status.HasValue)
        {
            application.Status = dto.Status.Value;
        }

        application.UpdatedAt = DateTime.UtcNow;

        var updated = await _applicationRepository.UpdateAsync(application, cancellationToken);
        return await MapToDtoAsync(updated, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var application = await _applicationRepository.GetByIdAsync(id, cancellationToken);
        if (application == null)
        {
            return;
        }

        await _applicationRepository.DeleteAsync(id, cancellationToken);
    }

    private async Task<ApplicationDto> MapToDtoAsync(Application application, CancellationToken cancellationToken)
    {
        var deviceCount = await _deviceRepository.CountByApplicationIdAsync(application.Id, cancellationToken);

        return new ApplicationDto
        {
            Id = application.Id,
            TenantId = application.TenantId,
            Name = application.Name,
            Description = application.Description,
            Status = application.Status,
            ChirpStackApplicationId = application.ChirpStackApplicationId,
            DeviceCount = deviceCount,
            CreatedAt = application.CreatedAt,
            UpdatedAt = application.UpdatedAt
        };
    }
}


