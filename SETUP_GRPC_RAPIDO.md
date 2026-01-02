# ⚡ Setup gRPC com ChirpStack - Guia Rápido

## 🎯 3 Passos para Configurar

### **1️⃣ Baixar Arquivos .proto**

```powershell
cd server.io
.\baixar-proto-chirpstack.ps1
```

**O que faz:**
- ✅ Baixa arquivos do GitHub: https://github.com/chirpstack/chirpstack/tree/master/api/proto
- ✅ Organiza em `src/Automais.Infrastructure/ChirpStack/Protos/`
- ✅ Cria estrutura de pastas automaticamente

---

### **2️⃣ Compilar Projeto**

```bash
cd src/Automais.Infrastructure
dotnet build
```

**O que faz:**
- ✅ Compila arquivos `.proto` automaticamente
- ✅ Gera clientes gRPC C# (Api.GatewayService, Api.TenantService, etc)
- ✅ Verifica se tudo está correto

**Se houver erros**, veja troubleshooting em `INSTALAR_PROTO_CHIRPSTACK.md`

---

### **3️⃣ Ativar Código Real**

Abra: `src/Automais.Infrastructure/ChirpStack/ChirpStackClient.cs`

Em cada método (ListGatewaysAsync, CreateGatewayAsync, etc), **descomente** o bloco de código:

```csharp
// ANTES (comentado):
/*
using var channel = CreateChannel();
var client = new Api.GatewayService.GatewayServiceClient(channel);
// ... código ...
*/

// DEPOIS (descomentado):
using var channel = CreateChannel();
var client = new Api.GatewayService.GatewayServiceClient(channel);
// ... código ...
```

---

## ✅ Validar

```bash
cd src/Automais.Api
dotnet run
```

### Teste no Swagger (http://localhost:5000):

1. **POST /api/tenants** → Criar tenant
   - Verá no console: chamada gRPC real ao ChirpStack! 🎉

2. **POST /api/tenants/{id}/gateways** → Criar gateway
   - Verá no console: gateway criado no ChirpStack!

---

## 🔑 Token Necessário

Não esqueça de configurar o token no `appsettings.json`:

```json
{
  "ChirpStack": {
    "ApiUrl": "http://srv01.automais.io:8080",
    "ApiToken": "SEU_TOKEN_AQUI"
  }
}
```

**Como obter token?**
1. Acesse: http://srv01.automais.io:8080
2. Login → API Keys → Create
3. Copie o token

---

## 📁 Estrutura Após Download

```
src/Automais.Infrastructure/
└── ChirpStack/
    ├── ChirpStackClient.cs          ⬅️ Descomentar código aqui
    └── Protos/
        ├── api/
        │   ├── gateway.proto        ⬅️ Baixado pelo script
        │   └── tenant.proto          ⬅️ Baixado pelo script
        └── common/
            └── common.proto          ⬅️ Baixado pelo script
```

---

## 🆘 Problemas?

### Erro ao compilar?

1. Verifique se os `.proto` estão nas pastas corretas
2. Verifique imports nos arquivos `.proto`
3. Veja detalhes em `INSTALAR_PROTO_CHIRPSTACK.md`

### Erro ao executar?

1. Token configurado? (`appsettings.json`)
2. ChirpStack acessível? (`http://srv01.automais.io:8080`)
3. Verifique logs no console

---

## 🎉 Pronto!

Após esses 3 passos, sua integração gRPC estará funcionando!

**Tempo estimado**: ~5 minutos ⏱️

---

**Referências:**
- 📖 Documentação completa: `INTEGRACAO_GRPC.md`
- 📥 Instalar .proto: `INSTALAR_PROTO_CHIRPSTACK.md`
- 🔗 Repositório: https://github.com/chirpstack/chirpstack/tree/master/api/proto

