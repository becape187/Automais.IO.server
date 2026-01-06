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
    public string? RouterOsApiUsername { get; set; }
    public Guid? VpnNetworkId { get; set; }
    public RouterStatus Status { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public string? HardwareInfo { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    /// <summary>
    /// Redes permitidas para o router via VPN (ex: ["10.0.1.0/24", "192.168.100.0/24"])
    /// </summary>
    public IEnumerable<string>? AllowedNetworks { get; set; }
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
    /// <summary>
    /// Redes permitidas para o router via WireGuard (ex: ["10.0.1.0/24", "192.168.100.0/24"])
    /// </summary>
    public IEnumerable<string>? AllowedNetworks { get; set; }
    /// <summary>
    /// IP manual para o router na VPN (ex: "10.222.111.5/24"). Se não especificado, será alocado automaticamente.
    /// O IP .1 é sempre reservado para o servidor.
    /// </summary>
    public string? VpnIp { get; set; }
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

