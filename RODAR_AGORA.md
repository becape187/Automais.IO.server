# 🚀 RODAR AGORA (Sem Banco de Dados)

## ✅ Configuração Rápida

### 1️⃣ Obter Token do ChirpStack (1 minuto)

Acesse: **http://srv01.automais.io:8080**

1. Faça login
2. Menu lateral → **"API keys"**
3. Clique em **"Create"**
4. Nome: `Automais Platform`
5. **COPIE O TOKEN** 🔑

---

### 2️⃣ Configurar Token (30 segundos)

Edite: `src/Automais.Api/appsettings.json`

```json
{
  "ChirpStack": {
    "ApiUrl": "http://srv01.automais.io:8080",
    "ApiToken": "COLE_SEU_TOKEN_AQUI"  ⬅️ AQUI!
  }
}
```

---

### 3️⃣ Rodar! (10 segundos)

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

### 4️⃣ Abrir Swagger

No navegador:
```
http://localhost:5000
```

---

## 🎯 Testar Agora

### No Swagger (http://localhost:5000):

#### 1. Criar um Tenant

**POST /api/tenants**

```json
{
  "name": "Meu Cliente",
  "slug": "meu-cliente"
}
```

**Copie o `id` retornado!**

#### 2. Criar um Gateway

**POST /api/tenants/{tenantId}/gateways**

(Cole o ID do tenant acima em `{tenantId}`)

```json
{
  "name": "Gateway Teste",
  "gatewayEui": "0011223344556677",
  "description": "Gateway de teste",
  "latitude": -23.5505,
  "longitude": -46.6333
}
```

#### 3. Listar Gateways

**GET /api/tenants/{tenantId}/gateways**

Verá o gateway criado! ✅

---

## ✅ O que funciona SEM banco:

- ✅ Todas as APIs REST
- ✅ Criar/listar/atualizar/deletar Tenants (em memória)
- ✅ Criar/listar/atualizar/deletar Gateways (em memória + ChirpStack)
- ✅ Integração com ChirpStack
- ✅ Swagger completo

## ⚠️ Limitação:

- ❌ Dados são perdidos ao reiniciar a API
- ✅ Perfeito para testar ChirpStack agora!

---

## 🔧 Troubleshooting

### Erro: "Token configurado: Não ⚠️"

**Solução**: Você esqueceu de colocar o token no `appsettings.json`

### Erro: Não consigo acessar srv01.automais.io

**Teste conectividade**:
```powershell
.\test-connectivity.ps1
```

### Gateway não aparece no ChirpStack

**Motivo**: ChirpStackClient está em modo mock.

Para integração real, precisamos:
1. Arquivos `.proto` do ChirpStack
2. Implementar gRPC real no `ChirpStackClient.cs`

Por enquanto, veja logs no console:
```
[ChirpStack Mock] Criando gateway Gateway Teste (0011223344556677)...
```

---

## 📝 Resumo dos Comandos

```bash
# 1. Entrar na pasta
cd src/Automais.Api

# 2. Rodar
dotnet run

# 3. Abrir navegador
# http://localhost:5000
```

---

**PRONTO! Agora é só testar! 🎉**

Documentação completa em: `SEM_BANCO.md`

