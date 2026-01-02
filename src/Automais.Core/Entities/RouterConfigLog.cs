namespace Automais.Core.Entities;

/// <summary>
/// Log de alterações de configuração do Router.
/// Armazena apenas logs de configuração (filtrados), não logs dinâmicos.
/// </summary>
public class RouterConfigLog
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// ID do router
    /// </summary>
    public Guid RouterId { get; set; }
    
    /// <summary>
    /// ID do tenant
    /// </summary>
    public Guid TenantId { get; set; }
    
    /// <summary>
    /// ID do usuário do portal que fez a alteração (se foi via portal)
    /// </summary>
    public Guid? PortalUserId { get; set; }
    
    /// <summary>
    /// Usuário do RouterOS que fez a alteração (se foi direto no router)
    /// </summary>
    public string? RouterOsUser { get; set; }
    
    /// <summary>
    /// Ação realizada: "set", "add", "remove"
    /// </summary>
    public string Action { get; set; } = string.Empty;
    
    /// <summary>
    /// Categoria: "firewall", "interface", "ip", "wireguard", etc
    /// </summary>
    public string Category { get; set; } = string.Empty;
    
    /// <summary>
    /// Caminho da configuração (ex: "/ip/firewall/filter/rule/5")
    /// </summary>
    public string ConfigPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Valor anterior (JSON)
    /// </summary>
    public string? BeforeValue { get; set; }
    
    /// <summary>
    /// Valor novo (JSON)
    /// </summary>
    public string? AfterValue { get; set; }
    
    /// <summary>
    /// Origem: "portal_api" ou "mikrotik_local"
    /// </summary>
    public string Source { get; set; } = string.Empty;
    
    /// <summary>
    /// Detalhes adicionais (JSON)
    /// </summary>
    public string? Details { get; set; }
    
    /// <summary>
    /// Timestamp da alteração
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    // Navigation Properties
    public Router Router { get; set; } = null!;
    public Tenant Tenant { get; set; } = null!;
    public TenantUser? PortalUser { get; set; }
}

