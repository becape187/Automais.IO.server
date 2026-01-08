namespace Automais.Core.DTOs;

/// <summary>
/// DTO para rota disponível de um router
/// </summary>
public class RouterRouteDto
{
    public Guid RouterAllowedNetworkId { get; set; }
    public Guid RouterId { get; set; }
    public string RouterName { get; set; } = string.Empty;
    public string NetworkCidr { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>
/// DTO para atualizar rotas permitidas de um usuário
/// </summary>
public class UpdateUserRoutesDto
{
    public IEnumerable<Guid> RouterAllowedNetworkIds { get; set; } = Enumerable.Empty<Guid>();
}

