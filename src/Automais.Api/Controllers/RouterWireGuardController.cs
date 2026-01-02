using Automais.Core.DTOs;
using Automais.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Automais.Api.Controllers;

[ApiController]
[Route("api")]
[Produces("application/json")]
public class RouterWireGuardController : ControllerBase
{
    private readonly IRouterWireGuardService _wireGuardService;
    private readonly ILogger<RouterWireGuardController> _logger;

    public RouterWireGuardController(
        IRouterWireGuardService wireGuardService,
        ILogger<RouterWireGuardController> logger)
    {
        _wireGuardService = wireGuardService;
        _logger = logger;
    }

    [HttpGet("routers/{routerId:guid}/wireguard/peers")]
    public async Task<ActionResult<IEnumerable<RouterWireGuardPeerDto>>> GetPeers(Guid routerId, CancellationToken cancellationToken)
    {
        var peers = await _wireGuardService.GetByRouterIdAsync(routerId, cancellationToken);
        return Ok(peers);
    }

    [HttpGet("wireguard/peers/{id:guid}")]
    public async Task<ActionResult<RouterWireGuardPeerDto>> GetPeerById(Guid id, CancellationToken cancellationToken)
    {
        var peer = await _wireGuardService.GetByIdAsync(id, cancellationToken);
        if (peer == null)
        {
            return NotFound(new { message = $"Peer WireGuard com ID {id} não encontrado" });
        }
        return Ok(peer);
    }

    [HttpPost("routers/{routerId:guid}/wireguard/peers")]
    public async Task<ActionResult<RouterWireGuardPeerDto>> CreatePeer(Guid routerId, [FromBody] CreateRouterWireGuardPeerDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _wireGuardService.CreatePeerAsync(routerId, dto, cancellationToken);
            return CreatedAtAction(nameof(GetPeerById), new { id = created.Id }, created);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Erro ao criar peer WireGuard");
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Erro ao criar peer WireGuard");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("wireguard/peers/{id:guid}")]
    public async Task<ActionResult<RouterWireGuardPeerDto>> UpdatePeer(Guid id, [FromBody] CreateRouterWireGuardPeerDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _wireGuardService.UpdatePeerAsync(id, dto, cancellationToken);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Peer WireGuard não encontrado");
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete("wireguard/peers/{id:guid}")]
    public async Task<IActionResult> DeletePeer(Guid id, CancellationToken cancellationToken)
    {
        await _wireGuardService.DeletePeerAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("wireguard/peers/{id:guid}/config")]
    public async Task<ActionResult<RouterWireGuardConfigDto>> GetConfig(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var config = await _wireGuardService.GetConfigAsync(id, cancellationToken);
            return Ok(config);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Peer WireGuard não encontrado");
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("wireguard/peers/{id:guid}/regenerate-keys")]
    public async Task<ActionResult<RouterWireGuardPeerDto>> RegenerateKeys(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _wireGuardService.RegenerateKeysAsync(id, cancellationToken);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Peer WireGuard não encontrado");
            return NotFound(new { message = ex.Message });
        }
    }
}

