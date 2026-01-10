using Automais.Core.DTOs;
using Automais.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace Automais.Infrastructure.Services;

/// <summary>
/// Configuração do serviço VPN Python
/// </summary>
public class VpnServiceOptions
{
    public string BaseUrl { get; set; } = "http://localhost:8000";
    public int TimeoutSeconds { get; set; } = 30;
    public int RetryCount { get; set; } = 3;
}

/// <summary>
/// Cliente HTTP para comunicação com o serviço VPN Python
/// IMPORTANTE: As URLs são construídas dinamicamente baseadas no ServerEndpoint da VpnNetwork.
/// Cada router pode estar associado a uma VpnNetwork diferente, que por sua vez pode ter um ServerEndpoint diferente.
/// O ServerEndpoint identifica qual servidor VPN físico gerencia aquela rede.
/// </summary>
public class VpnServiceClient : IVpnServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<VpnServiceClient> _logger;
    private readonly VpnServiceOptions _options;
    private readonly IVpnNetworkRepository? _vpnNetworkRepository;
    private readonly IRouterRepository? _routerRepository;

    public VpnServiceClient(
        HttpClient httpClient,
        IOptions<VpnServiceOptions> options,
        ILogger<VpnServiceClient> logger,
        IVpnNetworkRepository? vpnNetworkRepository = null,
        IRouterRepository? routerRepository = null)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _vpnNetworkRepository = vpnNetworkRepository;
        _routerRepository = routerRepository;

        // NÃO configurar BaseAddress - URLs serão construídas dinamicamente por chamada
        // baseadas no ServerEndpoint da VpnNetwork
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    /// <summary>
    /// Busca o ServerEndpoint da VpnNetwork
    /// </summary>
    private async Task<string?> GetServerEndpointAsync(Guid vpnNetworkId, CancellationToken cancellationToken)
    {
        if (_vpnNetworkRepository == null)
        {
            return null;
        }

        try
        {
            var vpnNetwork = await _vpnNetworkRepository.GetByIdAsync(vpnNetworkId, cancellationToken);
            return vpnNetwork?.ServerEndpoint;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Busca o ServerEndpoint do router via sua VpnNetwork
    /// </summary>
    private async Task<string?> GetServerEndpointFromRouterAsync(Guid routerId, CancellationToken cancellationToken)
    {
        if (_routerRepository == null || _vpnNetworkRepository == null)
        {
            return null;
        }

        try
        {
            var router = await _routerRepository.GetByIdAsync(routerId, cancellationToken);
            if (router?.VpnNetworkId == null)
            {
                return null;
            }

            return await GetServerEndpointAsync(router.VpnNetworkId.Value, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Constrói a URL base do serviço VPN baseada no ServerEndpoint.
    /// IMPORTANTE: O ServerEndpoint identifica qual servidor VPN físico gerencia a rede.
    /// Cada VpnNetwork pode ter um ServerEndpoint diferente, permitindo múltiplos servidores VPN.
    /// Se o ServerEndpoint não for fornecido, usa a URL padrão da configuração.
    /// </summary>
    private string GetBaseUrl(string? serverEndpoint = null)
    {
        if (string.IsNullOrWhiteSpace(serverEndpoint))
        {
            // Fallback para URL padrão se não houver ServerEndpoint
            return _options.BaseUrl;
        }

        // Construir URL baseada no ServerEndpoint
        // Formato esperado: http://{ServerEndpoint}:8000 ou https://{ServerEndpoint}:8000
        // Se o ServerEndpoint já contém protocolo, usar; senão, adicionar http://
        if (serverEndpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            serverEndpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            // Se já tem porta, usar como está; senão, adicionar porta padrão
            if (serverEndpoint.Contains(':', StringComparison.Ordinal) && 
                !serverEndpoint.EndsWith("://", StringComparison.OrdinalIgnoreCase))
            {
                return serverEndpoint;
            }
            return $"{serverEndpoint}:8000";
        }

        // Adicionar protocolo e porta padrão
        return $"http://{serverEndpoint}:8000";
    }

    public async Task<ProvisionPeerResult> ProvisionPeerAsync(
        Guid routerId,
        Guid vpnNetworkId,
        IEnumerable<string> allowedNetworks,
        string? manualIp = null,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            router_id = routerId.ToString(),
            vpn_network_id = vpnNetworkId.ToString(),
            allowed_networks = allowedNetworks.ToList(),
            manual_ip = manualIp
        };

        _logger.LogInformation(
            "Chamando serviço VPN para provisionar peer: Router={RouterId}, VPN={VpnNetworkId}",
            routerId, vpnNetworkId);

        try
        {
            // Buscar ServerEndpoint da VpnNetwork para construir URL dinâmica
            var serverEndpoint = await GetServerEndpointAsync(vpnNetworkId, cancellationToken);
            var baseUrl = GetBaseUrl(serverEndpoint);
            var fullUrl = $"{baseUrl.TrimEnd('/')}/api/v1/vpn/provision-peer";

            var response = await _httpClient.PostAsJsonAsync(
                fullUrl,
                request,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ProvisionPeerResponse>(
                cancellationToken: cancellationToken);

            if (result == null)
            {
                throw new InvalidOperationException("Resposta vazia do serviço VPN");
            }

            // Retornar resultado com private key
            return new ProvisionPeerResult
            {
                PublicKey = result.PublicKey,
                PrivateKey = result.PrivateKey ?? string.Empty,
                AllowedIps = result.AllowedIps ?? manualIp ?? string.Empty
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Erro ao chamar serviço VPN para provisionar peer");
            throw new InvalidOperationException(
                $"Erro ao provisionar peer no serviço VPN: {ex.Message}", ex);
        }
    }

    public async Task<RouterWireGuardConfigDto> GetConfigAsync(
        Guid routerId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Chamando serviço VPN para obter config: Router={RouterId}", routerId);

        try
        {
            // Buscar ServerEndpoint do router via VpnNetwork para construir URL dinâmica
            var serverEndpoint = await GetServerEndpointFromRouterAsync(routerId, cancellationToken);
            var baseUrl = GetBaseUrl(serverEndpoint);
            var fullUrl = $"{baseUrl.TrimEnd('/')}/api/v1/vpn/config/{routerId}";

            var response = await _httpClient.GetAsync(
                fullUrl,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<VpnConfigResponse>(
                cancellationToken: cancellationToken);

            if (result == null)
            {
                throw new InvalidOperationException("Resposta vazia do serviço VPN");
            }

            return new RouterWireGuardConfigDto
            {
                ConfigContent = result.ConfigContent,
                FileName = result.Filename
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Erro ao chamar serviço VPN para obter config");
            throw new InvalidOperationException(
                $"Erro ao obter config do serviço VPN: {ex.Message}", ex);
        }
    }

    public async Task AddNetworkToRouterAsync(
        Guid routerId,
        string networkCidr,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            router_id = routerId.ToString(),
            network_cidr = networkCidr,
            description = description
        };

        _logger.LogInformation(
            "Chamando serviço VPN para adicionar rede: Router={RouterId}, Network={NetworkCidr}",
            routerId, networkCidr);

        try
        {
            // Buscar ServerEndpoint do router para construir URL dinâmica
            var serverEndpoint = await GetServerEndpointFromRouterAsync(routerId, cancellationToken);
            var baseUrl = GetBaseUrl(serverEndpoint);
            var fullUrl = $"{baseUrl.TrimEnd('/')}/api/v1/vpn/add-network";

            var response = await _httpClient.PostAsJsonAsync(
                fullUrl,
                request,
                cancellationToken);

            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Erro ao chamar serviço VPN para adicionar rede");
            throw new InvalidOperationException(
                $"Erro ao adicionar rede no serviço VPN: {ex.Message}", ex);
        }
    }

    public async Task RemoveNetworkFromRouterAsync(
        Guid routerId,
        string networkCidr,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            router_id = routerId.ToString(),
            network_cidr = networkCidr
        };

        _logger.LogInformation(
            "Chamando serviço VPN para remover rede: Router={RouterId}, Network={NetworkCidr}",
            routerId, networkCidr);

        try
        {
            // Buscar ServerEndpoint do router para construir URL dinâmica
            var serverEndpoint = await GetServerEndpointFromRouterAsync(routerId, cancellationToken);
            var baseUrl = GetBaseUrl(serverEndpoint);
            var fullUrl = $"{baseUrl.TrimEnd('/')}/api/v1/vpn/remove-network";

            var response = await _httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Delete, fullUrl)
                {
                    Content = JsonContent.Create(request)
                },
                cancellationToken);

            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Erro ao chamar serviço VPN para remover rede");
            throw new InvalidOperationException(
                $"Erro ao remover rede no serviço VPN: {ex.Message}", ex);
        }
    }

    public async Task EnsureInterfaceAsync(
        Guid vpnNetworkId,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            vpn_network_id = vpnNetworkId.ToString()
        };

        _logger.LogInformation(
            "Chamando serviço VPN para garantir interface: VPN={VpnNetworkId}",
            vpnNetworkId);

        try
        {
            // Buscar ServerEndpoint da VpnNetwork para construir URL dinâmica
            var serverEndpoint = await GetServerEndpointAsync(vpnNetworkId, cancellationToken);
            var baseUrl = GetBaseUrl(serverEndpoint);
            var fullUrl = $"{baseUrl.TrimEnd('/')}/api/v1/vpn/ensure-interface";

            var response = await _httpClient.PostAsJsonAsync(
                fullUrl,
                request,
                cancellationToken);

            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Erro ao chamar serviço VPN para garantir interface");
            throw new InvalidOperationException(
                $"Erro ao garantir interface no serviço VPN: {ex.Message}", ex);
        }
    }

    public async Task RemoveInterfaceAsync(
        Guid vpnNetworkId,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            vpn_network_id = vpnNetworkId.ToString()
        };

        _logger.LogInformation(
            "Chamando serviço VPN para remover interface: VPN={VpnNetworkId}",
            vpnNetworkId);

        try
        {
            // Buscar ServerEndpoint da VpnNetwork para construir URL dinâmica
            var serverEndpoint = await GetServerEndpointAsync(vpnNetworkId, cancellationToken);
            var baseUrl = GetBaseUrl(serverEndpoint);
            var fullUrl = $"{baseUrl.TrimEnd('/')}/api/v1/vpn/remove-interface";

            var response = await _httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Delete, fullUrl)
                {
                    Content = JsonContent.Create(request)
                },
                cancellationToken);

            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Erro ao chamar serviço VPN para remover interface");
            throw new InvalidOperationException(
                $"Erro ao remover interface no serviço VPN: {ex.Message}", ex);
        }
    }

    public async Task<bool> AddRouteAsync(
        Guid routerId,
        RouterStaticRouteDto route,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            router_id = routerId.ToString(),
            route_id = route.Id.ToString(),
            destination = route.Destination,
            gateway = route.Gateway,
            interface_name = route.Interface,
            distance = route.Distance,
            scope = route.Scope,
            routing_table = route.RoutingTable,
            comment = route.Comment
        };

        _logger.LogInformation(
            "Chamando serviço VPN para adicionar rota: Router={RouterId}, Route={RouteId}, Destination={Destination}",
            routerId, route.Id, route.Destination);

        try
        {
            // Buscar ServerEndpoint do router para construir URL dinâmica
            var serverEndpoint = await GetServerEndpointFromRouterAsync(routerId, cancellationToken);
            var baseUrl = GetBaseUrl(serverEndpoint);
            var fullUrl = $"{baseUrl.TrimEnd('/')}/api/v1/routeros/add-route";

            var response = await _httpClient.PostAsJsonAsync(
                fullUrl,
                request,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<RouteOperationResponse>(cancellationToken: cancellationToken);
                return result?.Success ?? false;
            }

            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Erro ao chamar serviço VPN para adicionar rota");
            return false;
        }
    }

    public async Task<bool> RemoveRouteAsync(
        Guid routerId,
        string routerOsRouteId,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            router_id = routerId.ToString(),
            router_os_route_id = routerOsRouteId
        };

        _logger.LogInformation(
            "Chamando serviço VPN para remover rota: Router={RouterId}, RouterOsRouteId={RouterOsRouteId}",
            routerId, routerOsRouteId);

        try
        {
            // Buscar ServerEndpoint do router para construir URL dinâmica
            var serverEndpoint = await GetServerEndpointFromRouterAsync(routerId, cancellationToken);
            var baseUrl = GetBaseUrl(serverEndpoint);
            var fullUrl = $"{baseUrl.TrimEnd('/')}/api/v1/routeros/remove-route";

            var response = await _httpClient.PostAsJsonAsync(
                fullUrl,
                request,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<RouteOperationResponse>(cancellationToken: cancellationToken);
                return result?.Success ?? false;
            }

            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Erro ao chamar serviço VPN para remover rota");
            return false;
        }
    }

    public async Task<List<RouterOsWireGuardInterfaceDto>> ListWireGuardInterfacesAsync(
        Guid routerId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Chamando serviço VPN para listar interfaces WireGuard: Router={RouterId}", routerId);

        try
        {
            // Buscar ServerEndpoint do router para construir URL dinâmica
            var serverEndpoint = await GetServerEndpointFromRouterAsync(routerId, cancellationToken);
            var baseUrl = GetBaseUrl(serverEndpoint);
            var fullUrl = $"{baseUrl.TrimEnd('/')}/api/v1/routeros/{routerId}/wireguard-interfaces";

            var response = await _httpClient.GetAsync(fullUrl, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<WireGuardInterfacesResponse>(cancellationToken: cancellationToken);
                if (result?.Success == true && result.Interfaces != null)
                {
                    return result.Interfaces.Select(i => new RouterOsWireGuardInterfaceDto
                    {
                        Name = i.Name ?? string.Empty,
                        PublicKey = i.PublicKey ?? string.Empty,
                        ListenPort = i.ListenPort,
                        Mtu = i.Mtu,
                        Disabled = i.Disabled == "true" || i.Disabled == true,
                        Running = i.Running == "true" || i.Running == true
                    }).ToList();
                }
            }

            return new List<RouterOsWireGuardInterfaceDto>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Erro ao chamar serviço VPN para listar interfaces WireGuard");
            return new List<RouterOsWireGuardInterfaceDto>();
        }
    }

    // Classes auxiliares para deserialização
    private class ProvisionPeerResponse
    {
        public string PublicKey { get; set; } = string.Empty;
        public string? PrivateKey { get; set; }
        public string? AllowedIps { get; set; }
        public string InterfaceName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    private class VpnConfigResponse
    {
        public string ConfigContent { get; set; } = string.Empty;
        public string Filename { get; set; } = string.Empty;
    }

    private class RouteOperationResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? RouterOsId { get; set; }
        public string? Error { get; set; }
    }

    private class WireGuardInterfacesResponse
    {
        public bool Success { get; set; }
        public List<WireGuardInterfaceItem>? Interfaces { get; set; }
    }

    private class WireGuardInterfaceItem
    {
        public string? Name { get; set; }
        public string? PublicKey { get; set; }
        public string? ListenPort { get; set; }
        public string? Mtu { get; set; }
        public object? Disabled { get; set; } // Pode ser string "true"/"false" ou bool
        public object? Running { get; set; } // Pode ser string "true"/"false" ou bool
    }
}


