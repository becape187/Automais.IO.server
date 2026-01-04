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
        try
        {
            _logger.LogInformation("Listando routers do tenant {TenantId}", tenantId);
            var routers = await _routerService.GetByTenantIdAsync(tenantId, cancellationToken);
            return Ok(routers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar routers do tenant {TenantId}", tenantId);
            return StatusCode(500, new 
            { 
                message = "Erro interno do servidor ao listar routers",
                detail = ex.Message,
                innerException = ex.InnerException?.Message,
                exceptionType = ex.GetType().Name
            });
        }
    }

    [HttpGet("routers/{id:guid}")]
    public async Task<ActionResult<RouterDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var router = await _routerService.GetByIdAsync(id, cancellationToken);
            if (router == null)
            {
                return NotFound(new { message = $"Router com ID {id} não encontrado" });
            }
            return Ok(router);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter router {RouterId}", id);
            return StatusCode(500, new 
            { 
                message = "Erro interno do servidor ao obter router",
                detail = ex.Message,
                innerException = ex.InnerException?.Message,
                exceptionType = ex.GetType().Name
            });
        }
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
        catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
        {
            // Erro específico do Entity Framework
            var innerException = dbEx.InnerException;
            var errorDetails = new
            {
                message = "Erro ao salvar no banco de dados",
                detail = dbEx.Message,
                innerException = innerException?.Message,
                innerExceptionType = innerException?.GetType().Name,
                stackTrace = dbEx.StackTrace
            };

            _logger.LogError(dbEx, "Erro do Entity Framework ao criar router {Name} para tenant {TenantId}. Inner: {InnerException}", 
                dto.Name, tenantId, innerException?.Message);

            // Verificar se é erro de foreign key
            if (innerException?.Message?.Contains("foreign key") == true || 
                innerException?.Message?.Contains("violates foreign key constraint") == true)
            {
                return BadRequest(new
                {
                    message = "Erro de validação: Rede VPN não encontrada",
                    detail = $"A rede VPN com ID '{dto.VpnNetworkId}' não existe no banco de dados. Verifique se o VpnNetworkId está correto.",
                    innerException = innerException.Message
                });
            }

            // Verificar se é erro de constraint única
            if (innerException?.Message?.Contains("unique constraint") == true ||
                innerException?.Message?.Contains("duplicate key") == true)
            {
                return BadRequest(new
                {
                    message = "Erro de validação: Dados duplicados",
                    detail = "Já existe um registro com esses dados. Verifique se o serial number ou outros campos únicos não estão duplicados.",
                    innerException = innerException.Message
                });
            }

            return StatusCode(500, errorDetails);
        }
        catch (Exception ex)
        {
            // Logar erro completo incluindo inner exception
            var errorDetails = new
            {
                message = "Erro interno do servidor ao criar router",
                detail = ex.Message,
                innerException = ex.InnerException?.Message,
                innerExceptionType = ex.InnerException?.GetType().Name,
                exceptionType = ex.GetType().Name,
                stackTrace = ex.StackTrace
            };

            if (ex.InnerException != null)
            {
                _logger.LogError(ex, "Erro ao criar router {Name} para tenant {TenantId}. Inner: {InnerException}", 
                    dto.Name, tenantId, ex.InnerException.Message);
            }
            else
            {
                _logger.LogError(ex, "Erro ao criar router {Name} para tenant {TenantId}", dto.Name, tenantId);
            }
            
            return StatusCode(500, errorDetails);
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
            return BadRequest(new { message = ex.Message, detail = ex.InnerException?.Message });
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
        {
            var innerException = dbEx.InnerException;
            _logger.LogError(dbEx, "Erro do Entity Framework ao atualizar router {RouterId}", id);
            return StatusCode(500, new
            {
                message = "Erro ao salvar no banco de dados",
                detail = dbEx.Message,
                innerException = innerException?.Message,
                innerExceptionType = innerException?.GetType().Name
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar router {RouterId}", id);
            return StatusCode(500, new
            {
                message = "Erro interno do servidor ao atualizar router",
                detail = ex.Message,
                innerException = ex.InnerException?.Message,
                exceptionType = ex.GetType().Name
            });
        }
    }

    [HttpDelete("routers/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _routerService.DeleteAsync(id, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Router não encontrado para exclusão");
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao deletar router {RouterId}", id);
            return StatusCode(500, new 
            { 
                message = "Erro interno do servidor ao deletar router",
                detail = ex.Message,
                innerException = ex.InnerException?.Message,
                exceptionType = ex.GetType().Name
            });
        }
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
            return BadRequest(new { message = ex.Message, detail = ex.InnerException?.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao testar conexão do router {RouterId}", id);
            return StatusCode(500, new
            {
                message = "Erro interno do servidor ao testar conexão",
                detail = ex.Message,
                innerException = ex.InnerException?.Message,
                exceptionType = ex.GetType().Name
            });
        }
    }
}

