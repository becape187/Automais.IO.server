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
        
        // Intervalo padrão: 10 segundos (configurável via appsettings)
        var intervalSeconds = configuration?.GetValue<int>("RouterMonitoring:CheckIntervalSeconds") ?? 10;
        _checkInterval = TimeSpan.FromSeconds(intervalSeconds);
        
        // Timeout do ping: 3 segundos (configurável via appsettings)
        _pingTimeout = configuration?.GetValue<int>("RouterMonitoring:PingTimeoutMs") ?? 3000;
        
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

            // Executar verificação de cada router com timeout individual
            // Cada router tem timeout de 30 segundos para não travar o processo
            var tasks = routerList.Select(async router =>
            {
                try
                {
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                    
                    await CheckRouterStatusAsync(router, linkedCts.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("⏱️ Timeout ao verificar status do router {RouterId} ({Name})", router.Id, router.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Erro ao verificar status do router {RouterId} ({Name})", router.Id, router.Name);
                }
            });
            
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

            // Fazer ping no IP - se o ping funcionar, o router está online
            // Tenta até 3 vezes antes de considerar offline
            // Timeout total de 15 segundos (3 tentativas x 5s cada)
            var (pingSuccess, latency) = await PingWithRetryAsync(ip, _pingTimeout, maxRetries: 3)
                .WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            
            // Status online baseado na conectividade (ping)
            // A verificação da interface WireGuard é apenas informativa
            var isOnline = pingSuccess;
            
            if (pingSuccess)
            {
                // Verificar interface WireGuard apenas para logs informativos (assíncrono e protegido)
                // Fire-and-forget: não bloqueia o processo principal
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                        
                        var hasWireGuardActive = await CheckWireGuardInterfaceAsync(router, linkedCts.Token);
                        
                        if (!hasWireGuardActive)
                        {
                            _logger.LogDebug("Router {RouterId} ({Name}) está online (ping OK) mas interface WireGuard não está ativa no router.", 
                                router.Id, router.Name);
                        }
                        else
                        {
                            _logger.LogDebug("Router {RouterId} ({Name}) está online e interface WireGuard está ativa.", 
                                router.Id, router.Name);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogDebug("⏱️ Timeout ao verificar interface WireGuard do router {RouterId}", router.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ Erro ao verificar interface WireGuard do router {RouterId}. Continuando operação.", router.Id);
                    }
                }, cancellationToken);
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
                router.Latency = latency; // Salvar latência quando online
            }
            else
            {
                router.Latency = null; // Limpar latência quando offline
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

                // Enviar atualização via SignalR de forma assíncrona e protegida
                // Fire-and-forget: não bloqueia o processo principal
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Timeout de 5 segundos para evitar travamentos
                        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                        
                        await _hubContext.Clients.All.SendAsync("RouterStatusChanged", new
                        {
                            routerId = router.Id,
                            name = router.Name,
                            status = newStatus.ToString(),
                            lastSeenAt = router.LastSeenAt,
                            latency = router.Latency,
                            previousStatus = previousStatus.ToString()
                        }, linkedCts.Token);
                        
                        _logger.LogDebug("✅ SignalR: Atualização enviada para router {RouterId}", router.Id);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogWarning("⏱️ Timeout ao enviar atualização SignalR para router {RouterId}", router.Id);
                    }
                    catch (Exception signalREx)
                    {
                        // Logar erro mas não derrubar a aplicação
                        _logger.LogWarning(signalREx, 
                            "⚠️ Erro ao enviar atualização SignalR para router {RouterId}. Continuando operação.", 
                            router.Id);
                    }
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
    /// Faz ping em um IP com retry (até 3 tentativas) e retorna (sucesso, latência)
    /// Se responder na primeira vez, retorna imediatamente
    /// Se não responder, tenta até maxRetries vezes
    /// </summary>
    private async Task<(bool success, int? latency)> PingWithRetryAsync(string ip, int timeoutMs, int maxRetries = 3)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(ip, timeoutMs);
                var isOnline = reply.Status == IPStatus.Success;
                
                if (isOnline)
                {
                    var latency = (int)reply.RoundtripTime;
                    if (attempt > 1)
                    {
                        _logger.LogDebug("✅ Ping OK para IP {Ip} na tentativa {Attempt}/{MaxRetries} - Tempo: {Time}ms", 
                            ip, attempt, maxRetries, latency);
                    }
                    else
                    {
                        _logger.LogDebug("✅ Ping OK para IP {Ip} - Tempo: {Time}ms", ip, latency);
                    }
                    return (true, latency);
                }
                else
                {
                    if (attempt < maxRetries)
                    {
                        _logger.LogDebug("❌ Ping falhou para IP {Ip} na tentativa {Attempt}/{MaxRetries} - Status: {Status}. Tentando novamente...", 
                            ip, attempt, maxRetries, reply.Status);
                    }
                    else
                    {
                        _logger.LogDebug("❌ Ping falhou para IP {Ip} após {MaxRetries} tentativas - Status: {Status}", 
                            ip, maxRetries, reply.Status);
                    }
                }
            }
            catch (Exception ex)
            {
                if (attempt < maxRetries)
                {
                    _logger.LogDebug(ex, "❌ Exceção ao fazer ping no IP {Ip} na tentativa {Attempt}/{MaxRetries}: {Message}. Tentando novamente...", 
                        ip, attempt, maxRetries, ex.Message);
                }
                else
                {
                    _logger.LogDebug(ex, "❌ Exceção ao fazer ping no IP {Ip} após {MaxRetries} tentativas: {Message}", 
                        ip, maxRetries, ex.Message);
                }
            }
            
            // Aguardar um pouco antes da próxima tentativa (apenas se não for a última)
            if (attempt < maxRetries)
            {
                await Task.Delay(500); // 500ms entre tentativas
            }
        }
        
        return (false, null);
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

