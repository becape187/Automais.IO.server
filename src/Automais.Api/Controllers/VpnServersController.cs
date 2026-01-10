using Automais.Core.DTOs;
using Automais.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Automais.Api.Controllers;

/// <summary>
/// Controller para gerenciamento de servidores VPN
/// </summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public class VpnServersController : ControllerBase
{
    private readonly IVpnNetworkRepository _vpnNetworkRepository;
    private readonly IRouterRepository _routerRepository;
    private readonly ILogger<VpnServersController> _logger;

    public VpnServersController(
        IVpnNetworkRepository vpnNetworkRepository,
        IRouterRepository routerRepository,
        ILogger<VpnServersController> logger)
    {
        _vpnNetworkRepository = vpnNetworkRepository;
        _routerRepository = routerRepository;
        _logger = logger;
    }

    /// <summary>
    /// Endpoint usado pelo serviço VPN Python para descobrir quais recursos ele deve gerenciar.
    /// O serviço Python consulta este endpoint usando seu VPN_SERVER_ENDPOINT (variável de ambiente).
    /// Busca todas as VpnNetworks que têm o ServerEndpoint correspondente.
    /// </summary>
    /// <param name="endpoint">Endpoint do servidor VPN (ex: automais.io) - deve corresponder ao ServerEndpoint das VpnNetworks</param>
    /// <returns>Lista de VpnNetworks e Routers que este servidor VPN deve gerenciar</returns>
    [HttpGet("vpn/networks/{endpoint}/resources")]
    public async Task<ActionResult<object>> GetNetworkResources(string endpoint, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Serviço VPN com endpoint '{Endpoint}' consultando seus recursos", endpoint);

            // Buscar todas as VpnNetworks que têm este ServerEndpoint
            var allVpnNetworks = await _vpnNetworkRepository.GetAllAsync(cancellationToken);
            
            // Filtrar VpnNetworks pelo ServerEndpoint
            var vpnNetworks = allVpnNetworks
                .Where(vpn => vpn.ServerEndpoint != null && vpn.ServerEndpoint.Equals(endpoint, StringComparison.OrdinalIgnoreCase))
                .Select(vpn => new
                {
                    id = vpn.Id.ToString(),
                    name = vpn.Name,
                    cidr = vpn.Cidr,
                    server_endpoint = vpn.ServerEndpoint,
                    server_private_key = vpn.ServerPrivateKey,
                    server_public_key = vpn.ServerPublicKey,
                    dns_servers = vpn.DnsServers,
                    tenant_id = vpn.TenantId.ToString()
                })
                .ToList();

            // Buscar todos os Routers que pertencem às VpnNetworks deste servidor
            var vpnNetworkIds = vpnNetworks.Select(v => Guid.Parse(v.id)).ToList();
            var allRouters = await _routerRepository.GetAllAsync(cancellationToken);
            
            var routers = allRouters
                .Where(r => r.VpnNetworkId.HasValue && vpnNetworkIds.Contains(r.VpnNetworkId.Value))
                .Select(r => new
                {
                    id = r.Id.ToString(),
                    name = r.Name,
                    vpn_network_id = r.VpnNetworkId?.ToString(),
                    router_os_api_url = r.RouterOsApiUrl,
                    status = r.Status.ToString()
                })
                .ToList();

            _logger.LogInformation(
                "Servidor VPN com endpoint '{Endpoint}' gerencia {VpnCount} VPNs e {RouterCount} Routers",
                endpoint, vpnNetworks.Count, routers.Count);

            return Ok(new
            {
                endpoint = endpoint,
                vpn_networks = vpnNetworks,
                routers = routers,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar recursos do servidor VPN com endpoint '{Endpoint}'", endpoint);
            return StatusCode(500, new
            {
                message = "Erro ao consultar recursos do servidor VPN",
                detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Health check do servidor VPN
    /// </summary>
    [HttpGet("vpn/networks/{endpoint}/health")]
    public async Task<ActionResult<object>> GetNetworkHealth(string endpoint, CancellationToken cancellationToken = default)
    {
        try
        {
            // Verificar se existem VpnNetworks com este endpoint
            var allVpnNetworks = await _vpnNetworkRepository.GetAllAsync(cancellationToken);
            var hasNetworks = allVpnNetworks.Any(vpn => 
                vpn.ServerEndpoint != null && vpn.ServerEndpoint.Equals(endpoint, StringComparison.OrdinalIgnoreCase));

            if (!hasNetworks)
            {
                return NotFound(new { message = $"Nenhuma VpnNetwork encontrada com endpoint '{endpoint}'" });
            }

            return Ok(new
            {
                endpoint = endpoint,
                status = "active",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar health do servidor VPN com endpoint '{Endpoint}'", endpoint);
            return StatusCode(500, new
            {
                message = "Erro ao verificar health do servidor VPN",
                detail = ex.Message
            });
        }
    }
}

