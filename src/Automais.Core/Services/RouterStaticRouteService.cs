using Automais.Core.DTOs;
using Automais.Core.Entities;
using Automais.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.RegularExpressions;

namespace Automais.Core.Services;

/// <summary>
/// Serviço para gerenciamento de rotas estáticas dos Routers
/// Apenas CRUD no banco de dados. Sincronização com RouterOS é feita via servidor VPN.
/// </summary>
public class RouterStaticRouteService : IRouterStaticRouteService
{
    private readonly IRouterStaticRouteRepository _routeRepository;
    private readonly IRouterRepository _routerRepository;
    private readonly ILogger<RouterStaticRouteService>? _logger;

    public RouterStaticRouteService(
        IRouterStaticRouteRepository routeRepository,
        IRouterRepository routerRepository,
        ILogger<RouterStaticRouteService>? logger = null)
    {
        _routeRepository = routeRepository;
        _routerRepository = routerRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<RouterStaticRouteDto>> GetByRouterIdAsync(Guid routerId, CancellationToken cancellationToken = default)
    {
        var routes = await _routeRepository.GetByRouterIdAsync(routerId, cancellationToken);
        return routes.Select(MapToDto);
    }

    public async Task<RouterStaticRouteDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var route = await _routeRepository.GetByIdAsync(id, cancellationToken);
        return route == null ? null : MapToDto(route);
    }

    public async Task<RouterStaticRouteDto> CreateAsync(Guid routerId, CreateRouterStaticRouteDto dto, CancellationToken cancellationToken = default)
    {
        var router = await _routerRepository.GetByIdAsync(routerId, cancellationToken);
        if (router == null)
        {
            throw new KeyNotFoundException($"Router com ID {routerId} não encontrado.");
        }

        // Validações
        ValidateRoute(dto);

        // Verificar se já existe rota com mesmo destino para este router
        var existing = await _routeRepository.GetByRouterIdAndDestinationAsync(routerId, dto.Destination, cancellationToken);
        if (existing != null)
        {
            throw new InvalidOperationException($"Já existe uma rota com destino {dto.Destination} para este router.");
        }

        var routeId = Guid.NewGuid();
        var route = new RouterStaticRoute
        {
            Id = routeId,
            RouterId = routerId,
            Destination = dto.Destination.Trim(),
            Gateway = dto.Gateway.Trim(),
            Interface = dto.Interface?.Trim(),
            Distance = dto.Distance,
            Scope = dto.Scope,
            RoutingTable = dto.RoutingTable?.Trim() ?? "main",
            Description = dto.Description?.Trim(),
            Comment = $"AUTOMAIS.IO NÃO APAGAR: {routeId}",
            Status = RouterStaticRouteStatus.PendingAdd,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await _routeRepository.CreateAsync(route, cancellationToken);
        
        _logger?.LogInformation("Rota estática criada: Router={RouterId}, Destination={Destination}, Gateway={Gateway}", 
            routerId, dto.Destination, dto.Gateway);
        
        return MapToDto(created);
    }

    public async Task<RouterStaticRouteDto> UpdateAsync(Guid id, UpdateRouterStaticRouteDto dto, CancellationToken cancellationToken = default)
    {
        var route = await _routeRepository.GetByIdAsync(id, cancellationToken);
        if (route == null)
        {
            throw new KeyNotFoundException($"Rota estática com ID {id} não encontrada.");
        }

        // Se destino foi alterado, verificar se não conflita com outra rota
        if (!string.IsNullOrWhiteSpace(dto.Destination) && dto.Destination != route.Destination)
        {
            var existing = await _routeRepository.GetByRouterIdAndDestinationAsync(route.RouterId, dto.Destination, cancellationToken);
            if (existing != null && existing.Id != id)
            {
                throw new InvalidOperationException($"Já existe uma rota com destino {dto.Destination} para este router.");
            }
        }

        // Atualizar apenas campos fornecidos
        if (!string.IsNullOrWhiteSpace(dto.Destination))
            route.Destination = dto.Destination;
        if (!string.IsNullOrWhiteSpace(dto.Gateway))
            route.Gateway = dto.Gateway;
        if (dto.Interface != null)
            route.Interface = dto.Interface;
        if (dto.Distance.HasValue)
            route.Distance = dto.Distance;
        if (dto.Scope.HasValue)
            route.Scope = dto.Scope;
        if (dto.RoutingTable != null)
            route.RoutingTable = dto.RoutingTable;
        if (dto.Description != null)
            route.Description = dto.Description;
        
        route.UpdatedAt = DateTime.UtcNow;

        var updated = await _routeRepository.UpdateAsync(route, cancellationToken);
        
        _logger?.LogInformation("Rota estática atualizada: RouteId={RouteId}, Router={RouterId}", 
            id, route.RouterId);
        
        return MapToDto(updated);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var route = await _routeRepository.GetByIdAsync(id, cancellationToken);
        if (route == null)
        {
            return;
        }

        await _routeRepository.DeleteAsync(id, cancellationToken);
        
        _logger?.LogInformation("Rota estática deletada: RouteId={RouteId}, Router={RouterId}", 
            id, route.RouterId);
    }

    public async Task BatchUpdateStatusAsync(Guid routerId, BatchUpdateRoutesDto dto, CancellationToken cancellationToken = default)
    {
        // Marcar rotas para adicionar
        foreach (var routeId in dto.RoutesToAdd)
        {
            var route = await _routeRepository.GetByIdAsync(routeId, cancellationToken);
            if (route != null && route.RouterId == routerId)
            {
                route.Status = RouterStaticRouteStatus.PendingAdd;
                route.ErrorMessage = null;
                route.UpdatedAt = DateTime.UtcNow;
                await _routeRepository.UpdateAsync(route, cancellationToken);
            }
        }

        // Marcar rotas para remover
        foreach (var routeId in dto.RoutesToRemove)
        {
            var route = await _routeRepository.GetByIdAsync(routeId, cancellationToken);
            if (route != null && route.RouterId == routerId)
            {
                route.Status = RouterStaticRouteStatus.PendingRemove;
                route.ErrorMessage = null;
                route.UpdatedAt = DateTime.UtcNow;
                await _routeRepository.UpdateAsync(route, cancellationToken);
            }
        }

        _logger?.LogInformation("Status de rotas atualizado em lote: Router={RouterId}, Add={AddCount}, Remove={RemoveCount}", 
            routerId, dto.RoutesToAdd.Count(), dto.RoutesToRemove.Count());
    }

    public async Task UpdateRouteStatusAsync(UpdateRouteStatusDto dto, CancellationToken cancellationToken = default)
    {
        var route = await _routeRepository.GetByIdAsync(dto.RouteId, cancellationToken);
        if (route == null)
        {
            throw new KeyNotFoundException($"Rota estática com ID {dto.RouteId} não encontrada.");
        }

        route.Status = dto.Status;
        route.RouterOsId = dto.RouterOsId;
        route.ErrorMessage = dto.ErrorMessage;
        
        // Atualizar gateway se fornecido (RouterOS pode ter usado interface como gateway)
        if (!string.IsNullOrWhiteSpace(dto.Gateway))
        {
            route.Gateway = dto.Gateway;
        }
        
        route.UpdatedAt = DateTime.UtcNow;

        // Se status é Applied, marcar como ativa
        if (dto.Status == RouterStaticRouteStatus.Applied)
        {
            route.IsActive = true;
        }

        await _routeRepository.UpdateAsync(route, cancellationToken);
        
        _logger?.LogInformation("Status da rota atualizado: RouteId={RouteId}, Status={Status}", 
            dto.RouteId, dto.Status);
    }


    private static RouterStaticRouteDto MapToDto(RouterStaticRoute route)
    {
        return new RouterStaticRouteDto
        {
            Id = route.Id,
            RouterId = route.RouterId,
            Destination = route.Destination,
            Gateway = route.Gateway,
            Interface = route.Interface,
            Distance = route.Distance,
            Scope = route.Scope,
            RoutingTable = route.RoutingTable,
            Description = route.Description,
            Comment = route.Comment,
            Status = route.Status,
            IsActive = route.IsActive,
            RouterOsId = route.RouterOsId,
            ErrorMessage = route.ErrorMessage,
            CreatedAt = route.CreatedAt,
            UpdatedAt = route.UpdatedAt
        };
    }

    private static void ValidateRoute(CreateRouterStaticRouteDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Destination))
        {
            throw new InvalidOperationException("Destination é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(dto.Gateway))
        {
            throw new InvalidOperationException("Gateway é obrigatório.");
        }

        // Validar formato Destination (IP/CIDR)
        if (!IsValidIpOrCidr(dto.Destination))
        {
            throw new InvalidOperationException($"Destination inválido: {dto.Destination}. Use formato IP/CIDR (ex: 0.0.0.0/0 ou 10.0.1.0/24).");
        }

        // Validar formato Gateway (IP)
        if (!IsValidIp(dto.Gateway))
        {
            throw new InvalidOperationException($"Gateway inválido: {dto.Gateway}. Use formato IP válido (ex: 10.0.0.1).");
        }

        // Validar Distance se fornecido
        if (dto.Distance.HasValue && (dto.Distance < 0 || dto.Distance > 255))
        {
            throw new InvalidOperationException("Distance deve estar entre 0 e 255.");
        }

        // Validar Scope se fornecido
        if (dto.Scope.HasValue && (dto.Scope < 0 || dto.Scope > 255))
        {
            throw new InvalidOperationException("Scope deve estar entre 0 e 255.");
        }
    }

    private static bool IsValidIpOrCidr(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        // Verificar se é IP/CIDR (ex: 10.0.0.0/24)
        if (input.Contains('/'))
        {
            var parts = input.Split('/');
            if (parts.Length != 2)
                return false;

            if (!IPAddress.TryParse(parts[0], out _))
                return false;

            if (!int.TryParse(parts[1], out var prefix) || prefix < 0 || prefix > 32)
                return false;

            return true;
        }

        // Verificar se é apenas IP
        return IPAddress.TryParse(input, out _);
    }

    private static bool IsValidIp(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        return IPAddress.TryParse(input, out _);
    }
}

