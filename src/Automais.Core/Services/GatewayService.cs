using Automais.Core.DTOs;
using Automais.Core.Entities;
using Automais.Core.Interfaces;

namespace Automais.Core.Services;

/// <summary>
/// Serviço com lógica de negócio para Gateways
/// </summary>
public class GatewayService : IGatewayService
{
    private readonly IGatewayRepository _gatewayRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IChirpStackClient _chirpStackClient;

    public GatewayService(
        IGatewayRepository gatewayRepository,
        ITenantRepository tenantRepository,
        IChirpStackClient chirpStackClient)
    {
        _gatewayRepository = gatewayRepository;
        _tenantRepository = tenantRepository;
        _chirpStackClient = chirpStackClient;
    }

    public async Task<GatewayDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var gateway = await _gatewayRepository.GetByIdAsync(id, cancellationToken);
        return gateway == null ? null : MapToDto(gateway);
    }

    public async Task<IEnumerable<GatewayDto>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var gateways = await _gatewayRepository.GetByTenantIdAsync(tenantId, cancellationToken);
        return gateways.Select(MapToDto);
    }

    public async Task<GatewayDto> CreateAsync(Guid tenantId, CreateGatewayDto dto, CancellationToken cancellationToken = default)
    {
        // Validar tenant existe
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant == null)
        {
            throw new KeyNotFoundException($"Tenant com ID {tenantId} não encontrado.");
        }

        if (string.IsNullOrEmpty(tenant.ChirpStackTenantId))
        {
            throw new InvalidOperationException("Tenant não possui ChirpStackTenantId configurado.");
        }

        // Validar EUI único
        if (await _gatewayRepository.EuiExistsAsync(dto.GatewayEui, cancellationToken))
        {
            throw new InvalidOperationException($"Gateway com EUI '{dto.GatewayEui}' já existe.");
        }

        // Criar gateway no ChirpStack primeiro
        await _chirpStackClient.CreateGatewayAsync(dto, tenant.ChirpStackTenantId, cancellationToken);

        // Criar gateway no nosso banco
        var gateway = new Gateway
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = dto.Name,
            GatewayEui = dto.GatewayEui.ToUpper(), // Padronizar maiúsculo
            Description = dto.Description,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            Altitude = dto.Altitude,
            Status = GatewayStatus.Offline, // Inicia como offline
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await _gatewayRepository.CreateAsync(gateway, cancellationToken);
        return MapToDto(created);
    }

    public async Task<GatewayDto> UpdateAsync(Guid id, UpdateGatewayDto dto, CancellationToken cancellationToken = default)
    {
        var gateway = await _gatewayRepository.GetByIdAsync(id, cancellationToken);
        if (gateway == null)
        {
            throw new KeyNotFoundException($"Gateway com ID {id} não encontrado.");
        }

        // Atualizar apenas campos fornecidos
        bool hasChanges = false;

        if (!string.IsNullOrWhiteSpace(dto.Name))
        {
            gateway.Name = dto.Name;
            hasChanges = true;
        }

        if (!string.IsNullOrWhiteSpace(dto.Description))
        {
            gateway.Description = dto.Description;
            hasChanges = true;
        }

        if (dto.Latitude.HasValue)
        {
            gateway.Latitude = dto.Latitude.Value;
            hasChanges = true;
        }

        if (dto.Longitude.HasValue)
        {
            gateway.Longitude = dto.Longitude.Value;
            hasChanges = true;
        }

        if (dto.Altitude.HasValue)
        {
            gateway.Altitude = dto.Altitude.Value;
            hasChanges = true;
        }

        if (dto.Status.HasValue)
        {
            gateway.Status = dto.Status.Value;
            hasChanges = true;
        }

        if (hasChanges)
        {
            gateway.UpdatedAt = DateTime.UtcNow;

            // Atualizar no ChirpStack também
            try
            {
                await _chirpStackClient.UpdateGatewayAsync(gateway.GatewayEui, dto, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao atualizar gateway no ChirpStack: {ex.Message}");
                // Continua mesmo se falhar no ChirpStack
            }

            var updated = await _gatewayRepository.UpdateAsync(gateway, cancellationToken);
            return MapToDto(updated);
        }

        return MapToDto(gateway);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var gateway = await _gatewayRepository.GetByIdAsync(id, cancellationToken);
        if (gateway == null)
        {
            throw new KeyNotFoundException($"Gateway com ID {id} não encontrado.");
        }

        // Deletar do ChirpStack primeiro
        try
        {
            await _chirpStackClient.DeleteGatewayAsync(gateway.GatewayEui, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao deletar gateway do ChirpStack: {ex.Message}");
            // Continua mesmo se falhar
        }

        // Deletar do nosso banco
        await _gatewayRepository.DeleteAsync(id, cancellationToken);
    }

    public async Task<GatewayStatsDto?> GetStatsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var gateway = await _gatewayRepository.GetByIdAsync(id, cancellationToken);
        if (gateway == null)
        {
            return null;
        }

        // Buscar stats do ChirpStack
        var stats = await _chirpStackClient.GetGatewayStatsAsync(gateway.GatewayEui, cancellationToken);
        return stats;
    }

    public async Task SyncWithChirpStackAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant == null || string.IsNullOrEmpty(tenant.ChirpStackTenantId))
        {
            throw new InvalidOperationException("Tenant inválido ou sem ChirpStackTenantId.");
        }

        // Buscar gateways do ChirpStack
        var chirpStackGateways = await _chirpStackClient.ListGatewaysAsync(tenant.ChirpStackTenantId, cancellationToken);
        
        // Buscar gateways do nosso banco
        var localGateways = await _gatewayRepository.GetByTenantIdAsync(tenantId, cancellationToken);
        var localEuis = localGateways.Select(g => g.GatewayEui.ToUpper()).ToHashSet();

        // Criar gateways que existem no ChirpStack mas não no nosso banco
        foreach (var csGateway in chirpStackGateways)
        {
            if (!localEuis.Contains(csGateway.GatewayEui.ToUpper()))
            {
                var gateway = new Gateway
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Name = csGateway.Name,
                    GatewayEui = csGateway.GatewayEui.ToUpper(),
                    Description = csGateway.Description,
                    Latitude = csGateway.Latitude,
                    Longitude = csGateway.Longitude,
                    Status = csGateway.Status == GatewayStatus.Online ? GatewayStatus.Online : GatewayStatus.Offline,
                    LastSeenAt = csGateway.LastSeenAt,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _gatewayRepository.CreateAsync(gateway, cancellationToken);
            }
        }
    }

    private static GatewayDto MapToDto(Gateway gateway)
    {
        return new GatewayDto
        {
            Id = gateway.Id,
            TenantId = gateway.TenantId,
            Name = gateway.Name,
            GatewayEui = gateway.GatewayEui,
            Description = gateway.Description,
            Latitude = gateway.Latitude,
            Longitude = gateway.Longitude,
            Altitude = gateway.Altitude,
            Status = gateway.Status,
            LastSeenAt = gateway.LastSeenAt,
            CreatedAt = gateway.CreatedAt,
            UpdatedAt = gateway.UpdatedAt
        };
    }
}

