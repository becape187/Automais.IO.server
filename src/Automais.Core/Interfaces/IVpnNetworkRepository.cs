using Automais.Core.Entities;

namespace Automais.Core.Interfaces;

public interface IVpnNetworkRepository
{
    Task<VpnNetwork?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<VpnNetwork>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IEnumerable<VpnNetwork>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    Task<VpnNetwork> CreateAsync(VpnNetwork network, CancellationToken cancellationToken = default);
    Task<VpnNetwork> UpdateAsync(VpnNetwork network, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> SlugExistsAsync(Guid tenantId, string slug, CancellationToken cancellationToken = default);
    Task<IEnumerable<VpnNetworkMembership>> GetMembershipsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<VpnNetworkMembership>> GetMembershipsByNetworkIdAsync(Guid networkId, CancellationToken cancellationToken = default);
    Task<int> CountMembershipsByNetworkIdAsync(Guid networkId, CancellationToken cancellationToken = default);
    Task ReplaceUserMembershipsAsync(Guid tenantId, Guid userId, IEnumerable<Guid> networkIds, CancellationToken cancellationToken = default);
    Task RemoveMembershipsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}


