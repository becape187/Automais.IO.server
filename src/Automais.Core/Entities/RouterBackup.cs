namespace Automais.Core.Entities;

/// <summary>
/// Backup de configuração do Router (comandos exportados).
/// Armazena metadados, arquivo fica no filesystem.
/// </summary>
public class RouterBackup
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// ID do router
    /// </summary>
    public Guid RouterId { get; set; }
    
    /// <summary>
    /// ID do tenant
    /// </summary>
    public Guid TenantId { get; set; }
    
    /// <summary>
    /// Nome do arquivo (ex: "backup_2024-01-15_10-30-00.rsc")
    /// </summary>
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// Caminho completo do arquivo no filesystem
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Tamanho do arquivo em bytes
    /// </summary>
    public long FileSizeBytes { get; set; }
    
    /// <summary>
    /// Descrição opcional
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Modelo do router no momento do backup (referência)
    /// </summary>
    public string? RouterModel { get; set; }
    
    /// <summary>
    /// Versão do RouterOS no momento do backup (referência)
    /// </summary>
    public string? RouterOsVersion { get; set; }
    
    /// <summary>
    /// Quantidade de comandos exportados
    /// </summary>
    public int CommandCount { get; set; }
    
    /// <summary>
    /// Hash SHA256 do arquivo para validação
    /// </summary>
    public string? FileHash { get; set; }
    
    /// <summary>
    /// Data de criação
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// ID do usuário que criou o backup
    /// </summary>
    public Guid? CreatedByUserId { get; set; }
    
    /// <summary>
    /// Backup automático (true) ou manual (false)
    /// </summary>
    public bool IsAutomatic { get; set; }
    
    // Navigation Properties
    public Router Router { get; set; } = null!;
    public Tenant Tenant { get; set; } = null!;
    public TenantUser? CreatedBy { get; set; }
}

