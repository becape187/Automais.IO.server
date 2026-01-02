using Automais.Core.DTOs;

namespace Automais.Core.Interfaces;

/// <summary>
/// Contrato para lógica de negócio de Gateways
/// </summary>
public interface IGatewayService
{
    Task<GatewayDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<GatewayDto>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<GatewayDto> CreateAsync(Guid tenantId, CreateGatewayDto dto, CancellationToken cancellationToken = default);
    Task<GatewayDto> UpdateAsync(Guid id, UpdateGatewayDto dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<GatewayStatsDto?> GetStatsAsync(Guid id, CancellationToken cancellationToken = default);
    Task SyncWithChirpStackAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

