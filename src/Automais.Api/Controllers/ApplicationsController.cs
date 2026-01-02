using Automais.Core.DTOs;
using Automais.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Automais.Api.Controllers;

[ApiController]
[Route("api")]
[Produces("application/json")]
public class ApplicationsController : ControllerBase
{
    private readonly IApplicationService _applicationService;
    private readonly ILogger<ApplicationsController> _logger;

    public ApplicationsController(
        IApplicationService applicationService,
        ILogger<ApplicationsController> logger)
    {
        _applicationService = applicationService;
        _logger = logger;
    }

    [HttpGet("tenants/{tenantId:guid}/applications")]
    public async Task<ActionResult<IEnumerable<ApplicationDto>>> GetByTenant(Guid tenantId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Listando applications do tenant {TenantId}", tenantId);
        var applications = await _applicationService.GetByTenantAsync(tenantId, cancellationToken);
        return Ok(applications);
    }

    [HttpPost("tenants/{tenantId:guid}/applications")]
    public async Task<ActionResult<ApplicationDto>> Create(Guid tenantId, [FromBody] CreateApplicationDto dto, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Criando application {Name} para tenant {TenantId}", dto.Name, tenantId);

        try
        {
            var created = await _applicationService.CreateAsync(tenantId, dto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Erro ao criar application");
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Tenant não encontrado ao criar application");
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("applications/{id:guid}")]
    public async Task<ActionResult<ApplicationDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var application = await _applicationService.GetByIdAsync(id, cancellationToken);
        if (application == null)
        {
            return NotFound(new { message = $"Application com ID {id} não encontrada" });
        }

        return Ok(application);
    }

    [HttpPut("applications/{id:guid}")]
    public async Task<ActionResult<ApplicationDto>> Update(Guid id, [FromBody] UpdateApplicationDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _applicationService.UpdateAsync(id, dto, cancellationToken);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Application não encontrada para atualização");
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Erro ao atualizar application");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("applications/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _applicationService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}


