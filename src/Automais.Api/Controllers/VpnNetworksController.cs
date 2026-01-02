using Automais.Core.DTOs;
using Automais.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Automais.Api.Controllers;

[ApiController]
[Route("api")]
[Produces("application/json")]
public class VpnNetworksController : ControllerBase
{
    private readonly IVpnNetworkService _vpnNetworkService;
    private readonly ILogger<VpnNetworksController> _logger;

    public VpnNetworksController(
        IVpnNetworkService vpnNetworkService,
        ILogger<VpnNetworksController> logger)
    {
        _vpnNetworkService = vpnNetworkService;
        _logger = logger;
    }

    [HttpGet("tenants/{tenantId:guid}/vpn/networks")]
    public async Task<ActionResult<IEnumerable<VpnNetworkDto>>> GetByTenant(Guid tenantId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Listando redes VPN do tenant {TenantId}", tenantId);
        var networks = await _vpnNetworkService.GetByTenantAsync(tenantId, cancellationToken);
        return Ok(networks);
    }

    [HttpPost("tenants/{tenantId:guid}/vpn/networks")]
    public async Task<ActionResult<VpnNetworkDto>> Create(Guid tenantId, [FromBody] CreateVpnNetworkDto dto, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Criando rede VPN {Slug} para tenant {TenantId}", dto.Slug, tenantId);

        try
        {
            var created = await _vpnNetworkService.CreateAsync(tenantId, dto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Erro ao criar rede VPN");
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Tenant não encontrado ao criar rede VPN");
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("vpn/networks/{id:guid}")]
    public async Task<ActionResult<VpnNetworkDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var network = await _vpnNetworkService.GetByIdAsync(id, cancellationToken);
        if (network == null)
        {
            return NotFound(new { message = $"Rede VPN com ID {id} não encontrada" });
        }

        return Ok(network);
    }

    [HttpPut("vpn/networks/{id:guid}")]
    public async Task<ActionResult<VpnNetworkDto>> Update(Guid id, [FromBody] UpdateVpnNetworkDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _vpnNetworkService.UpdateAsync(id, dto, cancellationToken);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Rede VPN não encontrada para atualização");
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete("vpn/networks/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _vpnNetworkService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("vpn/networks/{id:guid}/users")]
    public async Task<ActionResult<IEnumerable<TenantUserDto>>> GetUsers(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var users = await _vpnNetworkService.GetUsersAsync(id, cancellationToken);
            return Ok(users);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Rede VPN não encontrada ao listar usuários");
            return NotFound(new { message = ex.Message });
        }
    }
}


