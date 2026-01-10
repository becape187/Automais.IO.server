using Automais.Core.DTOs;
using Automais.Core.Entities;
using Automais.Core.Interfaces;
using System.Text.Json;

namespace Automais.Core.Services;

/// <summary>
/// Serviço de lógica de negócio para Routers
/// </summary>
public class RouterService : IRouterService
{
    private readonly IRouterRepository _routerRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IRouterOsClient _routerOsClient;
    private readonly IRouterAllowedNetworkRepository? _allowedNetworkRepository;

    public RouterService(
        IRouterRepository routerRepository,
        ITenantRepository tenantRepository,
        IRouterOsClient routerOsClient,
        IRouterAllowedNetworkRepository? allowedNetworkRepository = null)
    {
        _routerRepository = routerRepository;
        _tenantRepository = tenantRepository;
        _routerOsClient = routerOsClient;
        _allowedNetworkRepository = allowedNetworkRepository;
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
        
        // Se tem VpnNetworkId, provisionar WireGuard automaticamente (allowedNetworks é opcional)
        // NOTA: Provisionamento de VPN agora é feito via RouterWireGuardService
        // que chama o serviço Python. Não fazer aqui automaticamente.
        // O provisionamento deve ser feito explicitamente via endpoint de criação de peer.
        // Isso permite mais controle e evita falhas silenciosas.
        
        return await MapToDtoAsync(created, cancellationToken);
    }

    public async Task<RouterDto> UpdateAsync(Guid id, UpdateRouterDto dto, CancellationToken cancellationToken = default)
    {
        var router = await _routerRepository.GetByIdAsync(id, cancellationToken);
        if (router == null)
        {
            throw new KeyNotFoundException($"Router com ID {id} não encontrado.");
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
        }

        if (dto.Description != null)
        {
            router.Description = dto.Description;
        }

        router.UpdatedAt = DateTime.UtcNow;

        var updated = await _routerRepository.UpdateAsync(router, cancellationToken);
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
        var router = await _routerRepository.GetByIdAsync(id, cancellationToken);
        if (router == null)
        {
            throw new KeyNotFoundException($"Router com ID {id} não encontrado.");
        }

        if (string.IsNullOrWhiteSpace(router.RouterOsApiUrl) ||
            string.IsNullOrWhiteSpace(router.RouterOsApiUsername) ||
            string.IsNullOrWhiteSpace(router.RouterOsApiPassword))
        {
            throw new InvalidOperationException("Credenciais da API RouterOS não configuradas.");
        }

        // Timeout adicional de 15 segundos para não travar a API
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        
        var isConnected = await _routerOsClient.TestConnectionAsync(
            router.RouterOsApiUrl,
            router.RouterOsApiUsername,
            router.RouterOsApiPassword,
            linkedCts.Token);

        router.Status = isConnected ? RouterStatus.Online : RouterStatus.Offline;
        router.LastSeenAt = isConnected ? DateTime.UtcNow : router.LastSeenAt;
        router.UpdatedAt = DateTime.UtcNow;

        // Se conectou, buscar informações do sistema (Model, SerialNumber, FirmwareVersion)
        if (isConnected)
        {
            try
            {
                // Timeout adicional de 15 segundos para não travar a API
                using var timeoutCts2 = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                using var linkedCts2 = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts2.Token);
                
                var systemInfo = await _routerOsClient.GetSystemInfoAsync(
                    router.RouterOsApiUrl,
                    router.RouterOsApiUsername,
                    router.RouterOsApiPassword,
                    linkedCts2.Token);

                // Atualizar apenas se não estiverem preenchidos ou se vierem novos dados
                if (!string.IsNullOrWhiteSpace(systemInfo.Model))
                {
                    router.Model = systemInfo.Model;
                }
                if (!string.IsNullOrWhiteSpace(systemInfo.SerialNumber))
                {
                    router.SerialNumber = systemInfo.SerialNumber;
                }
                if (!string.IsNullOrWhiteSpace(systemInfo.FirmwareVersion))
                {
                    router.FirmwareVersion = systemInfo.FirmwareVersion;
                }
            }
            catch (Exception ex)
            {
                // Logar erro mas não falhar o teste de conexão
                // TODO: Adicionar logging quando tiver ILogger
                // Por enquanto, apenas continua sem atualizar essas informações
            }

            // Se conectou e ainda não criou o usuário automais-io-api, criar agora
            if (!router.AutomaisApiUserCreated)
            {
                await CreateAutomaisApiUserAsync(router, cancellationToken);
            }
        }

        var updated = await _routerRepository.UpdateAsync(router, cancellationToken);
        return await MapToDtoAsync(updated, cancellationToken);
    }

    private async Task CreateAutomaisApiUserAsync(Router router, CancellationToken cancellationToken)
    {
        try
        {
            // Gerar senha forte
            var password = GenerateStrongPassword();
            const string username = "automais-io-api";

            // Criar usuário no RouterOS
            await _routerOsClient.CreateUserAsync(
                router.RouterOsApiUrl!,
                router.RouterOsApiUsername!,
                router.RouterOsApiPassword!,
                username,
                password,
                cancellationToken);

            // Atualizar router com credenciais do automais-io-api
            router.RouterOsApiUsername = username;
            router.RouterOsApiPassword = password; // Atualizar também o RouterOsApiPassword
            router.AutomaisApiPassword = password; // Texto plano inicialmente
            router.AutomaisApiUserCreated = true;
            router.UpdatedAt = DateTime.UtcNow;

            // TODO: Logar criação do usuário
        }
        catch (Exception ex)
        {
            // Logar erro mas não falhar o teste de conexão
            // TODO: Adicionar logging
            throw new InvalidOperationException(
                $"Erro ao criar usuário automais-io-api no router: {ex.Message}", ex);
        }
    }

    private static string GenerateStrongPassword()
    {
        // Gera senha forte de 32 caracteres
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 32)
            .Select(s => s[random.Next(s.Length)]).ToArray());
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
            VpnNetworkId = router.VpnNetworkId,
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
        var router = await _routerRepository.GetByIdAsync(id, cancellationToken);
        if (router == null)
        {
            throw new KeyNotFoundException($"Router com ID {id} não encontrado");
        }

        // Verificar se tem credenciais
        if (string.IsNullOrWhiteSpace(router.RouterOsApiUrl) ||
            string.IsNullOrWhiteSpace(router.RouterOsApiUsername) ||
            string.IsNullOrWhiteSpace(router.RouterOsApiPassword))
        {
            throw new InvalidOperationException("Credenciais da API RouterOS não configuradas");
        }

        // Buscar informações do sistema
        var systemInfo = await _routerOsClient.GetSystemInfoAsync(
            router.RouterOsApiUrl,
            router.RouterOsApiUsername,
            router.RouterOsApiPassword,
            cancellationToken);

        // Atualizar informações (sempre atualizar, mesmo se já existirem)
        router.Model = systemInfo.Model;
        router.SerialNumber = systemInfo.SerialNumber;
        router.FirmwareVersion = systemInfo.FirmwareVersion;
        
        // Atualizar HardwareInfo com informações adicionais (JSON)
        var hardwareInfo = new
        {
            cpuLoad = systemInfo.CpuLoad,
            memoryUsage = systemInfo.MemoryUsage,
            uptime = systemInfo.Uptime,
            temperature = systemInfo.Temperature,
            lastUpdated = DateTime.UtcNow
        };
        router.HardwareInfo = JsonSerializer.Serialize(hardwareInfo);
        
        router.UpdatedAt = DateTime.UtcNow;

        var updated = await _routerRepository.UpdateAsync(router, cancellationToken);
        return await MapToDtoAsync(updated, cancellationToken);
    }
}

