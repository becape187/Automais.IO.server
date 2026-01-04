# 🔧 Como Configurar VPN no MikroTik

## 📋 Pré-requisitos

1. Router criado no sistema com rede VPN configurada
2. Certificado VPN baixado (arquivo `.conf`)
3. Acesso ao MikroTik via Winbox, WebFig ou Terminal

## 📥 Passo 1: Baixar a Configuração VPN

1. Acesse a página **Routers** no sistema
2. Localize o router desejado
3. Clique no botão **"Config VPN"** (ícone de download)
4. O arquivo `.conf` será baixado automaticamente

**Se der erro ao baixar:**
- Verifique se o router tem uma rede VPN configurada (`vpnNetworkId`)
- Verifique se o peer VPN foi criado corretamente
- Verifique os logs da API para mais detalhes

## 🔧 Passo 2: Importar no MikroTik

### Opção A: Via Winbox/WebFig (Interface Gráfica)

1. Abra o **Winbox** ou **WebFig**
2. Vá em **Interfaces** → **WireGuard**
3. Clique em **"+"** para adicionar nova interface
4. Clique em **"Import"** (Importar)
5. Cole o conteúdo do arquivo `.conf` baixado
6. Clique em **"Apply"** e depois **"OK"**

### Opção B: Via Terminal (SSH/Telnet)

1. Conecte-se ao MikroTik via SSH ou Telnet
2. Execute os comandos abaixo (substitua pelos valores do seu arquivo `.conf`):

```bash
# Criar interface WireGuard
/interface/wireguard/add name=wg-automais private-key="<PRIVATE_KEY_DO_ARQUIVO>" listen-port=51820

# Adicionar endereço IP
/ip/address/add interface=wg-automais address=<ADDRESS_DO_ARQUIVO>

# Adicionar peer (servidor)
/interface/wireguard/peers/add interface=wg-automais public-key="<PUBLIC_KEY_DO_SERVIDOR>" endpoint-address=<ENDPOINT> endpoint-port=<PORT> allowed-address=<ALLOWED_IPS> persistent-keepalive=25s
```

### Opção C: Importar Arquivo Completo

1. No Winbox, vá em **Files**
2. Faça upload do arquivo `.conf` para o MikroTik
3. No terminal, execute:
```bash
/import file-name=router_nome_router.conf
```

## 📝 Exemplo de Arquivo .conf

O arquivo baixado terá o seguinte formato:

```ini
[Interface]
PrivateKey = <chave_privada_do_router>
Address = 10.100.1.50/32

[Peer]
PublicKey = <chave_publica_do_servidor>
Endpoint = srv01.automais.io:51820
AllowedIPs = 10.100.1.0/24, 10.0.1.0/24
PersistentKeepalive = 25
```

## ✅ Passo 3: Verificar Conexão

### No MikroTik

1. Vá em **Interfaces** → **WireGuard**
2. Verifique se a interface está **Running** (ativa)
3. Clique na interface e vá na aba **Peers**
4. Verifique se o peer mostra **Last Handshake** recente

### Comandos de Verificação

```bash
# Ver status da interface
/interface/wireguard/print

# Ver detalhes do peer
/interface/wireguard/peers/print detail

# Verificar roteamento
/ip/route/print where interface=wg-automais
```

## 🔍 Troubleshooting

### Interface não inicia

**Problema**: Interface fica em estado "disabled" ou não inicia

**Soluções**:
1. Verifique se o WireGuard está habilitado no MikroTik:
   ```bash
   /system/package/update
   /system/package/print where name~"wireguard"
   ```
2. Verifique se a chave privada está correta
3. Verifique se o endereço IP está no formato correto (ex: `10.100.1.50/32`)

### Peer não conecta

**Problema**: Peer não mostra "Last Handshake"

**Soluções**:
1. Verifique se o `Endpoint` está acessível:
   ```bash
   /ping srv01.automais.io
   ```
2. Verifique se a porta `51820` está aberta no firewall
3. Verifique se o `PublicKey` do servidor está correto
4. Verifique se o `AllowedIPs` está configurado corretamente

### Sem roteamento

**Problema**: Interface conecta mas não há roteamento

**Soluções**:
1. Adicione rotas estáticas para as redes permitidas:
   ```bash
   /ip/route/add dst-address=10.0.1.0/24 gateway=wg-automais
   ```
2. Ou configure roteamento dinâmico (OSPF, BGP, etc.)

### Firewall bloqueando

**Problema**: Conexão é bloqueada pelo firewall

**Soluções**:
1. Adicione regra no firewall para permitir WireGuard:
   ```bash
   /ip/firewall/filter/add chain=input protocol=udp dst-port=51820 action=accept
   ```
2. Verifique se há NAT configurado corretamente

## 📊 Verificar Status

### Ver estatísticas da interface

```bash
/interface/wireguard/print stats
```

### Ver tráfego

```bash
/interface/wireguard/peers/print stats
```

### Ver logs

```bash
/log/print where topics~"wireguard"
```

## 🔐 Segurança

1. **Mantenha a chave privada segura**: Nunca compartilhe o arquivo `.conf`
2. **Use firewall**: Configure regras de firewall adequadas
3. **Atualize regularmente**: Mantenha o RouterOS atualizado
4. **Monitore conexões**: Verifique regularmente os peers conectados

## 📞 Suporte

Se encontrar problemas:

1. Verifique os logs do sistema: `/log/print`
2. Verifique os logs da API no servidor
3. Teste conectividade: `/ping` e `/tool/traceroute`
4. Verifique configuração: `/interface/wireguard/export`

---

**Nota**: O MikroTik RouterOS 7.0+ tem suporte nativo ao WireGuard. Para versões anteriores, pode ser necessário instalar o pacote adicional.

