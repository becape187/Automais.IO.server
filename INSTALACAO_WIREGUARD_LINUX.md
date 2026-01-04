# 🔧 Instalação e Configuração do WireGuard no Linux

## 📋 Pré-requisitos

- Servidor Linux (Ubuntu/Debian recomendado)
- Acesso root ou sudo
- IP público configurado
- Porta UDP 51820 (ou outra de sua escolha) aberta no firewall

---

## 1️⃣ Instalação do WireGuard

### Ubuntu/Debian

```bash
# Atualizar pacotes
sudo apt update
sudo apt upgrade -y

# Instalar WireGuard
sudo apt install wireguard wireguard-tools -y

# Verificar instalação
wg --version
```

### CentOS/RHEL

```bash
# Instalar EPEL repository
sudo yum install epel-release -y

# Instalar WireGuard
sudo yum install wireguard-tools -y

# Verificar instalação
wg --version
```

---

## 2️⃣ Habilitar IP Forwarding

O WireGuard precisa que o IP forwarding esteja habilitado para rotear tráfego:

```bash
# Habilitar temporariamente
sudo sysctl -w net.ipv4.ip_forward=1

# Habilitar permanentemente
echo "net.ipv4.ip_forward=1" | sudo tee -a /etc/sysctl.conf
echo "net.ipv6.conf.all.forwarding=1" | sudo tee -a /etc/sysctl.conf

# Aplicar mudanças
sudo sysctl -p
```

---

## 3️⃣ Configuração Inicial

### Criar diretório de configurações

```bash
sudo mkdir -p /etc/wireguard
sudo chmod 700 /etc/wireguard
```

### Configurar Firewall (UFW)

```bash
# Permitir porta WireGuard (padrão 51820)
sudo ufw allow 51820/udp

# Ou se usar outra porta, substitua 51820 pela sua porta
# sudo ufw allow 51821/udp
```

### Configurar Firewall (iptables)

```bash
# Permitir porta WireGuard
sudo iptables -A INPUT -p udp --dport 51820 -j ACCEPT

# Salvar regras (Ubuntu/Debian)
sudo netfilter-persistent save

# Ou (CentOS/RHEL)
sudo service iptables save
```

---

## 4️⃣ Estrutura de Arquivos

O WireGuard usa arquivos de configuração em `/etc/wireguard/` com extensão `.conf`.

**Formato do nome**: `wg-{nome}.conf` (ex: `wg-tenant1.conf`, `wg-tenant2.conf`)

**Estrutura de um arquivo de configuração**:

```ini
[Interface]
# Chave privada do servidor
PrivateKey = <SERVER_PRIVATE_KEY>

# IP e máscara da interface WireGuard
Address = 10.100.1.1/24

# Porta de escuta
ListenPort = 51820

# Comandos pós-ativação (opcional)
PostUp = iptables -A FORWARD -i %i -j ACCEPT; iptables -A FORWARD -o %i -j ACCEPT; iptables -t nat -A POSTROUTING -o eth0 -j MASQUERADE
PostDown = iptables -D FORWARD -i %i -j ACCEPT; iptables -D FORWARD -o %i -j ACCEPT; iptables -t nat -D POSTROUTING -o eth0 -j MASQUERADE

[Peer]
# Router Matriz
PublicKey = <ROUTER_PUBLIC_KEY>
AllowedIPs = 10.100.1.50/32, 10.0.1.0/24, 192.168.100.0/24
PersistentKeepalive = 25

[Peer]
# Router Filial
PublicKey = <ROUTER2_PUBLIC_KEY>
AllowedIPs = 10.100.1.51/32, 10.0.2.0/24
PersistentKeepalive = 25
```

---

## 5️⃣ Permissões para a API

A API C# precisa executar comandos `wg` e `wg-quick`. Você tem duas opções:

### Opção 1: Adicionar usuário ao grupo wireguard (Recomendado)

```bash
# Criar grupo wireguard (se não existir)
sudo groupadd wireguard

# Adicionar usuário da API ao grupo
# Substitua 'www-data' pelo usuário que roda sua API (pode ser 'dotnet', 'automais', etc)
sudo usermod -aG wireguard www-data

# Dar permissões de execução para o grupo
sudo chmod 750 /usr/bin/wg
sudo chmod 750 /usr/bin/wg-quick
sudo chgrp wireguard /usr/bin/wg
sudo chgrp wireguard /usr/bin/wg-quick
```

### Opção 2: Usar sudo sem senha (Mais simples, menos seguro)

```bash
# Editar sudoers
sudo visudo

# Adicionar linha (substitua 'www-data' pelo usuário da API):
www-data ALL=(ALL) NOPASSWD: /usr/bin/wg, /usr/bin/wg-quick
```

### Opção 3: Executar API como root (Não recomendado para produção)

Se a API já roda como root (via systemd), não precisa de configuração adicional.

---

## 6️⃣ Testar Instalação

### Gerar chaves de teste

```bash
# Gerar chave privada
wg genkey | sudo tee /etc/wireguard/private.key
sudo chmod 600 /etc/wireguard/private.key

# Gerar chave pública a partir da privada
sudo cat /etc/wireguard/private.key | wg pubkey | sudo tee /etc/wireguard/public.key
```

### Criar interface de teste

```bash
# Criar arquivo de configuração de teste
sudo nano /etc/wireguard/wg0-test.conf
```

Conteúdo mínimo:

```ini
[Interface]
PrivateKey = <SUA_CHAVE_PRIVADA>
Address = 10.100.1.1/24
ListenPort = 51820
```

### Ativar interface

```bash
# Ativar interface
sudo wg-quick up wg0-test

# Verificar status
sudo wg show

# Desativar interface
sudo wg-quick down wg0-test
```

---

## 7️⃣ Configuração do Systemd Service

Para que as interfaces WireGuard iniciem automaticamente:

```bash
# Habilitar serviço WireGuard
sudo systemctl enable wg-quick@wg0-test

# Iniciar serviço
sudo systemctl start wg-quick@wg0-test

# Verificar status
sudo systemctl status wg-quick@wg0-test
```

**Nota**: O nome do serviço segue o padrão `wg-quick@{nome-da-interface}`. Se sua interface se chama `wg-tenant1`, o serviço será `wg-quick@wg-tenant1`.

---

## 8️⃣ Configuração de NAT (Opcional mas Recomendado)

Se os routers precisarem acessar a internet através do servidor WireGuard:

```bash
# Habilitar NAT no iptables
sudo iptables -t nat -A POSTROUTING -o eth0 -j MASQUERADE

# Substitua 'eth0' pela sua interface de rede principal
# Para descobrir: ip route | grep default

# Salvar regras
sudo netfilter-persistent save
```

Ou adicione no arquivo de configuração WireGuard:

```ini
[Interface]
...
PostUp = iptables -t nat -A POSTROUTING -o eth0 -j MASQUERADE
PostDown = iptables -t nat -D POSTROUTING -o eth0 -j MASQUERADE
```

---

## 9️⃣ Verificação Final

### Verificar se WireGuard está rodando

```bash
# Ver interfaces ativas
sudo wg show

# Ver todas as interfaces (incluindo inativas)
sudo wg show all

# Ver status de uma interface específica
sudo wg show wg-tenant1
```

### Verificar logs

```bash
# Logs do systemd
sudo journalctl -u wg-quick@wg-tenant1 -f

# Logs do kernel (WireGuard)
sudo dmesg | grep wireguard
```

### Testar conectividade

```bash
# Ping de um peer para outro (se configurado)
ping 10.100.1.50

# Ver estatísticas de tráfego
sudo wg show wg-tenant1 transfer
```

---

## 🔟 Comandos Úteis

### Gerenciar interfaces

```bash
# Ativar interface
sudo wg-quick up wg-tenant1

# Desativar interface
sudo wg-quick down wg-tenant1

# Recarregar configuração (sem reiniciar)
sudo wg syncconf wg-tenant1 <(wg-quick strip wg-tenant1)
```

### Adicionar/remover peers dinamicamente

```bash
# Adicionar peer
sudo wg set wg-tenant1 peer <PUBLIC_KEY> allowed-ips 10.100.1.50/32

# Remover peer
sudo wg set wg-tenant1 peer <PUBLIC_KEY> remove

# Ver peers ativos
sudo wg show wg-tenant1 peers
```

### Gerar chaves

```bash
# Gerar chave privada
wg genkey

# Gerar chave pública a partir de uma privada
echo "PRIVATE_KEY" | wg pubkey

# Gerar chave pré-compartilhada (opcional, para segurança extra)
wg genpsk
```

---

## 1️⃣1️⃣ Troubleshooting

### Interface não inicia

```bash
# Verificar erros
sudo wg-quick up wg-tenant1

# Verificar se porta está em uso
sudo netstat -ulnp | grep 51820

# Verificar permissões do arquivo
ls -la /etc/wireguard/wg-tenant1.conf
```

### Peer não conecta

```bash
# Verificar se peer está na configuração
sudo wg show wg-tenant1

# Verificar logs
sudo journalctl -u wg-quick@wg-tenant1 -n 50

# Verificar firewall
sudo ufw status
sudo iptables -L -n -v
```

### Tráfego não roteia

```bash
# Verificar IP forwarding
sysctl net.ipv4.ip_forward

# Verificar rotas
ip route show

# Verificar iptables
sudo iptables -L FORWARD -n -v
```

---

## 1️⃣2️⃣ Segurança

### Boas Práticas

1. **Mantenha chaves privadas seguras**:
   ```bash
   sudo chmod 600 /etc/wireguard/*.key
   sudo chmod 600 /etc/wireguard/*.conf
   ```

2. **Use firewall**:
   ```bash
   # Permitir apenas porta WireGuard
   sudo ufw default deny incoming
   sudo ufw allow 51820/udp
   ```

3. **Monitore conexões**:
   ```bash
   # Ver handshakes recentes
   sudo wg show wg-tenant1 latest-handshakes
   ```

4. **Rotacione chaves periodicamente** (recomendado a cada 90 dias)

---

## 📝 Checklist de Instalação

- [ ] WireGuard instalado (`wg --version`)
- [ ] IP forwarding habilitado (`sysctl net.ipv4.ip_forward`)
- [ ] Firewall configurado (porta 51820/udp aberta)
- [ ] Diretório `/etc/wireguard` criado com permissões corretas
- [ ] Permissões configuradas para usuário da API
- [ ] Interface de teste criada e funcionando
- [ ] NAT configurado (se necessário)
- [ ] Systemd service configurado (se necessário)

---

## 🚀 Próximos Passos

Após instalar e configurar o WireGuard no servidor:

1. **Teste manualmente** criando uma interface e adicionando um peer
2. **Configure a API** para usar os comandos `wg` e `wg-quick`
3. **Monitore logs** durante os primeiros testes
4. **Documente** as configurações específicas do seu ambiente

---

## 📚 Referências

- [WireGuard Quick Start](https://www.wireguard.com/quickstart/)
- [WireGuard Installation Guide](https://www.wireguard.com/install/)
- [Ubuntu WireGuard Guide](https://ubuntu.com/server/docs/wireguard-vpn)

---

**Pronto para usar!** 🎉

A API C# agora pode gerenciar o WireGuard usando os comandos `wg` e `wg-quick` configurados acima.

