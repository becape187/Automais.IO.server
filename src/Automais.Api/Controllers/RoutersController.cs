using Automais.Core.DTOs;
using Automais.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Automais.Api.Controllers;

[ApiController]
[Route("api")]
[Produces("application/json")]
public class RoutersController : ControllerBase
{
    private readonly IRouterService _routerService;
    private readonly ILogger<RoutersController> _logger;

    public RoutersController(
        IRouterService routerService,
        ILogger<RoutersController> logger)
    {
        _routerService = routerService;
        _logger = logger;
    }

    [HttpGet("tenants/{tenantId:guid}/routers")]
    public async Task<ActionResult<IEnumerable<RouterDto>>> GetByTenant(Guid tenantId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Listando routers do tenant {TenantId}", tenantId);
        var routers = await _routerService.GetByTenantIdAsync(tenantId, cancellationToken);
        return Ok(routers);
    }

    [HttpGet("routers/{id:guid}")]
    public async Task<ActionResult<RouterDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var router = await _routerService.GetByIdAsync(id, cancellationToken);
        if (router == null)
        {
            return NotFound(new { message = $"Router com ID {id} não encontrado" });
        }
        return Ok(router);
    }

    [HttpPost("tenants/{tenantId:guid}/routers")]
    public async Task<ActionResult<RouterDto>> Create(Guid tenantId, [FromBody] CreateRouterDto dto, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Criando router {Name} para tenant {TenantId}", dto.Name, tenantId);

        try
        {
            var created = await _routerService.CreateAsync(tenantId, dto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Tenant não encontrado ao criar router");
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Erro ao criar router");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("routers/{id:guid}")]
    public async Task<ActionResult<RouterDto>> Update(Guid id, [FromBody] UpdateRouterDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _routerService.UpdateAsync(id, dto, cancellationToken);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Router não encontrado para atualização");
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Erro ao atualizar router");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("routers/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _routerService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("routers/{id:guid}/test-connection")]
    public async Task<ActionResult<RouterDto>> TestConnection(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var router = await _routerService.TestConnectionAsync(id, cancellationToken);
            return Ok(router);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Router não encontrado para teste de conexão");
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Erro ao testar conexão");
            return BadRequest(new { message = ex.Message });
        }
    }
}

