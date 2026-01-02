using Automais.Core.DTOs;

namespace Automais.Core.Interfaces;

/// <summary>
/// Contrato para comunicação com a API do ChirpStack
/// </summary>
public interface IChirpStackClient
{
    // Gateways
    Task<IEnumerable<GatewayDto>> ListGatewaysAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<GatewayDto?> GetGatewayAsync(string gatewayEui, CancellationToken cancellationToken = default);
    Task CreateGatewayAsync(CreateGatewayDto gateway, string tenantId, CancellationToken cancellationToken = default);
    Task UpdateGatewayAsync(string gatewayEui, UpdateGatewayDto gateway, CancellationToken cancellationToken = default);
    Task DeleteGatewayAsync(string gatewayEui, CancellationToken cancellationToken = default);
    Task<GatewayStatsDto?> GetGatewayStatsAsync(string gatewayEui, CancellationToken cancellationToken = default);
    
    // Tenants (ChirpStack)
    Task<string> CreateChirpStackTenantAsync(string tenantName, CancellationToken cancellationToken = default);
    Task DeleteChirpStackTenantAsync(string tenantId, CancellationToken cancellationToken = default);
}

