using Automais.Core.Entities;
using Automais.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Automais.Api.Controllers;

[ApiController]
[Route("api/routers/{routerId:guid}/management")]
[Produces("application/json")]
public class RouterManagementController : ControllerBase
{
    private readonly IRouterRepository _routerRepository;
    private readonly IRouterOsClient _routerOsClient;
    private readonly ILogger<RouterManagementController> _logger;

    public RouterManagementController(
        IRouterRepository routerRepository,
        IRouterOsClient routerOsClient,
        ILogger<RouterManagementController> logger)
    {
        _routerRepository = routerRepository;
        _routerOsClient = routerOsClient;
        _logger = logger;
    }

    /// <summary>
    /// Testa conexão com o router e retorna status
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<object>> GetConnectionStatus(Guid routerId, CancellationToken cancellationToken)
    {
        try
        {
            var router = await _routerRepository.GetByIdAsync(routerId, cancellationToken);
            if (router == null)
            {
                return NotFound(new { message = $"Router com ID {routerId} não encontrado" });
            }

            if (string.IsNullOrWhiteSpace(router.RouterOsApiUrl) ||
                string.IsNullOrWhiteSpace(router.RouterOsApiUsername) ||
                string.IsNullOrWhiteSpace(router.RouterOsApiPassword))
            {
                return BadRequest(new { message = "Credenciais da API RouterOS não configuradas" });
            }

            var isConnected = await _routerOsClient.TestConnectionAsync(
                router.RouterOsApiUrl,
                router.RouterOsApiUsername,
                router.RouterOsApiPassword,
                cancellationToken);

            return Ok(new
            {
                connected = isConnected,
                routerId = router.Id,
                routerName = router.Name,
                apiUrl = router.RouterOsApiUrl,
                username = router.RouterOsApiUsername
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar status da conexão do router {RouterId}", routerId);
            return StatusCode(500, new { message = "Erro ao verificar conexão", detail = ex.Message });
        }
    }

    /// <summary>
    /// Lista regras de firewall
    /// </summary>
    [HttpGet("firewall")]
    public async Task<ActionResult<object>> GetFirewallRules(Guid routerId, CancellationToken cancellationToken)
    {
        try
        {
            var router = await GetRouterWithCredentials(routerId, cancellationToken);
            if (router == null) return NotFound(new { message = "Router não encontrado" });

            var rules = await _routerOsClient.ExecuteCommandAsync(
                router.RouterOsApiUrl!,
                router.RouterOsApiUsername!,
                router.RouterOsApiPassword!,
                "/ip/firewall/filter/print",
                cancellationToken);

            return Ok(new { rules });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar regras de firewall do router {RouterId}", routerId);
            return StatusCode(500, new { message = "Erro ao buscar regras de firewall", detail = ex.Message });
        }
    }

    /// <summary>
    /// Lista regras NAT
    /// </summary>
    [HttpGet("nat")]
    public async Task<ActionResult<object>> GetNatRules(Guid routerId, CancellationToken cancellationToken)
    {
        try
        {
            var router = await GetRouterWithCredentials(routerId, cancellationToken);
            if (router == null) return NotFound(new { message = "Router não encontrado" });

            var rules = await _routerOsClient.ExecuteCommandAsync(
                router.RouterOsApiUrl!,
                router.RouterOsApiUsername!,
                router.RouterOsApiPassword!,
                "/ip/firewall/nat/print",
                cancellationToken);

            return Ok(new { rules });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar regras NAT do router {RouterId}", routerId);
            return StatusCode(500, new { message = "Erro ao buscar regras NAT", detail = ex.Message });
        }
    }

    /// <summary>
    /// Lista rotas
    /// </summary>
    [HttpGet("routes")]
    public async Task<ActionResult<object>> GetRoutes(Guid routerId, CancellationToken cancellationToken)
    {
        try
        {
            var router = await GetRouterWithCredentials(routerId, cancellationToken);
            if (router == null) return NotFound(new { message = "Router não encontrado" });

            var routes = await _routerOsClient.ExecuteCommandAsync(
                router.RouterOsApiUrl!,
                router.RouterOsApiUsername!,
                router.RouterOsApiPassword!,
                "/ip/route/print",
                cancellationToken);

            return Ok(new { routes });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar rotas do router {RouterId}", routerId);
            return StatusCode(500, new { message = "Erro ao buscar rotas", detail = ex.Message });
        }
    }

    /// <summary>
    /// Lista interfaces
    /// </summary>
    [HttpGet("interfaces")]
    public async Task<ActionResult<object>> GetInterfaces(Guid routerId, CancellationToken cancellationToken)
    {
        try
        {
            var router = await GetRouterWithCredentials(routerId, cancellationToken);
            if (router == null) return NotFound(new { message = "Router não encontrado" });

            var interfaces = await _routerOsClient.ExecuteCommandAsync(
                router.RouterOsApiUrl!,
                router.RouterOsApiUsername!,
                router.RouterOsApiPassword!,
                "/interface/print",
                cancellationToken);

            return Ok(new { interfaces });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar interfaces do router {RouterId}", routerId);
            return StatusCode(500, new { message = "Erro ao buscar interfaces", detail = ex.Message });
        }
    }

    /// <summary>
    /// Executa comando manual no terminal RouterOS
    /// </summary>
    [HttpPost("terminal")]
    public async Task<ActionResult<object>> ExecuteTerminalCommand(
        Guid routerId,
        [FromBody] TerminalCommandDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var router = await GetRouterWithCredentials(routerId, cancellationToken);
            if (router == null) return NotFound(new { message = "Router não encontrado" });

            if (string.IsNullOrWhiteSpace(dto.Command))
            {
                return BadRequest(new { message = "Comando não pode ser vazio" });
            }

            // Verificar se o comando é de leitura (print) ou escrita (add/set/remove)
            var command = dto.Command.Trim();
            var isReadCommand = command.Contains("print", StringComparison.OrdinalIgnoreCase) ||
                               command.Contains("get", StringComparison.OrdinalIgnoreCase) ||
                               command.Contains("export", StringComparison.OrdinalIgnoreCase);

            if (isReadCommand)
            {
                var result = await _routerOsClient.ExecuteCommandAsync(
                    router.RouterOsApiUrl!,
                    router.RouterOsApiUsername!,
                    router.RouterOsApiPassword!,
                    command,
                    cancellationToken);

                return Ok(new { result, command });
            }
            else
            {
                await _routerOsClient.ExecuteCommandNoResultAsync(
                    router.RouterOsApiUrl!,
                    router.RouterOsApiUsername!,
                    router.RouterOsApiPassword!,
                    command,
                    cancellationToken);

                return Ok(new { success = true, message = "Comando executado com sucesso", command });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar comando no router {RouterId}: {Command}", routerId, dto.Command);
            return StatusCode(500, new { message = "Erro ao executar comando", detail = ex.Message, command = dto.Command });
        }
    }

    private async Task<Automais.Core.Entities.Router?> GetRouterWithCredentials(Guid routerId, CancellationToken cancellationToken)
    {
        var router = await _routerRepository.GetByIdAsync(routerId, cancellationToken);
        if (router == null) return null;

        if (string.IsNullOrWhiteSpace(router.RouterOsApiUrl) ||
            string.IsNullOrWhiteSpace(router.RouterOsApiUsername) ||
            string.IsNullOrWhiteSpace(router.RouterOsApiPassword))
        {
            throw new InvalidOperationException("Credenciais da API RouterOS não configuradas");
        }

        return router;
    }
}

public class TerminalCommandDto
{
    public string Command { get; set; } = string.Empty;
}

