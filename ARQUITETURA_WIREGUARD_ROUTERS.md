# 🏗️ Arquitetura: WireGuard Server para Routers MikroTik

## 📋 Contexto

Ao adicionar um router MikroTik na plataforma, ele deve:
1. **Receber automaticamente um IP da VPN** (WireGuard)
2. **Ter acesso a "n" redes** configuradas (como `add route` do MikroTik)
3. **Ter sua configuração WireGuard gerada e disponível para download** no portal

## 🎯 Objetivos

- Router conecta-se via WireGuard ao servidor central
- Router recebe IP da VPN automaticamente
- Router pode acessar múltiplas redes configuradas
- Configuração (.conf) disponível para download a qualquer momento
- Gerenciamento centralizado via API

---

## 🤔 Decisão Arquitetural: WireGuard Server

### **Opção 1: WireGuard Gerenciado Diretamente no Linux** ⚙️

#### Como Funciona:
- WireGuard instalado e rodando diretamente no servidor Linux
- API C# executa comandos `wg` e `wg-quick` via shell/Process
- Configurações persistidas em arquivos `/etc/wireguard/wg*.conf`
- API apenas orquestra comandos, não gerencia o WireGuard diretamente

#### Vantagens:
✅ **Simplicidade**: Usa ferramentas nativas do Linux
✅ **Performance**: WireGuard rodando nativamente no kernel
✅ **Confiabilidade**: Ferramentas maduras e testadas
✅ **Facilidade de debug**: Pode acessar servidor e verificar com `wg show`
✅ **Menos dependências**: Não precisa de bibliotecas C# complexas

#### Desvantagens:
❌ **Dependência de shell**: Precisa executar processos externos
❌ **Sincronização**: Precisa manter DB e arquivos .conf sincronizados
❌ **Permissões**: API precisa rodar com permissões elevadas (sudo)
❌ **Atomicidade**: Operações podem falhar parcialmente

#### Exemplo de Implementação:
```csharp
public class WireGuardServerService
{
    public async Task AddPeerAsync(string interfaceName, string publicKey, string allowedIps)
    {
        // Executa: wg set wg0 peer <pubkey> allowed-ips <ips>
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "wg",
                Arguments = $"set {interfaceName} peer {publicKey} allowed-ips {allowedIps}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.Start();
        await process.WaitForExitAsync();
        
        // Salva configuração persistente
        await SaveConfigToFileAsync(interfaceName);
    }
}
```

---

### **Opção 2: WireGuard Gerenciado via API C# (Biblioteca)** 📚

#### Como Funciona:
- Usa biblioteca C# como `WireGuardSharp` ou `WgNet`
- API gerencia WireGuard diretamente via código
- Não depende de comandos shell
- Tudo gerenciado via código C#

#### Vantagens:
✅ **Controle total**: Tudo via código, sem dependência de shell
✅ **Atomicidade**: Transações podem ser mais seguras
✅ **Testabilidade**: Mais fácil de mockar e testar
✅ **Sincronização**: DB e WireGuard sempre sincronizados

#### Desvantagens:
❌ **Dependência de biblioteca**: Precisa de biblioteca C# confiável
❌ **Complexidade**: Bibliotecas podem ser limitadas ou desatualizadas
❌ **Debug mais difícil**: Não pode usar comandos `wg` diretamente
❌ **Possível overhead**: Camada extra entre API e WireGuard

#### Exemplo de Implementação:
```csharp
using WireGuardSharp;

public class WireGuardServerService
{
    private readonly IWireGuardInterface _wgInterface;
    
    public async Task AddPeerAsync(string publicKey, string allowedIps)
    {
        var peer = new WireGuardPeer
        {
            PublicKey = publicKey,
            AllowedIPs = allowedIps
        };
        
        await _wgInterface.AddPeerAsync(peer);
    }
}
```

---

## 🎯 **Recomendação: Opção 1 (Linux Direto)**

### Justificativa:
1. **WireGuard é nativo do kernel Linux** - melhor performance
2. **Ferramentas maduras** - `wg` e `wg-quick` são padrão da indústria
3. **Facilidade de manutenção** - qualquer sysadmin consegue debugar
4. **Menos dependências** - não precisa de bibliotecas C# externas
5. **Padrão da indústria** - maioria dos sistemas usam essa abordagem

### Abordagem Híbrida (Recomendada):
- **Comandos WireGuard**: Via shell (`wg`, `wg-quick`)
- **Gerenciamento de Estado**: API C# mantém estado no PostgreSQL
- **Sincronização**: API aplica mudanças do DB para WireGuard em tempo real

---

## 🏗️ Arquitetura Proposta

### **Fluxo: Adicionar Router**

```
1. Usuário cria router via API
   POST /api/tenants/{tenantId}/routers
   {
     "name": "Router Matriz",
     "vpnNetworkId": "...",
     "allowedNetworks": ["10.0.1.0/24", "192.168.100.0/24"]
   }

2. RouterService.CreateAsync():
   ├─ Cria registro no DB (Router)
   ├─ Chama WireGuardServerService.ProvisionRouterAsync()
   │   ├─ Gera par de chaves (privada/pública)
   │   ├─ Aloca IP da VPN (ex: 10.100.1.50/32)
   │   ├─ Cria RouterWireGuardPeer no DB
   │   ├─ Adiciona peer no WireGuard server (wg set ...)
   │   ├─ Adiciona rotas (allowed-ips com todas as redes)
   │   └─ Salva configuração persistente
   └─ Retorna router criado

3. Usuário pode baixar configuração:
   GET /api/routers/{routerId}/wireguard/config
   → Retorna arquivo .conf para importar no MikroTik
```

### **Estrutura de Dados**

#### Tabela: `routers` (já existe)
```sql
CREATE TABLE routers (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    name VARCHAR(100) NOT NULL,
    vpn_network_id UUID, -- Rede VPN principal
    router_os_api_url VARCHAR(255), -- IP via WireGuard
    -- ... outros campos
);
```

#### Tabela: `router_wireguard_peers` (já existe)
```sql
CREATE TABLE router_wireguard_peers (
    id UUID PRIMARY KEY,
    router_id UUID NOT NULL,
    vpn_network_id UUID NOT NULL,
    public_key VARCHAR(100) NOT NULL,
    private_key VARCHAR(100) NOT NULL, -- criptografado
    allowed_ips VARCHAR(255) NOT NULL, -- ex: "10.100.1.50/32"
    endpoint VARCHAR(255), -- IP público do servidor
    listen_port INT, -- Porta do servidor (ex: 51820)
    -- ... outros campos
);
```

#### **NOVA Tabela: `router_allowed_networks`** (precisa criar)
```sql
CREATE TABLE router_allowed_networks (
    id UUID PRIMARY KEY,
    router_id UUID NOT NULL,
    network_cidr VARCHAR(50) NOT NULL, -- ex: "10.0.1.0/24"
    description VARCHAR(255),
    created_at TIMESTAMP NOT NULL,
    UNIQUE(router_id, network_cidr)
);
```

**Explicação**: Quando o router é criado, podemos adicionar múltiplas redes que ele poderá acessar. Essas redes são adicionadas ao `allowed-ips` do peer WireGuard.

---

## 🔧 Implementação Detalhada

### **1. Serviço: WireGuardServerService**

```csharp
public interface IWireGuardServerService
{
    // Provisionar router (criar peer + adicionar rotas)
    Task<RouterWireGuardPeerDto> ProvisionRouterAsync(
        Guid routerId, 
        Guid vpnNetworkId,
        IEnumerable<string> allowedNetworks,
        CancellationToken cancellationToken = default);
    
    // Adicionar rede ao router (adicionar ao allowed-ips)
    Task AddNetworkToRouterAsync(
        Guid routerId, 
        string networkCidr,
        CancellationToken cancellationToken = default);
    
    // Remover rede do router
    Task RemoveNetworkFromRouterAsync(
        Guid routerId, 
        string networkCidr,
        CancellationToken cancellationToken = default);
    
    // Atualizar configuração do peer (reload)
    Task ReloadPeerConfigAsync(
        Guid peerId,
        CancellationToken cancellationToken = default);
    
    // Gerar arquivo .conf para download
    Task<RouterWireGuardConfigDto> GenerateConfigFileAsync(
        Guid routerId,
        CancellationToken cancellationToken = default);
    
    // Alocar IP disponível na VPN
    Task<string> AllocateVpnIpAsync(
        Guid vpnNetworkId,
        CancellationToken cancellationToken = default);
}
```

### **2. Fluxo: ProvisionRouterAsync**

```csharp
public async Task<RouterWireGuardPeerDto> ProvisionRouterAsync(
    Guid routerId,
    Guid vpnNetworkId,
    IEnumerable<string> allowedNetworks,
    CancellationToken cancellationToken = default)
{
    // 1. Buscar router e VpnNetwork
    var router = await _routerRepository.GetByIdAsync(routerId, cancellationToken);
    var vpnNetwork = await _vpnNetworkRepository.GetByIdAsync(vpnNetworkId, cancellationToken);
    
    // 2. Gerar par de chaves WireGuard
    var (publicKey, privateKey) = GenerateWireGuardKeys();
    
    // 3. Alocar IP da VPN (ex: 10.100.1.50/32)
    var routerIp = await AllocateVpnIpAsync(vpnNetworkId, cancellationToken);
    
    // 4. Construir allowed-ips (IP do router + redes permitidas)
    var allowedIps = new List<string> { routerIp };
    allowedIps.AddRange(allowedNetworks);
    var allowedIpsString = string.Join(",", allowedIps);
    
    // 5. Criar peer no banco
    var peer = new RouterWireGuardPeer
    {
        Id = Guid.NewGuid(),
        RouterId = routerId,
        VpnNetworkId = vpnNetworkId,
        PublicKey = publicKey,
        PrivateKey = EncryptPrivateKey(privateKey), // Criptografar!
        AllowedIps = routerIp, // IP do router na VPN
        Endpoint = GetServerPublicIp(), // IP público do servidor
        ListenPort = vpnNetwork.ListenPort ?? 51820,
        IsEnabled = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
    
    await _peerRepository.CreateAsync(peer, cancellationToken);
    
    // 6. Salvar redes permitidas
    foreach (var network in allowedNetworks)
    {
        await _routerAllowedNetworkRepository.CreateAsync(new RouterAllowedNetwork
        {
            Id = Guid.NewGuid(),
            RouterId = routerId,
            NetworkCidr = network,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);
    }
    
    // 7. Aplicar no WireGuard server (Linux)
    var interfaceName = GetInterfaceName(vpnNetworkId); // ex: "wg-tenant1"
    await ExecuteWireGuardCommandAsync(
        $"set {interfaceName} peer {publicKey} allowed-ips {allowedIpsString}"
    );
    
    // 8. Salvar configuração persistente
    await SaveWireGuardConfigAsync(interfaceName);
    
    return MapToDto(peer);
}
```

### **3. Executar Comandos WireGuard**

```csharp
private async Task ExecuteWireGuardCommandAsync(string command)
{
    var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "/usr/bin/wg",
            Arguments = command,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        }
    };
    
    process.Start();
    var output = await process.StandardOutput.ReadToEndAsync();
    var error = await process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    
    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException(
            $"Erro ao executar comando WireGuard: {error}");
    }
}
```

### **4. Alocar IP da VPN**

```csharp
public async Task<string> AllocateVpnIpAsync(
    Guid vpnNetworkId,
    CancellationToken cancellationToken = default)
{
    var vpnNetwork = await _vpnNetworkRepository.GetByIdAsync(vpnNetworkId, cancellationToken);
    if (vpnNetwork == null)
        throw new KeyNotFoundException("Rede VPN não encontrada");
    
    // Parse do CIDR (ex: "10.100.1.0/24")
    var (networkIp, prefixLength) = ParseCidr(vpnNetwork.Cidr);
    
    // Buscar IPs já alocados
    var allocatedIps = await _peerRepository
        .GetAllocatedIpsByNetworkAsync(vpnNetworkId, cancellationToken);
    
    // Encontrar próximo IP disponível
    var availableIp = FindNextAvailableIp(networkIp, prefixLength, allocatedIps);
    
    return $"{availableIp}/{prefixLength}";
}
```

### **5. Gerar Arquivo .conf para Download**

```csharp
public async Task<RouterWireGuardConfigDto> GenerateConfigFileAsync(
    Guid routerId,
    CancellationToken cancellationToken = default)
{
    var router = await _routerRepository.GetByIdAsync(routerId, cancellationToken);
    var peer = await _peerRepository.GetByRouterIdAsync(routerId, cancellationToken)
        .FirstOrDefaultAsync(cancellationToken);
    var vpnNetwork = await _vpnNetworkRepository.GetByIdAsync(peer.VpnNetworkId, cancellationToken);
    var allowedNetworks = await _routerAllowedNetworkRepository
        .GetByRouterIdAsync(routerId, cancellationToken);
    
    var config = new StringBuilder();
    config.AppendLine("[Interface]");
    config.AppendLine($"PrivateKey = {DecryptPrivateKey(peer.PrivateKey)}");
    config.AppendLine($"Address = {peer.AllowedIps}");
    config.AppendLine();
    config.AppendLine("[Peer]");
    config.AppendLine($"PublicKey = {GetServerPublicKey(vpnNetworkId)}");
    config.AppendLine($"Endpoint = {peer.Endpoint}:{peer.ListenPort}");
    
    // Adicionar todas as redes permitidas
    var allNetworks = new List<string> { vpnNetwork.Cidr };
    allNetworks.AddRange(allowedNetworks.Select(n => n.NetworkCidr));
    config.AppendLine($"AllowedIPs = {string.Join(", ", allNetworks)}");
    
    config.AppendLine("PersistentKeepalive = 25");
    
    return new RouterWireGuardConfigDto
    {
        ConfigContent = config.ToString(),
        FileName = $"router_{router.Name}_{routerId}.conf"
    };
}
```

---

## 📝 Estrutura de Arquivos WireGuard no Servidor

```
/etc/wireguard/
├── wg-tenant1.conf          # Interface principal do tenant 1
├── wg-tenant2.conf          # Interface principal do tenant 2
└── ...

# Exemplo de wg-tenant1.conf:
[Interface]
Address = 10.100.1.1/24
ListenPort = 51820
PrivateKey = <server_private_key>

[Peer]
# Router Matriz
PublicKey = <router_public_key>
AllowedIPs = 10.100.1.50/32, 10.0.1.0/24, 192.168.100.0/24
PersistentKeepalive = 25

[Peer]
# Router Filial
PublicKey = <router2_public_key>
AllowedIPs = 10.100.1.51/32, 10.0.2.0/24
PersistentKeepalive = 25
```

**Nota**: A API gerencia esses arquivos, mas também pode usar `wg set` para mudanças dinâmicas sem reiniciar a interface.

---

## 🔄 Sincronização DB ↔ WireGuard

### **Estratégia: DB como Source of Truth**

1. **Todas as mudanças** são feitas primeiro no DB
2. **Depois** aplicadas no WireGuard server
3. **Se WireGuard falhar**, rollback no DB (ou retry)

### **Exemplo: Adicionar Rede ao Router**

```csharp
public async Task AddNetworkToRouterAsync(
    Guid routerId,
    string networkCidr,
    CancellationToken cancellationToken = default)
{
    // 1. Salvar no DB
    await _routerAllowedNetworkRepository.CreateAsync(new RouterAllowedNetwork
    {
        RouterId = routerId,
        NetworkCidr = networkCidr
    }, cancellationToken);
    
    // 2. Buscar peer
    var peer = await _peerRepository.GetByRouterIdAsync(routerId, cancellationToken)
        .FirstOrDefaultAsync(cancellationToken);
    var allowedNetworks = await _routerAllowedNetworkRepository
        .GetByRouterIdAsync(routerId, cancellationToken);
    
    // 3. Reconstruir allowed-ips
    var allowedIps = new List<string> { peer.AllowedIps };
    allowedIps.AddRange(allowedNetworks.Select(n => n.NetworkCidr));
    
    // 4. Atualizar no WireGuard
    var interfaceName = GetInterfaceName(peer.VpnNetworkId);
    await ExecuteWireGuardCommandAsync(
        $"set {interfaceName} peer {peer.PublicKey} allowed-ips {string.Join(",", allowedIps)}"
    );
}
```

---

## 🔐 Segurança

### **1. Criptografia de Chaves Privadas**
- Chaves privadas devem ser **criptografadas** no banco
- Usar AES-256 com chave mestra (armazenada em variável de ambiente)

### **2. Permissões**
- API precisa rodar com permissões para executar `wg` (sudo ou grupo wireguard)
- Considerar usar `sudo wg` ou adicionar usuário ao grupo `wireguard`

### **3. Validação de IPs**
- Validar que IPs alocados estão dentro do CIDR da VPN
- Validar que redes permitidas não conflitam

---

## 🚀 Próximos Passos

### **Fase 1: Estrutura Base**
- [ ] Criar tabela `router_allowed_networks`
- [ ] Criar `IWireGuardServerService` e implementação
- [ ] Implementar `AllocateVpnIpAsync`
- [ ] Implementar `GenerateWireGuardKeys` (usar biblioteca real)

### **Fase 2: Provisionamento**
- [ ] Implementar `ProvisionRouterAsync`
- [ ] Integrar com `RouterService.CreateAsync`
- [ ] Testar criação de router e provisionamento automático

### **Fase 3: Gerenciamento de Redes**
- [ ] Implementar `AddNetworkToRouterAsync`
- [ ] Implementar `RemoveNetworkFromRouterAsync`
- [ ] Criar endpoints na API

### **Fase 4: Download de Configuração**
- [ ] Implementar `GenerateConfigFileAsync`
- [ ] Criar endpoint de download
- [ ] Testar importação no MikroTik

### **Fase 5: Sincronização e Monitoramento**
- [ ] Implementar job de sincronização (verificar se DB e WireGuard estão sincronizados)
- [ ] Adicionar logs e métricas
- [ ] Criar dashboard de status dos peers

---

## 📚 Referências

- [WireGuard Quick Start](https://www.wireguard.com/quickstart/)
- [WireGuard Configuration](https://www.wireguard.com/#simple-network-interface)
- [MikroTik WireGuard](https://help.mikrotik.com/docs/display/ROS/WireGuard)

---

## ❓ Questões para Discussão

1. **Alocação de IPs**: Preferimos alocação automática ou manual?
2. **Rede padrão**: Todo router deve ter acesso à rede VPN principal automaticamente?
3. **Limites**: Quantas redes um router pode ter? Há limite?
4. **Backup**: Como fazer backup das configurações WireGuard?
5. **Monitoramento**: Como monitorar status dos peers (handshake, tráfego)?

---

**Pronto para discutir e implementar!** 🚀

