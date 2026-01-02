using Automais.Core.DTOs;

namespace Automais.Core.Interfaces;

/// <summary>
/// Interface para serviço de Routers
/// </summary>
public interface IRouterService
{
    Task<IEnumerable<RouterDto>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<RouterDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RouterDto> CreateAsync(Guid tenantId, CreateRouterDto dto, CancellationToken cancellationToken = default);
    Task<RouterDto> UpdateAsync(Guid id, UpdateRouterDto dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RouterDto> TestConnectionAsync(Guid id, CancellationToken cancellationToken = default);
}

