namespace Automais.Core.Interfaces;

/// <summary>
/// Interface para cliente da API RouterOS
/// </summary>
public interface IRouterOsClient
{
    Task<bool> TestConnectionAsync(string apiUrl, string username, string password, CancellationToken cancellationToken = default);
    Task<RouterOsSystemInfo> GetSystemInfoAsync(string apiUrl, string username, string password, CancellationToken cancellationToken = default);
    Task<string> ExportConfigAsync(string apiUrl, string username, string password, CancellationToken cancellationToken = default);
    Task ImportConfigAsync(string apiUrl, string username, string password, string configContent, CancellationToken cancellationToken = default);
    Task<List<RouterOsLog>> GetConfigLogsAsync(string apiUrl, string username, string password, DateTime? since = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Informações do sistema RouterOS
/// </summary>
public class RouterOsSystemInfo
{
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? FirmwareVersion { get; set; }
    public string? CpuLoad { get; set; }
    public string? MemoryUsage { get; set; }
    public string? Temperature { get; set; }
    public string? Uptime { get; set; }
}

/// <summary>
/// Log do RouterOS
/// </summary>
public class RouterOsLog
{
    public DateTime Timestamp { get; set; }
    public string? Topic { get; set; }
    public string? Action { get; set; }
    public string? Message { get; set; }
    public string? User { get; set; }
}

