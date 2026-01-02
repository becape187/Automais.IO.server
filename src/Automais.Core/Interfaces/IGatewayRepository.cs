using Automais.Core.Entities;

namespace Automais.Core.Interfaces;

/// <summary>
/// Contrato para acesso a dados de Gateways
/// </summary>
public interface IGatewayRepository
{
    Task<Gateway?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Gateway?> GetByEuiAsync(string gatewayEui, CancellationToken cancellationToken = default);
    Task<IEnumerable<Gateway>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Gateway>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Gateway> CreateAsync(Gateway gateway, CancellationToken cancellationToken = default);
    Task<Gateway> UpdateAsync(Gateway gateway, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> EuiExistsAsync(string gatewayEui, CancellationToken cancellationToken = default);
}

