# Como Habilitar API RouterOS no Mikrotik

## ⚠️ Erro: Connection Refused na Porta 8728

Se você está recebendo o erro "Connection refused" ao tentar conectar na API RouterOS, significa que:

1. ❌ O serviço da API RouterOS não está habilitado
2. ❌ A porta 8728 está bloqueada pelo firewall
3. ❌ O serviço está configurado para aceitar apenas conexões locais

## ✅ Solução: Habilitar API RouterOS

### Opção 1: Via Terminal (SSH/Telnet)

```bash
# Verificar status atual do serviço API
/ip service print where name=api

# Habilitar o serviço API
/ip service enable api

# Configurar para aceitar conexões de qualquer IP (ou apenas da VPN)
/ip service set api disabled=no

# Se quiser restringir apenas para IPs da VPN (mais seguro)
/ip service set api address=10.222.111.0/24

# Verificar se está habilitado
/ip service print where name=api
```

### Opção 2: Via Winbox/WebFig

1. Abra o Winbox ou WebFig
2. Vá em **IP** → **Services**
3. Encontre o serviço **api**
4. Clique duas vezes para editar
5. Marque **Enabled**
6. Em **Available From**, configure:
   - **0.0.0.0/0** (aceita de qualquer lugar - menos seguro)
   - **10.222.111.0/24** (aceita apenas da VPN - mais seguro)
7. Clique em **OK**

### Opção 3: Permitir Porta no Firewall

Se o serviço está habilitado mas ainda não funciona, pode ser bloqueio do firewall:

```bash
# Verificar regras de firewall que bloqueiam a porta 8728
/ip firewall filter print where dst-port=8728

# Adicionar regra para permitir conexões na porta 8728 (API RouterOS)
/ip firewall filter add chain=input protocol=tcp dst-port=8728 action=accept comment="Allow RouterOS API"

# Se quiser permitir apenas da VPN:
/ip firewall filter add chain=input protocol=tcp dst-port=8728 src-address=10.222.111.0/24 action=accept comment="Allow RouterOS API from VPN"
```

## 🔍 Verificar se Está Funcionando

### Teste 1: Verificar se o serviço está escutando

```bash
# No servidor Linux, teste se a porta está aberta
telnet 10.222.111.2 8728

# Ou usando nc (netcat)
nc -zv 10.222.111.2 8728

# Se conectar, você verá algo como:
# Connected to 10.222.111.2
```

### Teste 2: Verificar no Mikrotik

```bash
# Ver status do serviço
/ip service print where name=api

# Deve mostrar algo como:
# Flags: X - disabled, I - invalid
#  0  X  name=api port=8728 address=0.0.0.0/0 certificate=none

# Se tiver "X" na coluna Flags, está desabilitado
# Se não tiver "X", está habilitado
```

### Teste 3: Verificar firewall

```bash
# Ver regras de firewall que podem estar bloqueando
/ip firewall filter print where chain=input

# Verificar se há regras que bloqueiam a porta 8728
/ip firewall filter print where dst-port=8728
```

## 🔐 Segurança: Restringir Apenas para VPN

Para maior segurança, configure a API para aceitar apenas conexões da VPN:

```bash
# Configurar API para aceitar apenas da rede VPN
/ip service set api address=10.222.111.0/24

# Verificar configuração
/ip service print where name=api
```

Isso garante que apenas máquinas conectadas na VPN possam acessar a API RouterOS.

## 📋 Checklist de Troubleshooting

- [ ] Serviço API está habilitado? (`/ip service enable api`)
- [ ] Porta 8728 está aberta no firewall?
- [ ] API está configurada para aceitar conexões da VPN?
- [ ] IP do router está correto (10.222.111.2)?
- [ ] Conectividade de rede está funcionando (ping funciona)?
- [ ] Usuário e senha estão corretos?

## 🚨 Problemas Comuns

### Problema 1: API habilitada mas ainda não conecta

**Solução**: Verifique o firewall:
```bash
/ip firewall filter print where chain=input
```

Adicione uma regra para permitir a porta 8728.

### Problema 2: Conecta mas autenticação falha

**Solução**: Verifique usuário e senha:
```bash
# Listar usuários
/user print

# Verificar se o usuário tem permissão para API
/user print where name=automais
```

### Problema 3: Timeout ao conectar

**Solução**: Verifique rotas e conectividade:
```bash
# No servidor, teste ping
ping 10.222.111.2

# Verifique rotas
ip route get 10.222.111.2
```

## 📝 Notas Importantes

1. **Porta padrão**: A API RouterOS usa a porta **8728** por padrão
2. **API-SSL**: Se usar API-SSL, a porta padrão é **8729**
3. **Segurança**: Sempre restrinja o acesso da API apenas para IPs confiáveis (VPN)
4. **Firewall**: O firewall do Mikrotik pode bloquear conexões mesmo com o serviço habilitado

