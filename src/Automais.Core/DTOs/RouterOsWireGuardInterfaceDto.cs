namespace Automais.Core.DTOs;

/// <summary>
/// DTO para interface WireGuard do RouterOS
/// </summary>
public class RouterOsWireGuardInterfaceDto
{
    public string Name { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string? ListenPort { get; set; }
    public string? Mtu { get; set; }
    public bool Disabled { get; set; }
    public bool Running { get; set; }
}
