# 📁 Estrutura do Projeto - Automais IoT Platform

## 🎯 Visão Geral

Projeto backend em **C# (.NET 8)** seguindo **Clean Architecture** para uma plataforma IoT multi-tenant.

**Fase Atual**: MVP com Tenants e Gateways (integração ChirpStack)

---

## 📂 Estrutura de Diretórios

```
server.io/
├── src/
│   ├── Automais.Api/              # 🌐 Camada de Apresentação (API REST)
│   │   ├── Controllers/
│   │   │   ├── TenantsController.cs      # CRUD de Tenants
│   │   │   └── GatewaysController.cs     # CRUD de Gateways
│   │   ├── Properties/
│   │   │   └── launchSettings.json       # Configuração de execução
│   │   ├── appsettings.json              # Configurações (DB, ChirpStack)
│   │   ├── appsettings.Development.json  # Configurações de dev
│   │   ├── Program.cs                    # Entry point e DI
│   │   └── Automais.Api.csproj
│   │
│   ├── Automais.Core/             # 🧠 Lógica de Negócio (Domain Layer)
│   │   ├── Entities/
│   │   │   ├── Tenant.cs                 # Entidade Tenant
│   │   │   └── Gateway.cs                # Entidade Gateway
│   │   ├── DTOs/
│   │   │   ├── TenantDto.cs              # DTOs de Tenant
│   │   │   └── GatewayDto.cs             # DTOs de Gateway
│   │   ├── Interfaces/
│   │   │   ├── ITenantRepository.cs      # Contrato de acesso a dados
│   │   │   ├── IGatewayRepository.cs
│   │   │   ├── ITenantService.cs         # Contrato de serviço
│   │   │   ├── IGatewayService.cs
│   │   │   └── IChirpStackClient.cs      # Contrato de integração
│   │   ├── Services/
│   │   │   ├── TenantService.cs          # Lógica de negócio - Tenants
│   │   │   └── GatewayService.cs         # Lógica de negócio - Gateways
│   │   └── Automais.Core.csproj
│   │
│   └── Automais.Infrastructure/   # 🔧 Implementações (Data + External APIs)
│       ├── Data/
│       │   ├── ApplicationDbContext.cs   # EF Core Context
│       │   └── Migrations/               # Migrations do banco
│       ├── Repositories/
│       │   ├── TenantRepository.cs       # Implementação EF Core
│       │   └── GatewayRepository.cs
│       ├── ChirpStack/
│       │   └── ChirpStackClient.cs       # Cliente gRPC (mock por enquanto)
│       └── Automais.Infrastructure.csproj
│
├── Automais.sln                   # Solution do Visual Studio
├── .gitignore
├── README.md                      # Documentação principal
├── ARQUITETURA.md                 # Arquitetura completa da plataforma
├── GETTING_STARTED.md             # Guia de início rápido
└── ESTRUTURA_PROJETO.md           # Este arquivo
```

---

## 📦 Dependências

### Automais.Api
- `Microsoft.EntityFrameworkCore.Design` - Ferramentas EF Core
- `Swashbuckle.AspNetCore` - Swagger/OpenAPI

### Automais.Core
- `FluentValidation` - Validação de DTOs (preparado para uso futuro)

### Automais.Infrastructure
- `Microsoft.EntityFrameworkCore` - ORM
- `Npgsql.EntityFrameworkCore.PostgreSQL` - Provider PostgreSQL
- `EFCore.NamingConventions` - Snake case no banco
- `Grpc.Net.Client` - Cliente gRPC (ChirpStack)
- `Google.Protobuf` - Serialização Protocol Buffers
- `Grpc.Tools` - Ferramentas para compilar .proto

---

## 🗄️ Banco de Dados

**PostgreSQL** com convenção **snake_case**.

### Tabelas

#### `tenants`
```
id                     UUID PRIMARY KEY
name                   VARCHAR(100) NOT NULL
slug                   VARCHAR(50) NOT NULL UNIQUE
status                 VARCHAR(20) NOT NULL
chirp_stack_tenant_id  VARCHAR(100)
metadata               TEXT
created_at             TIMESTAMP NOT NULL
updated_at             TIMESTAMP NOT NULL
```

#### `gateways`
```
id             UUID PRIMARY KEY
tenant_id      UUID NOT NULL REFERENCES tenants(id)
name           VARCHAR(100) NOT NULL
gateway_eui    VARCHAR(16) NOT NULL UNIQUE
description    VARCHAR(500)
latitude       DOUBLE PRECISION
longitude      DOUBLE PRECISION
altitude       DOUBLE PRECISION
status         VARCHAR(20) NOT NULL
last_seen_at   TIMESTAMP
metadata       TEXT
created_at     TIMESTAMP NOT NULL
updated_at     TIMESTAMP NOT NULL
```

### Índices
- `tenants.slug` - UNIQUE
- `gateways.gateway_eui` - UNIQUE
- `gateways.tenant_id` - INDEX

---

## 🔌 APIs REST

### Base URL
```
http://localhost:5000/api
```

### Endpoints Implementados

#### **Tenants**
| Método | Endpoint | Descrição |
|--------|----------|-----------|
| GET | `/tenants` | Lista todos os tenants |
| GET | `/tenants/{id}` | Obtém tenant por ID |
| GET | `/tenants/by-slug/{slug}` | Obtém tenant por slug |
| POST | `/tenants` | Cria novo tenant |
| PUT | `/tenants/{id}` | Atualiza tenant |
| DELETE | `/tenants/{id}` | Desativa tenant |

#### **Gateways**
| Método | Endpoint | Descrição |
|--------|----------|-----------|
| GET | `/tenants/{tenantId}/gateways` | Lista gateways do tenant |
| GET | `/gateways/{id}` | Obtém gateway por ID |
| GET | `/gateways/{id}/stats` | Estatísticas do gateway |
| POST | `/tenants/{tenantId}/gateways` | Cria gateway |
| POST | `/tenants/{tenantId}/gateways/sync` | Sincroniza com ChirpStack |
| PUT | `/gateways/{id}` | Atualiza gateway |
| DELETE | `/gateways/{id}` | Deleta gateway |

---

## 🏛️ Padrões Arquiteturais

### Clean Architecture

#### 1️⃣ **Api Layer** (Controllers)
- Recebe requisições HTTP
- Valida entrada básica
- Chama Services
- Retorna respostas HTTP

```csharp
[HttpPost]
public async Task<ActionResult<TenantDto>> Create(CreateTenantDto dto)
{
    var tenant = await _tenantService.CreateAsync(dto);
    return CreatedAtAction(nameof(GetById), new { id = tenant.Id }, tenant);
}
```

#### 2️⃣ **Core Layer** (Entities, DTOs, Interfaces, Services)
- **Sem dependências externas**
- Contém lógica de negócio
- Define contratos (interfaces)

```csharp
public interface ITenantService
{
    Task<TenantDto> CreateAsync(CreateTenantDto dto);
    // ... outros métodos
}
```

#### 3️⃣ **Infrastructure Layer** (Repositories, External Clients)
- Implementa interfaces do Core
- Acessa banco de dados (EF Core)
- Integra com APIs externas (ChirpStack)

```csharp
public class TenantRepository : ITenantRepository
{
    private readonly ApplicationDbContext _context;
    // ... implementação
}
```

### Dependency Injection

Configurado no `Program.cs`:

```csharp
// Repositories
builder.Services.AddScoped<ITenantRepository, TenantRepository>();
builder.Services.AddScoped<IGatewayRepository, GatewayRepository>();

// Services
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<IGatewayService, GatewayService>();

// External Clients
builder.Services.AddSingleton<IChirpStackClient>(sp => 
    new ChirpStackClient(chirpStackUrl, chirpStackToken));
```

---

## 🔄 Fluxo de Requisição

```
1. HTTP Request
   ↓
2. Controller (Api Layer)
   ↓
3. Service (Core Layer)
   ├─→ Repository (Infrastructure → Database)
   └─→ ChirpStackClient (Infrastructure → ChirpStack gRPC)
   ↓
4. Retorna DTO
   ↓
5. HTTP Response
```

**Exemplo**: Criar Gateway

```
POST /api/tenants/{id}/gateways

GatewaysController.Create()
  ↓
GatewayService.CreateAsync()
  ├─→ TenantRepository.GetByIdAsync() ✅ valida tenant
  ├─→ GatewayRepository.EuiExistsAsync() ✅ valida EUI único
  ├─→ ChirpStackClient.CreateGatewayAsync() ✅ cria no ChirpStack
  └─→ GatewayRepository.CreateAsync() ✅ salva no banco
  ↓
Retorna GatewayDto
```

---

## 🧪 Como Testar

### 1. Via Swagger (Recomendado)
```
http://localhost:5000/swagger
```

### 2. Via cURL
```bash
curl -X POST http://localhost:5000/api/tenants \
  -H "Content-Type: application/json" \
  -d '{"name":"Teste","slug":"teste"}'
```

### 3. Via PowerShell
```powershell
Invoke-RestMethod -Method Post -Uri "http://localhost:5000/api/tenants" `
    -ContentType "application/json" -Body '{"name":"Teste","slug":"teste"}'
```

---

## 🎓 Conceitos Importantes

### 1. **Clean Architecture**
Separação em camadas com dependências unidirecionais:
```
Api → Core ← Infrastructure
(Core não depende de nada)
```

### 2. **Repository Pattern**
Abstração do acesso a dados:
```csharp
// Interface no Core
public interface ITenantRepository { ... }

// Implementação na Infrastructure
public class TenantRepository : ITenantRepository { ... }
```

### 3. **DTO (Data Transfer Object)**
Objetos para transferir dados entre camadas:
```csharp
// Input DTO (receber dados)
public class CreateTenantDto { ... }

// Output DTO (retornar dados)
public class TenantDto { ... }
```

### 4. **Entity Framework Core**
ORM que mapeia objetos C# para tabelas SQL:
```csharp
public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    // ... será mapeado para tabela 'tenants'
}
```

### 5. **Migrations**
Versionamento do banco de dados:
```bash
# Criar migration
dotnet ef migrations add NomeDaMigration

# Aplicar no banco
dotnet ef database update
```

---

## ✅ O que está Funcionando

- ✅ Estrutura de 3 camadas (Api, Core, Infrastructure)
- ✅ Banco de dados PostgreSQL com EF Core
- ✅ CRUD completo de Tenants
- ✅ CRUD completo de Gateways
- ✅ Relacionamento Tenant ↔ Gateways (1:N)
- ✅ Swagger para testar APIs
- ✅ Logs estruturados
- ✅ CORS para frontend
- ✅ Health check endpoint
- ✅ ChirpStack Client (mock - preparado para integração real)

---

## 🔜 Próximos Passos (Futuras Fases)

### Fase 2: Integração Real com ChirpStack
- [ ] Adicionar arquivos `.proto` do ChirpStack
- [ ] Implementar chamadas gRPC reais
- [ ] Testar com ChirpStack rodando

### Fase 3: Autenticação
- [ ] JWT Tokens
- [ ] User management
- [ ] RBAC (Roles)

### Fase 4: Applications e Devices
- [ ] CRUD de Applications
- [ ] CRUD de Devices
- [ ] Provisioning no ChirpStack

### Fase 5: Telemetria
- [ ] MQTT consumer
- [ ] Armazenar mensagens dos devices
- [ ] APIs de consulta de telemetria

---

## 📚 Recursos

- **Documentação Completa**: `ARQUITETURA.md`
- **Guia de Início**: `GETTING_STARTED.md`
- **Código Limpo**: Cada camada tem responsabilidade clara
- **Comentários**: Código documentado em português

---

**Projeto criado com foco em clareza e facilidade de entendimento!** 🎯

