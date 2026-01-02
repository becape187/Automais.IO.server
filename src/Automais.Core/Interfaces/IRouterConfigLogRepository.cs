using Automais.Core.Entities;

namespace Automais.Core.Interfaces;

/// <summary>
/// Interface para repositório de Router Config Logs
/// </summary>
public interface IRouterConfigLogRepository
{
    Task<RouterConfigLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<RouterConfigLog>> GetByRouterIdAsync(Guid routerId, DateTime? since = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<RouterConfigLog>> GetByTenantIdAsync(Guid tenantId, DateTime? since = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<RouterConfigLog>> GetByUserIdAsync(Guid userId, DateTime? since = null, CancellationToken cancellationToken = default);
    Task<RouterConfigLog> CreateAsync(RouterConfigLog log, CancellationToken cancellationToken = default);
    Task<int> CountByRouterIdAsync(Guid routerId, DateTime? since = null, CancellationToken cancellationToken = default);
}

