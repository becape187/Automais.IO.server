namespace Automais.Core.Entities;

/// <summary>
/// Representa um Cliente (Tenant) da plataforma.
/// Cada tenant é isolado e possui seus próprios gateways, devices, etc.
/// </summary>
public class Tenant
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Nome do cliente/empresa
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Slug único para URLs (ex: "acme-corp")
    /// </summary>
    public string Slug { get; set; } = string.Empty;
    
    /// <summary>
    /// Status do tenant
    /// </summary>
    public TenantStatus Status { get; set; } = TenantStatus.Active;
    
    /// <summary>
    /// ID do tenant no ChirpStack (se criado)
    /// </summary>
    public string? ChirpStackTenantId { get; set; }
    
    /// <summary>
    /// Informações adicionais (JSON)
    /// </summary>
    public string? Metadata { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation Properties
    public ICollection<Gateway> Gateways { get; set; } = new List<Gateway>();
    public ICollection<TenantUser> Users { get; set; } = new List<TenantUser>();
    public ICollection<Application> Applications { get; set; } = new List<Application>();
    public ICollection<Device> Devices { get; set; } = new List<Device>();
    public ICollection<VpnNetwork> VpnNetworks { get; set; } = new List<VpnNetwork>();
}

public enum TenantStatus
{
    Active = 1,      // Ativo e operacional
    Suspended = 2,   // Suspenso temporariamente
    Deleted = 3      // Marcado para exclusão
}

