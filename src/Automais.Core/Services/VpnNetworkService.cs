using Automais.Core.DTOs;
using Automais.Core.Entities;
using Automais.Core.Interfaces;

namespace Automais.Core.Services;

public class VpnNetworkService : IVpnNetworkService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IVpnNetworkRepository _vpnNetworkRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly ITenantUserService _tenantUserService;

    public VpnNetworkService(
        ITenantRepository tenantRepository,
        IVpnNetworkRepository vpnNetworkRepository,
        IDeviceRepository deviceRepository,
        ITenantUserService tenantUserService)
    {
        _tenantRepository = tenantRepository;
        _vpnNetworkRepository = vpnNetworkRepository;
        _deviceRepository = deviceRepository;
        _tenantUserService = tenantUserService;
    }

    public async Task<IEnumerable<VpnNetworkDto>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var networks = await _vpnNetworkRepository.GetByTenantIdAsync(tenantId, cancellationToken);
        var result = new List<VpnNetworkDto>();

        foreach (var network in networks)
        {
            result.Add(await MapToDtoAsync(network, cancellationToken));
        }

        return result;
    }

    public async Task<VpnNetworkDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var network = await _vpnNetworkRepository.GetByIdAsync(id, cancellationToken);
        return network == null ? null : await MapToDtoAsync(network, cancellationToken);
    }

    public async Task<VpnNetworkDto> CreateAsync(Guid tenantId, CreateVpnNetworkDto dto, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        if (tenant == null)
        {
            throw new KeyNotFoundException($"Tenant com ID {tenantId} não encontrado.");
        }

        if (await _vpnNetworkRepository.SlugExistsAsync(tenantId, dto.Slug, cancellationToken))
        {
            throw new InvalidOperationException($"Slug '{dto.Slug}' já está em uso para este tenant.");
        }

        var network = new VpnNetwork
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = dto.Name,
            Slug = dto.Slug,
            Cidr = dto.Cidr,
            Description = dto.Description,
            IsDefault = dto.IsDefault,
            DnsServers = dto.DnsServers,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await _vpnNetworkRepository.CreateAsync(network, cancellationToken);
        return await MapToDtoAsync(created, cancellationToken);
    }

    public async Task<VpnNetworkDto> UpdateAsync(Guid id, UpdateVpnNetworkDto dto, CancellationToken cancellationToken = default)
    {
        var network = await _vpnNetworkRepository.GetByIdAsync(id, cancellationToken);
        if (network == null)
        {
            throw new KeyNotFoundException($"Rede VPN com ID {id} não encontrada.");
        }

        if (!string.IsNullOrWhiteSpace(dto.Name))
        {
            network.Name = dto.Name;
        }

        if (dto.Description != null)
        {
            network.Description = dto.Description;
        }

        if (dto.IsDefault.HasValue)
        {
            network.IsDefault = dto.IsDefault.Value;
        }

        if (dto.DnsServers != null)
        {
            network.DnsServers = dto.DnsServers;
        }

        network.UpdatedAt = DateTime.UtcNow;

        var updated = await _vpnNetworkRepository.UpdateAsync(network, cancellationToken);
        return await MapToDtoAsync(updated, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var network = await _vpnNetworkRepository.GetByIdAsync(id, cancellationToken);
        if (network == null)
        {
            return;
        }

        await _vpnNetworkRepository.DeleteAsync(id, cancellationToken);
    }

    public async Task<IEnumerable<TenantUserDto>> GetUsersAsync(Guid networkId, CancellationToken cancellationToken = default)
    {
        var network = await _vpnNetworkRepository.GetByIdAsync(networkId, cancellationToken);
        if (network == null)
        {
            throw new KeyNotFoundException($"Rede VPN com ID {networkId} não encontrada.");
        }

        var memberships = await _vpnNetworkRepository.GetMembershipsByNetworkIdAsync(networkId, cancellationToken);
        var result = new List<TenantUserDto>();

        foreach (var membership in memberships)
        {
            var user = await _tenantUserService.GetByIdAsync(membership.TenantUserId, cancellationToken);
            if (user != null)
            {
                result.Add(user);
            }
        }

        return result;
    }

    private async Task<VpnNetworkDto> MapToDtoAsync(VpnNetwork network, CancellationToken cancellationToken)
    {
        var userCount = await _vpnNetworkRepository.CountMembershipsByNetworkIdAsync(network.Id, cancellationToken);
        var deviceCount = await _deviceRepository.CountByNetworkIdAsync(network.Id, cancellationToken);

        return new VpnNetworkDto
        {
            Id = network.Id,
            TenantId = network.TenantId,
            Name = network.Name,
            Slug = network.Slug,
            Cidr = network.Cidr,
            Description = network.Description,
            IsDefault = network.IsDefault,
            DnsServers = network.DnsServers,
            UserCount = userCount,
            DeviceCount = deviceCount,
            CreatedAt = network.CreatedAt,
            UpdatedAt = network.UpdatedAt
        };
    }
}


