# 📍 Onde Ficam os Certificados/Configurações WireGuard

## 🗄️ Localização no Banco de Dados

Os certificados e configurações WireGuard são armazenados na tabela **`router_wireguard_peers`** no PostgreSQL.

### Estrutura da Tabela

```sql
CREATE TABLE router_wireguard_peers (
    "Id" UUID PRIMARY KEY,
    "RouterId" UUID NOT NULL,
    "VpnNetworkId" UUID NOT NULL,
    "PublicKey" VARCHAR(100) NOT NULL,      -- Chave pública WireGuard
    "PrivateKey" VARCHAR(500) NOT NULL,      -- Chave privada WireGuard (texto plano inicialmente)
    "AllowedIps" VARCHAR(255) NOT NULL,     -- IP do router na VPN (ex: "10.100.1.50/32")
    "Endpoint" VARCHAR(255),                 -- IP público do servidor
    "ListenPort" INT,                       -- Porta do servidor (ex: 51820)
    "ConfigContent" TEXT,                   -- ⭐ ARQUIVO .conf COMPLETO (aqui está o certificado!)
    "IsEnabled" BOOLEAN,
    "CreatedAt" TIMESTAMP,
    "UpdatedAt" TIMESTAMP
);
```

## 🔍 Como Verificar se o Router Tem Certificado

### 1. Via SQL

```sql
-- Verificar se o router tem peer WireGuard configurado
SELECT 
    r."Id" as router_id,
    r."Name" as router_name,
    p."Id" as peer_id,
    p."PublicKey",
    p."AllowedIps",
    CASE 
        WHEN p."ConfigContent" IS NOT NULL THEN 'Sim'
        ELSE 'Não'
    END as tem_configuracao
FROM routers r
LEFT JOIN router_wireguard_peers p ON p."RouterId" = r."Id"
WHERE r."Id" = 'SEU_ROUTER_ID_AQUI';
```

### 2. Via API

```http
GET /api/routers/{routerId}/wireguard/peers
```

Retorna a lista de peers do router. Se retornar vazio, o router não foi provisionado na VPN.

### 3. Verificar Configuração Completa

```http
GET /api/routers/{routerId}/wireguard/config/download
```

Retorna o arquivo `.conf` completo para download.

## ⚠️ Quando o Certificado É Gerado?

O certificado/configuração WireGuard é gerado **automaticamente** quando:

1. ✅ Router é criado com `vpnNetworkId` (ID da rede VPN)
2. ✅ Router é criado com `allowedNetworks` (lista de redes permitidas)

**Se você criou o router sem esses campos, o certificado NÃO foi gerado!**

## 🔧 Como Provisionar Manualmente

Se o router foi criado sem VPN, você pode provisionar depois:

### Via API

```http
POST /api/routers/{routerId}/wireguard/peers
Content-Type: application/json

{
  "vpnNetworkId": "uuid-da-rede-vpn",
  "allowedIps": "10.100.1.50/32",
  "endpoint": "srv01.automais.io",
  "listenPort": 51820
}
```

Ou usar o serviço diretamente:

```csharp
await _wireGuardServerService.ProvisionRouterAsync(
    routerId,
    vpnNetworkId,
    new[] { "10.0.1.0/24", "192.168.100.0/24" } // redes permitidas
);
```

## 📥 Como Baixar o Certificado

### 1. Via Frontend

Na página de detalhes do router, clique no botão **"Config VPN"** (aparece apenas se o router tiver `vpnNetworkId`).

### 2. Via API Direta

```http
GET /api/routers/{routerId}/wireguard/config/download
```

Retorna o arquivo `.conf` para importar no MikroTik.

### 3. Via SQL (para debug)

```sql
SELECT 
    "ConfigContent"
FROM router_wireguard_peers
WHERE "RouterId" = 'SEU_ROUTER_ID_AQUI';
```

## 📋 Conteúdo do Certificado (.conf)

O arquivo `.conf` contém:

```ini
[Interface]
PrivateKey = <chave_privada_do_router>
Address = 10.100.1.50/32

[Peer]
PublicKey = <chave_publica_do_servidor>
Endpoint = srv01.automais.io:51820
AllowedIPs = 10.100.1.0/24, 10.0.1.0/24, 192.168.100.0/24
PersistentKeepalive = 25
```

Este arquivo é salvo no campo `ConfigContent` da tabela `router_wireguard_peers`.

## 🔐 Segurança

- **Chaves privadas**: Atualmente em texto plano no banco (conforme solicitado para testes)
- **Futuro**: Implementar criptografia AES-256 antes de salvar
- **Acesso**: Apenas usuários autorizados podem baixar a configuração

## 🐛 Troubleshooting

### Router não tem certificado

**Causa**: Router foi criado sem `vpnNetworkId` ou `allowedNetworks`.

**Solução**: 
1. Editar o router e adicionar `vpnNetworkId` e `allowedNetworks`
2. Ou provisionar manualmente via API

### Certificado não aparece no frontend

**Causa**: Router não tem `vpnNetworkId` configurado.

**Solução**: O botão "Config VPN" só aparece se `router.vpnNetworkId` não for null.

### Erro ao baixar certificado

**Causa**: Peer WireGuard não foi criado ou `ConfigContent` está vazio.

**Solução**: 
1. Verificar se existe peer: `GET /api/routers/{id}/wireguard/peers`
2. Se não existir, criar peer primeiro
3. Se existir mas `ConfigContent` estiver vazio, regenerar: `POST /api/wireguard/peers/{id}/regenerate-keys`

## 📊 Resumo

| Item | Localização |
|------|-------------|
| **Chaves WireGuard** | `router_wireguard_peers.PublicKey` e `PrivateKey` |
| **Arquivo .conf** | `router_wireguard_peers.ConfigContent` |
| **IP do Router** | `router_wireguard_peers.AllowedIps` |
| **Redes Permitidas** | `router_allowed_networks.NetworkCidr` |
| **Download** | `GET /api/routers/{id}/wireguard/config/download` |

---

**Tudo está no banco de dados PostgreSQL!** 🗄️

