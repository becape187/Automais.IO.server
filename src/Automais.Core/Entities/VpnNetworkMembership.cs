namespace Automais.Core.Entities;

/// <summary>
/// Define a associação de um usuário a uma rede VPN.
/// </summary>
public class VpnNetworkMembership
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid VpnNetworkId { get; set; }
    public Guid TenantUserId { get; set; }
    public string? AssignedIp { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public VpnNetwork VpnNetwork { get; set; } = null!;
    public TenantUser TenantUser { get; set; } = null!;
}


