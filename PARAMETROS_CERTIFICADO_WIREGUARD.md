# 📋 Parâmetros Mínimos para Gerar Certificados WireGuard

## ✅ Parâmetros Obrigatórios

Para gerar os certificados WireGuard automaticamente ao criar um router, você precisa fornecer **1 parâmetro obrigatório**:

### 1. `vpnNetworkId` (Guid) ⭐ OBRIGATÓRIO
- **Tipo**: `Guid?` (nullable, mas obrigatório para gerar certificado)
- **Descrição**: ID da rede VPN onde o router será conectado
- **Exemplo**: `"550e8400-e29b-41d4-a716-446655440000"`

## 📋 Parâmetros Opcionais

### 2. `allowedNetworks` (IEnumerable<string>) ⭐ OPCIONAL
- **Tipo**: `IEnumerable<string>?` (nullable e opcional)
- **Descrição**: Lista de redes CIDR adicionais que o router terá acesso via WireGuard
- **Pode estar vazio ou null**: Se não fornecer, o router terá acesso apenas à rede VPN base
- **Formato**: Array de strings no formato CIDR
- **Exemplo**: `["10.0.1.0/24", "192.168.100.0/24"]`
- **Nota**: As redes permitidas são uma camada adicional de roteamento. O peer WireGuard será criado mesmo sem elas, apenas com o IP do router na VPN.

## 📝 Exemplo de Requisição

### Via API REST - Mínimo necessário

```http
POST /api/routers
Content-Type: application/json

{
  "name": "Router Principal",
  "vpnNetworkId": "550e8400-e29b-41d4-a716-446655440000"
}
```

**Isso é suficiente!** O certificado será gerado apenas com o `vpnNetworkId`.

### Via API REST - Com redes adicionais

```http
POST /api/routers
Content-Type: application/json

{
  "name": "Router Principal",
  "vpnNetworkId": "550e8400-e29b-41d4-a716-446655440000",
  "allowedNetworks": [
    "10.0.1.0/24",
    "192.168.100.0/24"
  ]
}
```

As `allowedNetworks` são opcionais e adicionam rotas adicionais ao router.

### Via Frontend (React)

```javascript
const routerData = {
  name: "Router Principal",
  vpnNetworkId: "550e8400-e29b-41d4-a716-446655440000",
  allowedNetworks: [
    "10.0.1.0/24",
    "192.168.100.0/24"
  ]
};

await createRouter.mutateAsync(routerData);
```

## 🔍 Validação no Código

O código verifica se ambos os parâmetros estão presentes:

```csharp
// Se tem VpnNetworkId e allowedNetworks, provisionar WireGuard automaticamente
if (dto.VpnNetworkId.HasValue && dto.AllowedNetworks != null && dto.AllowedNetworks.Any())
{
    await _wireGuardServerService.ProvisionRouterAsync(
        created.Id,
        dto.VpnNetworkId.Value,
        dto.AllowedNetworks,
        cancellationToken);
}
```

## ⚠️ O que acontece se não fornecer?

### Se `vpnNetworkId` for `null` ou não fornecido:
- ✅ Router é criado normalmente
- ❌ **Certificado WireGuard NÃO é gerado**
- ❌ Peer WireGuard NÃO é criado
- ❌ Arquivo `.conf` NÃO é gerado

### Se `allowedNetworks` for `null` ou vazio:
- ✅ Router é criado normalmente
- ✅ **Certificado WireGuard É gerado** (apenas com o IP do router na VPN)
- ✅ Peer WireGuard É criado (com acesso apenas à rede VPN base)
- ✅ Arquivo `.conf` É gerado
- ⚠️ Router terá acesso apenas à própria rede VPN (sem rotas adicionais)

### Se `vpnNetworkId` estiver presente (com ou sem `allowedNetworks`):
- ✅ Router é criado
- ✅ **Certificado WireGuard é gerado automaticamente**
- ✅ Peer WireGuard é criado no servidor
- ✅ Arquivo `.conf` é gerado e salvo no banco
- ✅ Chaves WireGuard são geradas (`wg genkey`)
- ✅ IP da VPN é alocado automaticamente
- ✅ Interface WireGuard é criada/ativada no servidor Linux
- ✅ Se `allowedNetworks` for fornecido, rotas adicionais são configuradas

## 📊 Parâmetros Opcionais (mas recomendados)

Embora não sejam obrigatórios para gerar o certificado, são úteis:

| Parâmetro | Tipo | Obrigatório? | Descrição |
|-----------|------|--------------|-----------|
| `name` | `string` | ✅ Sim | Nome do router |
| `serialNumber` | `string?` | ❌ Não | Número de série |
| `model` | `string?` | ❌ Não | Modelo do router |
| `routerOsApiUrl` | `string?` | ❌ Não | URL da API RouterOS |
| `routerOsApiUsername` | `string?` | ❌ Não | Usuário da API RouterOS |
| `routerOsApiPassword` | `string?` | ❌ Não | Senha da API RouterOS |
| `description` | `string?` | ❌ Não | Descrição do router |

## 🔄 Provisionar Depois

Se você criou o router sem os parâmetros de VPN, pode provisionar depois:

### Via API

```http
POST /api/routers/{routerId}/wireguard/peers
Content-Type: application/json

{
  "vpnNetworkId": "550e8400-e29b-41d4-a716-446655440000",
  "allowedIps": "10.100.1.50/32",
  "endpoint": "srv01.automais.io",
  "listenPort": 51820
}
```

## 📋 Resumo

| Situação | `vpnNetworkId` | `allowedNetworks` | Resultado |
|----------|----------------|-------------------|-----------|
| ✅ Gerar certificado | ✅ Fornecido | ✅ Fornecido (não vazio) | Certificado gerado + rotas adicionais |
| ✅ Gerar certificado | ✅ Fornecido | ❌ `null` ou vazio | Certificado gerado (apenas VPN base) |
| ❌ Não gerar | ❌ `null` ou ausente | ❌ `null` ou ausente | Router criado sem VPN |
| ❌ Não gerar | ❌ `null` ou ausente | ✅ Fornecido | Router criado sem VPN (ignora allowedNetworks) |

## 🎯 Exemplo Mínimo Completo

### Apenas VPN (sem rotas adicionais)

```json
{
  "name": "Router Teste",
  "vpnNetworkId": "550e8400-e29b-41d4-a716-446655440000"
}
```

**Isso é suficiente para gerar o certificado!** 🎉

### Com rotas adicionais

```json
{
  "name": "Router Teste",
  "vpnNetworkId": "550e8400-e29b-41d4-a716-446655440000",
  "allowedNetworks": ["10.0.1.0/24", "192.168.100.0/24"]
}
```

As `allowedNetworks` são opcionais e adicionam rotas de roteamento adicionais ao peer WireGuard.

---

**Nota**: O `vpnNetworkId` deve ser um ID válido de uma `VpnNetwork` existente no banco de dados. Se não existir, ocorrerá um erro `KeyNotFoundException`.

