using Automais.Core.DTOs;
using Automais.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Text;

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

    /// <summary>
    /// Download da configuração WireGuard do router (arquivo .conf)
    /// </summary>
    [HttpGet("routers/{routerId:guid}/wireguard/config/download")]
    public async Task<IActionResult> DownloadConfig(
        Guid routerId,
        [FromServices] IWireGuardServerService wireGuardServerService,
        CancellationToken cancellationToken)
    {
        try
        {
            var config = await wireGuardServerService.GetConfigAsync(routerId, cancellationToken);
            
            if (string.IsNullOrEmpty(config.ConfigContent))
            {
                _logger.LogWarning("Configuração VPN vazia para router {RouterId}", routerId);
                return BadRequest(new { 
                    message = "Configuração VPN não disponível. O router precisa ter uma rede VPN configurada.",
                    detail = "Certifique-se de que o router foi criado com uma rede VPN (vpnNetworkId)."
                });
            }
            
            var bytes = Encoding.UTF8.GetBytes(config.ConfigContent);
            
            // Sanitizar o nome do arquivo removendo caracteres problemáticos
            var fileName = config.FileName
                .Replace("\"", "")
                .Replace("\\", "")
                .Replace("/", "")
                .Replace(":", "")
                .Replace("*", "")
                .Replace("?", "")
                .Replace("<", "")
                .Replace(">", "")
                .Replace("|", "");
            
            // Configurar o header Content-Disposition manualmente para evitar codificação duplicada
            Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{fileName}\"");
            
            return new FileContentResult(bytes, "text/plain");
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Router não encontrado para download de config: {RouterId}", routerId);
            return NotFound(new { 
                message = "Router não encontrado",
                detail = ex.Message 
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Erro ao gerar configuração VPN para router {RouterId}: {Error}", routerId, ex.Message);
            return BadRequest(new { 
                message = "Erro ao gerar configuração VPN",
                detail = ex.Message,
                hint = "Certifique-se de que o router possui uma rede VPN configurada e que o peer foi criado corretamente."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao baixar configuração VPN para router {RouterId}", routerId);
            return StatusCode(500, new { 
                message = "Erro interno do servidor ao baixar configuração VPN",
                detail = ex.Message,
                innerException = ex.InnerException?.Message
            });
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

