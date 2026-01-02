using Automais.Core.Entities;

namespace Automais.Core.Interfaces;

/// <summary>
/// Interface para repositório de Router Backups
/// </summary>
public interface IRouterBackupRepository
{
    Task<RouterBackup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<RouterBackup>> GetByRouterIdAsync(Guid routerId, CancellationToken cancellationToken = default);
    Task<IEnumerable<RouterBackup>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<RouterBackup> CreateAsync(RouterBackup backup, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> CountByRouterIdAsync(Guid routerId, CancellationToken cancellationToken = default);
}

