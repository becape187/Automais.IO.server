# 🔑 Acesso ao Servidor - srv01.automais.io

## ⚡ Quick Start

### 1️⃣ Testar Conectividade

Execute o script PowerShell:
```powershell
.\test-connectivity.ps1
```

### 2️⃣ Obter Token do ChirpStack

1. Acesse: **http://srv01.automais.io:8080**
2. Faça login (solicite credenciais ao administrador)
3. Vá em **"API keys"** no menu lateral
4. Clique em **"Create"**
5. Preencha o nome: `Automais IoT Platform`
6. Clique em **"Submit"**
7. **COPIE O TOKEN** (só aparece uma vez!)

### 3️⃣ Configurar Token na Aplicação

**Edite o arquivo**: `src/Automais.Api/appsettings.json`

```json
{
  "ChirpStack": {
    "ApiUrl": "http://srv01.automais.io:8080",
    "ApiToken": "COLE_SEU_TOKEN_AQUI"
  }
}
```

### 4️⃣ Rodar a Aplicação

```bash
cd src/Automais.Api
dotnet run
```

---

## 🌐 URLs dos Serviços

| Serviço | URL | Credenciais |
|---------|-----|-------------|
| **ChirpStack Web** | http://srv01.automais.io:8080 | (solicitar admin) |
| **EMQX Dashboard** | http://srv01.automais.io:18083 | admin / public |
| **MQTT Broker** | mqtt://srv01.automais.io:1883 | (conforme config) |
| **WebSocket MQTT** | ws://srv01.automais.io:8083 | (conforme config) |

---

## 🧪 Testes Rápidos

### Testar ChirpStack via cURL

```bash
# Listar tenants (requer token)
curl -X GET http://srv01.automais.io:8080/api/tenants \
  -H "Authorization: Bearer SEU_TOKEN"

# Listar gateways
curl -X GET http://srv01.automais.io:8080/api/gateways \
  -H "Authorization: Bearer SEU_TOKEN"
```

### Testar MQTT via Mosquitto

```bash
# Subscribe
mosquitto_sub -h srv01.automais.io -p 1883 -t "test/#" -v

# Publish
mosquitto_pub -h srv01.automais.io -p 1883 -t "test/topic" -m "Hello"
```

---

## 📝 Checklist

- [ ] Testei conectividade com `test-connectivity.ps1`
- [ ] Acessei ChirpStack em http://srv01.automais.io:8080
- [ ] Criei API Key no ChirpStack
- [ ] Copiei o token gerado
- [ ] Coloquei o token no `appsettings.json`
- [ ] Testei rodar a aplicação com `dotnet run`
- [ ] Acessei o Swagger em http://localhost:5000/swagger

---

## 🔐 Segurança

⚠️ **IMPORTANTE**: 
- **NÃO** commite o `appsettings.json` com o token real
- Use variáveis de ambiente em produção
- Mantenha o token em segredo

✅ **Boa prática**: Copie o `appsettings.json` para `appsettings.Local.json` e adicione o token lá:

```json
// appsettings.Local.json (não commitado)
{
  "ChirpStack": {
    "ApiToken": "token_real_aqui"
  }
}
```

No `.gitignore` adicione:
```
appsettings.Local.json
```

No `Program.cs`:
```csharp
builder.Configuration
    .AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.Local.json", optional: true);
```

---

**Configuração concluída!** ✨

Para documentação completa, veja: `CONFIGURACAO_SERVIDOR.md`

