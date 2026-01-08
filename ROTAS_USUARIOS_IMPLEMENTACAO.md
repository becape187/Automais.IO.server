# Implementação de Rotas Permitidas para Usuários VPN

## ✅ Backend Implementado

### Entidades Criadas
- `UserAllowedRoute` - Armazena quais rotas (redes) cada usuário pode acessar via VPN

### Repositórios
- `IUserAllowedRouteRepository` - Interface
- `UserAllowedRouteRepository` - Implementação com métodos:
  - `GetByUserIdAsync` - Buscar rotas de um usuário
  - `ReplaceUserRoutesAsync` - Substituir todas as rotas de um usuário

### DTOs
- `UserAllowedRouteDto` - DTO para rotas permitidas (incluído em `UserVpnConfigDto`)
- `RouterRouteDto` - DTO para rotas disponíveis de routers
- `UpdateUserRoutesDto` - DTO para atualizar rotas de um usuário

### Endpoints Criados/Modificados

1. **GET /api/tenants/{tenantId}/routes**
   - Retorna todas as rotas disponíveis de todos os routers do tenant

2. **GET /api/users/{id}/routes**
   - Retorna rotas permitidas de um usuário específico

3. **PUT /api/users/{id}/routes**
   - Atualiza rotas permitidas de um usuário
   - Body: `{ "routerAllowedNetworkIds": ["guid1", "guid2", ...] }`

4. **GET /api/user/vpn/config** (Modificado)
   - Agora inclui `allowedRoutes` e `vpnGatewayIp` na resposta

### Serviços Modificados
- `UserVpnService` - Agora inclui rotas permitidas na configuração VPN
- `RouterAllowedNetworkRepository` - Adicionado método `GetAllByTenantIdAsync`

## 📋 Próximos Passos - Frontend

### 1. Atualizar UserModal.jsx
- Adicionar checkbox "Habilitar VPN" ao criar/editar usuário
- Ao editar, mostrar seção de "Rotas Permitidas" com tabela de rotas

### 2. Criar componente UserRoutesSelector.jsx
- Tabela com todas as rotas disponíveis
- Checkboxes para selecionar quais rotas o usuário pode acessar
- Agrupar por router

### 3. Atualizar hooks/useUsers.js
- Adicionar função para buscar rotas disponíveis
- Adicionar função para buscar rotas do usuário
- Adicionar função para atualizar rotas do usuário

## 📋 Próximos Passos - App Windows

### 1. Atualizar WireGuardService.cs
- Ao conectar, adicionar rotas temporárias usando `route add` (Windows)
- Ao desconectar, remover rotas usando `route delete`
- Usar `VpnGatewayIp` como gateway para as rotas

### 2. Atualizar ApiService.cs
- A resposta de `GetUserVpnConfigAsync` agora inclui `allowedRoutes` e `vpnGatewayIp`

## 🔧 Migration Necessária

```bash
dotnet ef migrations add AddUserAllowedRoutes --startup-project ../Automais.Api
dotnet ef database update --startup-project ../Automais.Api
```

Ou SQL manual:
```sql
CREATE TABLE "user_allowed_routes" (
    "Id" UUID PRIMARY KEY,
    "UserId" UUID NOT NULL,
    "RouterId" UUID NOT NULL,
    "RouterAllowedNetworkId" UUID NOT NULL,
    "NetworkCidr" VARCHAR(50) NOT NULL,
    "Description" VARCHAR(255),
    "CreatedAt" TIMESTAMP NOT NULL,
    "UpdatedAt" TIMESTAMP NOT NULL,
    FOREIGN KEY ("UserId") REFERENCES "TenantUsers"("Id") ON DELETE CASCADE,
    FOREIGN KEY ("RouterId") REFERENCES "Routers"("Id") ON DELETE RESTRICT,
    FOREIGN KEY ("RouterAllowedNetworkId") REFERENCES "router_allowed_networks"("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX "IX_user_allowed_routes_UserId_RouterAllowedNetworkId" 
    ON "user_allowed_routes"("UserId", "RouterAllowedNetworkId");
CREATE INDEX "IX_user_allowed_routes_UserId" ON "user_allowed_routes"("UserId");
CREATE INDEX "IX_user_allowed_routes_RouterId" ON "user_allowed_routes"("RouterId");
```

