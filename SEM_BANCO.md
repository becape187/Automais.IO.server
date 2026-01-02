# 🚀 Rodando SEM Banco de Dados

## ✅ Modo In-Memory

A aplicação foi configurada para rodar **sem PostgreSQL**, usando repositórios em memória.

### O que isso significa?

- ✅ **Não precisa de PostgreSQL**
- ✅ **Roda imediatamente** (sem migrations)
- ✅ **Dados em memória** (perdidos ao reiniciar)
- ✅ **Perfeito para testar ChirpStack**

---

## 🎯 Como Usar

### 1. Configurar Token do ChirpStack

Edite: `src/Automais.Api/appsettings.json`

```json
{
  "ChirpStack": {
    "ApiUrl": "http://srv01.automais.io:8080",
    "ApiToken": "COLE_SEU_TOKEN_AQUI"
  }
}
```

**Como obter o token?**
1. Acesse http://srv01.automais.io:8080
2. Faça login
3. Menu lateral → "API keys"
4. "Create" → Nome: "Automais Platform"
5. Copie o token gerado

---

### 2. Rodar a API

```bash
cd src/Automais.Api
dotnet run
```

**Você verá:**
```
🔗 ChirpStack URL: http://srv01.automais.io:8080
🔑 Token configurado: Sim ✅
🚀 API rodando!
📝 Swagger: http://localhost:5000
❤️  Health: http://localhost:5000/health
💾 Modo: IN-MEMORY (sem banco de dados)
📡 ChirpStack: http://srv01.automais.io:8080
```

---

### 3. Acessar Swagger

Abra seu navegador em:
```
http://localhost:5000
```

Ou diretamente:
```
http://localhost:5000/swagger
```

---

## 🧪 Testar

### 1. Health Check

```bash
curl http://localhost:5000/health
```

**Resposta:**
```json
{
  "status": "healthy",
  "mode": "in-memory",
  "database": "disabled",
  "chirpstack": "http://srv01.automais.io:8080",
  "timestamp": "2024-10-31T15:30:00Z"
}
```

### 2. Criar Tenant (em memória)

```bash
curl -X POST http://localhost:5000/api/tenants \
  -H "Content-Type: application/json" \
  -d '{"name":"Acme Corp","slug":"acme-corp"}'
```

### 3. Criar Gateway (no ChirpStack)

**Primeiro, pegue o ID do tenant criado acima**

```bash
curl -X POST http://localhost:5000/api/tenants/{tenantId}/gateways \
  -H "Content-Type: application/json" \
  -d '{
    "name":"Gateway Teste",
    "gatewayEui":"0011223344556677",
    "latitude":-23.5505,
    "longitude":-46.6333
  }'
```

### 4. Listar Gateways do Tenant

```bash
curl http://localhost:5000/api/tenants/{tenantId}/gateways
```

---

## ⚠️ Limitações do Modo In-Memory

### ❌ O que NÃO funciona:
- Dados **não persistem** ao reiniciar a API
- **Sem consultas SQL complexas**
- **Sem relacionamentos complexos**

### ✅ O que FUNCIONA:
- Todas as APIs REST
- CRUD de Tenants (em memória)
- CRUD de Gateways (em memória + ChirpStack)
- Integração com ChirpStack
- Swagger totalmente funcional

---

## 🔄 Mudar para Banco de Dados Depois

Quando quiser usar PostgreSQL de verdade:

### 1. Instalar e configurar PostgreSQL

### 2. Editar `Program.cs`

Trocar:
```csharp
// Repositories IN MEMORY
builder.Services.AddSingleton<ITenantRepository, InMemoryTenantRepository>();
builder.Services.AddSingleton<IGatewayRepository, InMemoryGatewayRepository>();
```

Por:
```csharp
// Database - PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(connectionString);
    options.UseSnakeCaseNamingConvention();
});

// Repositories com banco
builder.Services.AddScoped<ITenantRepository, TenantRepository>();
builder.Services.AddScoped<IGatewayRepository, GatewayRepository>();
```

### 3. Aplicar migrations

```bash
dotnet ef database update --project ../Automais.Infrastructure
```

---

## 📝 Arquivos Modificados

- ✅ `Program.cs` - Removido DbContext, usando InMemory
- ✅ `InMemoryTenantRepository.cs` - Novo repositório em memória
- ✅ `InMemoryGatewayRepository.cs` - Novo repositório em memória

---

## 🎯 Próximos Passos

1. ✅ Configurar token do ChirpStack
2. ✅ Rodar `dotnet run`
3. ✅ Abrir http://localhost:5000
4. ✅ Criar tenants e gateways via Swagger
5. ✅ Verificar se gateways aparecem no ChirpStack

---

## 💡 Dicas

### Ver logs do ChirpStack Client

O `ChirpStackClient` imprime logs no console quando faz operações:

```
[ChirpStack Mock] Criando gateway Gateway Teste (0011223344556677) no tenant ...
[ChirpStack Mock] Atualizando gateway 0011223344556677
[ChirpStack Mock] Deletando gateway 0011223344556677
```

### Dados de exemplo

Ao reiniciar, os dados são perdidos. Para popular dados de teste, você pode:

1. **Via Swagger** - Criar manualmente
2. **Via script** - Criar um `seed.http` com requests
3. **Via código** - Adicionar dados no startup do `Program.cs`

**Exemplo de seed no Program.cs:**

```csharp
// Seed data para desenvolvimento
if (app.Environment.IsDevelopment())
{
    var tenantRepo = app.Services.GetRequiredService<ITenantRepository>();
    
    var tenant = new Tenant
    {
        Id = Guid.NewGuid(),
        Name = "Tenant de Teste",
        Slug = "teste",
        Status = TenantStatus.Active,
        ChirpStackTenantId = "test-tenant-id",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
    
    await tenantRepo.CreateAsync(tenant);
    Console.WriteLine($"✅ Tenant de teste criado: {tenant.Id}");
}
```

---

**Pronto! Agora você pode rodar sem banco de dados e focar na integração com ChirpStack!** 🎉

