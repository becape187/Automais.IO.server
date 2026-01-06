using Automais.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Automais.Infrastructure.WireGuard;

/// <summary>
/// Serviço que sincroniza arquivos de configuração do WireGuard com o banco de dados
/// Executa na inicialização da API para garantir integridade
/// </summary>
public class WireGuardSyncService : IHostedService
{
    private readonly IWireGuardServerService _wireGuardServerService;
    private readonly IVpnNetworkRepository _vpnNetworkRepository;
    private readonly IRouterWireGuardPeerRepository _peerRepository;
    private readonly IRouterRepository _routerRepository;
    private readonly IRouterAllowedNetworkRepository _allowedNetworkRepository;
    private readonly ILogger<WireGuardSyncService> _logger;

    public WireGuardSyncService(
        IWireGuardServerService wireGuardServerService,
        IVpnNetworkRepository vpnNetworkRepository,
        IRouterWireGuardPeerRepository peerRepository,
        IRouterRepository routerRepository,
        IRouterAllowedNetworkRepository allowedNetworkRepository,
        ILogger<WireGuardSyncService> logger)
    {
        _wireGuardServerService = wireGuardServerService;
        _vpnNetworkRepository = vpnNetworkRepository;
        _peerRepository = peerRepository;
        _routerRepository = routerRepository;
        _allowedNetworkRepository = allowedNetworkRepository;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🔄 Iniciando sincronização do WireGuard com banco de dados...");

        try
        {
            await SyncWireGuardConfigurationsAsync(cancellationToken);
            _logger.LogInformation("✅ Sincronização do WireGuard concluída com sucesso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro durante sincronização do WireGuard");
            // Não lança exceção para não impedir a inicialização da API
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sincroniza todas as configurações do WireGuard do banco de dados para os arquivos
    /// </summary>
    private async Task SyncWireGuardConfigurationsAsync(CancellationToken cancellationToken)
    {
        // Buscar todas as VpnNetworks através dos peers (peers só existem se houver VpnNetwork)
        var allPeers = await _peerRepository.GetAllAsync(cancellationToken);
        
        // Obter VpnNetworkIds únicos
        var vpnNetworkIds = allPeers
            .Select(p => p.VpnNetworkId)
            .Distinct()
            .ToList();

        if (!vpnNetworkIds.Any())
        {
            _logger.LogInformation("Nenhuma VpnNetwork com peers encontrada. Nada para sincronizar.");
            return;
        }

        _logger.LogInformation("Encontradas {Count} VpnNetworks para sincronizar", vpnNetworkIds.Count);

        foreach (var vpnNetworkId in vpnNetworkIds)
        {
            try
            {
                await SyncVpnNetworkAsync(vpnNetworkId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao sincronizar VpnNetwork {VpnNetworkId}", vpnNetworkId);
                // Continua com as próximas redes
            }
        }
    }

    /// <summary>
    /// Sincroniza uma VpnNetwork específica
    /// </summary>
    private async Task SyncVpnNetworkAsync(Guid vpnNetworkId, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Sincronizando VpnNetwork {VpnNetworkId}", vpnNetworkId);

        var vpnNetwork = await _vpnNetworkRepository.GetByIdAsync(vpnNetworkId, cancellationToken);
        if (vpnNetwork == null)
        {
            _logger.LogWarning("VpnNetwork {VpnNetworkId} não encontrada no banco", vpnNetworkId);
            return;
        }

        // Buscar todos os peers desta VpnNetwork
        var peers = await _peerRepository.GetByVpnNetworkIdAsync(vpnNetworkId, cancellationToken);
        
        if (!peers.Any())
        {
            _logger.LogDebug("VpnNetwork {VpnNetworkId} não possui peers. Apenas garantindo que a interface existe.", vpnNetworkId);
            // Ainda assim, garantir que a interface existe (pode ter sido criada mas sem peers ainda)
            var interfaceName = GetInterfaceName(vpnNetworkId);
            await EnsureInterfaceExistsAsync(interfaceName, vpnNetwork, cancellationToken);
            return;
        }

        _logger.LogInformation("Sincronizando {PeerCount} peers da VpnNetwork {VpnNetworkId}", peers.Count(), vpnNetworkId);

        // Garantir que a interface existe
        var interfaceName = GetInterfaceName(vpnNetworkId);
        await EnsureInterfaceExistsAsync(interfaceName, vpnNetwork, cancellationToken);

        // Sincronizar cada peer
        foreach (var peer in peers)
        {
            try
            {
                await SyncPeerAsync(peer, vpnNetwork, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao sincronizar peer {PeerId}", peer.Id);
            }
        }

        // Salvar configuração persistente
        await SaveWireGuardConfigAsync(interfaceName, cancellationToken);
        
        _logger.LogInformation("VpnNetwork {VpnNetworkId} sincronizada com sucesso", vpnNetworkId);
    }

    /// <summary>
    /// Sincroniza um peer específico
    /// </summary>
    private async Task SyncPeerAsync(
        Automais.Core.Entities.RouterWireGuardPeer peer,
        Automais.Core.Entities.VpnNetwork vpnNetwork,
        CancellationToken cancellationToken)
    {
        var router = await _routerRepository.GetByIdAsync(peer.RouterId, cancellationToken);
        if (router == null)
        {
            _logger.LogWarning("Router {RouterId} do peer {PeerId} não encontrado", peer.RouterId, peer.Id);
            return;
        }

        // Buscar redes permitidas
        var allowedNetworks = await _allowedNetworkRepository.GetByRouterIdAsync(peer.RouterId, cancellationToken);
        var allowedIps = new List<string> { peer.AllowedIps };
        allowedIps.AddRange(allowedNetworks.Select(n => n.NetworkCidr));

        var interfaceName = GetInterfaceName(vpnNetwork.Id);
        var allowedIpsString = string.Join(",", allowedIps);

        // Adicionar/atualizar peer na interface WireGuard
        await ExecuteWireGuardCommandAsync(
            $"set {interfaceName} peer {peer.PublicKey} allowed-ips {allowedIpsString}",
            cancellationToken);

        _logger.LogDebug("Peer {PeerId} sincronizado na interface {InterfaceName}", peer.Id, interfaceName);
    }

    /// <summary>
    /// Garante que a interface WireGuard existe (delega para o WireGuardServerService)
    /// </summary>
    private async Task EnsureInterfaceExistsAsync(
        string interfaceName,
        Automais.Core.Entities.VpnNetwork vpnNetwork,
        CancellationToken cancellationToken)
    {
        // Usar o método público do WireGuardServerService
        await _wireGuardServerService.EnsureInterfaceForVpnNetworkAsync(vpnNetwork.Id, cancellationToken);
    }

    private string GetInterfaceName(Guid vpnNetworkId)
    {
        return $"wg-{vpnNetworkId.ToString("N")[..8]}";
    }

    private async Task ExecuteWireGuardCommandAsync(string command, CancellationToken cancellationToken)
    {
        try
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/usr/bin/wg",
                    Arguments = command,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("Comando WireGuard falhou: {Command}, Erro: {Error}", command, error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar comando WireGuard: {Command}", command);
        }
    }

    private async Task SaveWireGuardConfigAsync(string interfaceName, CancellationToken cancellationToken)
    {
        try
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/usr/bin/wg-quick",
                    Arguments = $"save {interfaceName}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar configuração WireGuard para interface {InterfaceName}", interfaceName);
        }
    }
}

