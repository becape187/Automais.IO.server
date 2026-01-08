using Automais.Core.DTOs;

namespace Automais.Core.Interfaces;

/// <summary>
/// Serviço para gerenciamento de VPN de usuários
/// </summary>
public interface IUserVpnService
{
    Task<UserVpnConfigDto> GetUserVpnConfigAsync(Guid userId, CancellationToken cancellationToken = default);
    Task EnsureUserVpnProvisionedAsync(Guid userId, CancellationToken cancellationToken = default);
}

