using Automais.Core.Entities;
using Automais.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Automais.Api.Controllers;

[ApiController]
[Route("api/routers/{routerId:guid}/management")]
[Produces("application/json")]
public class RouterManagementController : ControllerBase
{
    private readonly IRouterRepository _routerRepository;
    private readonly IRouterOsClient _routerOsClient;
    private readonly IRouterWireGuardPeerRepository _peerRepository;
    private readonly ILogger<RouterManagementController> _logger;

    public RouterManagementController(
        IRouterRepository routerRepository,
        IRouterOsClient routerOsClient,
        IRouterWireGuardPeerRepository peerRepository,
        ILogger<RouterManagementController> logger)
    {
        _routerRepository = routerRepository;
        _routerOsClient = routerOsClient;
        _peerRepository = peerRepository;
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
                _logger.LogWarning("Router {RouterId} não encontrado", routerId);
                return NotFound(new { message = $"Router com ID {routerId} não encontrado" });
            }

            _logger.LogInformation("Verificando status do router {RouterId} ({RouterName})", routerId, router.Name);
            
            // Log detalhado das credenciais (sem mostrar senha completa)
            _logger.LogInformation("RouterOsApiUrl: {Url} (null: {IsNull}, empty: {IsEmpty})", 
                router.RouterOsApiUrl ?? "(null)", 
                router.RouterOsApiUrl == null, 
                string.IsNullOrWhiteSpace(router.RouterOsApiUrl));
            _logger.LogInformation("RouterOsApiUsername: {Username} (null: {IsNull}, empty: {IsEmpty})", 
                router.RouterOsApiUsername ?? "(null)", 
                router.RouterOsApiUsername == null, 
                string.IsNullOrWhiteSpace(router.RouterOsApiUsername));
            _logger.LogInformation("RouterOsApiPassword: {PasswordInfo} (null: {IsNull}, empty: {IsEmpty})", 
                string.IsNullOrWhiteSpace(router.RouterOsApiPassword) ? "(vazio)" : "***", 
                router.RouterOsApiPassword == null, 
                string.IsNullOrWhiteSpace(router.RouterOsApiPassword));

            // Se RouterOsApiUrl estiver vazio, tentar buscar IP do peer WireGuard
            var apiUrl = router.RouterOsApiUrl;
            if (string.IsNullOrWhiteSpace(apiUrl))
            {
                _logger.LogInformation("RouterOsApiUrl está vazio. Tentando buscar IP do peer WireGuard...");
                var peers = await _peerRepository.GetByRouterIdAsync(routerId, cancellationToken);
                var peerList = peers.ToList();
                
                if (peerList.Any())
                {
                    var firstPeer = peerList.First();
                    if (!string.IsNullOrWhiteSpace(firstPeer.AllowedIps))
                    {
                        // Extrair IP do formato "10.222.111.2/24" -> "10.222.111.2"
                        var allowedIps = firstPeer.AllowedIps.Split(',')[0].Trim();
                        var ipParts = allowedIps.Split('/');
                        var ip = ipParts[0].Trim();
                        
                        // Construir URL da API com porta padrão 8728
                        apiUrl = $"{ip}:8728";
                        _logger.LogInformation("IP extraído do peer WireGuard: {Ip}. URL da API: {ApiUrl}", ip, apiUrl);
                    }
                }
            }

            // Verificar quais credenciais estão faltando
            var missingFields = new List<string>();
            if (string.IsNullOrWhiteSpace(apiUrl))
                missingFields.Add("RouterOsApiUrl");
            if (string.IsNullOrWhiteSpace(router.RouterOsApiUsername))
                missingFields.Add("RouterOsApiUsername");
            if (string.IsNullOrWhiteSpace(router.RouterOsApiPassword))
                missingFields.Add("RouterOsApiPassword");

            if (missingFields.Any())
            {
                _logger.LogWarning("Router {RouterId} está sem credenciais: {MissingFields}. RouterOsApiUrl='{Url}', RouterOsApiUsername='{Username}', RouterOsApiPassword está null/empty: {PasswordEmpty}", 
                    routerId, 
                    string.Join(", ", missingFields),
                    apiUrl ?? "(null)",
                    router.RouterOsApiUsername ?? "(null)",
                    string.IsNullOrWhiteSpace(router.RouterOsApiPassword));
                return BadRequest(new 
                { 
                    message = "Credenciais da API RouterOS não configuradas",
                    missingFields = missingFields,
                    routerId = router.Id,
                    routerName = router.Name,
                    details = new
                    {
                        routerOsApiUrl = apiUrl ?? "(null)",
                        routerOsApiUsername = router.RouterOsApiUsername ?? "(null)",
                        routerOsApiPasswordConfigured = !string.IsNullOrWhiteSpace(router.RouterOsApiPassword)
                    }
                });
            }

            _logger.LogInformation("Testando conexão RouterOS: {ApiUrl}, Username: {Username}", 
                apiUrl, router.RouterOsApiUsername);

            var isConnected = await _routerOsClient.TestConnectionAsync(
                apiUrl,
                router.RouterOsApiUsername,
                router.RouterOsApiPassword,
                cancellationToken);

            _logger.LogInformation("Resultado do teste de conexão para router {RouterId}: {IsConnected}", routerId, isConnected);

            // Se conectou, atualizar informações do sistema automaticamente
            if (isConnected)
            {
                try
                {
                    var systemInfo = await _routerOsClient.GetSystemInfoAsync(
                        apiUrl,
                        router.RouterOsApiUsername,
                        router.RouterOsApiPassword,
                        cancellationToken);

                    // Atualizar informações no banco
                    router.Model = systemInfo.Model ?? router.Model;
                    router.SerialNumber = systemInfo.SerialNumber ?? router.SerialNumber;
                    router.FirmwareVersion = systemInfo.FirmwareVersion ?? router.FirmwareVersion;
                    
                    // Atualizar HardwareInfo com informações adicionais
                    var hardwareInfo = new
                    {
                        cpuLoad = systemInfo.CpuLoad,
                        memoryUsage = systemInfo.MemoryUsage,
                        uptime = systemInfo.Uptime,
                        temperature = systemInfo.Temperature,
                        lastUpdated = DateTime.UtcNow
                    };
                    router.HardwareInfo = JsonSerializer.Serialize(hardwareInfo);
                    
                    router.LastSeenAt = DateTime.UtcNow;
                    router.UpdatedAt = DateTime.UtcNow;
                    router.Status = Core.Entities.RouterStatus.Online;

                    await _routerRepository.UpdateAsync(router, cancellationToken);
                    _logger.LogInformation("✅ Informações do sistema atualizadas para router {RouterId}", routerId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ Erro ao atualizar informações do sistema para router {RouterId}: {Error}", routerId, ex.Message);
                    // Não falhar o teste de conexão se a atualização falhar
                }
            }
            else
            {
                // Se não conectou, atualizar status
                router.Status = Core.Entities.RouterStatus.Offline;
                router.UpdatedAt = DateTime.UtcNow;
                await _routerRepository.UpdateAsync(router, cancellationToken);
            }

            return Ok(new
            {
                connected = isConnected,
                routerId = router.Id,
                routerName = router.Name,
                apiUrl = apiUrl,
                username = router.RouterOsApiUsername,
                model = router.Model,
                serialNumber = router.SerialNumber,
                firmwareVersion = router.FirmwareVersion,
                hardwareInfo = router.HardwareInfo != null ? JsonSerializer.Deserialize<object>(router.HardwareInfo) : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar status da conexão do router {RouterId}", routerId);
            return StatusCode(500, new 
            { 
                message = "Erro ao verificar conexão", 
                detail = ex.Message,
                innerException = ex.InnerException?.Message,
                stackTrace = ex.StackTrace
            });
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
            var routerData = await GetRouterWithCredentials(routerId, cancellationToken);
            if (routerData == null) return NotFound(new { message = "Router não encontrado" });

            var router = routerData.Value.router;
            var apiUrl = routerData.Value.apiUrl;
            var rules = await _routerOsClient.ExecuteCommandAsync(
                apiUrl,
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
            var routerData = await GetRouterWithCredentials(routerId, cancellationToken);
            if (routerData == null) return NotFound(new { message = "Router não encontrado" });

            var router = routerData.Value.router;
            var apiUrl = routerData.Value.apiUrl;
            var rules = await _routerOsClient.ExecuteCommandAsync(
                apiUrl,
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
            var routerData = await GetRouterWithCredentials(routerId, cancellationToken);
            if (routerData == null) return NotFound(new { message = "Router não encontrado" });

            var router = routerData.Value.router;
            var apiUrl = routerData.Value.apiUrl;
            var routes = await _routerOsClient.ExecuteCommandAsync(
                apiUrl,
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
            var routerData = await GetRouterWithCredentials(routerId, cancellationToken);
            if (routerData == null) return NotFound(new { message = "Router não encontrado" });

            var router = routerData.Value.router;
            var apiUrl = routerData.Value.apiUrl;
            var interfaces = await _routerOsClient.ExecuteCommandAsync(
                apiUrl,
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
            var routerData = await GetRouterWithCredentials(routerId, cancellationToken);
            if (routerData == null) return NotFound(new { message = "Router não encontrado" });

            var router = routerData.Value.router;
            var apiUrl = routerData.Value.apiUrl;

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
                    apiUrl,
                    router.RouterOsApiUsername!,
                    router.RouterOsApiPassword!,
                    command,
                    cancellationToken);

                return Ok(new { result, command });
            }
            else
            {
                await _routerOsClient.ExecuteCommandNoResultAsync(
                    apiUrl,
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

    /// <summary>
    /// Atualiza informações do sistema do router (Model, SerialNumber, FirmwareVersion, etc)
    /// </summary>
    [HttpPost("system-info/refresh")]
    public async Task<ActionResult<object>> RefreshSystemInfo(Guid routerId, CancellationToken cancellationToken)
    {
        try
        {
            var routerData = await GetRouterWithCredentials(routerId, cancellationToken);
            if (routerData == null) return NotFound(new { message = "Router não encontrado" });

            var router = routerData.Value.router;
            var apiUrl = routerData.Value.apiUrl;

            _logger.LogInformation("Atualizando informações do sistema para router {RouterId}", routerId);

            // Buscar informações do sistema
            var systemInfo = await _routerOsClient.GetSystemInfoAsync(
                apiUrl,
                router.RouterOsApiUsername!,
                router.RouterOsApiPassword!,
                cancellationToken);

            // Atualizar informações no banco
            router.Model = systemInfo.Model ?? router.Model;
            router.SerialNumber = systemInfo.SerialNumber ?? router.SerialNumber;
            router.FirmwareVersion = systemInfo.FirmwareVersion ?? router.FirmwareVersion;
            
            // Atualizar HardwareInfo com informações adicionais
            var hardwareInfo = new
            {
                cpuLoad = systemInfo.CpuLoad,
                memoryUsage = systemInfo.MemoryUsage,
                uptime = systemInfo.Uptime,
                temperature = systemInfo.Temperature,
                lastUpdated = DateTime.UtcNow
            };
            router.HardwareInfo = JsonSerializer.Serialize(hardwareInfo);
            
            router.LastSeenAt = DateTime.UtcNow;
            router.UpdatedAt = DateTime.UtcNow;
            router.Status = RouterStatus.Online;

            await _routerRepository.UpdateAsync(router, cancellationToken);

            _logger.LogInformation("✅ Informações do sistema atualizadas para router {RouterId}: Model={Model}, Serial={Serial}, Firmware={Firmware}", 
                routerId, router.Model, router.SerialNumber, router.FirmwareVersion);

            return Ok(new
            {
                success = true,
                message = "Informações do sistema atualizadas com sucesso",
                router = new
                {
                    id = router.Id,
                    name = router.Name,
                    model = router.Model,
                    serialNumber = router.SerialNumber,
                    firmwareVersion = router.FirmwareVersion,
                    hardwareInfo = hardwareInfo
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar informações do sistema do router {RouterId}", routerId);
            return StatusCode(500, new { message = "Erro ao atualizar informações do sistema", detail = ex.Message });
        }
    }

    private async Task<(Automais.Core.Entities.Router router, string apiUrl)?> GetRouterWithCredentials(Guid routerId, CancellationToken cancellationToken)
    {
        var router = await _routerRepository.GetByIdAsync(routerId, cancellationToken);
        if (router == null) return null;

        // Se RouterOsApiUrl estiver vazio, tentar buscar IP do peer WireGuard
        var apiUrl = router.RouterOsApiUrl;
        if (string.IsNullOrWhiteSpace(apiUrl))
        {
            _logger.LogInformation("RouterOsApiUrl está vazio. Tentando buscar IP do peer WireGuard para router {RouterId}...", routerId);
            var peers = await _peerRepository.GetByRouterIdAsync(routerId, cancellationToken);
            var peerList = peers.ToList();
            
            if (peerList.Any())
            {
                var firstPeer = peerList.First();
                if (!string.IsNullOrWhiteSpace(firstPeer.AllowedIps))
                {
                    // Extrair IP do formato "10.222.111.2/24" -> "10.222.111.2"
                    var allowedIps = firstPeer.AllowedIps.Split(',')[0].Trim();
                    var ipParts = allowedIps.Split('/');
                    var ip = ipParts[0].Trim();
                    
                    // Construir URL da API com porta padrão 8728
                    apiUrl = $"{ip}:8728";
                    _logger.LogInformation("IP extraído do peer WireGuard: {Ip}. URL da API: {ApiUrl}", ip, apiUrl);
                }
            }
        }

        if (string.IsNullOrWhiteSpace(apiUrl) ||
            string.IsNullOrWhiteSpace(router.RouterOsApiUsername) ||
            string.IsNullOrWhiteSpace(router.RouterOsApiPassword))
        {
            throw new InvalidOperationException("Credenciais da API RouterOS não configuradas");
        }

        return (router, apiUrl);
    }
}

public class TerminalCommandDto
{
    public string Command { get; set; } = string.Empty;
}

