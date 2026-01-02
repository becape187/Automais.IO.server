# 🔌 Integração gRPC com ChirpStack

## ✅ O que foi feito

### 1. **Pacotes NuGet Adicionados**

Já estavam no projeto:
- ✅ `Grpc.Net.Client` - Cliente gRPC
- ✅ `Google.Protobuf` - Serialização Protocol Buffers
- ✅ `Grpc.Tools` - Compilador de arquivos .proto

### 2. **Configuração do .csproj**

O `Automais.Infrastructure.csproj` foi configurado para:
- Compilar automaticamente arquivos `.proto`
- Gerar clientes gRPC C# a partir dos `.proto`

```xml
<ItemGroup>
  <Protobuf Include="ChirpStack\Protos\*.proto" GrpcServices="Client" />
</ItemGroup>
```

### 3. **ChirpStackClient.cs Atualizado**

- ✅ Métodos preparados para gRPC
- ✅ Tratamento de erros com `RpcException`
- ✅ Autenticação Bearer Token
- ✅ Logging integrado
- ⚠️ Código comentado aguardando arquivos `.proto`

---

## 📥 Próximo Passo: Instalar Arquivos .proto

### **Opção 1: Git Clone (Recomendado)**

```bash
cd server.io/src/Automais.Infrastructure

# Criar pasta temporária e clonar
git clone --depth=1 --filter=blob:none --sparse https://github.com/brocaar/chirpstack-api.git temp-proto
cd temp-proto
git sparse-checkout set proto
cd ..

# Criar estrutura de pastas
mkdir -p ChirpStack/Protos/api
mkdir -p ChirpStack/Protos/common

# Copiar arquivos necessários
cp temp-proto/proto/api/gateway.proto ChirpStack/Protos/api/
cp temp-proto/proto/api/tenant.proto ChirpStack/Protos/api/
cp temp-proto/proto/common/common.proto ChirpStack/Protos/common/ 2>/dev/null || echo "common.proto não encontrado"

# Limpar
rm -rf temp-proto

# Verificar
ls -R ChirpStack/Protos/
```

### **Opção 2: Download Manual**

1. Acesse: https://github.com/brocaar/chirpstack-api/tree/master/proto
2. Baixe os arquivos `.proto`:
   - `proto/api/gateway.proto`
   - `proto/api/tenant.proto`
   - `proto/common/common.proto` (se existir)

3. Coloque em:
```
src/Automais.Infrastructure/
└── ChirpStack/
    └── Protos/
        ├── api/
        │   ├── gateway.proto
        │   └── tenant.proto
        └── common/
            └── common.proto
```

### **Opção 3: Submodule Git (Produção)**

```bash
cd server.io
git submodule add https://github.com/brocaar/chirpstack-api.git externals/chirpstack-api

# Depois criar symlinks ou cópia para ChirpStack/Protos/
```

---

## 🔧 Após Instalar os .proto

### 1. Descomentar Código

Abra `ChirpStackClient.cs` e descomente os blocos `/* ... */` em cada método.

### 2. Ajustar Namespaces

Os namespaces gerados podem variar. Ajuste se necessário:

```csharp
// Se for necessário, ajustar imports:
using Api = ChirpStack.Api; // ou o namespace correto
```

### 3. Compilar

```bash
cd src/Automais.Infrastructure
dotnet build
```

Se compilar sem erros, os clientes gRPC foram gerados! ✅

### 4. Testar

```bash
cd ../Automais.Api
dotnet run
```

Tente criar um tenant ou gateway via Swagger e veja os logs!

---

## 📊 Endpoints gRPC do ChirpStack

### **Gateway Service**

- `List` - Lista gateways
- `Get` - Obtém um gateway
- `Create` - Cria gateway
- `Update` - Atualiza gateway
- `Delete` - Deleta gateway
- `GetStats` - Estatísticas do gateway

### **Tenant Service**

- `List` - Lista tenants
- `Get` - Obtém um tenant
- `Create` - Cria tenant
- `Update` - Atualiza tenant
- `Delete` - Deleta tenant

---

## 🔐 Autenticação

O ChirpStack gRPC usa **Bearer Token** no header `authorization`:

```csharp
var metadata = new Metadata
{
    { "authorization", $"Bearer {_apiToken}" }
};
```

---

## 🧪 Testar Conexão gRPC

### Via cURL (se ChirpStack expor endpoint HTTP)

```bash
curl -X GET http://srv01.automais.io:8080/api/tenants \
  -H "Authorization: Bearer SEU_TOKEN"
```

### Via .NET (quando compilar)

O cliente gRPC será gerado automaticamente e você pode testar:

```csharp
// Exemplo de teste direto
var channel = GrpcChannel.ForAddress("http://srv01.automais.io:8080");
var client = new Api.GatewayService.GatewayServiceClient(channel);
var response = await client.ListAsync(...);
```

---

## ⚠️ Troubleshooting

### Erro: "Cannot find proto files"

**Causa**: Arquivos `.proto` não encontrados

**Solução**: Verifique se os arquivos estão em `ChirpStack/Protos/`

### Erro: "Unknown import"

**Causa**: Arquivos `.proto` referenciam outros que não estão presentes

**Solução**: Baixe todos os `.proto` necessários (incluindo `common.proto`, etc)

### Erro: "Namespace not found"

**Causa**: Namespace gerado diferente do esperado

**Solução**: Ajuste os `using` no `ChirpStackClient.cs`

### Erro: "Connection refused"

**Causa**: ChirpStack não está acessível ou porta errada

**Solução**: 
```bash
# Testar conectividade
telnet srv01.automais.io 8080
```

---

## 📚 Referências

- **ChirpStack API Docs**: https://www.chirpstack.io/docs/chirpstack/api/grpc.html
- **Repositório ChirpStack**: https://github.com/brocaar/chirpstack-api
- **gRPC .NET Guide**: https://learn.microsoft.com/aspnet/core/grpc/

---

## ✅ Checklist

- [x] Pacotes NuGet configurados
- [x] .csproj configurado para compilar .proto
- [x] ChirpStackClient.cs preparado para gRPC
- [ ] Arquivos .proto baixados e colocados em `ChirpStack/Protos/`
- [ ] Código descomentado no ChirpStackClient.cs
- [ ] Projeto compila sem erros
- [ ] Token configurado no appsettings.json
- [ ] Testado criar tenant via Swagger
- [ ] Testado criar gateway via Swagger
- [ ] Testado listar gateways do ChirpStack

---

**Próximo passo**: Baixar os arquivos `.proto` e descomentar o código! 🚀

