# Automais IoT Platform - Backend (C#)

API REST para gerenciamento de plataforma IoT multi-tenant com integração ChirpStack.

## 🖥️ Servidor

**Servidor de Produção**: `srv01.automais.io`

- **ChirpStack**: http://srv01.automais.io:8080
- **EMQX Dashboard**: http://srv01.automais.io:18083
- **MQTT Broker**: mqtt://srv01.automais.io:1883

📖 **Documentação de Acesso**: Veja `ACESSO_SERVIDOR.md` e `CONFIGURACAO_SERVIDOR.md`

## 🏗️ Arquitetura

Seguimos **Clean Architecture** com separação de responsabilidades:

```
src/
├── Automais.Api/              # Controllers, Middlewares, Startup
├── Automais.Core/             # Entities, Interfaces, DTOs, Services
└── Automais.Infrastructure/   # EF Core, Repositories, ChirpStack Client
```

### Camadas:

- **Api**: Camada de apresentação (HTTP/REST)
- **Core**: Lógica de negócio e contratos (sem dependências externas)
- **Infrastructure**: Implementações concretas (banco, APIs externas)

## 🚀 Stack

- **.NET 8** - Framework
- **ASP.NET Core** - Web API
- **Entity Framework Core** - ORM
- **PostgreSQL** - Banco de dados
- **Grpc.Net.Client** - Comunicação com ChirpStack
- **FluentValidation** - Validação
- **AutoMapper** - Mapeamento de objetos

## 📦 Primeira Fase - MVP

Nesta primeira fase, implementamos:

### ✅ 1. Clientes (Tenants)
- Criar cliente
- Listar clientes
- Obter cliente por ID
- Atualizar cliente
- Desativar cliente

### ✅ 2. Gateways (ChirpStack)
- Listar gateways do ChirpStack (por tenant)
- Criar gateway no ChirpStack
- Obter detalhes de um gateway
- Atualizar gateway
- Deletar gateway

## 🗄️ Modelo de Dados (Fase 1)

### Tenant (Cliente)
```csharp
public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; }           // ex: "Acme Corporation"
    public string Slug { get; set; }           // ex: "acme-corp"
    public TenantStatus Status { get; set; }   // Active, Suspended, Deleted
    public string? ChirpstackTenantId { get; set; }  // ID do tenant no ChirpStack
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

### Gateway
```csharp
public class Gateway
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }         // FK para Tenant
    public string Name { get; set; }           // ex: "Gateway Matriz"
    public string GatewayEui { get; set; }     // ex: "0011223344556677"
    public string? Description { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public GatewayStatus Status { get; set; }  // Online, Offline, Maintenance
    public DateTime? LastSeenAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation
    public Tenant Tenant { get; set; }
}
```

## 🔌 Integração ChirpStack

O ChirpStack usa **gRPC** para sua API. Nos comunicamos com ele através de:

1. **API gRPC**: Para criar/listar/atualizar gateways
2. **Tenant Isolation**: Cada tenant tem um `TenantId` no ChirpStack

### Endpoints ChirpStack usados:
- `gateway_service.proto` - Gestão de gateways
- `tenant_service.proto` - Gestão de tenants (futuramente)

## 📋 APIs REST (Fase 1)

### Tenants (Clientes)

```http
GET    /api/tenants              # Listar todos
POST   /api/tenants              # Criar novo
GET    /api/tenants/{id}         # Obter por ID
PUT    /api/tenants/{id}         # Atualizar
DELETE /api/tenants/{id}         # Desativar
```

**Exemplo - Criar Tenant:**
```json
POST /api/tenants
{
  "name": "Acme Corporation",
  "slug": "acme-corp"
}
```

**Resposta:**
```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "name": "Acme Corporation",
  "slug": "acme-corp",
  "status": "active",
  "chirpstackTenantId": "chirpstack-tenant-id",
  "createdAt": "2024-10-30T10:00:00Z",
  "updatedAt": "2024-10-30T10:00:00Z"
}
```

### Gateways

```http
GET    /api/tenants/{tenantId}/gateways           # Listar gateways do tenant
POST   /api/tenants/{tenantId}/gateways           # Criar gateway
GET    /api/gateways/{id}                         # Obter por ID
PUT    /api/gateways/{id}                         # Atualizar
DELETE /api/gateways/{id}                         # Deletar
GET    /api/gateways/{id}/stats                   # Estatísticas do gateway
```

**Exemplo - Criar Gateway:**
```json
POST /api/tenants/{tenantId}/gateways
{
  "name": "Gateway Matriz",
  "gatewayEui": "0011223344556677",
  "description": "Gateway principal da matriz",
  "latitude": -23.5505,
  "longitude": -46.6333
}
```

**Resposta:**
```json
{
  "id": "456e7890-e89b-12d3-a456-426614174111",
  "tenantId": "123e4567-e89b-12d3-a456-426614174000",
  "name": "Gateway Matriz",
  "gatewayEui": "0011223344556677",
  "description": "Gateway principal da matriz",
  "latitude": -23.5505,
  "longitude": -46.6333,
  "status": "offline",
  "lastSeenAt": null,
  "createdAt": "2024-10-30T10:05:00Z",
  "updatedAt": "2024-10-30T10:05:00Z"
}
```

## ⚙️ Configuração

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=automais_iot;Username=postgres;Password=postgres"
  },
  "ChirpStack": {
    "ApiUrl": "http://localhost:8080",
    "ApiToken": "seu-token-aqui"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### Variáveis de Ambiente (.env)

```bash
ASPNETCORE_ENVIRONMENT=Development
DATABASE_CONNECTION=Host=localhost;Database=automais_iot;Username=postgres;Password=postgres
CHIRPSTACK_API_URL=http://localhost:8080
CHIRPSTACK_API_TOKEN=seu-token-aqui
```

## 🚀 Como Executar

### 1. Pré-requisitos

- .NET 8 SDK
- PostgreSQL 15+
- ChirpStack rodando (com API acessível)

### 2. Configurar Banco de Dados

```bash
# Criar banco
createdb automais_iot

# Aplicar migrations
cd src/Automais.Api
dotnet ef database update
```

### 3. Executar API

```bash
cd src/Automais.Api
dotnet run
```

API estará disponível em: `http://localhost:5000`  
Swagger: `http://localhost:5000/swagger`

## 📊 Migrations

```bash
# Criar nova migration
dotnet ef migrations add NomeDaMigration --project src/Automais.Infrastructure --startup-project src/Automais.Api

# Aplicar migrations
dotnet ef database update --project src/Automais.Infrastructure --startup-project src/Automais.Api

# Reverter última migration
dotnet ef migrations remove --project src/Automais.Infrastructure --startup-project src/Automais.Api
```

## 🧪 Testando

### Com Swagger
Acesse `http://localhost:5000/swagger` e teste diretamente pela interface.

### Com cURL

```bash
# Criar tenant
curl -X POST http://localhost:5000/api/tenants \
  -H "Content-Type: application/json" \
  -d '{"name":"Acme Corp","slug":"acme-corp"}'

# Listar tenants
curl http://localhost:5000/api/tenants

# Criar gateway
curl -X POST http://localhost:5000/api/tenants/{tenantId}/gateways \
  -H "Content-Type: application/json" \
  -d '{"name":"Gateway 1","gatewayEui":"0011223344556677"}'

# Listar gateways do tenant
curl http://localhost:5000/api/tenants/{tenantId}/gateways
```

## 📁 Estrutura Detalhada

```
src/
├── Automais.Api/
│   ├── Controllers/
│   │   ├── TenantsController.cs      # CRUD de tenants
│   │   └── GatewaysController.cs     # CRUD de gateways
│   ├── Middlewares/
│   │   └── ExceptionMiddleware.cs    # Tratamento global de erros
│   ├── appsettings.json
│   ├── Program.cs                    # Configuração da app
│   └── Automais.Api.csproj
│
├── Automais.Core/
│   ├── Entities/
│   │   ├── Tenant.cs                 # Entidade Tenant
│   │   └── Gateway.cs                # Entidade Gateway
│   ├── Enums/
│   │   ├── TenantStatus.cs
│   │   └── GatewayStatus.cs
│   ├── DTOs/
│   │   ├── TenantDto.cs
│   │   ├── CreateTenantDto.cs
│   │   ├── GatewayDto.cs
│   │   └── CreateGatewayDto.cs
│   ├── Interfaces/
│   │   ├── ITenantRepository.cs
│   │   ├── IGatewayRepository.cs
│   │   ├── ITenantService.cs
│   │   ├── IGatewayService.cs
│   │   └── IChirpStackClient.cs
│   ├── Services/
│   │   ├── TenantService.cs          # Lógica de negócio - Tenants
│   │   └── GatewayService.cs         # Lógica de negócio - Gateways
│   └── Automais.Core.csproj
│
└── Automais.Infrastructure/
    ├── Data/
    │   ├── ApplicationDbContext.cs   # EF Core Context
    │   └── Migrations/               # Migrations do EF Core
    ├── Repositories/
    │   ├── TenantRepository.cs       # Acesso a dados - Tenants
    │   └── GatewayRepository.cs      # Acesso a dados - Gateways
    ├── ChirpStack/
    │   ├── ChirpStackClient.cs       # Cliente gRPC ChirpStack
    │   └── Protos/                   # Arquivos .proto
    └── Automais.Infrastructure.csproj
```

## 🔄 Próximos Passos (Fase 2)

Após termos Tenants e Gateways funcionando:

1. **Autenticação JWT**
2. **Applications e Devices**
3. **MQTT e Telemetria**
4. **WireGuard**

## 📚 Referências

- [ChirpStack API Documentation](https://www.chirpstack.io/docs/chirpstack/api/grpc.html)
- [Entity Framework Core](https://docs.microsoft.com/ef/core/)
- [ASP.NET Core](https://docs.microsoft.com/aspnet/core/)

