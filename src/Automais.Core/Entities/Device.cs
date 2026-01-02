namespace Automais.Core.Entities;

/// <summary>
/// Representa um dispositivo LoRaWAN registrado na plataforma.
/// </summary>
public class Device
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid? VpnNetworkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DevEui { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DeviceProfileId { get; set; }
    public DeviceStatus Status { get; set; } = DeviceStatus.Provisioning;
    public double? BatteryLevel { get; set; }
    public double? SignalStrength { get; set; }
    public string? Location { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public string? Metadata { get; set; }
    public bool VpnEnabled { get; set; }
    public string? VpnPublicKey { get; set; }
    public string? VpnIpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public Application Application { get; set; } = null!;
    public VpnNetwork? VpnNetwork { get; set; }
}

public enum DeviceStatus
{
    Provisioning = 1,
    Active = 2,
    Warning = 3,
    Offline = 4,
    Decommissioned = 5
}


