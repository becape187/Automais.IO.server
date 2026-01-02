namespace Automais.Core.Entities;

/// <summary>
/// Representa um Gateway LoRaWAN.
/// Gateways recebem mensagens dos devices e encaminham para o ChirpStack.
/// </summary>
public class Gateway
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// ID do tenant proprietário
    /// </summary>
    public Guid TenantId { get; set; }
    
    /// <summary>
    /// Nome descritivo do gateway
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Gateway EUI (identificador único LoRaWAN) - 16 caracteres hex
    /// Ex: "0011223344556677"
    /// </summary>
    public string GatewayEui { get; set; } = string.Empty;
    
    /// <summary>
    /// Descrição opcional
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Latitude da localização
    /// </summary>
    public double? Latitude { get; set; }
    
    /// <summary>
    /// Longitude da localização
    /// </summary>
    public double? Longitude { get; set; }
    
    /// <summary>
    /// Altitude em metros
    /// </summary>
    public double? Altitude { get; set; }
    
    /// <summary>
    /// Status atual do gateway
    /// </summary>
    public GatewayStatus Status { get; set; } = GatewayStatus.Offline;
    
    /// <summary>
    /// Última vez que o gateway enviou mensagem
    /// </summary>
    public DateTime? LastSeenAt { get; set; }
    
    /// <summary>
    /// Informações adicionais (JSON)
    /// </summary>
    public string? Metadata { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation Properties
    public Tenant Tenant { get; set; } = null!;
}

public enum GatewayStatus
{
    Online = 1,       // Gateway online e operacional
    Offline = 2,      // Gateway offline
    Maintenance = 3   // Gateway em manutenção
}

