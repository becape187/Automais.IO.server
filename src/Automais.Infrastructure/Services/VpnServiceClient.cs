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
/// </summary>
public class VpnServiceClient : IVpnServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<VpnServiceClient> _logger;
    private readonly VpnServiceOptions _options;

    public VpnServiceClient(
        HttpClient httpClient,
        IOptions<VpnServiceOptions> options,
        ILogger<VpnServiceClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        // Configurar base URL e timeout
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
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
            var response = await _httpClient.PostAsJsonAsync(
                "/api/v1/vpn/provision-peer",
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
            var response = await _httpClient.GetAsync(
                $"/api/v1/vpn/config/{routerId}",
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
            var response = await _httpClient.PostAsJsonAsync(
                "/api/v1/vpn/add-network",
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
            var response = await _httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Delete, "/api/v1/vpn/remove-network")
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
            var response = await _httpClient.PostAsJsonAsync(
                "/api/v1/vpn/ensure-interface",
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
            var response = await _httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Delete, "/api/v1/vpn/remove-interface")
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
}

/// <summary>
/// Resultado do provisionamento de peer
/// </summary>
public class ProvisionPeerResult
{
    public string PublicKey { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
    public string AllowedIps { get; set; } = string.Empty;
}

