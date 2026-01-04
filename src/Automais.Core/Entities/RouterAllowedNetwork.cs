namespace Automais.Core.Entities;

/// <summary>
/// Representa uma rede permitida para um Router via WireGuard.
/// Quando um router é criado, pode ter acesso a múltiplas redes.
/// Essas redes são adicionadas ao allowed-ips do peer WireGuard.
/// </summary>
public class RouterAllowedNetwork
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// ID do router
    /// </summary>
    public Guid RouterId { get; set; }
    
    /// <summary>
    /// CIDR da rede permitida (ex: "10.0.1.0/24", "192.168.100.0/24")
    /// </summary>
    public string NetworkCidr { get; set; } = string.Empty;
    
    /// <summary>
    /// Descrição opcional da rede
    /// </summary>
    public string? Description { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    // Navigation Properties
    public Router Router { get; set; } = null!;
}

