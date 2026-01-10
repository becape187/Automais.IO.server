namespace Automais.Core.Entities;

/// <summary>
/// Representa um servidor VPN (WireGuard) físico ou virtual
/// Permite ter múltiplos servidores VPN e distribuir peers entre eles
/// </summary>
public class VpnServer
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Nome descritivo do servidor (ex: "Servidor VPN EUA", "Servidor VPN Brasil")
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Identificador único do servidor VPN usado para auto-descoberta.
    /// Deve corresponder ao valor da variável de ambiente VPN_SERVER_NAME na instância do serviço Python.
    /// Ex: "vpn-server-usa", "vpn-server-brasil", "vpn-server-europe"
    /// </summary>
    public string ServerName { get; set; } = string.Empty;
    
    /// <summary>
    /// Hostname ou IP do servidor VPN
    /// </summary>
    public string Host { get; set; } = string.Empty;
    
    /// <summary>
    /// Porta SSH para acesso ao servidor (padrão: 22)
    /// </summary>
    public int SshPort { get; set; } = 22;
    
    /// <summary>
    /// Usuário SSH para acesso ao servidor
    /// </summary>
    public string? SshUsername { get; set; }
    
    /// <summary>
    /// Senha SSH (criptografada no banco)
    /// </summary>
    public string? SshPassword { get; set; }
    
    /// <summary>
    /// Caminho para chave SSH privada (opcional, alternativa à senha)
    /// </summary>
    public string? SshKeyPath { get; set; }
    
    /// <summary>
    /// Indica se o servidor está ativo e disponível
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Descrição opcional do servidor
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Região/localização do servidor (ex: "us-east", "br-south")
    /// </summary>
    public string? Region { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation Properties
    public ICollection<VpnNetwork> VpnNetworks { get; set; } = new List<VpnNetwork>();
}

