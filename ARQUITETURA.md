# Arquitetura da Plataforma IoT Multi-Tenant

## 📋 Índice

1. [Visão Geral](#visão-geral)
2. [Conceitos Fundamentais](#conceitos-fundamentais)
3. [Arquitetura do Sistema](#arquitetura-do-sistema)
4. [Modelo de Dados](#modelo-de-dados)
5. [Fluxos Principais](#fluxos-principais)
6. [APIs e Integrações](#apis-e-integrações)
7. [Segurança e Permissões](#segurança-e-permissões)
8. [Roadmap de Implementação](#roadmap-de-implementação)

---

## Visão Geral

### O que é a Plataforma?

Uma plataforma **multi-tenant** (múltiplos clientes) para gerenciar uma infraestrutura IoT completa baseada em LoRaWAN, onde cada cliente pode:

- ✅ Criar e gerenciar **Gateways** (equipamentos que recebem sinais LoRa)
- ✅ Criar e gerenciar **Devices** (sensores/atuadores IoT)
- ✅ Criar **Applications** (agrupamento lógico de devices)
- ✅ Criar **Usuários** e delegar permissões granulares
- ✅ Gerenciar acesso VPN via **WireGuard** para redes específicas

### Stack Tecnológica

```
┌─────────────────────────────────────────────────┐
│                   Front.io                      │
│              React + TypeScript                 │
└─────────────────────────────────────────────────┘
                       ↓ REST API
┌─────────────────────────────────────────────────┐
│                  Server.io                      │
│        ASP.NET Core 8 (C#) + EF Core           │
│      JWT Auth + RBAC + Multi-Tenant            │
└─────────────────────────────────────────────────┘
           ↓              ↓              ↓
    ┌──────────┐   ┌──────────┐   ┌──────────┐
    │ChirpStack│   │   EMQX   │   │WireGuard │
    │ (LoRaWAN)│   │  (MQTT)  │   │  (VPN)   │
    └──────────┘   └──────────┘   └──────────┘
```

### Banco de Dados

- **PostgreSQL**: Dados principais (multi-tenant)
- **Redis**: Cache, sessões, locks distribuídos

---

## Conceitos Fundamentais

### O que é Multi-Tenant?

**Multi-tenant** significa que **vários clientes** usam a mesma aplicação, mas cada um vê **apenas seus próprios dados**.

**Analogia**: É como um prédio de apartamentos:
- Todos usam a mesma estrutura (elevador, água, luz)
- Mas cada morador só acessa seu próprio apartamento
- O síndico (admin) consegue ver/gerenciar tudo

**No nosso caso**:
- Cada cliente é um **Tenant**
- Todos os dados têm um `tenant_id`
- As queries **sempre** filtram por `tenant_id`

### Hierarquia de Entidades

```
Tenant (Cliente)
 │
 ├─ Users (Usuários do cliente)
 │   └─ Roles (Owner, Admin, Operator, Viewer)
 │
 ├─ Applications (Aplicações)
 │   └─ Devices (Dispositivos IoT)
 │
 ├─ Gateways (Gateways LoRaWAN)
 │
 └─ WireGuard
     ├─ Interface (rede VPN do tenant)
     └─ Peers (usuários/devices conectados)
```

### Componentes Externos

#### 1. **ChirpStack** (Network Server LoRaWAN)

**O que faz**: Gerencia a comunicação LoRaWAN entre gateways e devices.

**Responsabilidades**:
- Recebe pacotes dos gateways
- Descriptografa mensagens dos devices
- Emite eventos (join, uplink, ack)
- Gerencia chaves de criptografia (AppKey, NwkKey)

**Integração**:
- API gRPC para criar applications/devices
- MQTT para receber eventos em tempo real

#### 2. **EMQX** (MQTT Broker)

**O que faz**: Broker MQTT que intermedia mensagens entre devices, ChirpStack e nossa API.

**Responsabilidades**:
- Pub/Sub de mensagens MQTT
- Autenticação de clientes MQTT
- ACL (controle de acesso) por tópicos

**Integração**:
- HTTP Auth: EMQX chama nossa API para validar credenciais
- HTTP ACL: EMQX chama nossa API para checar permissões de tópicos

#### 3. **WireGuard** (VPN)

**O que faz**: Cria túneis VPN seguros para usuários/devices acessarem redes privadas.

**Responsabilidades**:
- Criar interfaces de rede (wg0, wg1, etc)
- Gerenciar peers (clientes conectados)
- Rotear tráfego baseado em AllowedIPs

**Integração**:
- Controlado diretamente pelo `server.io` via comandos ou biblioteca

---

## Arquitetura do Sistema

### Diagrama de Componentes

```
┌─────────────────────────────────────────────────────────────┐
│                        USUÁRIO FINAL                        │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│                     FRONT.IO (React)                        │
│  - Portal Admin (gerenciar todos os tenants)               │
│  - Portal Cliente (gerenciar seu tenant)                   │
│  - Dashboards (visualizar telemetria)                      │
└─────────────────────────────────────────────────────────────┘
                            ↓ HTTPS/REST
┌─────────────────────────────────────────────────────────────┐
│                    SERVER.IO (ASP.NET Core)                 │
│                                                             │
│  ┌───────────────┐  ┌───────────────┐  ┌────────────────┐ │
│  │   Auth API    │  │  Management   │  │  Telemetry API │ │
│  │ (JWT/OIDC)    │  │      API      │  │  (Metrics)     │ │
│  └───────────────┘  └───────────────┘  └────────────────┘ │
│                                                             │
│  ┌───────────────┐  ┌───────────────┐  ┌────────────────┐ │
│  │  ChirpStack   │  │     EMQX      │  │   WireGuard    │ │
│  │   Service     │  │   Service     │  │    Service     │ │
│  └───────────────┘  └───────────────┘  └────────────────┘ │
│                                                             │
│  ┌───────────────────────────────────────────────────────┐ │
│  │            Background Jobs (Hangfire)                 │ │
│  │  - Sync ChirpStack ↔ DB                              │ │
│  │  - Rotate WireGuard Keys                             │ │
│  │  - Process MQTT Messages                             │ │
│  └───────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
          ↓                 ↓                 ↓
┌──────────────────┐ ┌──────────────┐ ┌─────────────────┐
│   ChirpStack     │ │     EMQX     │ │   WireGuard     │
│   (External)     │ │  (External)  │ │   (System)      │
└──────────────────┘ └──────────────┘ └─────────────────┘
          ↓                 ↓
┌──────────────────────────────────────────┐
│      LoRaWAN Gateways + Devices          │
└──────────────────────────────────────────┘
```

### Camadas da Aplicação

#### 1. **API Layer** (Controllers)
- Recebe requisições HTTP
- Valida entrada
- Chama serviços
- Retorna respostas

#### 2. **Service Layer** (Business Logic)
- Regras de negócio
- Orquestração de operações
- Validações complexas

#### 3. **Integration Layer** (External Services)
- Comunicação com ChirpStack
- Comunicação com EMQX
- Controle do WireGuard

#### 4. **Data Layer** (Repositories + EF Core)
- Acesso ao banco de dados
- Queries otimizadas
- Migrations

#### 5. **Background Jobs**
- Tarefas assíncronas
- Sincronizações
- Processamento de eventos

---

## Modelo de Dados

### Schema Principal (PostgreSQL)

#### Tabela: `tenants`
```sql
-- Cliente/Organização
CREATE TABLE tenants (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(100) NOT NULL,
    slug VARCHAR(50) NOT NULL UNIQUE, -- ex: 'acme-corp'
    status VARCHAR(20) NOT NULL, -- active, suspended, deleted
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW()
);
```

#### Tabela: `users`
```sql
-- Usuário do sistema (pode pertencer a vários tenants)
CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email VARCHAR(255) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    full_name VARCHAR(100) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW()
);
```

#### Tabela: `user_tenant_roles`
```sql
-- Relacionamento N:N entre users e tenants com roles
CREATE TABLE user_tenant_roles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    role VARCHAR(20) NOT NULL, -- owner, admin, operator, viewer
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    UNIQUE(user_id, tenant_id)
);
```

#### Tabela: `applications`
```sql
-- Aplicação IoT (agrupa devices)
CREATE TABLE applications (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    chirpstack_application_id VARCHAR(100), -- ID no ChirpStack
    mqtt_username VARCHAR(100),
    mqtt_password_hash VARCHAR(255),
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_applications_tenant ON applications(tenant_id);
```

#### Tabela: `devices`
```sql
-- Device IoT (sensor/atuador)
CREATE TABLE devices (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    application_id UUID NOT NULL REFERENCES applications(id) ON DELETE CASCADE,
    name VARCHAR(100) NOT NULL,
    dev_eui VARCHAR(16) NOT NULL UNIQUE, -- identificador LoRaWAN
    chirpstack_device_id VARCHAR(100), -- ID no ChirpStack
    device_profile_id VARCHAR(100), -- perfil no ChirpStack
    app_key VARCHAR(32), -- chave de criptografia (criptografada no DB)
    status VARCHAR(20) NOT NULL, -- active, inactive, maintenance
    last_seen_at TIMESTAMP,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_devices_tenant ON devices(tenant_id);
CREATE INDEX idx_devices_application ON devices(application_id);
CREATE INDEX idx_devices_dev_eui ON devices(dev_eui);
```

#### Tabela: `gateways`
```sql
-- Gateway LoRaWAN
CREATE TABLE gateways (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    name VARCHAR(100) NOT NULL,
    gateway_eui VARCHAR(16) NOT NULL UNIQUE, -- identificador LoRaWAN
    chirpstack_gateway_id VARCHAR(100), -- ID no ChirpStack
    location_lat DECIMAL(10, 8),
    location_lng DECIMAL(11, 8),
    status VARCHAR(20) NOT NULL, -- online, offline, maintenance
    last_seen_at TIMESTAMP,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_gateways_tenant ON gateways(tenant_id);
CREATE INDEX idx_gateways_eui ON gateways(gateway_eui);
```

#### Tabela: `wireguard_interfaces`
```sql
-- Interface WireGuard por tenant
CREATE TABLE wireguard_interfaces (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    name VARCHAR(50) NOT NULL UNIQUE, -- ex: wg-tenant1
    address VARCHAR(50) NOT NULL, -- ex: 10.100.1.1/24
    listen_port INT NOT NULL,
    private_key VARCHAR(100) NOT NULL, -- criptografado
    public_key VARCHAR(100) NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_wg_interfaces_tenant ON wireguard_interfaces(tenant_id);
```

#### Tabela: `wireguard_peers`
```sql
-- Peer WireGuard (usuário ou device conectado)
CREATE TABLE wireguard_peers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    interface_id UUID NOT NULL REFERENCES wireguard_interfaces(id) ON DELETE CASCADE,
    user_id UUID REFERENCES users(id) ON DELETE CASCADE, -- se for peer de usuário
    device_id UUID REFERENCES devices(id) ON DELETE CASCADE, -- se for peer de device
    name VARCHAR(100) NOT NULL,
    public_key VARCHAR(100) NOT NULL UNIQUE,
    allowed_ips TEXT NOT NULL, -- ex: 10.100.1.10/32
    allowed_networks TEXT, -- JSON array: ["netX", "netY"]
    is_enabled BOOLEAN NOT NULL DEFAULT true,
    last_handshake_at TIMESTAMP,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    CHECK (user_id IS NOT NULL OR device_id IS NOT NULL)
);

CREATE INDEX idx_wg_peers_tenant ON wireguard_peers(tenant_id);
CREATE INDEX idx_wg_peers_interface ON wireguard_peers(interface_id);
```

#### Tabela: `device_messages`
```sql
-- Mensagens (telemetria) dos devices
CREATE TABLE device_messages (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    device_id UUID NOT NULL REFERENCES devices(id) ON DELETE CASCADE,
    message_type VARCHAR(20) NOT NULL, -- uplink, downlink, join
    payload JSONB NOT NULL, -- dados do sensor
    metadata JSONB, -- rssi, snr, gateway info, etc
    received_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_messages_tenant ON device_messages(tenant_id);
CREATE INDEX idx_messages_device ON device_messages(device_id);
CREATE INDEX idx_messages_received_at ON device_messages(received_at DESC);
```

#### Tabela: `audit_logs`
```sql
-- Log de auditoria
CREATE TABLE audit_logs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID REFERENCES tenants(id) ON DELETE SET NULL,
    user_id UUID REFERENCES users(id) ON DELETE SET NULL,
    action VARCHAR(100) NOT NULL, -- ex: 'device.created', 'user.deleted'
    resource_type VARCHAR(50) NOT NULL, -- ex: 'device', 'application'
    resource_id UUID,
    details JSONB,
    ip_address VARCHAR(45),
    created_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_audit_tenant ON audit_logs(tenant_id);
CREATE INDEX idx_audit_user ON audit_logs(user_id);
CREATE INDEX idx_audit_created_at ON audit_logs(created_at DESC);
```

### Relacionamentos

```
tenants 1──┬─── users (N:N via user_tenant_roles)
           │
           ├──N applications ──N devices
           │
           ├──N gateways
           │
           └──1 wireguard_interfaces ──N wireguard_peers
```

---

## Fluxos Principais

### 1. Criação de um Novo Tenant

**Sequência**:

```
1. Admin cria tenant via API
   POST /api/admin/tenants
   { "name": "Acme Corp", "slug": "acme-corp" }

2. Server.io:
   ├─ Cria registro em `tenants`
   ├─ Cria usuário owner inicial
   ├─ Cria interface WireGuard (wg-acme-corp)
   ├─ Cria namespace no ChirpStack (via tags)
   └─ Retorna credenciais ao admin

3. Owner do tenant faz primeiro login
```

**Implementação**:
- Controller: `AdminController.CreateTenant()`
- Service: `TenantService.CreateAsync()`
- Integrations:
  - `WireGuardService.CreateInterface()`
  - `ChirpStackService.SetupTenantNamespace()`

### 2. Criação de uma Application

**Sequência**:

```
1. Usuário (admin do tenant) cria application
   POST /api/tenants/{tenantId}/applications
   { "name": "Sensores Temperatura", "description": "..." }

2. Server.io:
   ├─ Valida permissão do usuário
   ├─ Cria registro em `applications`
   ├─ Cria application no ChirpStack via API
   ├─ Gera credenciais MQTT
   └─ Retorna application criada

3. Usuário pode agora adicionar devices
```

### 3. Criação de um Device

**Sequência**:

```
1. Usuário cria device
   POST /api/applications/{appId}/devices
   { 
     "name": "Sensor Sala 101",
     "dev_eui": "0123456789ABCDEF",
     "app_key": "00112233445566778899AABBCCDDEEFF"
   }

2. Server.io:
   ├─ Valida DEV_EUI único
   ├─ Cria registro em `devices`
   ├─ Provisiona device no ChirpStack
   │   └─ Cria device com keys
   ├─ Configura ACL no EMQX para o device
   └─ Retorna device criado

3. Device pode fazer JOIN na rede LoRaWAN
```

### 4. Recebimento de Mensagem (Telemetria)

**Sequência**:

```
1. Device envia mensagem LoRa
   ↓
2. Gateway recebe e encaminha ao ChirpStack
   ↓
3. ChirpStack descriptografa e publica no EMQX
   Topic: application/{appId}/device/{devEui}/event/up
   ↓
4. Server.io (subscriber MQTT) consome mensagem
   ├─ Valida tenant/device
   ├─ Persiste em `device_messages`
   ├─ Processa regras de alerta
   └─ Atualiza `last_seen_at` do device
   ↓
5. Front.io consulta via API para exibir
   GET /api/devices/{deviceId}/messages
```

### 5. Criação de Acesso WireGuard para Usuário

**Sequência**:

```
1. Admin do tenant cria peer WireGuard para usuário
   POST /api/tenants/{tenantId}/wireguard/peers
   {
     "user_id": "...",
     "allowed_networks": ["netX", "netY"]
   }

2. Server.io:
   ├─ Gera par de chaves (privada/pública)
   ├─ Cria registro em `wireguard_peers`
   ├─ Atualiza config do WireGuard
   │   └─ wg set wg-tenant1 peer <pubkey> allowed-ips 10.100.1.10/32
   └─ Retorna arquivo .conf para o usuário

3. Usuário importa .conf no WireGuard client e conecta
```

---

## APIs e Integrações

### Endpoints REST (Server.io)

#### **Autenticação**
```
POST   /api/auth/login
POST   /api/auth/refresh
POST   /api/auth/logout
```

#### **Tenants** (Admin apenas)
```
GET    /api/admin/tenants
POST   /api/admin/tenants
GET    /api/admin/tenants/{id}
PUT    /api/admin/tenants/{id}
DELETE /api/admin/tenants/{id}
```

#### **Users**
```
GET    /api/tenants/{tenantId}/users
POST   /api/tenants/{tenantId}/users (convite)
PUT    /api/tenants/{tenantId}/users/{userId}/role
DELETE /api/tenants/{tenantId}/users/{userId}
```

#### **Applications**
```
GET    /api/tenants/{tenantId}/applications
POST   /api/tenants/{tenantId}/applications
GET    /api/applications/{id}
PUT    /api/applications/{id}
DELETE /api/applications/{id}
```

#### **Devices**
```
GET    /api/applications/{appId}/devices
POST   /api/applications/{appId}/devices
GET    /api/devices/{id}
PUT    /api/devices/{id}
DELETE /api/devices/{id}
GET    /api/devices/{id}/messages (telemetria)
POST   /api/devices/{id}/downlink (enviar comando)
```

#### **Gateways**
```
GET    /api/tenants/{tenantId}/gateways
POST   /api/tenants/{tenantId}/gateways
GET    /api/gateways/{id}
PUT    /api/gateways/{id}
DELETE /api/gateways/{id}
```

#### **WireGuard**
```
GET    /api/tenants/{tenantId}/wireguard/peers
POST   /api/tenants/{tenantId}/wireguard/peers
GET    /api/wireguard/peers/{id}/config (download .conf)
DELETE /api/wireguard/peers/{id}
PUT    /api/wireguard/peers/{id}/toggle (enable/disable)
```

### Integrações Externas

#### **ChirpStack Integration**

**Provisionamento (gRPC API)**:
```csharp
// Exemplo: Criar application
var request = new CreateApplicationRequest
{
    Application = new Application
    {
        Name = "Acme - Sensores",
        Description = "...",
        TenantId = chirpstackTenantId
    }
};
var response = await applicationServiceClient.CreateAsync(request);
```

**Eventos (MQTT)**:
```
Subscribe topics:
  - application/+/device/+/event/up (uplink)
  - application/+/device/+/event/join (join request)
  - application/+/device/+/event/ack (downlink ack)
```

#### **EMQX Integration**

**HTTP Authentication**:
```
EMQX envia: POST http://server.io/api/mqtt/auth
Body: { "username": "device-001", "password": "secret" }

Server.io valida e retorna:
  200 OK → autenticado
  401 Unauthorized → rejeitado
```

**HTTP Authorization (ACL)**:
```
EMQX envia: POST http://server.io/api/mqtt/acl
Body: {
  "username": "device-001",
  "topic": "uplink/tenant1/app1/device-001",
  "action": "publish"
}

Server.io valida e retorna:
  200 OK → permitido
  403 Forbidden → negado
```

#### **WireGuard Integration**

**Controle via comandos**:
```bash
# Criar interface
wg-quick up wg-tenant1

# Adicionar peer
wg set wg-tenant1 peer <PUBLIC_KEY> \
  allowed-ips 10.100.1.10/32 \
  persistent-keepalive 25

# Remover peer
wg set wg-tenant1 peer <PUBLIC_KEY> remove

# Status
wg show wg-tenant1
```

**Biblioteca C#**: WireGuardSharp / WgNet / comandos shell via Process

---

## Segurança e Permissões

### Multi-Tenancy Enforcement

**Princípio**: TODO acesso deve filtrar por `tenant_id`.

**Implementação**:
```csharp
// Middleware que injeta TenantId no contexto
public class TenantMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        var userId = context.User.FindFirst("sub")?.Value;
        var tenantId = await GetTenantIdForUser(userId);
        context.Items["TenantId"] = tenantId;
        await _next(context);
    }
}

// Base repository que sempre filtra por tenant
public class TenantRepository<T> where T : ITenantEntity
{
    public IQueryable<T> GetAll()
    {
        var tenantId = _httpContextAccessor.HttpContext.Items["TenantId"];
        return _dbContext.Set<T>().Where(e => e.TenantId == tenantId);
    }
}
```

### RBAC (Role-Based Access Control)

**Roles por Tenant**:

| Role     | Permissões                                              |
|----------|---------------------------------------------------------|
| **Owner**    | Tudo (incluindo deletar tenant, gerenciar billing)     |
| **Admin**    | Gerenciar users, applications, devices, gateways, VPN   |
| **Operator** | Criar/editar devices, ver telemetria, enviar downlinks  |
| **Viewer**   | Apenas visualizar (read-only)                           |

**Implementação**:
```csharp
[Authorize(Roles = "admin,owner")]
[HttpPost("applications")]
public async Task<IActionResult> CreateApplication(...)
{
    // apenas admin ou owner podem criar applications
}

[Authorize]
[RequirePermission("devices:read")]
[HttpGet("devices/{id}")]
public async Task<IActionResult> GetDevice(...)
{
    // qualquer role com permissão devices:read
}
```

### Autenticação

**JWT (JSON Web Token)**:
```json
{
  "sub": "user-id-123",
  "email": "user@example.com",
  "tenant_id": "tenant-id-456",
  "role": "admin",
  "exp": 1698765432
}
```

**Fluxo**:
```
1. POST /api/auth/login { email, password }
   ↓
2. Server valida credenciais
   ↓
3. Gera JWT (access_token + refresh_token)
   ↓
4. Front armazena tokens (HttpOnly cookie ou localStorage)
   ↓
5. Toda requisição: Authorization: Bearer <access_token>
```

### Auditoria

**Todas as ações importantes** são logadas em `audit_logs`:
- Criação/edição/exclusão de entities
- Login/logout
- Mudanças de permissões
- Downlinks enviados

**Exemplo**:
```json
{
  "tenant_id": "...",
  "user_id": "...",
  "action": "device.created",
  "resource_type": "device",
  "resource_id": "device-id-123",
  "details": { "dev_eui": "...", "name": "..." },
  "ip_address": "192.168.1.100",
  "created_at": "2025-10-30T10:30:00Z"
}
```

---

## Roadmap de Implementação

### 🎯 Fase 1: Fundação (Semanas 1-2)

**Objetivo**: Estrutura básica do projeto e autenticação.

**Tarefas**:
- [x] Criar solução C# (ASP.NET Core 8)
- [x] Configurar EF Core + PostgreSQL
- [x] Criar migrations para `tenants`, `users`, `user_tenant_roles`
- [x] Implementar autenticação JWT
- [x] Criar endpoints de auth (login, refresh, logout)
- [x] Criar middleware de multi-tenancy
- [x] Setup do projeto React
- [x] Criar página de login no front

**Entregável**: Login funcional com multi-tenancy básico.

---

### 🎯 Fase 2: Gestão de Tenants e Users (Semanas 3-4)

**Objetivo**: CRUD completo de tenants e usuários com RBAC.

**Tarefas**:
- [ ] Criar endpoints de tenants (admin)
- [ ] Criar endpoints de users por tenant
- [ ] Implementar RBAC (roles + permissions)
- [ ] Criar telas de gestão de tenants (admin)
- [ ] Criar telas de gestão de users
- [ ] Implementar convite de usuários (email)

**Entregável**: Admin pode criar tenants e owners podem gerenciar usuários.

---

### 🎯 Fase 3: Applications e Devices (Semanas 5-7)

**Objetivo**: CRUD de applications e devices + integração ChirpStack.

**Tarefas**:
- [ ] Criar migrations para `applications`, `devices`
- [ ] Implementar serviço de integração ChirpStack (gRPC)
- [ ] Criar endpoints de applications
- [ ] Criar endpoints de devices
- [ ] Provisionar devices no ChirpStack automaticamente
- [ ] Criar telas de gestão de applications
- [ ] Criar telas de gestão de devices
- [ ] Implementar visualização de devices por mapa (lat/lng)

**Entregável**: Usuários podem criar applications e devices que são automaticamente provisionados no ChirpStack.

---

### 🎯 Fase 4: Gateways (Semana 8)

**Objetivo**: Gestão de gateways LoRaWAN.

**Tarefas**:
- [ ] Criar migrations para `gateways`
- [ ] Integrar com ChirpStack para listar gateways
- [ ] Criar endpoints de gateways
- [ ] Criar telas de gestão de gateways
- [ ] Exibir status (online/offline) em tempo real

**Entregável**: Usuários podem ver e gerenciar seus gateways.

---

### 🎯 Fase 5: Telemetria (MQTT + EMQX) (Semanas 9-11)

**Objetivo**: Receber e armazenar mensagens dos devices.

**Tarefas**:
- [ ] Criar migrations para `device_messages`
- [ ] Implementar subscriber MQTT no server.io
- [ ] Consumir eventos do ChirpStack via EMQX
- [ ] Processar e armazenar uplinks
- [ ] Criar endpoints de telemetria (GET messages)
- [ ] Implementar autenticação MQTT (HTTP Auth no EMQX)
- [ ] Implementar ACL MQTT (HTTP ACL no EMQX)
- [ ] Criar telas de visualização de telemetria
- [ ] Criar dashboards com gráficos (Chart.js/Recharts)

**Entregável**: Mensagens dos devices são recebidas, armazenadas e exibidas no front.

---

### 🎯 Fase 6: Downlinks (Semana 12)

**Objetivo**: Enviar comandos para devices.

**Tarefas**:
- [ ] Implementar envio de downlink via ChirpStack API
- [ ] Criar endpoint POST /devices/{id}/downlink
- [ ] Criar UI para enviar comandos
- [ ] Implementar fila de downlinks (se necessário)

**Entregável**: Usuários podem enviar comandos para devices.

---

### 🎯 Fase 7: WireGuard (Semanas 13-15)

**Objetivo**: Gestão de VPN para usuários e devices.

**Tarefas**:
- [ ] Criar migrations para `wireguard_interfaces`, `wireguard_peers`
- [ ] Implementar serviço WireGuard (criar interface, peers)
- [ ] Criar endpoints de WireGuard
- [ ] Provisionar interface WireGuard ao criar tenant
- [ ] Criar peers para usuários
- [ ] Gerar arquivos .conf para download
- [ ] Implementar políticas de acesso (allowed_networks)
- [ ] Criar telas de gestão de VPN
- [ ] Documentar setup para usuários finais

**Entregável**: Usuários podem criar peers VPN e conectar às redes do tenant.

---

### 🎯 Fase 8: Auditoria e Observabilidade (Semanas 16-17)

**Objetivo**: Logs, auditoria e monitoramento.

**Tarefas**:
- [ ] Criar migrations para `audit_logs`
- [ ] Implementar middleware de auditoria
- [ ] Logar todas as ações importantes
- [ ] Criar endpoint de audit logs (com filtros)
- [ ] Criar tela de visualização de audit logs
- [ ] Configurar logging estruturado (Serilog)
- [ ] Integrar com Prometheus (métricas)
- [ ] Criar dashboards no Grafana

**Entregável**: Todas as ações são auditadas e métricas são expostas.

---

### 🎯 Fase 9: Alertas e Notificações (Semanas 18-19)

**Objetivo**: Sistema de alertas baseado em regras.

**Tarefas**:
- [ ] Criar tabelas de `alert_rules`, `alert_triggers`
- [ ] Implementar engine de processamento de regras
- [ ] Integrar com sistema de notificações (email, webhook)
- [ ] Criar endpoints de alertas
- [ ] Criar telas de gestão de alertas

**Entregável**: Usuários podem criar regras de alerta para telemetria.

---

### 🎯 Fase 10: Polimento e Testes (Semanas 20-22)

**Objetivo**: Testes, documentação e otimizações.

**Tarefas**:
- [ ] Escrever testes unitários (backend)
- [ ] Escrever testes de integração (backend)
- [ ] Escrever testes E2E (Playwright/Cypress)
- [ ] Revisar segurança (OWASP checklist)
- [ ] Otimizar queries (índices, N+1)
- [ ] Documentar APIs (Swagger/OpenAPI)
- [ ] Criar guia de deploy
- [ ] Configurar CI/CD

**Entregável**: Sistema testado, documentado e pronto para produção.

---

## 📚 Recursos e Referências

### Documentação Oficial

- **ASP.NET Core**: https://docs.microsoft.com/aspnet/core
- **Entity Framework Core**: https://docs.microsoft.com/ef/core
- **ChirpStack**: https://www.chirpstack.io/docs/
- **EMQX**: https://www.emqx.io/docs/
- **WireGuard**: https://www.wireguard.com/

### Bibliotecas Úteis

**Backend (C#)**:
- `EFCore.NamingConventions` - snake_case no PostgreSQL
- `Hangfire` - Background jobs
- `FluentValidation` - Validação de inputs
- `AutoMapper` - Mapeamento DTO ↔ Entity
- `Serilog` - Logging estruturado
- `MQTTnet` - Cliente MQTT
- `Grpc.Net.Client` - Cliente gRPC (ChirpStack)

**Frontend (React)**:
- `react-router-dom` - Roteamento
- `@tanstack/react-query` - Data fetching
- `axios` - HTTP client
- `react-hook-form` - Formulários
- `zod` - Validação
- `recharts` - Gráficos
- `leaflet` - Mapas

---

## 🤔 Próximos Passos

Agora que temos o mapa completo, vamos começar **passo a passo**:

### **Sugestão**: Começar pela Fase 1

1. **Criar a estrutura do projeto C#**
   - Solução, projetos (API, Core, Infrastructure)
   - Configurar appsettings.json

2. **Configurar banco de dados**
   - Connection string PostgreSQL
   - EF Core setup
   - Primeira migration (tenants, users)

3. **Implementar autenticação JWT**
   - Login endpoint
   - Geração de tokens
   - Middleware de autenticação

4. **Testar com Postman/curl**

5. **Criar tela de login no React**

---

**Você quer que eu comece pela Fase 1 ou prefere discutir algo específico primeiro?** 🚀

