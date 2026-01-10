using Automais.Core.Configuration;
using Automais.Core.DTOs;
using Automais.Core.Entities;
using Automais.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Automais.Core.Services;

/// <summary>
/// Serviço para gerenciamento de WireGuard dos Routers
/// Cria peers diretamente no banco de dados. O serviço Python (vpnserver.io) sincroniza
/// automaticamente a cada minuto e adiciona os peers às interfaces WireGuard.
/// </summary>
public class RouterWireGuardService : IRouterWireGuardService
{
    private readonly IRouterWireGuardPeerRepository _peerRepository;
    private readonly IRouterRepository _routerRepository;
    private readonly IVpnNetworkRepository _vpnNetworkRepository;
    private readonly IVpnServiceClient _vpnServiceClient;
    private readonly WireGuardSettings _wireGuardSettings;
    private readonly ILogger<RouterWireGuardService>? _logger;

    public RouterWireGuardService(
        IRouterWireGuardPeerRepository peerRepository,
        IRouterRepository routerRepository,
        IVpnNetworkRepository vpnNetworkRepository,
        IOptions<WireGuardSettings> wireGuardSettings,
        IVpnServiceClient vpnServiceClient,
        ILogger<RouterWireGuardService>? logger = null)
    {
        _peerRepository = peerRepository;
        _routerRepository = routerRepository;
        _vpnNetworkRepository = vpnNetworkRepository;
        _wireGuardSettings = wireGuardSettings.Value;
        _vpnServiceClient = vpnServiceClient;
        _logger = logger;
    }

    public async Task<IEnumerable<RouterWireGuardPeerDto>> GetByRouterIdAsync(Guid routerId, CancellationToken cancellationToken = default)
    {
        var peers = await _peerRepository.GetByRouterIdAsync(routerId, cancellationToken);
        return peers.Select(MapToDto);
    }

    public async Task<RouterWireGuardPeerDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var peer = await _peerRepository.GetByIdAsync(id, cancellationToken);
        return peer == null ? null : MapToDto(peer);
    }

    public async Task<RouterWireGuardPeerDto> CreatePeerAsync(Guid routerId, CreateRouterWireGuardPeerDto dto, CancellationToken cancellationToken = default)
    {
        var router = await _routerRepository.GetByIdAsync(routerId, cancellationToken);
        if (router == null)
        {
            throw new KeyNotFoundException($"Router com ID {routerId} não encontrado.");
        }

        var vpnNetwork = await _vpnNetworkRepository.GetByIdAsync(dto.VpnNetworkId, cancellationToken);
        if (vpnNetwork == null)
        {
            throw new KeyNotFoundException($"Rede VPN com ID {dto.VpnNetworkId} não encontrada.");
        }

        // Verificar se já existe peer para este router e network
        var existing = await _peerRepository.GetByRouterIdAndNetworkIdAsync(routerId, dto.VpnNetworkId, cancellationToken);
        if (existing != null)
        {
            throw new InvalidOperationException($"Já existe um peer WireGuard para este router e rede VPN.");
        }

        _logger?.LogInformation("Criando peer WireGuard no banco de dados: Router={RouterId}, VPN={VpnNetworkId}", 
            routerId, dto.VpnNetworkId);

        // Gerar chaves WireGuard localmente
        var (publicKey, privateKey) = await GenerateWireGuardKeysAsync(cancellationToken);

        // Alocar IP (manual ou automático)
        string routerIp;
        var allowedNetworks = new List<string>();
        
        if (!string.IsNullOrWhiteSpace(dto.AllowedIps))
        {
            // Se AllowedIps foi fornecido, pode conter múltiplas redes separadas por vírgula
            // O primeiro elemento é o IP do router (manual), os demais são redes permitidas
            var networks = dto.AllowedIps.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            
            if (networks.Length > 0)
            {
                // Primeiro elemento é o IP do router (manual)
                routerIp = networks[0];
                
                // Validar formato do IP manual
                if (!IsValidIpWithPrefix(routerIp))
                {
                    throw new InvalidOperationException($"IP manual inválido: {routerIp}. Use o formato IP/PREFIX (ex: 10.100.1.50/32)");
                }
                
                // Verificar se IP está na rede VPN
                if (!IsIpInNetwork(routerIp, vpnNetwork.Cidr))
                {
                    throw new InvalidOperationException($"IP {routerIp} não está na rede VPN {vpnNetwork.Cidr}");
                }
                
                // Demais elementos são redes permitidas
                if (networks.Length > 1)
                {
                    allowedNetworks.AddRange(networks.Skip(1));
                }
            }
            else
            {
                // Alocar IP automaticamente
                routerIp = await AllocateNextAvailableIpAsync(vpnNetwork, cancellationToken);
            }
        }
        else
        {
            // Alocar IP automaticamente
            routerIp = await AllocateNextAvailableIpAsync(vpnNetwork, cancellationToken);
        }

        // Construir AllowedIps completo (IP do router + redes permitidas)
        var allowedIpsParts = new List<string> { routerIp };
        allowedIpsParts.AddRange(allowedNetworks);
        var allowedIps = string.Join(",", allowedIpsParts);

        // Criar peer no banco de dados
        // O serviço Python sincroniza automaticamente a cada minuto e adiciona à interface WireGuard
        var peer = new RouterWireGuardPeer
        {
            Id = Guid.NewGuid(),
            RouterId = routerId,
            VpnNetworkId = dto.VpnNetworkId,
            PublicKey = publicKey,
            PrivateKey = privateKey,
            AllowedIps = allowedIps,
            Endpoint = vpnNetwork.ServerEndpoint, // Endpoint vem da VpnNetwork
            ListenPort = dto.ListenPort ?? 51820,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await _peerRepository.CreateAsync(peer, cancellationToken);
        
        _logger?.LogInformation("Peer WireGuard criado no banco: Router={RouterId}, VPN={VpnNetworkId}, IP={RouterIp}", 
            routerId, dto.VpnNetworkId, routerIp);
        
        return MapToDto(created);
    }

    public async Task<RouterWireGuardPeerDto> UpdatePeerAsync(Guid id, CreateRouterWireGuardPeerDto dto, CancellationToken cancellationToken = default)
    {
        var peer = await _peerRepository.GetByIdAsync(id, cancellationToken);
        if (peer == null)
        {
            throw new KeyNotFoundException($"Peer WireGuard com ID {id} não encontrado.");
        }

        peer.AllowedIps = dto.AllowedIps;
        // Endpoint não é atualizado aqui - ele vem da VpnNetwork
        peer.ListenPort = dto.ListenPort;
        peer.UpdatedAt = DateTime.UtcNow;

        var updated = await _peerRepository.UpdateAsync(peer, cancellationToken);
        return MapToDto(updated);
    }

    public async Task UpdatePeerStatsAsync(Guid id, UpdatePeerStatsDto dto, CancellationToken cancellationToken = default)
    {
        var peer = await _peerRepository.GetByIdAsync(id, cancellationToken);
        if (peer == null)
        {
            throw new KeyNotFoundException($"Peer WireGuard com ID {id} não encontrado.");
        }

        // Atualizar apenas estatísticas (não configuração)
        if (dto.LastHandshake.HasValue)
        {
            peer.LastHandshake = dto.LastHandshake.Value;
        }
        if (dto.BytesReceived.HasValue)
        {
            peer.BytesReceived = dto.BytesReceived.Value;
        }
        if (dto.BytesSent.HasValue)
        {
            peer.BytesSent = dto.BytesSent.Value;
        }
        if (dto.PingSuccess.HasValue)
        {
            peer.PingSuccess = dto.PingSuccess.Value;
        }
        if (dto.PingAvgTimeMs.HasValue)
        {
            peer.PingAvgTimeMs = dto.PingAvgTimeMs.Value;
        }
        if (dto.PingPacketLoss.HasValue)
        {
            peer.PingPacketLoss = dto.PingPacketLoss.Value;
        }
        
        peer.UpdatedAt = DateTime.UtcNow;

        await _peerRepository.UpdateAsync(peer, cancellationToken);
    }

    public async Task DeletePeerAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var peer = await _peerRepository.GetByIdAsync(id, cancellationToken);
        if (peer == null)
        {
            return;
        }

        await _peerRepository.DeleteAsync(id, cancellationToken);
    }

    public async Task<RouterWireGuardConfigDto> GetConfigAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var peer = await _peerRepository.GetByIdAsync(id, cancellationToken);
        if (peer == null)
        {
            throw new KeyNotFoundException($"Peer WireGuard com ID {id} não encontrado.");
        }

        // Buscar configuração do serviço Python
        _logger?.LogInformation("Chamando serviço VPN Python para obter config: Router={RouterId}", peer.RouterId);
        
        var config = await _vpnServiceClient.GetConfigAsync(peer.RouterId, cancellationToken);
        
        return config;
    }

    public async Task<RouterWireGuardPeerDto> RegenerateKeysAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var peer = await _peerRepository.GetByIdAsync(id, cancellationToken);
        if (peer == null)
        {
            throw new KeyNotFoundException($"Peer WireGuard com ID {id} não encontrado.");
        }

        // ⚠️ ATENÇÃO: Regenerar chaves requer remover o peer antigo do servidor e adicionar o novo
        // Por enquanto, lançar exceção informando que precisa deletar e recriar
        throw new NotImplementedException(
            "Regeneração de chaves ainda não está implementada. " +
            "Para regenerar chaves, delete o peer e crie novamente via ProvisionRouterAsync. " +
            "Isso garantirá que as chaves sejam geradas corretamente usando 'wg genkey' do sistema Linux.");
    }


    private static RouterWireGuardPeerDto MapToDto(RouterWireGuardPeer peer)
    {
        return new RouterWireGuardPeerDto
        {
            Id = peer.Id,
            RouterId = peer.RouterId,
            VpnNetworkId = peer.VpnNetworkId,
            PublicKey = peer.PublicKey,
            AllowedIps = peer.AllowedIps,
            Endpoint = peer.Endpoint,
            ListenPort = peer.ListenPort,
            LastHandshake = peer.LastHandshake,
            BytesReceived = peer.BytesReceived,
            BytesSent = peer.BytesSent,
            PingSuccess = peer.PingSuccess,
            PingAvgTimeMs = peer.PingAvgTimeMs,
            PingPacketLoss = peer.PingPacketLoss,
            IsEnabled = peer.IsEnabled,
            CreatedAt = peer.CreatedAt,
            UpdatedAt = peer.UpdatedAt
        };
    }

    /// <summary>
    /// Gera chaves WireGuard usando wg genkey e wg pubkey
    /// </summary>
    private async Task<(string publicKey, string privateKey)> GenerateWireGuardKeysAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Gerar chave privada
            var privateKeyProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/usr/bin/wg",
                    Arguments = "genkey",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            privateKeyProcess.Start();
            var privateKey = (await privateKeyProcess.StandardOutput.ReadToEndAsync(cancellationToken)).Trim();
            await privateKeyProcess.WaitForExitAsync(cancellationToken);

            if (privateKeyProcess.ExitCode != 0 || string.IsNullOrEmpty(privateKey))
            {
                throw new InvalidOperationException("Erro ao gerar chave privada WireGuard");
            }

            // Gerar chave pública a partir da privada
            var publicKeyProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/usr/bin/wg",
                    Arguments = "pubkey",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            publicKeyProcess.Start();
            await publicKeyProcess.StandardInput.WriteAsync(privateKey);
            await publicKeyProcess.StandardInput.FlushAsync();
            publicKeyProcess.StandardInput.Close();
            
            var publicKey = (await publicKeyProcess.StandardOutput.ReadToEndAsync(cancellationToken)).Trim();
            await publicKeyProcess.WaitForExitAsync(cancellationToken);

            if (publicKeyProcess.ExitCode != 0 || string.IsNullOrEmpty(publicKey))
            {
                throw new InvalidOperationException("Erro ao gerar chave pública WireGuard");
            }

            return (publicKey, privateKey);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Erro ao gerar chaves WireGuard");
            throw new InvalidOperationException($"Erro ao gerar chaves WireGuard: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Aloca o próximo IP disponível na rede VPN
    /// </summary>
    private async Task<string> AllocateNextAvailableIpAsync(VpnNetwork vpnNetwork, CancellationToken cancellationToken)
    {
        // Parsear CIDR da rede VPN (ex: "10.100.1.0/24")
        var cidrParts = vpnNetwork.Cidr.Split('/');
        if (cidrParts.Length != 2)
        {
            throw new InvalidOperationException($"CIDR inválido: {vpnNetwork.Cidr}");
        }

        if (!IPAddress.TryParse(cidrParts[0], out var networkIp))
        {
            throw new InvalidOperationException($"IP de rede inválido: {cidrParts[0]}");
        }

        if (!int.TryParse(cidrParts[1], out var prefixLength) || prefixLength < 0 || prefixLength > 32)
        {
            throw new InvalidOperationException($"Prefix length inválido: {cidrParts[1]}");
        }

        // Buscar IPs já alocados nesta rede VPN
        var allocatedIps = await _peerRepository.GetAllocatedIpsByNetworkAsync(vpnNetwork.Id, cancellationToken);
        var allocatedIpSet = new HashSet<string>();
        
        foreach (var allocatedIp in allocatedIps)
        {
            // Extrair apenas o IP (sem o prefix) do AllowedIps
            // AllowedIps pode ser "10.100.1.50/32" ou "10.100.1.50/32,10.0.0.0/8"
            var firstIp = allocatedIp.Split(',')[0].Trim();
            if (IsValidIpWithPrefix(firstIp))
            {
                var ipOnly = firstIp.Split('/')[0];
                allocatedIpSet.Add(ipOnly);
            }
        }

        // Encontrar próximo IP disponível (começando do .2, pois .1 é reservado para o servidor)
        var networkBytes = networkIp.GetAddressBytes();
        
        // Converter IP de rede para inteiro (big-endian)
        var networkValue = (uint)(networkBytes[0] << 24 | networkBytes[1] << 16 | networkBytes[2] << 8 | networkBytes[3]);
        var hostBits = 32 - prefixLength;
        var maxHosts = (uint)Math.Pow(2, hostBits) - 2; // -2 para excluir .0 e broadcast
        
        // Limitar busca até 254 para evitar problemas com redes muito grandes
        var maxSearch = Math.Min(maxHosts, 254u);
        
        for (uint hostOffset = 2; hostOffset <= maxSearch; hostOffset++)
        {
            var ipValue = networkValue + hostOffset;
            
            // Converter de volta para IPAddress (big-endian)
            var ipBytes = new byte[4];
            ipBytes[0] = (byte)((ipValue >> 24) & 0xFF);
            ipBytes[1] = (byte)((ipValue >> 16) & 0xFF);
            ipBytes[2] = (byte)((ipValue >> 8) & 0xFF);
            ipBytes[3] = (byte)(ipValue & 0xFF);
            
            var candidateIp = new IPAddress(ipBytes).ToString();
            
            if (!allocatedIpSet.Contains(candidateIp))
            {
                return $"{candidateIp}/{prefixLength}";
            }
        }

        throw new InvalidOperationException($"Não há IPs disponíveis na rede VPN {vpnNetwork.Cidr}");
    }

    /// <summary>
    /// Valida se o formato do IP está correto (IP/PREFIX)
    /// </summary>
    private static bool IsValidIpWithPrefix(string ipWithPrefix)
    {
        if (string.IsNullOrWhiteSpace(ipWithPrefix))
            return false;

        var parts = ipWithPrefix.Split('/');
        if (parts.Length != 2)
            return false;

        if (!IPAddress.TryParse(parts[0], out _))
            return false;

        if (!int.TryParse(parts[1], out var prefix) || prefix < 0 || prefix > 32)
            return false;

        return true;
    }

    /// <summary>
    /// Verifica se um IP está dentro de uma rede CIDR
    /// </summary>
    private static bool IsIpInNetwork(string ipWithPrefix, string networkCidr)
    {
        try
        {
            var ipParts = ipWithPrefix.Split('/');
            if (ipParts.Length != 2)
                return false;

            if (!IPAddress.TryParse(ipParts[0], out var ip))
                return false;

            var networkParts = networkCidr.Split('/');
            if (networkParts.Length != 2)
                return false;

            if (!IPAddress.TryParse(networkParts[0], out var networkIp))
                return false;

            if (!int.TryParse(networkParts[1], out var prefixLength))
                return false;

            // Calcular máscara de rede
            var mask = (uint)(0xFFFFFFFF << (32 - prefixLength));
            mask = (uint)IPAddress.HostToNetworkOrder((int)mask);

            var ipBytes = BitConverter.ToUInt32(ip.GetAddressBytes(), 0);
            var networkBytes = BitConverter.ToUInt32(networkIp.GetAddressBytes(), 0);

            return (ipBytes & mask) == (networkBytes & mask);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sanitiza o nome do arquivo removendo caracteres inválidos
    /// </summary>
    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "router";

        // Remover caracteres inválidos para nomes de arquivo (sem usar Path para manter na camada Core)
        var invalidChars = new[] { '"', '<', '>', '|', ':', '*', '?', '\\', '/' };
        var sanitized = new string(fileName
            .Where(c => !invalidChars.Contains(c))
            .ToArray());

        // Substituir espaços por underscores
        sanitized = sanitized.Replace(" ", "_");

        // Remover underscores múltiplos
        while (sanitized.Contains("__"))
        {
            sanitized = sanitized.Replace("__", "_");
        }

        // Remover underscores no início e fim
        sanitized = sanitized.Trim('_');

        // Se ficou vazio após sanitização, usar nome padrão
        if (string.IsNullOrWhiteSpace(sanitized))
            return "router";

        return sanitized;
    }
}

