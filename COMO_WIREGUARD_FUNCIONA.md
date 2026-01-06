# 🔍 Como o WireGuard Funciona na Aplicação

Este documento explica como o WireGuard é gerenciado pela aplicação, especialmente em relação a:
1. Ativação de interfaces na inicialização
2. Criação de regras NAT
3. Atribuição de IPs aos peers

## 1. ✅ Ativação de Interfaces na Inicialização

### Como funciona:

Quando a API inicia, o `WireGuardSyncService` executa automaticamente e:

1. **Sincroniza todas as VpnNetworks do banco de dados**
2. **Para cada VpnNetwork:**
   - Garante que o arquivo de configuração existe (`/etc/wireguard/wg-{id}.conf`)
   - Sincroniza todos os peers do banco para a interface WireGuard
   - **Ativa a interface se não estiver ativa** usando `wg-quick up {interfaceName}`

### Código responsável:

```csharp
// WireGuardSyncService.cs - método ActivateInterfaceIfNeededAsync
// Verifica se interface está ativa, se não, executa: wg-quick up {interfaceName}
```

### Verificação:

Após iniciar a API, você pode verificar com:
```bash
sudo wg show                    # Lista todas as interfaces ativas
ip addr show wg-*              # Mostra interfaces WireGuard
sudo systemctl status automais-api  # Ver logs de ativação
```

---

## 2. ✅ Criação de Regras NAT

### Como funciona:

As regras NAT são criadas em **dois momentos**:

#### A) Na inicialização (regras básicas):
- Permite tráfego UDP na porta 51820 (porta do WireGuard)
- Configurado no `WireGuardSyncService.StartAsync()`

#### B) Quando uma interface é criada (regras específicas):
- NAT (MASQUERADE) para permitir que clientes VPN acessem a internet
- Regras de forwarding para a interface específica
- Configurado no `ConfigureFirewallRulesAsync()`

### Regras NAT criadas:

```bash
# NAT para tráfego da VPN
iptables -t nat -A POSTROUTING -s {vpnCidr} -o {mainInterface} -j MASQUERADE

# Forwarding
iptables -A FORWARD -i {interfaceName} -j ACCEPT
iptables -A FORWARD -o {interfaceName} -j ACCEPT
```

### Persistência das regras:

As regras são salvas permanentemente usando:
1. **netfilter-persistent** (se instalado) - método recomendado
2. **iptables-save** para `/etc/iptables/rules.v4` - método alternativo

### Verificação:

```bash
# Ver regras NAT
sudo iptables -t nat -L -v -n

# Ver regras de forwarding
sudo iptables -L FORWARD -v -n

# Verificar se regras foram salvas
sudo cat /etc/iptables/rules.v4
```

### Importante:

Se as regras não persistirem após reinicialização, instale:
```bash
sudo apt install iptables-persistent
sudo netfilter-persistent save
```

---

## 3. ✅ Atribuição de IP aos Peers

### Como funciona:

O WireGuard **não atribui IPs diretamente aos peers no servidor**. O IP é configurado no **cliente** (router MikroTik).

#### Fluxo completo:

1. **Alocação de IP** (`AllocateVpnIpAsync`):
   - Busca IPs já alocados no banco de dados
   - Encontra próximo IP disponível na rede VPN (ex: 10.222.111.0/24)
   - Retorna IP no formato `10.222.111.2/24`

2. **Salvamento no banco**:
   - O IP alocado é salvo em `peer.AllowedIps` (ex: `10.222.111.2/24`)
   - Este é o IP que o **cliente** deve usar

3. **Configuração no servidor WireGuard**:
   - O comando `wg set` usa `allowed-ips` que inclui:
     - O IP do peer (`peer.AllowedIps`) - ex: `10.222.111.2/24`
     - Redes permitidas adicionais (se houver) - ex: `10.0.1.0/24`
   - **Importante**: O `allowed-ips` no WireGuard define quais redes o peer pode **acessar**, não o IP do peer em si

4. **Arquivo .conf para download**:
   - O arquivo `.conf` gerado contém:
     ```ini
     [Interface]
     PrivateKey = {chave_privada_do_peer}
     Address = 10.222.111.2/24    # IP atribuído ao cliente
     
     [Peer]
     PublicKey = {chave_publica_do_servidor}
     Endpoint = srv01.automais.io:51820
     AllowedIPs = 10.222.111.0/24  # Redes que o cliente pode acessar
     ```

### Código responsável:

```csharp
// 1. Alocação de IP
var routerIp = await AllocateVpnIpAsync(vpnNetworkId, cancellationToken);
// Retorna: "10.222.111.2/24"

// 2. Salvamento no peer
peer.AllowedIps = routerIp;  // "10.222.111.2/24"

// 3. Configuração no servidor
var allowedIps = new List<string> { peer.AllowedIps };  // IP do peer
allowedIps.AddRange(allowedNetworks);  // Redes adicionais
wg set {interface} peer {publicKey} allowed-ips {allowedIpsString}

// 4. Geração do arquivo .conf
sb.AppendLine($"Address = {peer.AllowedIps}");  // IP do cliente
```

### Verificação:

```bash
# Ver peers configurados no servidor
sudo wg show {interfaceName}

# Ver IPs alocados no banco
# SELECT "allowed_ips" FROM router_wireguard_peers WHERE vpn_network_id = '{id}';
```

---

## 📊 Resumo do Fluxo Completo

### Na inicialização da API:

1. ✅ Valida instalação do WireGuard
2. ✅ Cria diretório `/etc/wireguard` se necessário
3. ✅ Habilita `ip_forward`
4. ✅ Configura regras básicas de firewall (porta 51820)
5. ✅ Para cada VpnNetwork:
   - Cria/verifica arquivo de configuração
   - Sincroniza peers do banco
   - **Ativa interface** (`wg-quick up`)
6. ✅ Salva regras de firewall/NAT permanentemente

### Ao criar um novo router com VPN:

1. Aloca IP disponível na rede VPN
2. Gera chaves WireGuard (pública/privada)
3. Cria peer no banco de dados
4. Adiciona peer à interface WireGuard (`wg set`)
5. Configura regras NAT específicas
6. Salva configuração persistente
7. Gera arquivo `.conf` para download

---

## 🔍 Comandos Úteis para Verificação

```bash
# Ver todas as interfaces WireGuard ativas
sudo wg show

# Ver interface específica
sudo wg show wg-38ddaccc

# Ver arquivo de configuração
sudo cat /etc/wireguard/wg-38ddaccc.conf

# Ver regras NAT
sudo iptables -t nat -L -v -n | grep MASQUERADE

# Ver encaminhamento IP
cat /proc/sys/net/ipv4/ip_forward  # Deve retornar 1

# Ver logs de sincronização
sudo journalctl -u automais-api -n 100 | grep WireGuard
```

---

## ⚠️ Pontos Importantes

1. **IP do Peer**: O IP é configurado no **cliente** (arquivo .conf), não no servidor
2. **Allowed-IPs**: Define quais redes o peer pode acessar, não o IP do peer
3. **NAT**: Necessário para clientes VPN acessarem a internet
4. **Persistência**: Regras iptables precisam ser salvas manualmente ou via netfilter-persistent
5. **Ativação**: Interfaces são ativadas automaticamente na sincronização

---

## 🐛 Troubleshooting

### Interface não ativa após reiniciar:

```bash
# Ativar manualmente
sudo wg-quick up wg-{id}

# Verificar erros
sudo journalctl -u automais-api | grep "Interface WireGuard"
```

### Regras NAT não persistem:

```bash
# Instalar netfilter-persistent
sudo apt install iptables-persistent

# Salvar regras
sudo netfilter-persistent save
```

### IP não atribuído corretamente:

Verificar no banco de dados:
```sql
SELECT id, router_id, allowed_ips, vpn_network_id 
FROM router_wireguard_peers;
```

Verificar no WireGuard:
```bash
sudo wg show wg-{id} | grep allowed-ips
```

