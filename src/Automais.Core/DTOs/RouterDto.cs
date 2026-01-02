using Automais.Core.Entities;

namespace Automais.Core.DTOs;

/// <summary>
/// DTO para retorno de Router
/// </summary>
public class RouterDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public string? Model { get; set; }
    public string? FirmwareVersion { get; set; }
    public string? RouterOsApiUrl { get; set; }
    public Guid? VpnNetworkId { get; set; }
    public RouterStatus Status { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public string? HardwareInfo { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// DTO para criação de Router
/// </summary>
public class CreateRouterDto
{
    public string Name { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public string? Model { get; set; }
    public string? RouterOsApiUrl { get; set; }
    public string? RouterOsApiUsername { get; set; }
    public string? RouterOsApiPassword { get; set; }
    public Guid? VpnNetworkId { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// DTO para atualização de Router
/// </summary>
public class UpdateRouterDto
{
    public string? Name { get; set; }
    public string? SerialNumber { get; set; }
    public string? Model { get; set; }
    public string? RouterOsApiUrl { get; set; }
    public string? RouterOsApiUsername { get; set; }
    public string? RouterOsApiPassword { get; set; }
    public Guid? VpnNetworkId { get; set; }
    public RouterStatus? Status { get; set; }
    public string? Description { get; set; }
}

