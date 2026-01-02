using Automais.Core.DTOs;
using Automais.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Automais.Api.Controllers;

/// <summary>
/// Controller para gerenciamento de Gateways LoRaWAN
/// </summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public class GatewaysController : ControllerBase
{
    private readonly IGatewayService _gatewayService;
    private readonly ILogger<GatewaysController> _logger;

    public GatewaysController(
        IGatewayService gatewayService,
        ILogger<GatewaysController> logger)
    {
        _gatewayService = gatewayService;
        _logger = logger;
    }

    /// <summary>
    /// Lista todos os gateways de um tenant
    /// </summary>
    [HttpGet("tenants/{tenantId:guid}/gateways")]
    public async Task<ActionResult<IEnumerable<GatewayDto>>> GetByTenant(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Listando gateways do tenant {TenantId}", tenantId);
        
        var gateways = await _gatewayService.GetByTenantIdAsync(tenantId, cancellationToken);
        return Ok(gateways);
    }

    /// <summary>
    /// Obtém um gateway por ID
    /// </summary>
    [HttpGet("gateways/{id:guid}")]
    public async Task<ActionResult<GatewayDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Buscando gateway {GatewayId}", id);
        
        var gateway = await _gatewayService.GetByIdAsync(id, cancellationToken);
        if (gateway == null)
        {
            return NotFound(new { message = $"Gateway com ID {id} não encontrado" });
        }

        return Ok(gateway);
    }

    /// <summary>
    /// Cria um novo gateway para um tenant
    /// </summary>
    [HttpPost("tenants/{tenantId:guid}/gateways")]
    public async Task<ActionResult<GatewayDto>> Create(
        Guid tenantId,
        [FromBody] CreateGatewayDto dto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Criando gateway {GatewayEui} para tenant {TenantId}", dto.GatewayEui, tenantId);

        try
        {
            var gateway = await _gatewayService.CreateAsync(tenantId, dto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = gateway.Id }, gateway);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Tenant não encontrado");
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Erro ao criar gateway");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Atualiza um gateway
    /// </summary>
    [HttpPut("gateways/{id:guid}")]
    public async Task<ActionResult<GatewayDto>> Update(
        Guid id,
        [FromBody] UpdateGatewayDto dto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Atualizando gateway {GatewayId}", id);

        try
        {
            var gateway = await _gatewayService.UpdateAsync(id, dto, cancellationToken);
            return Ok(gateway);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Gateway não encontrado");
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Deleta um gateway
    /// </summary>
    [HttpDelete("gateways/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deletando gateway {GatewayId}", id);

        try
        {
            await _gatewayService.DeleteAsync(id, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Gateway não encontrado");
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Obtém estatísticas de um gateway (do ChirpStack)
    /// </summary>
    [HttpGet("gateways/{id:guid}/stats")]
    public async Task<ActionResult<GatewayStatsDto>> GetStats(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Buscando estatísticas do gateway {GatewayId}", id);
        
        var stats = await _gatewayService.GetStatsAsync(id, cancellationToken);
        if (stats == null)
        {
            return NotFound(new { message = $"Gateway com ID {id} não encontrado" });
        }

        return Ok(stats);
    }

    /// <summary>
    /// Sincroniza gateways do ChirpStack para o banco local
    /// </summary>
    [HttpPost("tenants/{tenantId:guid}/gateways/sync")]
    public async Task<IActionResult> SyncWithChirpStack(Guid tenantId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Sincronizando gateways do tenant {TenantId} com ChirpStack", tenantId);

        try
        {
            await _gatewayService.SyncWithChirpStackAsync(tenantId, cancellationToken);
            return Ok(new { message = "Sincronização concluída com sucesso" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Erro ao sincronizar gateways");
            return BadRequest(new { message = ex.Message });
        }
    }
}

