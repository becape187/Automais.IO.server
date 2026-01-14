using Automais.Core.DTOs;
using Automais.Core.Entities;
using Automais.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Automais.Core.Services;

/// <summary>
/// Serviço de lógica de negócio para Routers
/// </summary>
public class RouterService : IRouterService
{
    private readonly IRouterRepository _routerRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IRouterAllowedNetworkRepository? _allowedNetworkRepository;
    private readonly IRouterWireGuardService? _wireGuardService;
    private readonly IVpnNetworkRepository? _vpnNetworkRepository;
    private readonly ILogger<RouterService>? _logger;

    public RouterService(
        IRouterRepository routerRepository,
        ITenantRepository tenantRepository,
        IRouterAllowedNetworkRepository? allowedNetworkRepository = null,
        IRouterWireGuardService? wireGuardService = null,
        IVpnNetworkRepository? vpnNetworkRepository = null,
        ILogger<RouterService>? logger = null)
    {
        _routerRepository = routerRepository;
        _tenantRepository = tenantRepository;
        _allowedNetworkRepository = allowedNetworkRepository;
        _wireGuardService = wireGuardService;
        _vpnNetworkRepository = vpnNetworkRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<RouterDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var routers = await _routerRepository.GetAllAsync(cancellationToken);
            var result = new List<RouterDto>();
            foreach (var router in routers)
            {
                result.Add(await MapToDtoAsync(router, cancellationToken));
            }
            return result;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Erro ao buscar todos os routers: {ex.Message}", ex);
        }
    }

    public async Task<IEnumerable<RouterDto>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Buscar routers diretamente sem verificar tenant primeiro
            // Isso evita JOINs desnecessários que podem causar problemas com snake_case
            var routers = await _routerRepository.GetByTenantIdAsync(tenantId, cancellationToken);
            var result = new List<RouterDto>();
            foreach (var router in routers)
            {
                result.Add(await MapToDtoAsync(router, cancellationToken));
            }
            return result;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Erro ao buscar routers do tenant {tenantId}: {ex.Message}", ex);
        }
    }

    public async Task<RouterDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var router = await _routerRepository.GetByIdAsync(id, cancellationToken);
        if (router == null) return null;
        
        return await MapToDtoAsync(router, cancellationToken);
    }

    public async Task<RouterDto> CreateAsync(Guid tenantId, CreateRouterDto dto, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant == null)
        {
            throw new KeyNotFoundException($"Tenant com ID {tenantId} não encontrado.");
        }

        // Validar VpnNetworkId se fornecido
        if (dto.VpnNetworkId.HasValue)
        {
            // TODO: Adicionar IVpnNetworkRepository para validar se a rede VPN existe
            // Por enquanto, a validação será feita pelo Entity Framework (foreign key constraint)
        }

        var router = new Router
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = dto.Name,
            // SerialNumber, Model e FirmwareVersion serão preenchidos automaticamente via API RouterOS
            SerialNumber = null,
            Model = null,
            FirmwareVersion = null,
            RouterOsApiUrl = dto.RouterOsApiUrl,
            RouterOsApiUsername = dto.RouterOsApiUsername,
            RouterOsApiPassword = dto.RouterOsApiPassword, // TODO: Criptografar senha
            VpnNetworkId = dto.VpnNetworkId,
            Description = dto.Description,
            Status = RouterStatus.Offline,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await _routerRepository.CreateAsync(router, cancellationToken);
        
        // Se tem VpnNetworkId, provisionar WireGuard automaticamente
        if (created.VpnNetworkId.HasValue && _wireGuardService != null)
        {
            try
            {
                // Construir AllowedIps: primeiro o IP do router (ou vazio para alocação automática),
                // depois as redes permitidas separadas por vírgula
                // O formato esperado é: "IP/PREFIX,rede1,rede2,..." ou apenas "rede1,rede2,..." (para alocação automática)
                var allowedIpsParts = new List<string>();
                
                // Se VpnIp foi fornecido, usar ele como primeiro elemento; caso contrário, deixar vazio para alocação automática
                if (!string.IsNullOrWhiteSpace(dto.VpnIp))
                {
                    allowedIpsParts.Add(dto.VpnIp);
                }
                
                // Adicionar redes permitidas se fornecidas
                if (dto.AllowedNetworks != null)
                {
                    allowedIpsParts.AddRange(dto.AllowedNetworks);
                }
                
                // Se não há IP manual nem redes permitidas, deixar vazio para alocação automática
                // Caso contrário, juntar tudo com vírgula
                var allowedIps = allowedIpsParts.Count > 0 ? string.Join(",", allowedIpsParts) : string.Empty;
                
                // Criar peer automaticamente
                var peerDto = new CreateRouterWireGuardPeerDto
                {
                    VpnNetworkId = created.VpnNetworkId.Value,
                    AllowedIps = allowedIps,
                    ListenPort = 51820 // Porta padrão do WireGuard
                };
                
                await _wireGuardService.CreatePeerAsync(created.Id, peerDto, cancellationToken);
            }
            catch (Exception ex)
            {
                // Logar erro mas não falhar a criação do router
                // O peer pode ser criado manualmente depois se necessário
                // TODO: Adicionar logging quando tiver ILogger
                // Por enquanto, apenas continua sem criar o peer
            }
        }
        
        return await MapToDtoAsync(created, cancellationToken);
    }

    public async Task<RouterDto> UpdateAsync(Guid id, UpdateRouterDto dto, CancellationToken cancellationToken = default)
    {
        var router = await _routerRepository.GetByIdAsync(id, cancellationToken);
        if (router == null)
        {
            throw new KeyNotFoundException($"Router com ID {id} não encontrado.");
        }

        // Log dos dados recebidos para debug
        _logger?.LogInformation($"📥 [SERVICE] Iniciando atualização do router {id}");
        _logger?.LogInformation($"   Status: {dto.Status}");
        _logger?.LogInformation($"   LastSeenAt: {dto.LastSeenAt}");
        _logger?.LogInformation($"   Latency: {dto.Latency}");
        _logger?.LogInformation($"   HardwareInfo: {(dto.HardwareInfo != null ? $"presente ({dto.HardwareInfo.Length} chars)" : "null")}");
        _logger?.LogInformation($"   FirmwareVersion: {dto.FirmwareVersion}");
        _logger?.LogInformation($"   Model: {dto.Model}");
        
        // Log do JSON completo do DTO
        try
        {
            var jsonDto = JsonSerializer.Serialize(dto, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            _logger?.LogInformation($"   📋 DTO completo recebido (JSON):\n{jsonDto}");
        }
        catch (Exception jsonEx)
        {
            _logger?.LogWarning(jsonEx, "   ⚠️ Erro ao serializar DTO para log");
        }

        if (!string.IsNullOrWhiteSpace(dto.Name))
        {
            router.Name = dto.Name;
        }

        // SerialNumber, Model e FirmwareVersion não podem ser editados manualmente
        // Eles são atualizados automaticamente via API RouterOS quando conecta

        if (dto.RouterOsApiUrl != null)
        {
            router.RouterOsApiUrl = dto.RouterOsApiUrl;
        }

        if (dto.RouterOsApiUsername != null)
        {
            router.RouterOsApiUsername = dto.RouterOsApiUsername;
        }

        if (dto.RouterOsApiPassword != null)
        {
            router.RouterOsApiPassword = dto.RouterOsApiPassword; // TODO: Criptografar senha
        }

        if (dto.VpnNetworkId.HasValue)
        {
            router.VpnNetworkId = dto.VpnNetworkId.Value;
        }

        if (dto.Status.HasValue)
        {
            router.Status = dto.Status.Value;
            _logger?.LogDebug($"✅ Status atualizado para {router.Status}");
        }

        if (dto.LastSeenAt.HasValue)
        {
            router.LastSeenAt = dto.LastSeenAt.Value;
            _logger?.LogDebug($"✅ LastSeenAt atualizado para {router.LastSeenAt}");
        }

        if (dto.Latency.HasValue)
        {
            router.Latency = dto.Latency.Value;
            _logger?.LogDebug($"✅ Latency atualizado para {router.Latency}");
        }

        if (dto.HardwareInfo != null)
        {
            router.HardwareInfo = dto.HardwareInfo;
            _logger?.LogDebug($"✅ HardwareInfo atualizado (tamanho: {dto.HardwareInfo.Length} chars)");
        }

        if (dto.FirmwareVersion != null)
        {
            router.FirmwareVersion = dto.FirmwareVersion;
            _logger?.LogDebug($"✅ FirmwareVersion atualizado para {router.FirmwareVersion}");
        }

        if (dto.Model != null)
        {
            router.Model = dto.Model;
            _logger?.LogDebug($"✅ Model atualizado para {router.Model}");
        }

        if (dto.Description != null)
        {
            router.Description = dto.Description;
        }

        router.UpdatedAt = DateTime.UtcNow;

        // Log do estado final antes de salvar
        _logger?.LogInformation($"💾 [SERVICE] Salvando router {id} no banco:");
        _logger?.LogInformation($"   Status final: {router.Status}");
        _logger?.LogInformation($"   LastSeenAt final: {router.LastSeenAt}");
        _logger?.LogInformation($"   Latency final: {router.Latency}");
        _logger?.LogInformation($"   HardwareInfo final: {(router.HardwareInfo != null ? $"presente ({router.HardwareInfo.Length} chars)" : "null")}");
        _logger?.LogInformation($"   FirmwareVersion final: {router.FirmwareVersion}");
        _logger?.LogInformation($"   Model final: {router.Model}");

        var updated = await _routerRepository.UpdateAsync(router, cancellationToken);
        _logger?.LogInformation($"✅ [SERVICE] Router {id} salvo no banco com sucesso");
        return await MapToDtoAsync(updated, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var router = await _routerRepository.GetByIdAsync(id, cancellationToken);
        if (router == null)
        {
            return;
        }

        await _routerRepository.DeleteAsync(id, cancellationToken);
    }

    public async Task<RouterDto> TestConnectionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Teste de conexão agora é feito via servidor VPN (WebSocket)
        // Este método é mantido para compatibilidade, mas não faz nada
        var router = await _routerRepository.GetByIdAsync(id, cancellationToken);
        if (router == null)
        {
            throw new KeyNotFoundException($"Router com ID {id} não encontrado.");
        }

        throw new NotImplementedException("TestConnectionAsync agora é feito via servidor VPN (WebSocket). Use o endpoint do servidor VPN.");
    }

    private async Task<RouterDto> MapToDtoAsync(Router router, CancellationToken cancellationToken = default)
    {
        // Buscar redes permitidas se houver repositório disponível
        IEnumerable<string>? allowedNetworks = null;
        if (_allowedNetworkRepository != null)
        {
            try
            {
                var networks = await _allowedNetworkRepository.GetByRouterIdAsync(router.Id, cancellationToken);
                allowedNetworks = networks.Select(n => n.NetworkCidr).ToList();
            }
            catch
            {
                // Se falhar, deixa como null
                allowedNetworks = null;
            }
        }

        // Buscar ServerEndpoint da VpnNetwork se houver repositório disponível
        string? vpnNetworkServerEndpoint = null;
        if (router.VpnNetworkId.HasValue && _vpnNetworkRepository != null)
        {
            try
            {
                var vpnNetwork = await _vpnNetworkRepository.GetByIdAsync(router.VpnNetworkId.Value, cancellationToken);
                vpnNetworkServerEndpoint = vpnNetwork?.ServerEndpoint;
            }
            catch
            {
                // Se falhar, deixa como null
                vpnNetworkServerEndpoint = null;
            }
        }

        return new RouterDto
        {
            Id = router.Id,
            TenantId = router.TenantId,
            Name = router.Name,
            SerialNumber = router.SerialNumber,
            Model = router.Model,
            FirmwareVersion = router.FirmwareVersion,
            RouterOsApiUrl = router.RouterOsApiUrl,
            RouterOsApiUsername = router.RouterOsApiUsername,
            RouterOsApiPassword = router.RouterOsApiPassword, // Incluir para o serviço Python usar quando AutomaisApiPassword for null
            AutomaisApiPassword = router.AutomaisApiPassword, // Incluir para o serviço Python verificar
            VpnNetworkId = router.VpnNetworkId,
            VpnNetworkServerEndpoint = vpnNetworkServerEndpoint,
            Status = router.Status,
            LastSeenAt = router.LastSeenAt,
            Latency = router.Latency,
            HardwareInfo = router.HardwareInfo,
            Description = router.Description,
            CreatedAt = router.CreatedAt,
            UpdatedAt = router.UpdatedAt,
            AllowedNetworks = allowedNetworks
        };
    }

    public async Task<RouterDto> UpdateSystemInfoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Atualização de informações do sistema agora é feita via servidor VPN (WebSocket)
        // Este método é mantido para compatibilidade, mas não faz nada
        var router = await _routerRepository.GetByIdAsync(id, cancellationToken);
        if (router == null)
        {
            throw new KeyNotFoundException($"Router com ID {id} não encontrado");
        }

        throw new NotImplementedException("UpdateSystemInfoAsync agora é feito via servidor VPN (WebSocket). Use o endpoint do servidor VPN.");
    }

    public async Task UpdatePasswordAsync(Guid id, string newPassword, CancellationToken cancellationToken = default)
    {
        var router = await _routerRepository.GetByIdAsync(id, cancellationToken);
        if (router == null)
        {
            throw new KeyNotFoundException($"Router com ID {id} não encontrado");
        }

        // Lógica: RouterOsApiPassword -> NULL, AutomaisApiPassword -> nova senha
        // Isso indica que a senha foi alterada e agora usamos AutomaisApiPassword
        router.RouterOsApiPassword = null; // Limpar senha original
        router.AutomaisApiPassword = newPassword; // TODO: Criptografar senha
        router.UpdatedAt = DateTime.UtcNow;

        await _routerRepository.UpdateAsync(router, cancellationToken);
        
        // TODO: Adicionar logging quando tiver ILogger
        // Senha do router atualizada: RouterOsApiPassword=NULL, AutomaisApiPassword=nova senha
    }
}

