namespace Automais.Core.Entities;

/// <summary>
/// Representa uma rota estática configurada em um Router Mikrotik.
/// As rotas são armazenadas no banco de dados e podem ser sincronizadas com o RouterOS.
/// </summary>
public class RouterStaticRoute
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// ID do router
    /// </summary>
    public Guid RouterId { get; set; }
    
    /// <summary>
    /// Destino da rota (ex: "0.0.0.0/0" para default gateway, "10.0.1.0/24" para rede específica)
    /// </summary>
    public string Destination { get; set; } = string.Empty;
    
    /// <summary>
    /// Gateway/nexthop da rota (ex: "10.0.0.1", "192.168.1.1")
    /// </summary>
    public string Gateway { get; set; } = string.Empty;
    
    /// <summary>
    /// Interface de saída (opcional, ex: "ether1", "wg-automais")
    /// </summary>
    public string? Interface { get; set; }
    
    /// <summary>
    /// Distância da rota (métrica, opcional)
    /// </summary>
    public int? Distance { get; set; }
    
    /// <summary>
    /// Escopo da rota (opcional)
    /// </summary>
    public int? Scope { get; set; }
    
    /// <summary>
    /// Tabela de roteamento (opcional, padrão: "main")
    /// </summary>
    public string? RoutingTable { get; set; }
    
    /// <summary>
    /// Descrição opcional da rota
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Comentário que será adicionado no RouterOS (formato: "AUTOMAIS.IO NÃO APAGAR: {Id}")
    /// </summary>
    public string Comment { get; set; } = string.Empty;
    
    /// <summary>
    /// Status da aplicação da rota no RouterOS
    /// </summary>
    public RouterStaticRouteStatus Status { get; set; } = RouterStaticRouteStatus.PendingAdd;
    
    /// <summary>
    /// Indica se a rota está ativa no RouterOS
    /// </summary>
    public bool IsActive { get; set; } = false;
    
    /// <summary>
    /// ID da rota no RouterOS (quando sincronizada)
    /// </summary>
    public string? RouterOsId { get; set; }
    
    /// <summary>
    /// Mensagem de erro se houver falha na aplicação
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation Properties
    public Router Router { get; set; } = null!;
}

/// <summary>
/// Status da aplicação da rota no RouterOS
/// </summary>
public enum RouterStaticRouteStatus
{
    PendingAdd = 1,      // Aguardando adição no RouterOS
    PendingRemove = 2,   // Aguardando remoção do RouterOS
    Applied = 3,         // Aplicada com sucesso no RouterOS
    Error = 4            // Erro ao aplicar no RouterOS
}

