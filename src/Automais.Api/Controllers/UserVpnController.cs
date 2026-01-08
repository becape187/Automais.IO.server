using System.Security.Claims;
using Automais.Core.DTOs;
using Automais.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Automais.Api.Controllers;

[ApiController]
[Route("api/user")]
[Produces("application/json")]
public class UserVpnController : ControllerBase
{
    private readonly IUserVpnService _userVpnService;
    private readonly IAuthService _authService;
    private readonly ILogger<UserVpnController> _logger;

    public UserVpnController(
        IUserVpnService userVpnService,
        IAuthService authService,
        ILogger<UserVpnController> logger)
    {
        _userVpnService = userVpnService;
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Obtém a configuração VPN do usuário autenticado
    /// </summary>
    [HttpGet("vpn/config")]
    public async Task<ActionResult<UserVpnConfigDto>> GetVpnConfig(CancellationToken cancellationToken)
    {
        try
        {
            // Obter token do header Authorization
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized(new { message = "Token de autenticação não fornecido" });
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();
            
            // Validar token e obter informações do usuário
            var userInfo = await _authService.ValidateTokenAsync(token, cancellationToken);
            if (userInfo == null)
            {
                return Unauthorized(new { message = "Token inválido ou expirado" });
            }

            // Obter configuração VPN
            var config = await _userVpnService.GetUserVpnConfigAsync(userInfo.Id, cancellationToken);
            return Ok(config);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Recurso não encontrado ao obter configuração VPN");
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Operação inválida ao obter configuração VPN: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Não autorizado ao obter configuração VPN");
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao obter configuração VPN");
            return StatusCode(500, new { message = "Erro interno do servidor ao obter configuração VPN" });
        }
    }
}

