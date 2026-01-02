namespace Automais.Core.DTOs;

/// <summary>
/// DTO para Router Config Log
/// </summary>
public class RouterConfigLogDto
{
    public Guid Id { get; set; }
    public Guid RouterId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? PortalUserId { get; set; }
    public string? PortalUserName { get; set; }
    public string? RouterOsUser { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ConfigPath { get; set; } = string.Empty;
    public string? BeforeValue { get; set; }
    public string? AfterValue { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; }
}

