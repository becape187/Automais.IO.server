using Automais.Core.DTOs;
using Automais.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Automais.Api.Controllers;

[ApiController]
[Route("api")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly ITenantUserService _tenantUserService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        ITenantUserService tenantUserService,
        ILogger<UsersController> logger)
    {
        _tenantUserService = tenantUserService;
        _logger = logger;
    }

    [HttpGet("tenants/{tenantId:guid}/users")]
    public async Task<ActionResult<IEnumerable<TenantUserDto>>> GetByTenant(Guid tenantId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Listando usuários do tenant {TenantId}", tenantId);
            var users = await _tenantUserService.GetByTenantAsync(tenantId, cancellationToken);
            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar usuários do tenant {TenantId}", tenantId);
            return StatusCode(500, new { message = "Erro interno do servidor ao listar usuários", error = ex.Message });
        }
    }

    [HttpPost("tenants/{tenantId:guid}/users")]
    public async Task<ActionResult<TenantUserDto>> Create(Guid tenantId, [FromBody] CreateTenantUserDto dto, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Criando usuário {Email} para tenant {TenantId}", dto.Email, tenantId);

        try
        {
            var created = await _tenantUserService.CreateAsync(tenantId, dto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Erro de validação ao criar usuário");
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Tenant não encontrado ao criar usuário");
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("users/{id:guid}")]
    public async Task<ActionResult<TenantUserDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var user = await _tenantUserService.GetByIdAsync(id, cancellationToken);
        if (user == null)
        {
            return NotFound(new { message = $"Usuário com ID {id} não encontrado" });
        }

        return Ok(user);
    }

    [HttpPut("users/{id:guid}")]
    public async Task<ActionResult<TenantUserDto>> Update(Guid id, [FromBody] UpdateTenantUserDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _tenantUserService.UpdateAsync(id, dto, cancellationToken);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Usuário não encontrado para atualização");
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("users/{id:guid}/networks")]
    public async Task<ActionResult<TenantUserDto>> UpdateNetworks(Guid id, [FromBody] UpdateUserNetworksDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _tenantUserService.UpdateNetworksAsync(id, dto, cancellationToken);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Usuário/rede não encontrado ao atualizar redes do usuário");
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Erro ao atualizar redes do usuário");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("users/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _tenantUserService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}


