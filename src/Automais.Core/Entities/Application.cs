namespace Automais.Core.Entities;

/// <summary>
/// Representa uma aplicação (lógica de negócio) que agrupa devices.
/// </summary>
public class Application
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Active;
    public string? ChirpStackApplicationId { get; set; }
    public string? Metadata { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public ICollection<Device> Devices { get; set; } = new List<Device>();
}

public enum ApplicationStatus
{
    Active = 1,
    Warning = 2,
    Archived = 3
}


