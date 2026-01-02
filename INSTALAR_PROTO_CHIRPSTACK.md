# 📥 Instalar Arquivos .proto do ChirpStack

## 🎯 Repositório Oficial

Os arquivos `.proto` do ChirpStack estão disponíveis em:
**https://github.com/chirpstack/chirpstack/tree/master/api/proto**

---

## 🚀 Método Rápido (PowerShell)

### **Opção 1: Script Automático (Recomendado)**

Execute o script PowerShell:

```powershell
cd server.io
.\baixar-proto-chirpstack.ps1
```

O script vai:
1. ✅ Criar a estrutura de pastas
2. ✅ Baixar todos os arquivos `.proto` necessários
3. ✅ Organizar na estrutura correta

---

## 📋 Estrutura Esperada

Após executar o script, você terá:

```
src/Automais.Infrastructure/
└── ChirpStack/
    └── Protos/
        ├── api/
        │   ├── gateway.proto          ⭐ Principal para gateways
        │   ├── tenant.proto           ⭐ Principal para tenants
        │   ├── application.proto
        │   ├── device.proto
        │   ├── internal.proto
        │   └── user.proto
        ├── common/
        │   └── common.proto           ⭐ Tipos comuns (Location, etc)
        └── google/
            └── api/
                ├── annotations.proto  ⭐ Para gRPC annotations
                └── http.proto
```

---

## 🔧 Método Manual (Alternativo)

Se preferir baixar manualmente:

### 1. Acessar GitHub

Acesse: https://github.com/chirpstack/chirpstack/tree/master/api/proto

### 2. Baixar Arquivos Principais

Para nossa implementação inicial, precisamos:

#### **Essenciais:**
- `api/gateway.proto` - https://raw.githubusercontent.com/chirpstack/chirpstack/master/api/proto/api/gateway.proto
- `api/tenant.proto` - https://raw.githubusercontent.com/chirpstack/chirpstack/master/api/proto/api/tenant.proto
- `common/common.proto` - https://raw.githubusercontent.com/chirpstack/chirpstack/master/api/proto/common/common.proto

#### **Opcionais (mas recomendados):**
- `google/api/annotations.proto` - Para annotations gRPC
- `google/api/http.proto` - Para HTTP mappings

### 3. Criar Estrutura

```powershell
cd server.io/src/Automais.Infrastructure

# Criar pastas
mkdir -p ChirpStack/Protos/api
mkdir -p ChirpStack/Protos/common
mkdir -p ChirpStack/Protos/google/api

# Colocar os arquivos .proto nas pastas correspondentes
```

---

## ✅ Validar Instalação

### 1. Compilar o Projeto

```bash
cd src/Automais.Infrastructure
dotnet build
```

**Se compilar sem erros**, os clientes gRPC foram gerados! ✅

**Se houver erros**, verifique:
- Arquivos `.proto` estão nas pastas corretas?
- Estrutura de pastas está correta?
- Todos os imports estão resolvidos?

### 2. Verificar Arquivos Gerados

Após compilar, você verá arquivos gerados automaticamente (geralmente ocultos):

```
bin/
└── Debug/
    └── net8.0/
        └── (arquivos .cs gerados dos .proto)
```

### 3. Verificar Namespaces

Os clientes serão gerados com namespace baseado nos `.proto`.

Exemplo típico:
```csharp
using ChirpStack.Api; // ou
using Api; // dependendo do package nos .proto
```

---

## 🔄 Ativar Integração Real

### 1. Abrir ChirpStackClient.cs

Arquivo: `src/Automais.Infrastructure/ChirpStack/ChirpStackClient.cs`

### 2. Descomentar Código

Em cada método, descomente o bloco `/* ... */` e remova o código temporário.

### 3. Ajustar Namespaces (se necessário)

Se os namespaces gerados forem diferentes, ajuste:

```csharp
// Exemplo:
using Api = ChirpStack.Api.Gateway;
// ou
using TenantApi = ChirpStack.Api.Tenant;
```

### 4. Compilar Novamente

```bash
dotnet build
```

---

## 🧪 Testar Integração

### 1. Configurar Token

Edite `src/Automais.Api/appsettings.json`:

```json
{
  "ChirpStack": {
    "ApiUrl": "http://srv01.automais.io:8080",
    "ApiToken": "SEU_TOKEN_AQUI"
  }
}
```

### 2. Rodar API

```bash
cd src/Automais.Api
dotnet run
```

### 3. Testar no Swagger

Acesse: http://localhost:5000

1. **POST /api/tenants** - Criar tenant
2. **POST /api/tenants/{id}/gateways** - Criar gateway

Verifique os logs no console para ver as chamadas gRPC reais!

---

## 📚 Arquivos .proto Importantes

### **gateway.proto**
Define:
- `GatewayService` - Serviço gRPC para gateways
- `Gateway` - Estrutura de gateway
- `ListGatewaysRequest/Response`
- `CreateGatewayRequest`
- `UpdateGatewayRequest`
- `DeleteGatewayRequest`
- `GetGatewayRequest/Response`
- `GetGatewayStatsRequest/Response`

### **tenant.proto**
Define:
- `TenantService` - Serviço gRPC para tenants
- `Tenant` - Estrutura de tenant
- `CreateTenantRequest/Response`
- `GetTenantRequest/Response`
- `UpdateTenantRequest`
- `DeleteTenantRequest`
- `ListTenantsRequest/Response`

### **common.proto**
Define:
- `Location` - Coordenadas GPS
- `KeyValue` - Pares chave-valor
- Outros tipos comuns

---

## ⚠️ Troubleshooting

### Erro: "Cannot find proto files"

**Causa**: Arquivos não estão na pasta correta

**Solução**: 
```bash
# Verificar estrutura
ls -R src/Automais.Infrastructure/ChirpStack/Protos/
```

### Erro: "Unknown import 'common/common.proto'"

**Causa**: Arquivo `common.proto` não encontrado ou path errado

**Solução**: Verifique se `common.proto` está em `Protos/common/` e se o `gateway.proto` importa corretamente:
```protobuf
import "common/common.proto";
```

### Erro: "Namespace not found"

**Causa**: Namespace gerado diferente do esperado

**Solução**: Verifique o `package` nos arquivos `.proto` e ajuste os `using` no código C#.

### Erro: "The type 'GrpcChannel' is not found"

**Causa**: Pacotes NuGet não instalados

**Solução**:
```bash
dotnet restore
dotnet build
```

---

## 🔗 Referências

- **Repositório ChirpStack**: https://github.com/chirpstack/chirpstack
- **Arquivos .proto**: https://github.com/chirpstack/chirpstack/tree/master/api/proto
- **Documentação gRPC**: https://www.chirpstack.io/docs/chirpstack/api/grpc.html
- **gRPC .NET**: https://learn.microsoft.com/aspnet/core/grpc/

---

## ✅ Checklist Final

- [ ] Executou `baixar-proto-chirpstack.ps1`
- [ ] Arquivos `.proto` baixados e organizados
- [ ] Projeto compila sem erros (`dotnet build`)
- [ ] Descomentou código no `ChirpStackClient.cs`
- [ ] Ajustou namespaces (se necessário)
- [ ] Token configurado no `appsettings.json`
- [ ] Testou criar tenant via Swagger
- [ ] Testou criar gateway via Swagger
- [ ] Verificou logs de chamadas gRPC no console

---

**Próximo passo**: Execute `.\baixar-proto-chirpstack.ps1` e depois `dotnet build`! 🚀
