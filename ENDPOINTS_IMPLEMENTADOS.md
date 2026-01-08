# Endpoints Implementados para Cliente Windows VPN

## ✅ Endpoints Criados

### 1. POST /api/auth/login
**Controller:** `AuthController.cs`  
**Serviço:** `AuthService.cs`

Autentica um usuário e retorna um token JWT.

**Request:**
```json
{
  "username": "usuario@example.com",
  "password": "senha123"
}
```

**Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2024-12-31T23:59:59Z",
  "user": {
    "id": "123e4567-e89b-12d3-a456-426614174000",
    "name": "Nome do Usuário",
    "email": "usuario@example.com",
    "tenantId": "tenant-guid"
  }
}
```

### 2. GET /api/user/vpn/config
**Controller:** `UserVpnController.cs`  
**Serviço:** `UserVpnService.cs`

Retorna a configuração WireGuard do usuário autenticado.

**Headers:**
```
Authorization: Bearer {token}
```

**Response:**
```json
{
  "configContent": "[Interface]\nPrivateKey = ...\nAddress = 10.0.0.5/24\n\n[Peer]\nPublicKey = ...\nEndpoint = vpn.automais.io:51820\nAllowedIPs = 10.0.0.0/24",
  "fileName": "automais-usuario_example.com.conf",
  "vpnEnabled": true,
  "vpnDeviceName": "Device-usuario@example.com",
  "vpnPublicKey": "...",
  "vpnIpAddress": "10.0.0.5/24"
}
```

## 📁 Arquivos Criados/Modificados

### DTOs
- `src/Automais.Core/DTOs/AuthDto.cs` - DTOs para autenticação e VPN

### Interfaces
- `src/Automais.Core/Interfaces/IAuthService.cs` - Interface do serviço de autenticação
- `src/Automais.Core/Interfaces/IUserVpnService.cs` - Interface do serviço VPN de usuários
- `src/Automais.Core/Interfaces/ITenantUserRepository.cs` - Adicionado método `GetByEmailAsync`

### Serviços
- `src/Automais.Infrastructure/Services/AuthService.cs` - Implementação de autenticação JWT
- `src/Automais.Infrastructure/Services/UserVpnService.cs` - Implementação de VPN para usuários

### Controllers
- `src/Automais.Api/Controllers/AuthController.cs` - Controller de autenticação
- `src/Automais.Api/Controllers/UserVpnController.cs` - Controller de VPN do usuário

### Repositórios
- `src/Automais.Infrastructure/Repositories/TenantUserRepository.cs` - Adicionado método `GetByEmailAsync`

### Entidades
- `src/Automais.Core/Entities/TenantUser.cs` - Adicionado campo `VpnPrivateKey`

### Configuração
- `src/Automais.Api/Program.cs` - Registrados serviços `IAuthService` e `IUserVpnService`
- `src/Automais.Infrastructure/Automais.Infrastructure.csproj` - Adicionado pacote `System.IdentityModel.Tokens.Jwt`

## 🔧 Configuração Necessária

### Variável de Ambiente (Opcional)
```bash
JWT_SECRET_KEY=your-secret-key-minimum-32-characters
```

Ou no `appsettings.json`:
```json
{
  "Jwt": {
    "SecretKey": "your-secret-key-minimum-32-characters"
  }
}
```

**Nota:** Se não configurado, será usado um valor padrão (não recomendado para produção).

## 📝 Migration Necessária

É necessário criar uma migration para adicionar o campo `VpnPrivateKey` na tabela `TenantUsers`:

```bash
cd server.io/src/Automais.Infrastructure
dotnet ef migrations add AddVpnPrivateKeyToTenantUser --startup-project ../Automais.Api
dotnet ef database update --startup-project ../Automais.Api
```

Ou manualmente no banco:
```sql
ALTER TABLE "TenantUsers" ADD COLUMN "VpnPrivateKey" TEXT NULL;
```

## 🔐 Segurança

### Autenticação
- Usa JWT tokens com expiração de 24 horas
- Validação de token em cada requisição protegida
- Verifica status do usuário (deve estar `Active`)

### VPN
- Chaves WireGuard geradas usando `/usr/bin/wg genkey`
- Chave privada armazenada no banco (criptografar em produção)
- IPs alocados automaticamente na rede VPN
- Peers adicionados ao servidor WireGuard automaticamente

## ⚠️ Notas Importantes

1. **Senha:** Atualmente, a autenticação aceita qualquer senha se o usuário estiver ativo. **Implementar hash de senha em produção** (BCrypt, Argon2, etc.).

2. **Chave Privada:** A chave privada WireGuard é armazenada em texto plano no banco. **Criptografar em produção** usando AES ou similar.

3. **JWT Secret:** Configure uma chave secreta forte em produção. O valor padrão não é seguro.

4. **WireGuard Server:** O serviço assume que o WireGuard está instalado e configurado no servidor Linux (`/usr/bin/wg`).

## 🚀 Próximos Passos

1. Criar migration para adicionar `VpnPrivateKey`
2. Implementar hash de senha para autenticação
3. Implementar criptografia para chaves privadas
4. Adicionar rate limiting no endpoint de login
5. Adicionar logs de auditoria para acessos VPN

