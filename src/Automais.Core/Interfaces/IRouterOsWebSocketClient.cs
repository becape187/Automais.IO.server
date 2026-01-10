namespace Automais.Core.Interfaces;

/// <summary>
/// Cliente WebSocket para comunicação com o serviço RouterOS WebSocket
/// </summary>
public interface IRouterOsWebSocketClient
{
    /// <summary>
    /// Obtém o status da conexão RouterOS
    /// </summary>
    Task<RouterOsConnectionStatusDto> GetConnectionStatusAsync(
        Guid routerId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Status da conexão RouterOS
/// </summary>
public class RouterOsConnectionStatusDto
{
    public bool Connected { get; set; }
    public bool Success { get; set; }
    public string? RouterIp { get; set; }
    public RouterOsIdentityDto? Identity { get; set; }
    public RouterOsResourceDto? Resource { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Identidade do RouterOS
/// </summary>
public class RouterOsIdentityDto
{
    public string? Name { get; set; }
}

/// <summary>
/// Recursos do RouterOS
/// </summary>
public class RouterOsResourceDto
{
    public string? Uptime { get; set; }
    public string? Version { get; set; }
    public string? CpuLoad { get; set; }
    public string? FreeMemory { get; set; }
    public string? TotalMemory { get; set; }
    public string? Cpu { get; set; }
    public string? BoardName { get; set; }
    public string? ArchitectureName { get; set; }
}
