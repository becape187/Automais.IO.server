using Automais.Core.DTOs;
using Automais.Core.Entities;
using Automais.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Automais.Api.Controllers;

/// <summary>
/// Controller para gerenciamento de rotas estáticas dos Routers
/// </summary>
[ApiController]
[Route("api/routers/{routerId:guid}/routes")]
[Produces("application/json")]
public class RouterStaticRoutesController : ControllerBase
{
    private readonly IRouterStaticRouteService _routeService;
    private readonly IRouterOsServiceClient? _routerOsServiceClient;
    private readonly ILogger<RouterStaticRoutesController> _logger;

    public RouterStaticRoutesController(
        IRouterStaticRouteService routeService,
        IRouterOsServiceClient? routerOsServiceClient,
        ILogger<RouterStaticRoutesController> logger)
    {
        _routeService = routeService;
        _routerOsServiceClient = routerOsServiceClient;
        _logger = logger;
    }

    /// <summary>
    /// Lista todas as rotas estáticas de um router
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RouterStaticRouteDto>>> GetByRouter(
        Guid routerId,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Listando rotas estáticas do router {RouterId}", routerId);
            var routes = await _routeService.GetByRouterIdAsync(routerId, cancellationToken);
            return Ok(routes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar rotas do router {RouterId}", routerId);
            return StatusCode(500, new 
            { 
                message = "Erro interno do servidor ao listar rotas",
                detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Obtém uma rota estática por ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RouterStaticRouteDto>> GetById(
        Guid routerId,
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var route = await _routeService.GetByIdAsync(id, cancellationToken);
            if (route == null)
            {
                return NotFound(new { message = $"Rota com ID {id} não encontrada" });
            }

            // Verificar se a rota pertence ao router
            if (route.RouterId != routerId)
            {
                return BadRequest(new { message = "A rota não pertence a este router" });
            }

            return Ok(route);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter rota {RouteId} do router {RouterId}", id, routerId);
            return StatusCode(500, new 
            { 
                message = "Erro interno do servidor ao obter rota",
                detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Cria uma nova rota estática para um router
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<RouterStaticRouteDto>> Create(
        Guid routerId,
        [FromBody] CreateRouterStaticRouteDto dto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Criando rota estática para router {RouterId}: Destination={Destination}, Gateway={Gateway}", 
            routerId, dto.Destination, dto.Gateway);

        try
        {
            var created = await _routeService.CreateAsync(routerId, dto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { routerId, id = created.Id }, created);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Router não encontrado ao criar rota");
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Erro ao criar rota");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar rota para router {RouterId}", routerId);
            return StatusCode(500, new 
            { 
                message = "Erro interno do servidor ao criar rota",
                detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Atualiza uma rota estática
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RouterStaticRouteDto>> Update(
        Guid routerId,
        Guid id,
        [FromBody] UpdateRouterStaticRouteDto dto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Atualizando rota {RouteId} do router {RouterId}", id, routerId);

        try
        {
            // Verificar se a rota existe e pertence ao router
            var existing = await _routeService.GetByIdAsync(id, cancellationToken);
            if (existing == null)
            {
                return NotFound(new { message = $"Rota com ID {id} não encontrada" });
            }

            if (existing.RouterId != routerId)
            {
                return BadRequest(new { message = "A rota não pertence a este router" });
            }

            var updated = await _routeService.UpdateAsync(id, dto, cancellationToken);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Rota não encontrada para atualização");
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Erro ao atualizar rota");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar rota {RouteId} do router {RouterId}", id, routerId);
            return StatusCode(500, new 
            { 
                message = "Erro interno do servidor ao atualizar rota",
                detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Deleta uma rota estática
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid routerId,
        Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deletando rota {RouteId} do router {RouterId}", id, routerId);

        try
        {
            // Verificar se a rota existe e pertence ao router
            var existing = await _routeService.GetByIdAsync(id, cancellationToken);
            if (existing == null)
            {
                return NotFound(new { message = $"Rota com ID {id} não encontrada" });
            }

            if (existing.RouterId != routerId)
            {
                return BadRequest(new { message = "A rota não pertence a este router" });
            }

            await _routeService.DeleteAsync(id, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao deletar rota {RouteId} do router {RouterId}", id, routerId);
            return StatusCode(500, new 
            { 
                message = "Erro interno do servidor ao deletar rota",
                detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Marca rotas para adicionar/remover (atualiza status no banco)
    /// </summary>
    [HttpPost("batch-status")]
    public async Task<IActionResult> BatchUpdateStatus(
        Guid routerId,
        [FromBody] BatchUpdateRoutesDto dto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Atualizando status em lote: Router={RouterId}, Add={AddCount}, Remove={RemoveCount}", 
            routerId, dto.RoutesToAdd.Count(), dto.RoutesToRemove.Count());

        try
        {
            await _routeService.BatchUpdateStatusAsync(routerId, dto, cancellationToken);
            return Ok(new { message = "Status atualizado com sucesso" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar status em lote do router {RouterId}", routerId);
            return StatusCode(500, new 
            { 
                message = "Erro interno do servidor ao atualizar status",
                detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Atualiza status de uma rota após aplicação no RouterOS
    /// </summary>
    [HttpPost("update-status")]
    public async Task<IActionResult> UpdateRouteStatus(
        [FromBody] UpdateRouteStatusDto dto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Atualizando status da rota {RouteId}: Status={Status}", 
            dto.RouteId, dto.Status);

        try
        {
            await _routeService.UpdateRouteStatusAsync(dto, cancellationToken);
            return Ok(new { message = "Status atualizado com sucesso" });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Rota não encontrada para atualizar status");
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar status da rota {RouteId}", dto.RouteId);
            return StatusCode(500, new 
            { 
                message = "Erro interno do servidor ao atualizar status",
                detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Aplica rotas pendentes no RouterOS via VPN server
    /// </summary>
    [HttpPost("apply")]
    public async Task<ActionResult<object>> ApplyRoutes(
        Guid routerId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Aplicando rotas pendentes do router {RouterId}", routerId);

        try
        {
            if (_routerOsServiceClient == null)
            {
                return BadRequest(new { message = "Serviço RouterOS não configurado" });
            }

            // Buscar rotas pendentes
            var routes = await _routeService.GetByRouterIdAsync(routerId, cancellationToken);
            var routesToAdd = routes.Where(r => r.Status == RouterStaticRouteStatus.PendingAdd).ToList();
            var routesToRemove = routes.Where(r => r.Status == RouterStaticRouteStatus.PendingRemove).ToList();

            var results = new List<object>();

            // Aplicar rotas para adicionar
            foreach (var route in routesToAdd)
            {
                try
                {
                    var (success, gatewayUsed) = await _routerOsServiceClient.AddRouteAsync(routerId, route, cancellationToken);
                    
                    if (success)
                    {
                        // Atualizar status para Applied, incluindo gateway usado pelo RouterOS
                        await _routeService.UpdateRouteStatusAsync(new UpdateRouteStatusDto
                        {
                            RouteId = route.Id,
                            Status = RouterStaticRouteStatus.Applied,
                            Gateway = gatewayUsed  // Gateway realmente usado pelo RouterOS (pode ser interface se gateway estava vazio)
                        }, cancellationToken);

                        results.Add(new
                        {
                            routeId = route.Id,
                            action = "add",
                            success = true
                        });
                    }
                    else
                    {
                        // Marcar como erro
                        await _routeService.UpdateRouteStatusAsync(new UpdateRouteStatusDto
                        {
                            RouteId = route.Id,
                            Status = RouterStaticRouteStatus.Error,
                            ErrorMessage = "Falha ao adicionar rota no RouterOS"
                        }, cancellationToken);

                        results.Add(new
                        {
                            routeId = route.Id,
                            action = "add",
                            success = false,
                            error = "Falha ao adicionar rota no RouterOS"
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao adicionar rota {RouteId}", route.Id);
                    await _routeService.UpdateRouteStatusAsync(new UpdateRouteStatusDto
                    {
                        RouteId = route.Id,
                        Status = RouterStaticRouteStatus.Error,
                        ErrorMessage = ex.Message
                    }, cancellationToken);

                    results.Add(new
                    {
                        routeId = route.Id,
                        action = "add",
                        success = false,
                        error = ex.Message
                    });
                }
            }

            // Aplicar rotas para remover
            foreach (var route in routesToRemove)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(route.RouterOsId))
                    {
                        // Se não tem RouterOsId, apenas deletar do banco
                        await _routeService.DeleteAsync(route.Id, cancellationToken);
                        results.Add(new
                        {
                            routeId = route.Id,
                            action = "remove",
                            success = true,
                            message = "Rota removida do banco (não estava no RouterOS)"
                        });
                        continue;
                    }

                    var success = await _routerOsServiceClient.RemoveRouteAsync(routerId, route.RouterOsId, cancellationToken);
                    
                    if (success)
                    {
                        // Deletar do banco após remoção bem-sucedida
                        await _routeService.DeleteAsync(route.Id, cancellationToken);
                        results.Add(new
                        {
                            routeId = route.Id,
                            action = "remove",
                            success = true
                        });
                    }
                    else
                    {
                        // Marcar como erro
                        await _routeService.UpdateRouteStatusAsync(new UpdateRouteStatusDto
                        {
                            RouteId = route.Id,
                            Status = RouterStaticRouteStatus.Error,
                            ErrorMessage = "Falha ao remover rota do RouterOS"
                        }, cancellationToken);

                        results.Add(new
                        {
                            routeId = route.Id,
                            action = "remove",
                            success = false,
                            error = "Falha ao remover rota do RouterOS"
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao remover rota {RouteId}", route.Id);
                    await _routeService.UpdateRouteStatusAsync(new UpdateRouteStatusDto
                    {
                        RouteId = route.Id,
                        Status = RouterStaticRouteStatus.Error,
                        ErrorMessage = ex.Message
                    }, cancellationToken);

                    results.Add(new
                    {
                        routeId = route.Id,
                        action = "remove",
                        success = false,
                        error = ex.Message
                    });
                }
            }

            return Ok(new
            {
                message = "Aplicação de rotas concluída",
                results = results,
                summary = new
                {
                    total = routesToAdd.Count + routesToRemove.Count,
                    added = results.Count(r => ((dynamic)r).action == "add" && ((dynamic)r).success == true),
                    removed = results.Count(r => ((dynamic)r).action == "remove" && ((dynamic)r).success == true),
                    errors = results.Count(r => ((dynamic)r).success == false)
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao aplicar rotas do router {RouterId}", routerId);
            return StatusCode(500, new 
            { 
                message = "Erro interno do servidor ao aplicar rotas",
                detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Lista interfaces WireGuard do RouterOS para dedução automática
    /// </summary>
    [HttpGet("wireguard-interfaces")]
    public async Task<ActionResult<List<RouterOsWireGuardInterfaceDto>>> GetWireGuardInterfaces(
        Guid routerId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Listando interfaces WireGuard do router {RouterId}", routerId);

        try
        {
            if (_routerOsServiceClient == null)
            {
                return BadRequest(new { message = "Serviço RouterOS não configurado" });
            }

            var interfaces = await _routerOsServiceClient.ListWireGuardInterfacesAsync(routerId, cancellationToken);
            return Ok(interfaces);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar interfaces WireGuard do router {RouterId}", routerId);
            return StatusCode(500, new 
            { 
                message = "Erro interno do servidor ao listar interfaces WireGuard",
                detail = ex.Message
            });
        }
    }

}

