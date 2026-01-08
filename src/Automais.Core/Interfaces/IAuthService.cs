using Automais.Core.DTOs;

namespace Automais.Core.Interfaces;

/// <summary>
/// Serviço para autenticação de usuários
/// </summary>
public interface IAuthService
{
    Task<LoginResponseDto> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<UserInfoDto?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);
    string GenerateToken(Guid userId, string email, Guid tenantId);
}

