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
    
    /// <summary>
    /// Cria um usuário no RouterOS com senha forte
    /// </summary>
    Task CreateUserAsync(string apiUrl, string username, string password, string newUsername, string newPassword, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Executa um comando RouterOS e retorna os resultados como lista de dicionários
    /// </summary>
    Task<List<Dictionary<string, string>>> ExecuteCommandAsync(string apiUrl, string username, string password, string command, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Executa um comando RouterOS que não retorna dados (add, set, remove, etc)
    /// </summary>
    Task ExecuteCommandNoResultAsync(string apiUrl, string username, string password, string command, CancellationToken cancellationToken = default);
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
    public string? TotalMemory { get; set; }
    public string? Temperature { get; set; }
    public string? Uptime { get; set; }
    public string? BoardName { get; set; }
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

