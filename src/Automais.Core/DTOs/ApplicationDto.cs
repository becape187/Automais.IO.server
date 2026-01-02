using Automais.Core.Entities;

namespace Automais.Core.DTOs;

public class ApplicationDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ApplicationStatus Status { get; set; }
    public string? ChirpStackApplicationId { get; set; }
    public int DeviceCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateApplicationDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class UpdateApplicationDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public ApplicationStatus? Status { get; set; }
}


