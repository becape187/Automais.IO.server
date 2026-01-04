using Automais.Core.DTOs;
using Automais.Core.Entities;
using Automais.Core.Interfaces;

namespace Automais.Core.Services;

/// <summary>
/// Serviço de lógica de negócio para Routers
/// </summary>
public class RouterService : IRouterService
{
    private readonly IRouterRepository _routerRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IRouterOsClient _routerOsClient;
    private readonly IWireGuardServerService? _wireGuardServerService;

    public RouterService(
        IRouterRepository routerRepository,
        ITenantRepository tenantRepository,
        IRouterOsClient routerOsClient,
        IWireGuardServerService? wireGuardServerService = null)
    {
        _routerRepository = routerRepository;
        _tenantRepository = tenantRepository;
        _routerOsClient = routerOsClient;
        _wireGuardServerService = wireGuardServerService;
    }

    public async Task<IEnumerable<RouterDto>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Buscar routers diretamente sem verificar tenant primeiro
            // Isso evita JOINs desnecessários que podem causar problemas com snake_case
            var routers = await _routerRepository.GetByTenantIdAsync(tenantId, cancellationToken);
            return routers.Select(MapToDto);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Erro ao buscar routers do tenant {tenantId}: {ex.Message}", ex);
        }
    }

    public async Task<RouterDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var router = await _routerRepository.GetByIdAsync(id, cancellationToken);
        return router == null ? null : MapToDto(router);
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

        if (!string.IsNullOrWhiteSpace(dto.SerialNumber))
        {
            if (await _routerRepository.SerialNumberExistsAsync(dto.SerialNumber, cancellationToken))
            {
                throw new InvalidOperationException($"Serial number '{dto.SerialNumber}' já está em uso.");
            }
        }

        var router = new Router
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = dto.Name,
            SerialNumber = dto.SerialNumber,
            Model = dto.Model,
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
        if (dto.VpnNetworkId.HasValue)
        {
            try
            {
                if (_wireGuardServerService != null)
                {
                    // allowedNetworks pode ser null ou vazio - o peer será criado apenas com o IP do router
                    var allowedNetworks = dto.AllowedNetworks ?? Enumerable.Empty<string>();
                    await _wireGuardServerService.ProvisionRouterAsync(
                        created.Id,
                        dto.VpnNetworkId.Value,
                        allowedNetworks,
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                // Logar erro mas não falhar criação do router
                // O WireGuard pode ser provisionado depois manualmente
                // TODO: Adicionar ILogger ao RouterService para logar este erro
                // Por enquanto, o erro será logado no controller se propagar
            }
        }
        
        return MapToDto(created);
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

        if (dto.SerialNumber != null)
        {
            if (dto.SerialNumber != router.SerialNumber)
            {
                if (await _routerRepository.SerialNumberExistsAsync(dto.SerialNumber, cancellationToken))
                {
                    throw new InvalidOperationException($"Serial number '{dto.SerialNumber}' já está em uso.");
                }
                router.SerialNumber = dto.SerialNumber;
            }
        }

        if (dto.Model != null)
        {
            router.Model = dto.Model;
        }

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
        return MapToDto(updated);
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

        var isConnected = await _routerOsClient.TestConnectionAsync(
            router.RouterOsApiUrl,
            router.RouterOsApiUsername,
            router.RouterOsApiPassword,
            cancellationToken);

        router.Status = isConnected ? RouterStatus.Online : RouterStatus.Offline;
        router.LastSeenAt = isConnected ? DateTime.UtcNow : router.LastSeenAt;
        router.UpdatedAt = DateTime.UtcNow;

        // Se conectou e ainda não criou o usuário automais-io-api, criar agora
        if (isConnected && !router.AutomaisApiUserCreated)
        {
            await CreateAutomaisApiUserAsync(router, cancellationToken);
        }

        var updated = await _routerRepository.UpdateAsync(router, cancellationToken);
        return MapToDto(updated);
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

    private static RouterDto MapToDto(Router router)
    {
        return new RouterDto
        {
            Id = router.Id,
            TenantId = router.TenantId,
            Name = router.Name,
            SerialNumber = router.SerialNumber,
            Model = router.Model,
            FirmwareVersion = router.FirmwareVersion,
            RouterOsApiUrl = router.RouterOsApiUrl,
            VpnNetworkId = router.VpnNetworkId,
            Status = router.Status,
            LastSeenAt = router.LastSeenAt,
            HardwareInfo = router.HardwareInfo,
            Description = router.Description,
            CreatedAt = router.CreatedAt,
            UpdatedAt = router.UpdatedAt
        };
    }
}

