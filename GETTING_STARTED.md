# 🚀 Guia de Início Rápido - Automais IoT Platform (Backend)

## 📋 Pré-requisitos

1. **.NET 8 SDK** instalado
   ```bash
   dotnet --version
   # Deve retornar: 8.0.x
   ```

2. **PostgreSQL** rodando
   - Host: `localhost`
   - Porta: `5432`
   - Usuário: `postgres`
   - Senha: `postgres`

3. **(Opcional) ChirpStack** rodando para integração real
   - URL padrão: `http://localhost:8080`

## 🛠️ Configuração Inicial

### 1. Criar o Banco de Dados

```bash
# Windows (PowerShell)
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" -U postgres -c "CREATE DATABASE automais_iot;"

# Linux/Mac
psql -U postgres -c "CREATE DATABASE automais_iot;"

# Ou use pgAdmin / DBeaver para criar manualmente
```

### 2. Restaurar Dependências

```bash
cd server.io
dotnet restore
```

### 3. Aplicar Migrations (Criar Tabelas)

```bash
cd src/Automais.Api

# Criar migration inicial
dotnet ef migrations add InitialCreate --project ../Automais.Infrastructure --startup-project .

# Aplicar migrations no banco
dotnet ef database update --project ../Automais.Infrastructure --startup-project .
```

Se tudo correr bem, você verá as tabelas criadas:
- `tenants`
- `gateways`
- `__EFMigrationsHistory`

## ▶️ Executar a API

```bash
cd src/Automais.Api
dotnet run
```

A API estará disponível em:
- **HTTP**: http://localhost:5000
- **Swagger**: http://localhost:5000/swagger
- **Health Check**: http://localhost:5000/health

## 🧪 Testar a API

### Usando Swagger (Recomendado para começar)

1. Acesse: http://localhost:5000/swagger
2. Expanda os endpoints e clique em "Try it out"
3. Preencha os dados e clique em "Execute"

### Usando cURL

#### 1. Criar um Tenant (Cliente)

```bash
curl -X POST http://localhost:5000/api/tenants \
  -H "Content-Type: application/json" \
  -d "{\"name\":\"Acme Corporation\",\"slug\":\"acme-corp\"}"
```

**Resposta esperada:**
```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "name": "Acme Corporation",
  "slug": "acme-corp",
  "status": "Active",
  "chirpStackTenantId": "a1b2c3d4-...",
  "createdAt": "2024-10-30T14:30:00Z",
  "updatedAt": "2024-10-30T14:30:00Z"
}
```

**Copie o `id` retornado para usar nos próximos comandos!**

#### 2. Listar Todos os Tenants

```bash
curl http://localhost:5000/api/tenants
```

#### 3. Obter Tenant por ID

```bash
curl http://localhost:5000/api/tenants/123e4567-e89b-12d3-a456-426614174000
```

#### 4. Criar um Gateway

**Substitua `{tenantId}` pelo ID do tenant criado acima:**

```bash
curl -X POST http://localhost:5000/api/tenants/{tenantId}/gateways \
  -H "Content-Type: application/json" \
  -d "{\"name\":\"Gateway Matriz\",\"gatewayEui\":\"0011223344556677\",\"description\":\"Gateway principal\",\"latitude\":-23.5505,\"longitude\":-46.6333}"
```

**Resposta esperada:**
```json
{
  "id": "456e7890-e89b-12d3-a456-426614174111",
  "tenantId": "123e4567-e89b-12d3-a456-426614174000",
  "name": "Gateway Matriz",
  "gatewayEui": "0011223344556677",
  "description": "Gateway principal",
  "latitude": -23.5505,
  "longitude": -46.6333,
  "status": "Offline",
  "lastSeenAt": null,
  "createdAt": "2024-10-30T14:35:00Z",
  "updatedAt": "2024-10-30T14:35:00Z"
}
```

#### 5. Listar Gateways do Tenant

```bash
curl http://localhost:5000/api/tenants/{tenantId}/gateways
```

#### 6. Obter Estatísticas de um Gateway

```bash
curl http://localhost:5000/api/gateways/{gatewayId}/stats
```

#### 7. Atualizar um Gateway

```bash
curl -X PUT http://localhost:5000/api/gateways/{gatewayId} \
  -H "Content-Type: application/json" \
  -d "{\"name\":\"Gateway Matriz - Atualizado\",\"status\":\"Online\"}"
```

#### 8. Deletar um Gateway

```bash
curl -X DELETE http://localhost:5000/api/gateways/{gatewayId}
```

### Usando PowerShell (Windows)

```powershell
# Criar Tenant
$body = @{
    name = "Acme Corporation"
    slug = "acme-corp"
} | ConvertTo-Json

Invoke-RestMethod -Method Post -Uri "http://localhost:5000/api/tenants" `
    -ContentType "application/json" -Body $body

# Listar Tenants
Invoke-RestMethod -Method Get -Uri "http://localhost:5000/api/tenants"
```

## 📊 Verificar no Banco de Dados

### Usando psql

```bash
psql -U postgres -d automais_iot

-- Listar tenants
SELECT * FROM tenants;

-- Listar gateways
SELECT * FROM gateways;

-- Ver gateways com nome do tenant
SELECT 
    g.name as gateway_name,
    g.gateway_eui,
    g.status,
    t.name as tenant_name
FROM gateways g
JOIN tenants t ON g.tenant_id = t.id;
```

### Usando DBeaver / pgAdmin

1. Conectar ao banco `automais_iot`
2. Navegar até as tabelas `tenants` e `gateways`
3. Ver os dados inseridos

## 🔍 Estrutura do Banco

```sql
-- Tabela: tenants
CREATE TABLE tenants (
    id UUID PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    slug VARCHAR(50) NOT NULL UNIQUE,
    status VARCHAR(20) NOT NULL,
    chirp_stack_tenant_id VARCHAR(100),
    metadata TEXT,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NOT NULL
);

-- Tabela: gateways
CREATE TABLE gateways (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    name VARCHAR(100) NOT NULL,
    gateway_eui VARCHAR(16) NOT NULL UNIQUE,
    description VARCHAR(500),
    latitude DOUBLE PRECISION,
    longitude DOUBLE PRECISION,
    altitude DOUBLE PRECISION,
    status VARCHAR(20) NOT NULL,
    last_seen_at TIMESTAMP,
    metadata TEXT,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NOT NULL
);

CREATE INDEX idx_gateways_tenant_id ON gateways(tenant_id);
CREATE INDEX idx_gateways_gateway_eui ON gateways(gateway_eui);
```

## 🎯 Próximos Passos

Agora que você tem o backend funcionando:

1. ✅ **Testar todos os endpoints** no Swagger
2. ✅ **Criar múltiplos tenants e gateways**
3. ✅ **Verificar os dados no banco**
4. 🔄 **Integrar com ChirpStack real** (próxima fase)
5. 🎨 **Conectar com o frontend React**

## ❓ Troubleshooting

### Erro: "Cannot connect to PostgreSQL"

- Verifique se o PostgreSQL está rodando
- Verifique as credenciais no `appsettings.json`
- Teste a conexão:
  ```bash
  psql -U postgres -h localhost -p 5432
  ```

### Erro: "The type or namespace name 'Microsoft' could not be found"

```bash
dotnet restore
dotnet build
```

### Erro: "No migrations were applied"

```bash
cd src/Automais.Api
dotnet ef database update --project ../Automais.Infrastructure --startup-project .
```

### Ver logs detalhados

```bash
dotnet run --verbosity detailed
```

## 🔧 Comandos Úteis

```bash
# Ver migrations aplicadas
dotnet ef migrations list --project src/Automais.Infrastructure --startup-project src/Automais.Api

# Reverter última migration
dotnet ef migrations remove --project src/Automais.Infrastructure --startup-project src/Automais.Api

# Recriar banco do zero
dotnet ef database drop --project src/Automais.Infrastructure --startup-project src/Automais.Api
dotnet ef database update --project src/Automais.Infrastructure --startup-project src/Automais.Api

# Build em modo Release
dotnet build -c Release

# Publicar aplicação
dotnet publish -c Release -o ./publish
```

## 📝 Notas Importantes

1. **ChirpStack Mock**: Por enquanto, o `ChirpStackClient` é um mock (não faz chamadas reais). Você verá mensagens no console quando "criar/deletar" no ChirpStack.

2. **Migrations Automáticas**: Em Development, as migrations são aplicadas automaticamente ao iniciar a API.

3. **CORS**: CORS está habilitado para `localhost:3000` e `localhost:5173` (frontend).

4. **Dados de Teste**: Use o Swagger ou cURL para criar dados de teste.

---

**Pronto!** 🎉 Agora você tem uma API REST funcional para gerenciar Tenants e Gateways!

Qualquer dúvida, consulte o arquivo `ARQUITETURA.md` para entender a estrutura completa.

