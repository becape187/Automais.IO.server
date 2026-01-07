namespace Automais.Core.Configuration;

/// <summary>
/// Configurações do WireGuard
/// </summary>
public class WireGuardSettings
{
    /// <summary>
    /// Endpoint padrão do servidor VPN (ex: "automais.io")
    /// </summary>
    public string DefaultServerEndpoint { get; set; } = "automais.io";
}

