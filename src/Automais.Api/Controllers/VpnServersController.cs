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
    /// O serviço Python consulta este endpoint usando seu VPN_SERVER_NAME (variável de ambiente).
    /// </summary>
    /// <param name="serverName">Nome do servidor VPN (deve corresponder ao VPN_SERVER_NAME do serviço Python)</param>
    /// <returns>Lista de VpnNetworks e Routers que este servidor VPN deve gerenciar</returns>
    [HttpGet("vpn-servers/{serverName}/resources")]
    public async Task<ActionResult<object>> GetServerResources(string serverName, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Serviço VPN '{ServerName}' consultando seus recursos", serverName);

            // Buscar todas as VpnNetworks associadas a este servidor VPN
            // TODO: Quando tiver VpnServerRepository, buscar pelo ServerName
            // Por enquanto, vamos buscar todas as VpnNetworks e filtrar depois
            var allVpnNetworks = await _vpnNetworkRepository.GetAllAsync(cancellationToken);
            
            // Filtrar VpnNetworks que pertencem a este servidor
            // TODO: Implementar busca direta quando tiver VpnServerRepository
            var vpnNetworks = allVpnNetworks
                .Where(vpn => vpn.VpnServer != null && vpn.VpnServer.ServerName == serverName)
                .Select(vpn => new
                {
                    id = vpn.Id.ToString(),
                    name = vpn.Name,
                    cidr = vpn.Cidr,
                    server_endpoint = vpn.ServerEndpoint,
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
                "Servidor VPN '{ServerName}' gerencia {VpnCount} VPNs e {RouterCount} Routers",
                serverName, vpnNetworks.Count, routers.Count);

            return Ok(new
            {
                server_name = serverName,
                vpn_networks = vpnNetworks,
                routers = routers,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar recursos do servidor VPN '{ServerName}'", serverName);
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
    [HttpGet("vpn-servers/{serverName}/health")]
    public async Task<ActionResult<object>> GetServerHealth(string serverName, CancellationToken cancellationToken = default)
    {
        try
        {
            // Verificar se o servidor existe
            // TODO: Quando tiver VpnServerRepository, buscar pelo ServerName
            var allVpnNetworks = await _vpnNetworkRepository.GetAllAsync(cancellationToken);
            var serverExists = allVpnNetworks.Any(vpn => 
                vpn.VpnServer != null && vpn.VpnServer.ServerName == serverName);

            if (!serverExists)
            {
                return NotFound(new { message = $"Servidor VPN '{serverName}' não encontrado" });
            }

            return Ok(new
            {
                server_name = serverName,
                status = "active",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar health do servidor VPN '{ServerName}'", serverName);
            return StatusCode(500, new
            {
                message = "Erro ao verificar health do servidor VPN",
                detail = ex.Message
            });
        }
    }
}

