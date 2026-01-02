# 📡 Onde são Requisitados Tenants e Gateways do ChirpStack

## 🎯 Resumo

Atualmente, **TODAS as chamadas estão em MOCK** (não fazem requisições reais).

Os métodos estão definidos mas retornam dados fictícios.

---

## 📍 Localizações das Chamadas

### 1️⃣ **ChirpStackClient.cs** (Infrastructure)

**Arquivo**: `src/Automais.Infrastructure/ChirpStack/ChirpStackClient.cs`

Este é o **único lugar** onde deveriam ser feitas as requisições ao ChirpStack.

#### **Métodos que Buscam do ChirpStack:**

| Método | Linha | O que faz | Status Atual |
|--------|-------|-----------|--------------|
| `ListGatewaysAsync` | 23 | Lista gateways de um tenant | ❌ Mock - retorna vazio |
| `GetGatewayAsync` | 58 | Busca um gateway específico | ❌ Mock - retorna null |
| `GetGatewayStatsAsync` | 121 | Estatísticas de um gateway | ❌ Mock - dados aleatórios |
| `CreateChirpStackTenantAsync` | 137 | Cria tenant no ChirpStack | ❌ Mock - retorna GUID fake |
| `CreateGatewayAsync` | 65 | Cria gateway no ChirpStack | ❌ Mock - só imprime log |
| `UpdateGatewayAsync` | 105 | Atualiza gateway no ChirpStack | ❌ Mock - só imprime log |
| `DeleteGatewayAsync` | 113 | Deleta gateway no ChirpStack | ❌ Mock - só imprime log |
| `DeleteChirpStackTenantAsync` | 172 | Deleta tenant no ChirpStack | ❌ Mock - só imprime log |

---

### 2️⃣ **Services que USAM o ChirpStackClient**

#### **TenantService.cs** (Core)

**Arquivo**: `src/Automais.Core/Services/TenantService.cs`

**Linha 50** - Cria tenant no ChirpStack:
```csharp
var chirpStackTenantId = await _chirpStackClient.CreateChirpStackTenantAsync(dto.Name, cancellationToken);
```

**Linha 111** - Deleta tenant no ChirpStack:
```csharp
await _chirpStackClient.DeleteChirpStackTenantAsync(tenant.ChirpStackTenantId, cancellationToken);
```

---

#### **GatewayService.cs** (Core)

**Arquivo**: `src/Automais.Core/Services/GatewayService.cs`

**Linha 59** - Cria gateway no ChirpStack:
```csharp
await _chirpStackClient.CreateGatewayAsync(dto, tenant.ChirpStackTenantId, cancellationToken);
```

**Linha 135** - Atualiza gateway no ChirpStack:
```csharp
await _chirpStackClient.UpdateGatewayAsync(gateway.GatewayEui, dto, cancellationToken);
```

**Linha 161** - Deleta gateway no ChirpStack:
```csharp
await _chirpStackClient.DeleteGatewayAsync(gateway.GatewayEui, cancellationToken);
```

**Linha 182** - Busca estatísticas do gateway:
```csharp
var stats = await _chirpStackClient.GetGatewayStatsAsync(gateway.GatewayEui, cancellationToken);
```

**Linha 195** - Lista gateways do ChirpStack (Sync):
```csharp
var chirpStackGateways = await _chirpStackClient.ListGatewaysAsync(tenant.ChirpStackTenantId, cancellationToken);
```

---

## 🔍 Fluxo de Chamadas

### **Exemplo: Criar Gateway**

```
1. Controller: GatewaysController.Create()
   ↓
2. Service: GatewayService.CreateAsync()
   ├─→ Valida tenant existe
   ├─→ Valida EUI único
   ├─→ ChirpStackClient.CreateGatewayAsync() ⬅️ AQUI!
   │   └─→ [ATUALMENTE] Só imprime log
   │   └─→ [FUTURO] HTTP POST para ChirpStack
   └─→ Salva no repositório em memória
```

### **Exemplo: Listar Gateways (Sync)**

```
1. Controller: GatewaysController.SyncWithChirpStack()
   ↓
2. Service: GatewayService.SyncWithChirpStackAsync()
   ├─→ ChirpStackClient.ListGatewaysAsync() ⬅️ AQUI!
   │   └─→ [ATUALMENTE] Retorna lista vazia
   │   └─→ [FUTURO] HTTP GET para ChirpStack
   └─→ Compara com gateways locais
   └─→ Cria gateways que não existem localmente
```

---

## 📊 Status Atual vs Futuro

### ❌ **ATUAL (Mock)**

```csharp
public async Task<IEnumerable<GatewayDto>> ListGatewaysAsync(string tenantId, ...)
{
    await Task.CompletedTask;
    return new List<GatewayDto>();  // ⬅️ Retorna vazio!
}
```

### ✅ **FUTURO (Real)**

```csharp
public async Task<IEnumerable<GatewayDto>> ListGatewaysAsync(string tenantId, ...)
{
    var client = new HttpClient();
    client.DefaultRequestHeaders.Authorization = 
        new AuthenticationHeaderValue("Bearer", _apiToken);
    
    var url = $"{_apiUrl}/api/gateways?tenantId={tenantId}";
    var response = await client.GetAsync(url, cancellationToken);
    
    var json = await response.Content.ReadAsStringAsync();
    var gateways = JsonSerializer.Deserialize<List<ChirpStackGateway>>(json);
    
    return gateways.Select(MapToGatewayDto);
}
```

---

## 🎯 Endpoints do ChirpStack que Devemos Chamar

### **API REST do ChirpStack**

O ChirpStack tem uma API REST além do gRPC. Vamos usar REST (mais simples).

Base URL: `http://srv01.automais.io:8080/api`

### **Tenants**

| Método | Endpoint | Descrição |
|--------|----------|-----------|
| POST | `/tenants` | Criar tenant |
| GET | `/tenants` | Listar tenants |
| GET | `/tenants/{id}` | Obter tenant |
| PUT | `/tenants/{id}` | Atualizar tenant |
| DELETE | `/tenants/{id}` | Deletar tenant |

### **Gateways**

| Método | Endpoint | Descrição |
|--------|----------|-----------|
| POST | `/gateways` | Criar gateway |
| GET | `/gateways` | Listar gateways |
| GET | `/gateways/{eui}` | Obter gateway |
| PUT | `/gateways/{eui}` | Atualizar gateway |
| DELETE | `/gateways/{eui}` | Deletar gateway |
| GET | `/gateways/{eui}/stats` | Estatísticas |

---

## 📝 Próximos Passos

1. ✅ Implementar HTTP client real no `ChirpStackClient.cs`
2. ✅ Fazer requisições REST para o ChirpStack
3. ✅ Mapear respostas do ChirpStack para nossos DTOs
4. ✅ Tratar erros e timeouts
5. ✅ Adicionar logs das requisições

---

**Resumo**: As chamadas estão no `ChirpStackClient.cs`, mas atualmente são todas mockadas. Precisamos implementar as requisições HTTP reais!

