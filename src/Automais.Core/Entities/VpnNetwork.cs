namespace Automais.Core.Entities;

/// <summary>
/// Define uma rede lógica utilizada para entrega de VPN (WireGuard sob o capô).
/// </summary>
public class VpnNetwork
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Cidr { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public string? DnsServers { get; set; }
    public string? ServerPublicKey { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public ICollection<VpnNetworkMembership> Memberships { get; set; } = new List<VpnNetworkMembership>();
    public ICollection<Device> Devices { get; set; } = new List<Device>();
}


