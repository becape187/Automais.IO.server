using Automais.Core.DTOs;

namespace Automais.Core.Interfaces;

/// <summary>
/// Serviço para gerenciamento do servidor WireGuard no Linux
/// Gerencia peers, alocação de IPs e configurações
/// </summary>
public interface IWireGuardServerService
{
    /// <summary>
    /// Provisiona um router na VPN WireGuard
    /// Gera chaves, aloca IP, adiciona peer no servidor e configura redes permitidas
    /// </summary>
    Task<RouterWireGuardPeerDto> ProvisionRouterAsync(
        Guid routerId,
        Guid vpnNetworkId,
        IEnumerable<string> allowedNetworks,
        string? manualIp = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Adiciona uma rede permitida ao router (adiciona ao allowed-ips)
    /// </summary>
    Task AddNetworkToRouterAsync(
        Guid routerId,
        string networkCidr,
        string? description = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Remove uma rede permitida do router
    /// </summary>
    Task RemoveNetworkFromRouterAsync(
        Guid routerId,
        string networkCidr,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Recarrega a configuração do peer no WireGuard server
    /// </summary>
    Task ReloadPeerConfigAsync(
        Guid peerId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gera e salva o arquivo de configuração .conf no banco
    /// </summary>
    Task<RouterWireGuardConfigDto> GenerateAndSaveConfigAsync(
        Guid routerId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Obtém a configuração salva no banco (ou gera se não existir)
    /// </summary>
    Task<RouterWireGuardConfigDto> GetConfigAsync(
        Guid routerId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Aloca um IP disponível na rede VPN
    /// Se manualIp for especificado, valida e reserva esse IP.
    /// Caso contrário, encontra o próximo IP disponível (começando do .2, pois .1 é reservado para servidor)
    /// </summary>
    Task<string> AllocateVpnIpAsync(
        Guid vpnNetworkId,
        string? manualIp = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Garante que a interface WireGuard existe para uma VpnNetwork
    /// Cria o arquivo de configuração se necessário
    /// </summary>
    Task EnsureInterfaceForVpnNetworkAsync(
        Guid vpnNetworkId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Remove a interface WireGuard de uma VpnNetwork
    /// Faz wg-quick down e remove o arquivo de configuração
    /// </summary>
    Task RemoveInterfaceForVpnNetworkAsync(
        Guid vpnNetworkId,
        CancellationToken cancellationToken = default);
}

