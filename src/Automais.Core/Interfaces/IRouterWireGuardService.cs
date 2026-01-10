using Automais.Core.DTOs;

namespace Automais.Core.Interfaces;

/// <summary>
/// Interface para serviço de Router WireGuard
/// </summary>
public interface IRouterWireGuardService
{
    Task<IEnumerable<RouterWireGuardPeerDto>> GetByRouterIdAsync(Guid routerId, CancellationToken cancellationToken = default);
    Task<RouterWireGuardPeerDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RouterWireGuardPeerDto> CreatePeerAsync(Guid routerId, CreateRouterWireGuardPeerDto dto, CancellationToken cancellationToken = default);
    Task<RouterWireGuardPeerDto> UpdatePeerAsync(Guid id, CreateRouterWireGuardPeerDto dto, CancellationToken cancellationToken = default);
    Task UpdatePeerStatsAsync(Guid id, UpdatePeerStatsDto dto, CancellationToken cancellationToken = default);
    Task DeletePeerAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RouterWireGuardConfigDto> GetConfigAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RouterWireGuardPeerDto> RegenerateKeysAsync(Guid id, CancellationToken cancellationToken = default);
}

