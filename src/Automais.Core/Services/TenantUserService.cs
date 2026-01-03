using Automais.Core.DTOs;
using Automais.Core.Entities;
using Automais.Core.Interfaces;

namespace Automais.Core.Services;

public class TenantUserService : ITenantUserService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantUserRepository _userRepository;
    private readonly IVpnNetworkRepository _vpnNetworkRepository;

    public TenantUserService(
        ITenantRepository tenantRepository,
        ITenantUserRepository userRepository,
        IVpnNetworkRepository vpnNetworkRepository)
    {
        _tenantRepository = tenantRepository;
        _userRepository = userRepository;
        _vpnNetworkRepository = vpnNetworkRepository;
    }

    public async Task<IEnumerable<TenantUserDto>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        // Verificar se o tenant existe
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant == null)
        {
            // Retorna lista vazia ao invés de lançar exceção se o tenant não existir
            return Enumerable.Empty<TenantUserDto>();
        }

        try
        {
            var users = await _userRepository.GetByTenantIdAsync(tenantId, cancellationToken);
            var result = new List<TenantUserDto>();

            foreach (var user in users)
            {
                try
                {
                    result.Add(await BuildDtoAsync(user, cancellationToken));
                }
                catch (Exception ex)
                {
                    // Log erro ao construir DTO de um usuário específico, mas continua com os outros
                    // Em produção, considere usar ILogger aqui
                    System.Diagnostics.Debug.WriteLine($"Erro ao construir DTO para usuário {user.Id}: {ex.Message}");
                    // Adiciona um DTO básico sem networks em caso de erro
                    result.Add(MapToDto(user, Enumerable.Empty<VpnNetwork>()));
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Erro ao buscar usuários do tenant {tenantId}: {ex.Message}", ex);
        }
    }

    public async Task<TenantUserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        return user == null ? null : await BuildDtoAsync(user, cancellationToken);
    }

    public async Task<TenantUserDto> CreateAsync(Guid tenantId, CreateTenantUserDto dto, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant == null)
        {
            throw new KeyNotFoundException($"Tenant com ID {tenantId} não encontrado.");
        }

        if (await _userRepository.EmailExistsAsync(tenantId, dto.Email, cancellationToken))
        {
            throw new InvalidOperationException($"E-mail '{dto.Email}' já está em uso para este tenant.");
        }

        var user = new TenantUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = dto.Name,
            Email = dto.Email,
            Role = dto.Role,
            Status = TenantUserStatus.Invited,
            VpnEnabled = dto.VpnEnabled,
            VpnDeviceName = dto.VpnDeviceName,
            VpnPublicKey = dto.VpnPublicKey,
            VpnIpAddress = dto.VpnIpAddress,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await _userRepository.CreateAsync(user, cancellationToken);

        if (dto.NetworkIds.Any())
        {
            await SetUserNetworksAsync(created, dto.NetworkIds, cancellationToken);
        }

        return await BuildDtoAsync(created, cancellationToken);
    }

    public async Task<TenantUserDto> UpdateAsync(Guid id, UpdateTenantUserDto dto, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user == null)
        {
            throw new KeyNotFoundException($"Usuário com ID {id} não encontrado.");
        }

        if (!string.IsNullOrWhiteSpace(dto.Name))
        {
            user.Name = dto.Name;
        }

        if (dto.Role.HasValue)
        {
            user.Role = dto.Role.Value;
        }

        if (dto.Status.HasValue)
        {
            user.Status = dto.Status.Value;
        }

        if (dto.VpnEnabled.HasValue)
        {
            user.VpnEnabled = dto.VpnEnabled.Value;
        }

        if (dto.VpnDeviceName != null)
        {
            user.VpnDeviceName = dto.VpnDeviceName;
        }

        if (dto.VpnPublicKey != null)
        {
            user.VpnPublicKey = dto.VpnPublicKey;
        }

        if (dto.VpnIpAddress != null)
        {
            user.VpnIpAddress = dto.VpnIpAddress;
        }

        user.UpdatedAt = DateTime.UtcNow;

        var updated = await _userRepository.UpdateAsync(user, cancellationToken);
        return await BuildDtoAsync(updated, cancellationToken);
    }

    public async Task<TenantUserDto> UpdateNetworksAsync(Guid id, UpdateUserNetworksDto dto, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user == null)
        {
            throw new KeyNotFoundException($"Usuário com ID {id} não encontrado.");
        }

        await SetUserNetworksAsync(user, dto.NetworkIds, cancellationToken);
        return await BuildDtoAsync(user, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user == null)
        {
            return;
        }

        await _vpnNetworkRepository.RemoveMembershipsByUserIdAsync(id, cancellationToken);
        await _userRepository.DeleteAsync(id, cancellationToken);
    }

    private async Task<TenantUserDto> BuildDtoAsync(TenantUser user, CancellationToken cancellationToken)
    {
        try
        {
            var memberships = await _vpnNetworkRepository.GetMembershipsByUserIdAsync(user.Id, cancellationToken);
            var networkIds = memberships.Select(m => m.VpnNetworkId).Distinct().ToList();
            var networks = networkIds.Count > 0
                ? await _vpnNetworkRepository.GetByIdsAsync(networkIds, cancellationToken)
                : Enumerable.Empty<VpnNetwork>();

            return MapToDto(user, networks);
        }
        catch (Exception ex)
        {
            // Em caso de erro ao buscar networks, retorna DTO sem networks
            System.Diagnostics.Debug.WriteLine($"Erro ao buscar networks para usuário {user.Id}: {ex.Message}");
            return MapToDto(user, Enumerable.Empty<VpnNetwork>());
        }
    }

    private async Task SetUserNetworksAsync(TenantUser user, IEnumerable<Guid> networkIds, CancellationToken cancellationToken)
    {
        var ids = networkIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            await _vpnNetworkRepository.ReplaceUserMembershipsAsync(user.TenantId, user.Id, Array.Empty<Guid>(), cancellationToken);
            return;
        }

        var networks = await _vpnNetworkRepository.GetByIdsAsync(ids, cancellationToken);
        var missing = ids.Except(networks.Select(n => n.Id)).ToList();
        if (missing.Any())
        {
            throw new KeyNotFoundException($"Rede(s) VPN não encontrada(s): {string.Join(", ", missing)}");
        }

        if (networks.Any(n => n.TenantId != user.TenantId))
        {
            throw new InvalidOperationException("Usuário não pode ser associado a redes de outro tenant.");
        }

        await _vpnNetworkRepository.ReplaceUserMembershipsAsync(user.TenantId, user.Id, ids, cancellationToken);
    }

    private static TenantUserDto MapToDto(TenantUser user, IEnumerable<VpnNetwork> networks)
    {
        return new TenantUserDto
        {
            Id = user.Id,
            TenantId = user.TenantId,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role,
            Status = user.Status,
            LastLoginAt = user.LastLoginAt,
            VpnEnabled = user.VpnEnabled,
            VpnDeviceName = user.VpnDeviceName,
            VpnPublicKey = user.VpnPublicKey,
            VpnIpAddress = user.VpnIpAddress,
            Networks = networks.Select(n => new VpnNetworkSummaryDto
            {
                NetworkId = n.Id,
                Name = n.Name,
                Cidr = n.Cidr
            }),
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}


