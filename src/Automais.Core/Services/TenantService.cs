using Automais.Core.DTOs;
using Automais.Core.Entities;
using Automais.Core.Interfaces;

namespace Automais.Core.Services;

/// <summary>
/// Serviço com lógica de negócio para Tenants
/// </summary>
public class TenantService : ITenantService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IChirpStackClient _chirpStackClient;

    public TenantService(
        ITenantRepository tenantRepository,
        IChirpStackClient chirpStackClient)
    {
        _tenantRepository = tenantRepository;
        _chirpStackClient = chirpStackClient;
    }

    public async Task<TenantDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(id, cancellationToken);
        return tenant == null ? null : MapToDto(tenant);
    }

    public async Task<TenantDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetBySlugAsync(slug, cancellationToken);
        return tenant == null ? null : MapToDto(tenant);
    }

    public async Task<IEnumerable<TenantDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenants = await _tenantRepository.GetAllAsync(cancellationToken);
        return tenants.Select(MapToDto);
    }

    public async Task<TenantDto> CreateAsync(CreateTenantDto dto, CancellationToken cancellationToken = default)
    {
        // Validar slug único
        if (await _tenantRepository.SlugExistsAsync(dto.Slug, cancellationToken))
        {
            throw new InvalidOperationException($"Slug '{dto.Slug}' já está em uso.");
        }

        // Criar tenant no ChirpStack primeiro
        var chirpStackTenantId = await _chirpStackClient.CreateChirpStackTenantAsync(dto.Name, cancellationToken);

        // Criar tenant no nosso banco
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Slug = dto.Slug,
            Status = TenantStatus.Active,
            ChirpStackTenantId = chirpStackTenantId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await _tenantRepository.CreateAsync(tenant, cancellationToken);
        return MapToDto(created);
    }

    public async Task<TenantDto> UpdateAsync(Guid id, UpdateTenantDto dto, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(id, cancellationToken);
        if (tenant == null)
        {
            throw new KeyNotFoundException($"Tenant com ID {id} não encontrado.");
        }

        // Atualizar apenas campos fornecidos
        if (!string.IsNullOrWhiteSpace(dto.Name))
        {
            tenant.Name = dto.Name;
        }

        if (dto.Status.HasValue)
        {
            tenant.Status = dto.Status.Value;
        }

        tenant.UpdatedAt = DateTime.UtcNow;

        var updated = await _tenantRepository.UpdateAsync(tenant, cancellationToken);
        return MapToDto(updated);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(id, cancellationToken);
        if (tenant == null)
        {
            throw new KeyNotFoundException($"Tenant com ID {id} não encontrado.");
        }

        // Marcar como deleted ao invés de deletar fisicamente
        tenant.Status = TenantStatus.Deleted;
        tenant.UpdatedAt = DateTime.UtcNow;
        await _tenantRepository.UpdateAsync(tenant, cancellationToken);

        // Deletar tenant no ChirpStack (se existir)
        if (!string.IsNullOrEmpty(tenant.ChirpStackTenantId))
        {
            try
            {
                await _chirpStackClient.DeleteChirpStackTenantAsync(tenant.ChirpStackTenantId, cancellationToken);
            }
            catch (Exception ex)
            {
                // Log error mas não falhar a operação
                Console.WriteLine($"Erro ao deletar tenant do ChirpStack: {ex.Message}");
            }
        }
    }

    private static TenantDto MapToDto(Tenant tenant)
    {
        return new TenantDto
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Slug = tenant.Slug,
            Status = tenant.Status,
            ChirpStackTenantId = tenant.ChirpStackTenantId,
            CreatedAt = tenant.CreatedAt,
            UpdatedAt = tenant.UpdatedAt
        };
    }
}

