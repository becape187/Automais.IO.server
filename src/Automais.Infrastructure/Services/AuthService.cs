using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Automais.Core.DTOs;
using Automais.Core.Entities;
using Automais.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Automais.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly ITenantUserRepository _userRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService>? _logger;

    public AuthService(
        ITenantUserRepository userRepository,
        IConfiguration configuration,
        ILogger<AuthService>? logger = null)
    {
        _userRepository = userRepository;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<LoginResponseDto> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        // Buscar usuário por email (username)
        var user = await _userRepository.GetByEmailAsync(username, cancellationToken);
        
        if (user == null)
        {
            _logger?.LogWarning("Tentativa de login com email não encontrado: {Email}", username);
            throw new UnauthorizedAccessException("Credenciais inválidas");
        }

        // Verificar status do usuário
        if (user.Status != TenantUserStatus.Active)
        {
            _logger?.LogWarning("Tentativa de login com usuário inativo: {Email} (Status: {Status})", username, user.Status);
            throw new UnauthorizedAccessException("Usuário não está ativo");
        }

        // Verificar se está usando senha temporária e se ela expirou
        if (!string.IsNullOrWhiteSpace(user.TemporaryPassword) && user.TemporaryPasswordExpiresAt.HasValue)
        {
            if (DateTime.UtcNow > user.TemporaryPasswordExpiresAt.Value)
            {
                _logger?.LogWarning("Tentativa de login com senha temporária expirada: {Email}", username);
                throw new UnauthorizedAccessException("Sua senha temporária expirou. Por favor, solicite uma nova senha.");
            }
        }

        // Verificar senha
        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            _logger?.LogWarning("Usuário sem senha configurada: {Email}", username);
            throw new UnauthorizedAccessException("Credenciais inválidas");
        }

        // Verificar se a senha fornecida corresponde
        var providedPasswordHash = HashPassword(password);
        if (providedPasswordHash != user.PasswordHash)
        {
            _logger?.LogWarning("Tentativa de login com senha incorreta: {Email}", username);
            throw new UnauthorizedAccessException("Credenciais inválidas");
        }
        
        // Atualizar último login
        user.LastLoginAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user, cancellationToken);

        // Gerar token JWT
        var token = GenerateToken(user.Id, user.Email, user.TenantId);
        var expiresAt = DateTime.UtcNow.AddHours(24); // Token válido por 24 horas

        _logger?.LogInformation("Login bem-sucedido para usuário {Email} (ID: {UserId})", user.Email, user.Id);

        return new LoginResponseDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            User = new UserInfoDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                TenantId = user.TenantId
            }
        };
    }

    public async Task<UserInfoDto?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = GetSigningKey();
            
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
            var jwtToken = (JwtSecurityToken)validatedToken;

            var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "userId");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return null;
            }

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user == null || user.Status != TenantUserStatus.Active)
            {
                return null;
            }

            return new UserInfoDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                TenantId = user.TenantId
            };
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Erro ao validar token: {Error}", ex.Message);
            return null;
        }
    }

    public string GenerateToken(Guid userId, string email, Guid tenantId)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = GetSigningKey();
        
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, email),
            new Claim("tenantId", tenantId.ToString()),
            new Claim("userId", userId.ToString())
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(24),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private SymmetricSecurityKey GetSigningKey()
    {
        // Obter chave secreta da configuração ou usar uma padrão
        var secretKey = _configuration["Jwt:SecretKey"] 
            ?? _configuration["JWT_SECRET_KEY"] 
            ?? "AutomaisSecretKey_ChangeInProduction_Minimum32Characters";
        
        // Garantir que a chave tem pelo menos 32 caracteres
        if (secretKey.Length < 32)
        {
            secretKey = secretKey.PadRight(32, '0');
        }

        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
    }

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }
}

