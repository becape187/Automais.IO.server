# Arquitetura WireGuard - Automais.io

## Visão Geral

Este documento descreve a arquitetura completa do sistema WireGuard, incluindo:
- Processo de criação de chaves
- Gestão de interfaces (criar/deletar)
- Suporte a múltiplas VPNs simultâneas
- Fonte de verdade e recuperação de desastres

---

## 1. Estrutura de Dados (Banco = Fonte de Verdade)

### 1.1 VpnNetwork (Servidor VPN)
```
vpn_networks
├── Id                  # Identificador único
├── TenantId           # Multi-tenancy
├── Name               # Nome legível
├── Slug               # Identificador único por tenant
├── Cidr               # Faixa IP da VPN (ex: "10.222.111.0/24")
├── ServerPrivateKey   # 🔑 CHAVE PRIVADA do servidor (FONTE DE VERDADE)
├── ServerPublicKey    # 🔑 CHAVE PÚBLICA do servidor (derivada da privada)
├── ServerEndpoint     # Endpoint público (ex: "automais.io")
└── DnsServers         # Servidores DNS opcionais
```

### 1.2 RouterWireGuardPeer (Cliente VPN)
```
router_wireguard_peers
├── Id                 # Identificador único
├── RouterId           # Router associado
├── VpnNetworkId       # VPN à qual pertence
├── PrivateKey         # 🔑 CHAVE PRIVADA do peer (FONTE DE VERDADE)
├── PublicKey          # 🔑 CHAVE PÚBLICA do peer (derivada da privada)
├── AllowedIps         # IP do peer na VPN (ex: "10.222.111.2/24")
├── ConfigContent      # Conteúdo do arquivo .conf para download
└── IsEnabled          # Se o peer está ativo
```

---

## 2. Processo de Criação de Chaves

### 2.1 Chaves do Servidor (VpnNetwork)

```
┌─────────────────────────────────────────────────────────────────┐
│                    CRIAR VPN NETWORK                            │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  1. VpnNetworkService.CreateAsync()                            │
│     └── Cria registro no banco                                 │
│                                                                 │
│  2. WireGuardServerService.EnsureInterfaceForVpnNetworkAsync() │
│     ├── Verifica se banco tem chaves                           │
│     │   ├── SIM: Usa chaves do banco                           │
│     │   └── NÃO: Gera novas com wg genkey                      │
│     │                                                          │
│     ├── Salva chaves no banco (ServerPrivateKey/PublicKey)     │
│     ├── Cria /etc/wireguard/wg-{id}.conf                       │
│     └── Ativa interface: wg-quick up wg-{id}                   │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

**Geração de chaves do servidor:**
```bash
# Executado via Process.Start no Linux
wg genkey                     # Gera chave privada
echo "privkey" | wg pubkey    # Deriva chave pública
```

### 2.2 Chaves do Peer (RouterWireGuardPeer)

```
┌─────────────────────────────────────────────────────────────────┐
│                    CRIAR ROUTER COM VPN                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  1. RouterService.CreateAsync()                                │
│     └── Cria registro do router                                │
│                                                                 │
│  2. WireGuardServerService.ProvisionRouterAsync()              │
│     ├── Gera chaves: wg genkey + wg pubkey                     │
│     ├── Aloca IP: próximo disponível ou manual                 │
│     │   └── .1 SEMPRE reservado para servidor                  │
│     ├── Cria peer no banco com chaves                          │
│     ├── Adiciona peer na interface: wg set ... peer ...        │
│     └── Gera ConfigContent para download                       │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## 3. Gestão de Interfaces (Múltiplas VPNs)

### 3.1 Nomenclatura de Interfaces
```
Interface = wg-{vpnNetworkId.Substring(0,8)}
Exemplo: wg-c9520d7d (para VPN c9520d7d-xxxx-xxxx-xxxx-xxxxxxxxxxxx)
```

Cada VPN tem sua própria interface. Podem coexistir múltiplas:
```
wg-c9520d7d   # VPN 1: 10.222.111.0/24
wg-a1b2c3d4   # VPN 2: 10.100.0.0/24
wg-e5f6g7h8   # VPN 3: 192.168.50.0/24
```

### 3.2 Criar VPN (e interface)

```
┌──────────────────────────────────────────────────────────────────┐
│                          CRIAR VPN                               │
├──────────────────────────────────────────────────────────────────┤
│                                                                  │
│  VpnNetworkService.CreateAsync()                                │
│  │                                                              │
│  ├─► 1. Salvar VpnNetwork no banco                             │
│  │                                                              │
│  └─► 2. EnsureInterfaceForVpnNetworkAsync()                     │
│       │                                                         │
│       ├─► Se banco tem ServerPrivateKey:                        │
│       │     └── Usar chaves do banco (recuperação)              │
│       │                                                         │
│       ├─► Se arquivo existe mas banco não tem chaves:           │
│       │     ├── Extrair PrivateKey do arquivo                   │
│       │     ├── Derivar PublicKey                               │
│       │     └── Salvar no banco                                 │
│       │                                                         │
│       ├─► Se nenhum:                                            │
│       │     ├── Gerar novas chaves                              │
│       │     └── Salvar no banco                                 │
│       │                                                         │
│       ├─► Criar arquivo /etc/wireguard/wg-{id}.conf             │
│       ├─► chmod 600                                             │
│       ├─► Configurar iptables (NAT/MASQUERADE)                  │
│       └─► wg-quick up wg-{id}                                   │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

### 3.3 Deletar VPN (e interface)

```
┌──────────────────────────────────────────────────────────────────┐
│                         DELETAR VPN                              │
├──────────────────────────────────────────────────────────────────┤
│                                                                  │
│  VpnNetworkService.DeleteAsync()                                │
│  │                                                              │
│  ├─► 1. RemoveInterfaceForVpnNetworkAsync()                     │
│  │     │                                                        │
│  │     ├─► wg-quick down wg-{id}                                │
│  │     │   (desativa APENAS esta interface)                     │
│  │     │                                                        │
│  │     └─► rm /etc/wireguard/wg-{id}.conf                       │
│  │         (remove APENAS este arquivo)                         │
│  │                                                              │
│  └─► 2. Deletar VpnNetwork do banco                             │
│       (CASCADE remove peers associados)                          │
│                                                                  │
│  ⚠️  OUTRAS VPNs NÃO SÃO AFETADAS                                │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

---

## 4. Sincronização na Inicialização (Recuperação de Desastre)

### 4.1 WireGuardSyncService (IHostedService)

Executa automaticamente ao iniciar a API:

```
┌──────────────────────────────────────────────────────────────────┐
│              INICIALIZAÇÃO DA API (StartAsync)                   │
├──────────────────────────────────────────────────────────────────┤
│                                                                  │
│  1. VerifyWireGuardInstallationAsync()                          │
│     └── Verifica se wg está instalado                           │
│                                                                  │
│  2. EnsureWireGuardDirectoryExistsAsync()                       │
│     └── Cria /etc/wireguard se não existir                      │
│                                                                  │
│  3. EnableIpForwardingAsync()                                   │
│     ├── echo 1 > /proc/sys/net/ipv4/ip_forward                  │
│     └── Adiciona net.ipv4.ip_forward=1 no sysctl.conf           │
│                                                                  │
│  4. ConfigureBasicFirewallRulesAsync()                          │
│     └── iptables -A INPUT -p udp --dport 51820 -j ACCEPT        │
│                                                                  │
│  5. SyncWireGuardConfigurationsAsync()                          │
│     └── Para CADA VpnNetwork no banco:                          │
│         ├── EnsureInterfaceForVpnNetworkAsync()                 │
│         │   └── Recria arquivo usando chaves do BANCO           │
│         ├── Para cada peer: wg set ... peer ...                 │
│         └── wg-quick up (se não estiver ativa)                  │
│                                                                  │
│  6. SaveFirewallRulesAsync()                                    │
│     └── netfilter-persistent save                               │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

### 4.2 Cenário de Recuperação de Desastre

```
CENÁRIO: VM reinstalada, arquivos /etc/wireguard perdidos
BANCO: Intacto com todas as VpnNetworks e Peers

RESULTADO APÓS INICIALIZAÇÃO:
1. Todas as interfaces são recriadas a partir do banco
2. Todas as chaves são as MESMAS (vindas do banco)
3. Todos os peers são adicionados
4. Clientes conectam sem necessidade de reconfiguração
```

---

## 5. Geração do Arquivo .conf (Cliente)

### 5.1 Estrutura do arquivo

```ini
# Configuração VPN para Router
# Router: NomeDoRouter
# Gerado em: 2026-01-07 12:00:00 UTC

[Interface]
PrivateKey = {peer.PrivateKey}           # Do banco
Address = {peer.AllowedIps}              # Ex: 10.222.111.2/24

[Peer]
PublicKey = {vpnNetwork.ServerPublicKey} # SEMPRE do servidor Linux
Endpoint = {vpnNetwork.ServerEndpoint}:51820
AllowedIPs = 10.222.111.0/24
PersistentKeepalive = 25
```

### 5.2 Obtenção da Chave Pública do Servidor

```
┌──────────────────────────────────────────────────────────────────┐
│              GetServerPublicKeyAsync()                           │
├──────────────────────────────────────────────────────────────────┤
│                                                                  │
│  1. Tentar via wg show {interface}                              │
│     └── Extrai "public key: ..." da saída                       │
│     └── FONTE DE VERDADE (interface ativa)                      │
│                                                                  │
│  2. Fallback: Ler do arquivo .conf                              │
│     └── Extrai PrivateKey e deriva PublicKey                    │
│                                                                  │
│  3. Fallback: Usar chave do banco                               │
│     └── vpnNetwork.ServerPublicKey (pode estar desatualizada)   │
│                                                                  │
│  ⚠️  Se nenhum funcionar: ERRO (interface não configurada)       │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

---

## 6. Alocação de IPs

### 6.1 Regras

```
CIDR: 10.222.111.0/24

.0   = Endereço de rede (não usar)
.1   = SERVIDOR (SEMPRE reservado)
.2   = Primeiro peer disponível
.3   = Segundo peer disponível
...
.254 = Último peer disponível
.255 = Broadcast (não usar)
```

### 6.2 Alocação Automática vs Manual

```
AllocateVpnIpAsync(vpnNetworkId, manualIp = null)
│
├─► manualIp especificado:
│   ├── Validar se está no CIDR da VPN
│   ├── Validar se NÃO é .1 (reservado)
│   ├── Validar se não está em uso (banco)
│   └── Retornar IP manual
│
└─► manualIp não especificado:
    ├── Buscar todos os peers existentes
    ├── Encontrar IPs já em uso
    ├── Começar do .2
    ├── Encontrar próximo disponível
    └── Retornar IP alocado
```

---

## 7. Fluxo Completo de Dados

```
                    ┌─────────────────┐
                    │     BANCO       │
                    │  (PostgreSQL)   │
                    │                 │
                    │  FONTE DE       │
                    │  VERDADE        │
                    └────────┬────────┘
                             │
                             ▼
        ┌────────────────────────────────────────┐
        │         WireGuardServerService          │
        │                                         │
        │  - Provisionar peers                    │
        │  - Gerar chaves                         │
        │  - Alocar IPs                          │
        │  - Gerenciar interfaces                │
        └────────────────────────────────────────┘
                             │
           ┌─────────────────┼─────────────────┐
           ▼                 ▼                 ▼
    ┌─────────────┐   ┌─────────────┐   ┌─────────────┐
    │  wg-vpn1    │   │  wg-vpn2    │   │  wg-vpn3    │
    │             │   │             │   │             │
    │ Peers:      │   │ Peers:      │   │ Peers:      │
    │ - Router1   │   │ - Router4   │   │ - Router7   │
    │ - Router2   │   │ - Router5   │   │ - Router8   │
    │ - Router3   │   │ - Router6   │   │             │
    └─────────────┘   └─────────────┘   └─────────────┘
```

---

## 8. Comandos WireGuard Utilizados

| Comando | Descrição | Quando |
|---------|-----------|--------|
| `wg genkey` | Gera chave privada | Criar VPN/Peer |
| `wg pubkey` | Deriva chave pública | Criar VPN/Peer |
| `wg show {iface}` | Mostra status da interface | Verificação/Sync |
| `wg set {iface} peer {pubkey} allowed-ips {ips}` | Adiciona/atualiza peer | Provisionar router |
| `wg-quick up {iface}` | Ativa interface | Criar VPN/Sync |
| `wg-quick down {iface}` | Desativa interface | Deletar VPN |
| `wg-quick save {iface}` | Salva config no arquivo | Após modificações |

---

## 9. Arquivos no Sistema

```
/etc/wireguard/
├── wg-c9520d7d.conf    # VPN 1
├── wg-a1b2c3d4.conf    # VPN 2
└── wg-e5f6g7h8.conf    # VPN 3

Cada arquivo contém:
- [Interface] com chave privada do servidor
- [Peer] para cada router conectado
```

---

## 10. Garantias do Sistema

1. **Banco é FONTE DE VERDADE**: Todas as chaves são salvas no banco
2. **Recuperação de Desastre**: Sistema reconstrói interfaces a partir do banco
3. **Múltiplas VPNs**: Cada VPN tem interface isolada
4. **Operações Atômicas**: Deletar VPN não afeta outras VPNs
5. **Sincronização Automática**: Na inicialização, tudo é sincronizado
6. **Chaves Imutáveis**: Uma vez criadas, chaves não mudam (a menos que recriado)

