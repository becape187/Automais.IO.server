namespace Automais.Core.DTOs;

/// <summary>
/// DTO para Router Backup
/// </summary>
public class RouterBackupDto
{
    public Guid Id { get; set; }
    public Guid RouterId { get; set; }
    public Guid TenantId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? Description { get; set; }
    public string? RouterModel { get; set; }
    public string? RouterOsVersion { get; set; }
    public int CommandCount { get; set; }
    public string? FileHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? CreatedByUserName { get; set; }
    public bool IsAutomatic { get; set; }
}

/// <summary>
/// DTO para criação de Router Backup
/// </summary>
public class CreateRouterBackupDto
{
    public string? Description { get; set; }
    public bool IsAutomatic { get; set; } = false;
}

/// <summary>
/// DTO para comparação de backup
/// </summary>
public class RouterBackupComparisonDto
{
    public Guid BackupId { get; set; }
    public string BackupFileName { get; set; } = string.Empty;
    public DateTime BackupCreatedAt { get; set; }
    public int CommandsToAdd { get; set; }
    public int CommandsToRemove { get; set; }
    public int CommandsToModify { get; set; }
    public List<ConfigDifferenceDto> Differences { get; set; } = new();
    public string? UnifiedDiff { get; set; }
}

/// <summary>
/// DTO para diferença de configuração
/// </summary>
public class ConfigDifferenceDto
{
    public string Type { get; set; } = string.Empty; // "add", "remove", "modify"
    public string Category { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? CurrentValue { get; set; }
    public string? BackupValue { get; set; }
    public string? Command { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// DTO para restauração de backup
/// </summary>
public class RestoreRouterBackupDto
{
    public bool DryRun { get; set; } = false;
    public bool SkipErrors { get; set; } = false;
}

