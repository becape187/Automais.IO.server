using Automais.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Automais.Api.Controllers;

/// <summary>
/// Controller desativado - Gerenciamento RouterOS agora é feito via servidor VPN (WebSocket)
/// </summary>
[ApiController]
[Route("api/routers/{routerId:guid}/management")]
[Produces("application/json")]
[Obsolete("Este controller foi desativado. Use o servidor VPN (WebSocket) para gerenciamento RouterOS.")]
public class RouterManagementController : ControllerBase
{
    private const string DEPRECATED_MESSAGE = "Este endpoint foi desativado. Use o servidor VPN (WebSocket) para gerenciamento RouterOS.";

    [HttpGet("status")]
    public ActionResult<object> GetConnectionStatus(Guid routerId) =>
        StatusCode(501, new { message = DEPRECATED_MESSAGE });

    [HttpGet("firewall")]
    public ActionResult<object> GetFirewallRules(Guid routerId) =>
        StatusCode(501, new { message = DEPRECATED_MESSAGE });

    [HttpGet("nat")]
    public ActionResult<object> GetNatRules(Guid routerId) =>
        StatusCode(501, new { message = DEPRECATED_MESSAGE });

    [HttpGet("routes")]
    public ActionResult<object> GetRoutes(Guid routerId) =>
        StatusCode(501, new { message = DEPRECATED_MESSAGE });

    [HttpGet("interfaces")]
    public ActionResult<object> GetInterfaces(Guid routerId) =>
        StatusCode(501, new { message = DEPRECATED_MESSAGE });

    [HttpPost("terminal")]
    public ActionResult<object> ExecuteTerminalCommand(Guid routerId, [FromBody] object dto) =>
        StatusCode(501, new { message = DEPRECATED_MESSAGE });

    [HttpPost("system-info/refresh")]
    public ActionResult<object> RefreshSystemInfo(Guid routerId) =>
        StatusCode(501, new { message = DEPRECATED_MESSAGE });
}
