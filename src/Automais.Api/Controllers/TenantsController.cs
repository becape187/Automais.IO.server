using Automais.Core.DTOs;
using Automais.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Automais.Api.Controllers;

/// <summary>
/// Controller para gerenciamento de Tenants (Clientes)
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class TenantsController : ControllerBase
{
    private readonly ITenantService _tenantService;
    private readonly ILogger<TenantsController> _logger;

    public TenantsController(
        ITenantService tenantService,
        ILogger<TenantsController> logger)
    {
        _tenantService = tenantService;
        _logger = logger;
    }

    /// <summary>
    /// Lista todos os tenants
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TenantDto>>> GetAll(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Listando todos os tenants");
        var tenants = await _tenantService.GetAllAsync(cancellationToken);
        return Ok(tenants);
    }

    /// <summary>
    /// Obtém um tenant por ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TenantDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Buscando tenant {TenantId}", id);
        
        var tenant = await _tenantService.GetByIdAsync(id, cancellationToken);
        if (tenant == null)
        {
            return NotFound(new { message = $"Tenant com ID {id} não encontrado" });
        }

        return Ok(tenant);
    }

    /// <summary>
    /// Obtém um tenant por slug
    /// </summary>
    [HttpGet("by-slug/{slug}")]
    public async Task<ActionResult<TenantDto>> GetBySlug(string slug, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Buscando tenant com slug {Slug}", slug);
        
        var tenant = await _tenantService.GetBySlugAsync(slug, cancellationToken);
        if (tenant == null)
        {
            return NotFound(new { message = $"Tenant com slug '{slug}' não encontrado" });
        }

        return Ok(tenant);
    }

    /// <summary>
    /// Cria um novo tenant
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TenantDto>> Create(
        [FromBody] CreateTenantDto dto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Criando novo tenant: {TenantName}", dto.Name);

        try
        {
            var tenant = await _tenantService.CreateAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = tenant.Id }, tenant);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Erro ao criar tenant");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Atualiza um tenant
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TenantDto>> Update(
        Guid id,
        [FromBody] UpdateTenantDto dto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Atualizando tenant {TenantId}", id);

        try
        {
            var tenant = await _tenantService.UpdateAsync(id, dto, cancellationToken);
            return Ok(tenant);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Tenant não encontrado");
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Deleta (desativa) um tenant
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deletando tenant {TenantId}", id);

        try
        {
            await _tenantService.DeleteAsync(id, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Tenant não encontrado");
            return NotFound(new { message = ex.Message });
        }
    }
}

