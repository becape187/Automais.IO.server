# Troubleshooting SSH - GitHub Actions

## Erro: "connection reset by peer"

Este erro geralmente indica que o servidor está rejeitando a conexão SSH.

### 1. Verificar se autenticação por senha está habilitada

No servidor (206.81.13.44):

```bash
# Verificar configuração SSH
sudo nano /etc/ssh/sshd_config

# Certifique-se de que estas linhas estão assim:
PasswordAuthentication yes
PubkeyAuthentication yes
PermitRootLogin yes  # ou PermitRootLogin prohibit-password (se usar chave)

# Depois, reiniciar SSH
sudo systemctl restart sshd
```

### 2. Verificar firewall

```bash
# Verificar se a porta 22 está aberta
sudo ufw status
# ou
sudo iptables -L -n | grep 22

# Se necessário, abrir porta 22
sudo ufw allow 22/tcp
sudo ufw reload
```

### 3. Verificar se o servidor aceita conexões do GitHub Actions

Os IPs do GitHub Actions podem variar. Você pode:

**Opção A: Permitir todos os IPs (menos seguro, mas funciona)**

```bash
# No servidor, verificar se há restrições de IP
sudo grep -i "AllowUsers\|DenyUsers\|AllowGroups\|DenyGroups" /etc/ssh/sshd_config
```

**Opção B: Usar chave SSH ao invés de senha (mais seguro)**

Veja instruções abaixo.

### 4. Testar conexão manualmente

Do seu computador local:

```bash
# Testar conexão SSH
ssh -v root@206.81.13.44

# Se funcionar localmente mas não no GitHub Actions, pode ser:
# - Firewall bloqueando IPs do GitHub
# - Rate limiting no servidor
```

### 5. Verificar logs do SSH no servidor

```bash
# Ver logs de tentativas de conexão
sudo tail -f /var/log/auth.log
# ou
sudo journalctl -u sshd -f
```

## Alternativa: Usar Chave SSH (Recomendado)

Se autenticação por senha não funcionar, use chave SSH:

### No servidor:

```bash
# Criar diretório .ssh se não existir
mkdir -p ~/.ssh
chmod 700 ~/.ssh

# Criar arquivo authorized_keys se não existir
touch ~/.ssh/authorized_keys
chmod 600 ~/.ssh/authorized_keys
```

### No GitHub Actions:

1. Gere uma chave SSH no servidor:
```bash
ssh-keygen -t ed25519 -C "github-actions" -f ~/.ssh/github_actions -N ""
cat ~/.ssh/github_actions.pub >> ~/.ssh/authorized_keys
```

2. Copie a chave privada:
```bash
cat ~/.ssh/github_actions
```

3. Adicione como secret no GitHub:
   - Vá em: Settings → Secrets and variables → Actions
   - Adicione: `SERVER_SSH_KEY` com o conteúdo da chave privada

4. Atualize o workflow para usar `key` ao invés de `password`

## Verificar conectividade do GitHub Actions

O GitHub Actions roda em runners que podem ter IPs diferentes. Para verificar:

1. Adicione um step temporário no workflow para ver o IP:
```yaml
- name: Show IP
  run: curl ifconfig.me
```

2. Depois, no servidor, permita esse IP temporariamente para teste.

## Solução Rápida: Testar com IP específico

Se você souber o IP do runner do GitHub Actions, pode permitir temporariamente:

```bash
# No servidor
sudo ufw allow from <IP_DO_GITHUB_ACTIONS> to any port 22
```

Mas isso não é prático pois os IPs mudam.

## Recomendação Final

**Use chave SSH** ao invés de senha. É mais seguro e geralmente funciona melhor com GitHub Actions.

