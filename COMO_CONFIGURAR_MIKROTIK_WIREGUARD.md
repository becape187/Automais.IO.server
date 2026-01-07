# Como Configurar MikroTik com WireGuard

## ⚠️ Problema Comum: TX funciona mas RX não funciona

Se você está vendo tráfego TX (transmitido) mas não RX (recebido) no MikroTik, isso significa:
- ✅ O MikroTik está conseguindo enviar para o servidor
- ❌ O servidor não está conseguindo responder ao MikroTik

## 🔍 Causas Possíveis

### 1. Peer não está configurado no servidor
O servidor Linux precisa conhecer a chave pública do MikroTik para poder se comunicar com ele.

### 2. Chaves diferentes
O MikroTik pode estar usando chaves diferentes das que estão no arquivo .conf.

### 3. Ordem de configuração
O peer precisa estar no servidor ANTES do MikroTik tentar se conectar.

## ✅ Processo Correto

### Passo 1: Criar Router na API
Quando você cria um router com VPN na API, o sistema:
1. Gera chaves WireGuard (pública e privada) para o peer
2. Aloca um IP na VPN
3. **Adiciona o peer no servidor Linux**: `wg set {interface} peer {publicKey} allowed-ips {ips}`
4. Gera arquivo .conf com as chaves

### Passo 2: Importar .conf no MikroTik

**IMPORTANTE**: Use o arquivo .conf gerado pela API. Não crie manualmente!

#### Opção A: Import via Winbox/WebFig
1. Baixe o arquivo .conf da API
2. No MikroTik: **WireGuard** → **Import** → Selecione o arquivo .conf
3. O MikroTik criará automaticamente:
   - Interface WireGuard
   - Peer com a chave pública do servidor

#### Opção B: Import via Terminal
```bash
/interface/wireguard/import file-name=router_nome.conf
```

### Passo 3: Verificar Configuração

#### No Servidor Linux:
```bash
# Verificar se o peer está configurado
sudo wg show {interface}

# Deve mostrar algo como:
# interface: wg-xxxx
#   peer: {chave_publica_do_mikrotik}
#     allowed ips: 10.222.111.2/32
```

#### No MikroTik:
```bash
# Verificar interface
/interface/wireguard/print

# Verificar peer
/interface/wireguard/peers/print

# Verificar tráfego
/interface/wireguard/peers/monitor {peer_name}
```

## 🔧 Solução para o Problema TX/RX

### Diagnóstico Rápido

**No servidor Linux, execute:**
```bash
sudo wg show wg-{vpn_id}
```

**O que você deve ver:**
```
interface: wg-xxxx
  peer: {chave_publica_do_mikrotik}  ← DEVE APARECER AQUI
    allowed ips: 10.222.111.2/32
```

**Se o peer NÃO aparecer**, o servidor não conhece o MikroTik e não conseguirá responder.

### Soluções

#### 1. Verificar se o peer foi criado na API
- Acesse a API e verifique se o router tem um peer WireGuard configurado
- Verifique se o peer tem `IsEnabled = true`

#### 2. Verificar se o peer está no servidor Linux
```bash
# Listar todas as interfaces
sudo wg show

# Ver interface específica
sudo wg show wg-{vpn_id}

# Ver arquivo de configuração
sudo cat /etc/wireguard/wg-{vpn_id}.conf
```

**O arquivo .conf do servidor deve ter:**
```ini
[Interface]
PrivateKey = {chave_privada_do_servidor}
Address = 10.222.111.1/24
ListenPort = 51820

[Peer]
PublicKey = {chave_publica_do_mikrotik}  ← DEVE ESTAR AQUI
AllowedIPs = 10.222.111.2/32
PersistentKeepalive = 25
```

#### 3. Recarregar peer no servidor

**Opção A: Reiniciar a API** (recomendado)
- A API faz sync automático na inicialização
- Todos os peers do banco são adicionados ao servidor

**Opção B: Recarregar interface manualmente**
```bash
# Recarregar interface (sincroniza arquivo com interface ativa)
sudo wg syncconf wg-{vpn_id} /etc/wireguard/wg-{vpn_id}.conf

# Ou fazer down/up
sudo wg-quick down wg-{vpn_id}
sudo wg-quick up wg-{vpn_id}
```

#### 4. Verificar chaves

**No MikroTik:**
```bash
/interface/wireguard/print detail
# Anote a chave pública da interface
```

**No servidor:**
```bash
sudo wg show wg-{vpn_id}
# Compare a chave pública do peer
```

**Se forem diferentes:**
- O MikroTik está usando chaves diferentes das geradas pela API
- Solução: Delete a interface no MikroTik e reimporte o arquivo .conf

### Se as chaves estão diferentes:

1. **Verificar chave pública no MikroTik**:
   ```bash
   /interface/wireguard/print detail
   ```
   - Anote a chave pública da interface

2. **Verificar chave pública no servidor**:
   ```bash
   sudo wg show wg-{vpn_id}
   ```
   - Compare com a chave pública do peer no banco

3. **Se forem diferentes**:
   - Delete o peer no MikroTik
   - Baixe o arquivo .conf novamente da API
   - Importe novamente no MikroTik

## 📋 Checklist de Troubleshooting

- [ ] Router foi criado na API com VPN configurada?
- [ ] Arquivo .conf foi baixado da API (não criado manualmente)?
- [ ] Arquivo .conf foi importado no MikroTik (não configurado manualmente)?
- [ ] Peer aparece no servidor Linux (`wg show`)?
- [ ] Chave pública do MikroTik está no servidor?
- [ ] IP do MikroTik está correto no servidor?
- [ ] Rotas estão configuradas corretamente?
- [ ] NAT está funcionando no servidor?

## 🚨 Erro Comum: Criar Manualmente no MikroTik

**NÃO FAÇA ISSO:**
```bash
# ❌ ERRADO - Não crie manualmente
/interface/wireguard/add name=wg-client1
/interface/wireguard/peers/add interface=wg-client1 public-key=...
```

**FAÇA ISSO:**
```bash
# ✅ CORRETO - Importe o arquivo .conf
/interface/wireguard/import file-name=router_nome.conf
```

Quando você cria manualmente, o MikroTik gera novas chaves, e essas chaves não estarão no servidor!

## 🔄 Recarregar Configuração

Se você precisar recarregar a configuração:

1. **No servidor**: Reinicie a API (faz sync automático)
2. **No MikroTik**: Reimporte o arquivo .conf

## 📝 Notas Importantes

1. **Chaves são geradas pelo servidor**: O servidor gera as chaves e as coloca no arquivo .conf
2. **Peer é adicionado automaticamente**: Quando você cria o router na API, o peer é adicionado no servidor
3. **Arquivo .conf é a fonte de verdade**: Use sempre o arquivo gerado pela API
4. **Não modifique manualmente**: Não altere chaves ou IPs manualmente

