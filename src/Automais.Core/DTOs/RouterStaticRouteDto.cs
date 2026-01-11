using Automais.Core.Entities;

namespace Automais.Core.DTOs;

/// <summary>
/// DTO para rota estática de um router
/// </summary>
public class RouterStaticRouteDto
{
    public Guid Id { get; set; }
    public Guid RouterId { get; set; }
    public string Destination { get; set; } = string.Empty;
    public string Gateway { get; set; } = string.Empty;
    public string? Interface { get; set; }
    public int? Distance { get; set; }
    public int? Scope { get; set; }
    public string? RoutingTable { get; set; }
    public string? Description { get; set; }
    public string Comment { get; set; } = string.Empty;
    public RouterStaticRouteStatus Status { get; set; }
    public bool IsActive { get; set; }
    public string? RouterOsId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// DTO para criação de rota estática
/// </summary>
public class CreateRouterStaticRouteDto
{
    public string Destination { get; set; } = string.Empty;
    public string Gateway { get; set; } = string.Empty;
    public string? Interface { get; set; }
    public int? Distance { get; set; }
    public int? Scope { get; set; }
    public string? RoutingTable { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// DTO para atualização de rota estática
/// </summary>
public class UpdateRouterStaticRouteDto
{
    public string? Destination { get; set; }
    public string? Gateway { get; set; }
    public string? Interface { get; set; }
    public int? Distance { get; set; }
    public int? Scope { get; set; }
    public string? RoutingTable { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// DTO para atualização em lote de rotas (adicionar/remover)
/// </summary>
public class BatchUpdateRoutesDto
{
    public IEnumerable<Guid> RoutesToAdd { get; set; } = Enumerable.Empty<Guid>();
    public IEnumerable<Guid> RoutesToRemove { get; set; } = Enumerable.Empty<Guid>();
}

/// <summary>
/// DTO para atualizar status de rota após aplicação
/// </summary>
public class UpdateRouteStatusDto
{
    public Guid RouteId { get; set; }
    public RouterStaticRouteStatus Status { get; set; }
    public string? RouterOsId { get; set; }
    public string? ErrorMessage { get; set; }
    /// <summary>
    /// Gateway usado na rota (pode ser atualizado pelo RouterOS quando interface é usada como gateway)
    /// </summary>
    public string? Gateway { get; set; }
}

