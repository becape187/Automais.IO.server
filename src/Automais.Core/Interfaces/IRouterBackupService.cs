using Automais.Core.DTOs;

namespace Automais.Core.Interfaces;

/// <summary>
/// Interface para serviço de Router Backups
/// </summary>
public interface IRouterBackupService
{
    Task<IEnumerable<RouterBackupDto>> GetByRouterIdAsync(Guid routerId, CancellationToken cancellationToken = default);
    Task<RouterBackupDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RouterBackupDto> CreateBackupAsync(Guid routerId, CreateRouterBackupDto dto, Guid? userId = null, CancellationToken cancellationToken = default);
    Task DeleteBackupAsync(Guid id, CancellationToken cancellationToken = default);
    Task<byte[]> DownloadBackupAsync(Guid id, CancellationToken cancellationToken = default);
    Task<string> GetBackupContentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RouterBackupComparisonDto> CompareBackupAsync(Guid routerId, Guid backupId, CancellationToken cancellationToken = default);
    Task RestoreBackupAsync(Guid routerId, Guid backupId, RestoreRouterBackupDto dto, CancellationToken cancellationToken = default);
}

