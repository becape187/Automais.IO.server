# 🔐 Como as Chaves WireGuard são Geradas e Gerenciadas

## ⚠️ PROBLEMA IDENTIFICADO

Há uma **divergência** na geração de chaves entre diferentes partes do sistema:

### ✅ Chaves do Servidor (VPN Network) - CORRETO
- **Local**: `WireGuardServerService.GenerateWireGuardKeysAsync()`
- **Método**: Usa `wg genkey` e `wg pubkey` do sistema Linux
- **Onde é usado**: Ao criar a interface WireGuard no servidor (`/etc/wireguard/{interface}.conf`)
- **Salvo em**: 
  - Arquivo `/etc/wireguard/{interface}.conf` (PrivateKey do servidor)
  - `VpnNetwork.ServerPublicKey` no banco de dados

### ❌ Chaves do Peer (Router) - PROBLEMA
- **Local**: `RouterWireGuardService.GenerateWireGuardKeys()`
- **Método**: Usa `Random()` e `Convert.ToBase64String()` - **CHAVES INVÁLIDAS!**
- **Onde é usado**: Ao criar peer via `RouterWireGuardService.CreatePeerAsync()`
- **Problema**: Essas chaves não são válidas para WireGuard e causam divergências

### ✅ Chaves do Peer (Router) - CORRETO (quando usa ProvisionRouterAsync)
- **Local**: `WireGuardServerService.ProvisionRouterAsync()`
- **Método**: Usa `GenerateWireGuardKeysAsync()` que chama `wg genkey` e `wg pubkey`
- **Onde é usado**: Quando o router é criado via `RouterService.CreateAsync()` com `VpnNetworkId`

## 📋 Fluxo Atual

### Fluxo Correto (Router criado com VPN)
```
RouterService.CreateAsync()
  └─> WireGuardServerService.ProvisionRouterAsync()
      ├─> GenerateWireGuardKeysAsync() [CORRETO - usa wg genkey]
      ├─> Cria peer no banco
      ├─> Adiciona peer ao servidor (wg set)
      └─> Gera arquivo .conf
```

### Fluxo Problemático (Peer criado diretamente)
```
RouterWireGuardService.CreatePeerAsync()
  └─> GenerateWireGuardKeys() [ERRADO - usa Random()]
      ├─> Cria peer no banco com chaves inválidas
      └─> Peer não funciona no WireGuard
```

## 🔧 Solução Necessária

O método `RouterWireGuardService.GenerateWireGuardKeys()` precisa ser corrigido para:

1. **Opção 1 (Recomendada)**: Fazer `CreatePeerAsync` chamar `WireGuardServerService.ProvisionRouterAsync()` ao invés de criar o peer diretamente
2. **Opção 2**: Adicionar método na interface `IWireGuardServerService` para gerar chaves e usar no `RouterWireGuardService`
3. **Opção 3**: Mover a lógica de geração de chaves para a camada Core usando uma biblioteca .NET para WireGuard

## 📍 Onde as Chaves são Usadas

### No Arquivo .conf do Router
```conf
[Interface]
PrivateKey = {peer.PrivateKey}  ← Chave privada do ROUTER (peer)
Address = {peer.AllowedIps}

[Peer]
PublicKey = {vpnNetwork.ServerPublicKey}  ← Chave pública do SERVIDOR
Endpoint = {vpnNetwork.ServerEndpoint}:51820
AllowedIPs = ...
```

### No Arquivo do Servidor (/etc/wireguard/{interface}.conf)
```conf
[Interface]
PrivateKey = {serverPrivateKey}  ← Chave privada do SERVIDOR
Address = {serverIp}/24
ListenPort = 51820

# Peers adicionados via: wg set {interface} peer {peer.PublicKey} allowed-ips {ips}
```

## 🔍 Como Verificar Divergências

1. **Verificar chaves no banco**:
   ```sql
   SELECT id, "PublicKey", "PrivateKey" 
   FROM router_wireguard_peers 
   WHERE "RouterId" = 'SEU_ROUTER_ID';
   ```

2. **Verificar chaves no servidor**:
   ```bash
   sudo wg show {interface} peers
   ```

3. **Verificar chave pública do servidor**:
   ```sql
   SELECT id, "ServerPublicKey" 
   FROM vpn_networks 
   WHERE id = 'VPN_NETWORK_ID';
   ```

4. **Verificar no arquivo do servidor**:
   ```bash
   sudo cat /etc/wireguard/{interface}.conf
   ```

## ⚠️ IMPORTANTE

- Chaves geradas com `Random()` e `Base64` **NÃO SÃO VÁLIDAS** para WireGuard
- WireGuard requer chaves geradas com `wg genkey` (usando Curve25519)
- Chaves inválidas causam falha na conexão VPN

