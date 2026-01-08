using Automais.Core.Entities;
using Automais.Core.Hubs;
using Automais.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.NetworkInformation;

namespace Automais.Infrastructure.Services;

/// <summary>
/// Serviço de monitoramento de status dos roteadores
/// Executa ping periódico nos IPs dos roteadores e atualiza status no banco
/// </summary>
public class RouterStatusMonitorService : BackgroundService
{
    private readonly IRouterRepository _routerRepository;
    private readonly IRouterWireGuardPeerRepository _peerRepository;
    private readonly IRouterOsClient _routerOsClient;
    private readonly IHubContext<RouterStatusHub> _hubContext;
    private readonly ILogger<RouterStatusMonitorService> _logger;
    private readonly TimeSpan _checkInterval;
    private readonly int _pingTimeout;

    public RouterStatusMonitorService(
        IRouterRepository routerRepository,
        IRouterWireGuardPeerRepository peerRepository,
        IRouterOsClient routerOsClient,
        IHubContext<RouterStatusHub> hubContext,
        ILogger<RouterStatusMonitorService> logger,
        IConfiguration configuration)
    {
        _routerRepository = routerRepository;
        _peerRepository = peerRepository;
        _routerOsClient = routerOsClient;
        _hubContext = hubContext;
        _logger = logger;
        
        // Intervalo padrão: 30 segundos (configurável via appsettings)
        var intervalSeconds = configuration?.GetValue<int>("RouterMonitoring:CheckIntervalSeconds") ?? 30;
        _checkInterval = TimeSpan.FromSeconds(intervalSeconds);
        
        // Timeout do ping: 5 segundos (aumentado para conexões via VPN)
        _pingTimeout = configuration?.GetValue<int>("RouterMonitoring:PingTimeoutMs") ?? 5000;
        
        _logger.LogInformation("RouterStatusMonitorService inicializado. Intervalo: {Interval}s, Timeout: {Timeout}ms", 
            intervalSeconds, _pingTimeout);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🔄 Serviço de monitoramento de status iniciado. Verificando roteadores a cada {Interval} segundos", 
            _checkInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAllRoutersStatusAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro durante verificação de status dos roteadores");
            }

            // Aguardar intervalo antes da próxima verificação
            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("🛑 Serviço de monitoramento de status encerrado");
    }

    private async Task CheckAllRoutersStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            var routers = await _routerRepository.GetAllAsync(cancellationToken);
            var routerList = routers.ToList();
            
            if (!routerList.Any())
            {
                _logger.LogDebug("Nenhum roteador encontrado para monitorar");
                return;
            }

            _logger.LogDebug("Verificando status de {Count} roteadores", routerList.Count);

            var tasks = routerList.Select(router => CheckRouterStatusAsync(router, cancellationToken));
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar roteadores para verificação de status");
        }
    }

    private async Task CheckRouterStatusAsync(Router router, CancellationToken cancellationToken)
    {
        try
        {
            // Tentar extrair IP do RouterOsApiUrl primeiro
            var ip = ExtractIpFromUrl(router.RouterOsApiUrl);
            
            // Se não tiver IP no RouterOsApiUrl, tentar pegar do peer WireGuard
            if (string.IsNullOrWhiteSpace(ip))
            {
                _logger.LogDebug("Router {RouterId} ({Name}) não possui RouterOsApiUrl. Tentando buscar IP do peer WireGuard...", 
                    router.Id, router.Name);
                
                var peers = await _peerRepository.GetByRouterIdAsync(router.Id, cancellationToken);
                var peerList = peers.ToList();
                
                if (peerList.Any())
                {
                    // Pegar o primeiro IP do AllowedIps (formato: "10.222.111.2" ou "10.222.111.2/32")
                    var firstPeer = peerList.First();
                    if (!string.IsNullOrWhiteSpace(firstPeer.AllowedIps))
                    {
                        // Extrair IP do formato CIDR (ex: "10.222.111.2/32" -> "10.222.111.2")
                        var allowedIps = firstPeer.AllowedIps.Split(',')[0].Trim();
                        var ipParts = allowedIps.Split('/');
                        ip = ipParts[0].Trim();
                        
                        _logger.LogDebug("Router {RouterId} ({Name}) usando IP do peer WireGuard: {Ip}", 
                            router.Id, router.Name, ip);
                    }
                }
            }
            
            if (string.IsNullOrWhiteSpace(ip))
            {
                _logger.LogWarning("Router {RouterId} ({Name}) não possui IP válido para monitoramento. RouterOsApiUrl: {RouterOsApiUrl}", 
                    router.Id, router.Name, router.RouterOsApiUrl ?? "(vazio)");
                return;
            }

            _logger.LogDebug("Fazendo ping no router {RouterId} ({Name}) no IP {Ip}", 
                router.Id, router.Name, ip);

            // Fazer ping no IP
            var pingSuccess = await PingAsync(ip, _pingTimeout);
            
            // Se o ping funcionou, verificar se a interface WireGuard está ativa
            var isOnline = false;
            if (pingSuccess)
            {
                // Verificar se há interface WireGuard ativa no RouterOS
                isOnline = await CheckWireGuardInterfaceAsync(router, cancellationToken);
                
                if (!isOnline)
                {
                    _logger.LogWarning("Router {RouterId} ({Name}) respondeu ao ping mas não possui interface WireGuard ativa. Marcando como offline.", 
                        router.Id, router.Name);
                }
            }
            else
            {
                _logger.LogDebug("Router {RouterId} ({Name}) não respondeu ao ping. Marcando como offline.", 
                    router.Id, router.Name);
            }

            // Determinar novo status
            var previousStatus = router.Status;
            var newStatus = isOnline ? RouterStatus.Online : RouterStatus.Offline;
            var statusChanged = previousStatus != newStatus;

            // Atualizar dados do router
            router.Status = newStatus;
            if (isOnline)
            {
                router.LastSeenAt = DateTime.UtcNow;
            }
            router.UpdatedAt = DateTime.UtcNow;

            // Atualizar no banco
            await _routerRepository.UpdateAsync(router, cancellationToken);

            // Notificar via SignalR se o status mudou OU se está online (para atualizar LastSeenAt na tela)
            if (statusChanged || isOnline)
            {
                if (statusChanged)
                {
                    _logger.LogInformation("📡 Status do router {RouterId} ({Name}) mudou: {PreviousStatus} → {NewStatus} (IP: {Ip})", 
                        router.Id, router.Name, previousStatus, newStatus, ip);
                }
                else
                {
                    _logger.LogDebug("Router {RouterId} ({Name}) online. Atualizando LastSeenAt (IP: {Ip})", 
                        router.Id, router.Name, ip);
                }

                await _hubContext.Clients.All.SendAsync("RouterStatusChanged", new
                {
                    routerId = router.Id,  // camelCase para compatibilidade com JavaScript
                    RouterId = router.Id,  // Manter PascalCase para compatibilidade
                    name = router.Name,
                    Name = router.Name,
                    status = newStatus.ToString(),
                    Status = newStatus.ToString(),
                    lastSeenAt = router.LastSeenAt,
                    LastSeenAt = router.LastSeenAt,
                    previousStatus = previousStatus.ToString(),
                    PreviousStatus = previousStatus.ToString()
                }, cancellationToken);
            }
            else
            {
                _logger.LogDebug("Router {RouterId} ({Name}) offline. Status mantido: {Status} (IP: {Ip})", 
                    router.Id, router.Name, newStatus, ip);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar status do router {RouterId} ({Name})", 
                router.Id, router.Name);
        }
    }

    /// <summary>
    /// Extrai o IP de uma URL no formato "IP:porta" ou apenas "IP"
    /// </summary>
    private string? ExtractIpFromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        try
        {
            // Remover protocolo se houver (http://, https://)
            url = url.Replace("http://", "").Replace("https://", "").Trim();

            // Se contém ":", separar IP e porta
            if (url.Contains(':'))
            {
                var parts = url.Split(':');
                if (parts.Length >= 1)
                {
                    var ip = parts[0].Trim();
                    // Validar se é um IP válido
                    if (IPAddress.TryParse(ip, out _))
                    {
                        return ip;
                    }
                }
            }
            else
            {
                // Tentar validar como IP direto
                if (IPAddress.TryParse(url, out _))
                {
                    return url;
                }
            }

            // Tentar extrair IP de uma URL completa
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                var host = uri.Host;
                if (IPAddress.TryParse(host, out _))
                {
                    return host;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao extrair IP da URL: {Url}", url);
        }

        return null;
    }

    /// <summary>
    /// Faz ping em um IP e retorna true se estiver online
    /// </summary>
    private async Task<bool> PingAsync(string ip, int timeoutMs)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ip, timeoutMs);
            var isOnline = reply.Status == IPStatus.Success;
            
            if (isOnline)
            {
                _logger.LogDebug("✅ Ping OK para IP {Ip} - Tempo: {Time}ms", ip, reply.RoundtripTime);
            }
            else
            {
                _logger.LogDebug("❌ Ping falhou para IP {Ip} - Status: {Status}", ip, reply.Status);
            }
            
            return isOnline;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "❌ Exceção ao fazer ping no IP {Ip}: {Message}", ip, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Verifica se há interface WireGuard ativa no RouterOS
    /// </summary>
    private async Task<bool> CheckWireGuardInterfaceAsync(Router router, CancellationToken cancellationToken)
    {
        try
        {
            // Verificar se temos credenciais da API RouterOS
            if (string.IsNullOrWhiteSpace(router.RouterOsApiUrl) ||
                string.IsNullOrWhiteSpace(router.RouterOsApiUsername) ||
                string.IsNullOrWhiteSpace(router.RouterOsApiPassword))
            {
                _logger.LogDebug("Router {RouterId} não possui credenciais RouterOS. Considerando offline.", router.Id);
                return false;
            }

            // Construir URL da API
            var apiUrl = router.RouterOsApiUrl;
            if (string.IsNullOrWhiteSpace(apiUrl))
            {
                // Tentar buscar IP do peer WireGuard
                var peers = await _peerRepository.GetByRouterIdAsync(router.Id, cancellationToken);
                var peerList = peers.ToList();
                
                if (peerList.Any())
                {
                    var firstPeer = peerList.First();
                    if (!string.IsNullOrWhiteSpace(firstPeer.AllowedIps))
                    {
                        var allowedIps = firstPeer.AllowedIps.Split(',')[0].Trim();
                        var ipParts = allowedIps.Split('/');
                        var ip = ipParts[0].Trim();
                        apiUrl = $"{ip}:8728";
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(apiUrl))
            {
                _logger.LogDebug("Router {RouterId} não possui URL da API RouterOS. Considerando offline.", router.Id);
                return false;
            }

            // Verificar se há interfaces WireGuard ativas
            var interfaces = await _routerOsClient.ExecuteCommandAsync(
                apiUrl,
                router.RouterOsApiUsername,
                router.RouterOsApiPassword,
                "/interface/wireguard/print",
                cancellationToken);

            // Log detalhado das interfaces encontradas
            if (interfaces.Count == 0)
            {
                _logger.LogWarning("❌ Router {RouterId} não possui interfaces WireGuard configuradas", router.Id);
                return false;
            }

            // Verificar se há pelo menos uma interface WireGuard com running=true
            var activeInterfaces = interfaces.Where(iface => 
            {
                var hasRunning = iface.TryGetValue("running", out var running);
                var isRunning = hasRunning && running?.ToLowerInvariant() == "true";
                
                // Log detalhado de cada interface
                var interfaceName = iface.TryGetValue("name", out var name) ? name : "sem nome";
                var disabled = iface.TryGetValue("disabled", out var disabledValue) && 
                              disabledValue?.ToLowerInvariant() == "true";
                
                _logger.LogDebug("Interface WireGuard {Name} - running: {Running}, disabled: {Disabled}", 
                    interfaceName, running ?? "n/a", disabledValue ?? "n/a");
                
                return isRunning && !disabled;
            }).ToList();

            var hasActiveInterface = activeInterfaces.Any();

            if (hasActiveInterface)
            {
                _logger.LogInformation("✅ Router {RouterId} possui {Count} interface(s) WireGuard ativa(s)", 
                    router.Id, activeInterfaces.Count);
                return true;
            }
            else
            {
                _logger.LogWarning("❌ Router {RouterId} não possui interface WireGuard ativa. Total de interfaces: {Count} (todas desativadas ou não rodando)", 
                    router.Id, interfaces.Count);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao verificar interface WireGuard do router {RouterId}. Considerando offline.", router.Id);
            return false;
        }
    }
}

