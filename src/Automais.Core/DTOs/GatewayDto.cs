using Automais.Core.Entities;

namespace Automais.Core.DTOs;

/// <summary>
/// DTO para retorno de Gateway
/// </summary>
public class GatewayDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string GatewayEui { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? Altitude { get; set; }
    public GatewayStatus Status { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// DTO para criação de Gateway
/// </summary>
public class CreateGatewayDto
{
    public string Name { get; set; } = string.Empty;
    public string GatewayEui { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? Altitude { get; set; }
}

/// <summary>
/// DTO para atualização de Gateway
/// </summary>
public class UpdateGatewayDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? Altitude { get; set; }
    public GatewayStatus? Status { get; set; }
}

/// <summary>
/// DTO para estatísticas do Gateway (do ChirpStack)
/// </summary>
public class GatewayStatsDto
{
    public string GatewayEui { get; set; } = string.Empty;
    public DateTime? LastSeenAt { get; set; }
    public int MessagesToday { get; set; }
    public double? SignalStrength { get; set; }
    public string Status { get; set; } = "offline";
}

