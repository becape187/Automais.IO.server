using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Automais.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace Automais.Api.Controllers;

/// <summary>
/// Controller WebSocket para proxy RouterOS
/// Faz proxy entre frontend e servidor routeros.io Python
/// NOTA: Este controller está desabilitado - o endpoint está mapeado diretamente no Program.cs
/// </summary>
// [ApiController]
// [Route("api/ws/routeros")]
public class RouterOsWebSocketController : ControllerBase
{
    private readonly IRouterRepository _routerRepository;
    private readonly IVpnNetworkRepository _vpnNetworkRepository;
    private readonly ILogger<RouterOsWebSocketController> _logger;
    private readonly IConfiguration _configuration;

    public RouterOsWebSocketController(
        IRouterRepository routerRepository,
        IVpnNetworkRepository vpnNetworkRepository,
        ILogger<RouterOsWebSocketController> logger,
        IConfiguration configuration)
    {
        _routerRepository = routerRepository;
        _vpnNetworkRepository = vpnNetworkRepository;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Endpoint WebSocket para proxy RouterOS
    /// Conecta o frontend ao servidor routeros.io Python baseado no routerId
    /// </summary>
    [HttpGet("{routerId:guid}")]
    public async Task Get(Guid routerId)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsync("Expected WebSocket request");
            return;
        }

        var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        _logger.LogInformation("WebSocket conectado para router {RouterId}", routerId);

        try
        {
            // Buscar router e obter ServerEndpoint
            var router = await _routerRepository.GetByIdAsync(routerId, HttpContext.RequestAborted);
            if (router == null)
            {
                await SendErrorAndClose(webSocket, "Router não encontrado");
                return;
            }

            string? serverEndpoint = null;
            if (router.VpnNetworkId.HasValue)
            {
                var vpnNetwork = await _vpnNetworkRepository.GetByIdAsync(router.VpnNetworkId.Value, HttpContext.RequestAborted);
                serverEndpoint = vpnNetwork?.ServerEndpoint;
            }

            // Construir URL do WebSocket do routeros.io
            var wsUrl = GetWebSocketUrl(serverEndpoint);
            _logger.LogInformation("Conectando ao routeros.io em {WsUrl} para router {RouterId}", wsUrl, routerId);

            // Conectar ao servidor routeros.io Python
            using var clientWebSocket = new ClientWebSocket();
            try
            {
                await clientWebSocket.ConnectAsync(new Uri(wsUrl), HttpContext.RequestAborted);
                _logger.LogInformation("Conectado ao routeros.io com sucesso para router {RouterId}", routerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao conectar ao routeros.io em {WsUrl} para router {RouterId}", wsUrl, routerId);
                await SendErrorAndClose(webSocket, $"Erro ao conectar ao servidor RouterOS: {ex.Message}");
                return;
            }

            // Fazer proxy bidirecional
            var cancellationToken = HttpContext.RequestAborted;
            var clientToServer = ProxyMessages(webSocket, clientWebSocket, cancellationToken);
            var serverToClient = ProxyMessages(clientWebSocket, webSocket, cancellationToken);

            // Aguardar até que uma das conexões feche
            await Task.WhenAny(clientToServer, serverToClient);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no WebSocket proxy para router {RouterId}", routerId);
        }
        finally
        {
            if (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.CloseReceived)
            {
                await webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Connection closed",
                    CancellationToken.None);
            }
            _logger.LogInformation("WebSocket desconectado para router {RouterId}", routerId);
        }
    }

    /// <summary>
    /// Faz proxy de mensagens entre dois WebSockets
    /// </summary>
    private async Task ProxyMessages(WebSocket source, WebSocket destination, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        try
        {
            while (source.State == WebSocketState.Open && destination.State == WebSocketState.Open)
            {
                var result = await source.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    if (destination.State == WebSocketState.Open)
                    {
                        await destination.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Connection closed by source",
                            cancellationToken);
                    }
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text || result.MessageType == WebSocketMessageType.Binary)
                {
                    await destination.SendAsync(
                        new ArraySegment<byte>(buffer, 0, result.Count),
                        result.MessageType,
                        result.EndOfMessage,
                        cancellationToken);
                }
            }
        }
        catch (WebSocketException ex)
        {
            _logger.LogWarning(ex, "WebSocket error durante proxy: {Error}", ex.Message);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Proxy cancelado");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado durante proxy: {Error}", ex.Message);
        }
    }

    /// <summary>
    /// Envia mensagem de erro e fecha conexão
    /// </summary>
    private async Task SendErrorAndClose(WebSocket webSocket, string errorMessage)
    {
        try
        {
            var errorJson = JsonSerializer.Serialize(new { error = errorMessage });
            var errorBytes = Encoding.UTF8.GetBytes(errorJson);
            await webSocket.SendAsync(
                new ArraySegment<byte>(errorBytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao enviar mensagem de erro: {Error}", ex.Message);
        }
        finally
        {
            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(
                    WebSocketCloseStatus.InternalServerError,
                    errorMessage,
                    CancellationToken.None);
            }
        }
    }

    /// <summary>
    /// Constrói a URL do WebSocket do routeros.io baseada no ServerEndpoint
    /// </summary>
    private string GetWebSocketUrl(string? serverEndpoint = null)
    {
        // Verificar se está em produção (HTTPS) para usar wss://
        var isHttps = HttpContext.Request.IsHttps || 
                     HttpContext.Request.Headers["X-Forwarded-Proto"].ToString().Equals("https", StringComparison.OrdinalIgnoreCase);
        var wsProtocol = isHttps ? "wss://" : "ws://";

        if (string.IsNullOrWhiteSpace(serverEndpoint))
        {
            // Fallback para URL padrão
            var defaultEndpoint = _configuration["RouterOsService:DefaultServerEndpoint"] ?? "localhost";
            return $"{wsProtocol}{defaultEndpoint}:8765";
        }

        // Construir URL baseada no ServerEndpoint
        // Se o ServerEndpoint já contém protocolo WebSocket, usar como está
        if (serverEndpoint.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) ||
            serverEndpoint.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
        {
            // Se já tem porta, usar como está; senão, adicionar porta padrão
            if (serverEndpoint.Contains(':', StringComparison.Ordinal) && 
                !serverEndpoint.EndsWith("://", StringComparison.OrdinalIgnoreCase))
            {
                return serverEndpoint;
            }
            return $"{serverEndpoint}:8765";
        }

        // Se o ServerEndpoint contém http:// ou https://, converter para ws:// ou wss://
        if (serverEndpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            var endpointWithoutProtocol = serverEndpoint.Replace("http://", "");
            return $"{wsProtocol}{endpointWithoutProtocol}:8765";
        }
        
        if (serverEndpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            // https:// sempre vira wss://
            var endpointWithoutProtocol = serverEndpoint.Replace("https://", "");
            return $"wss://{endpointWithoutProtocol}:8765";
        }

        // Adicionar protocolo apropriado e porta
        return $"{wsProtocol}{serverEndpoint}:8765";
    }
}
