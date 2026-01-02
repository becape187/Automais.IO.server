# 🚀 Instruções Rápidas de Deploy

## No Servidor (206.81.13.44)

### 1. Conectar ao servidor
```bash
ssh root@206.81.13.44
```

### 2. Instalar .NET 8
```bash
# Adicionar repositório Microsoft
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Atualizar e instalar
apt-get update
apt-get install -y dotnet-sdk-8.0 aspnetcore-runtime-8.0

# Verificar
dotnet --version
```

### 3. Criar diretórios
```bash
mkdir -p /root/automais.io/server.io
mkdir -p /backups/routers
```

### 4. Copiar arquivo de serviço
```bash
# Copie o arquivo automais-api.service para o servidor
# Depois execute:
cp automais-api.service /etc/systemd/system/
systemctl daemon-reload
systemctl enable automais-api.service
```

### 5. Primeiro deploy manual (teste)

Depois que o GitHub Action fizer o deploy automático, ou faça manualmente:

```bash
cd /root/automais.io/server.io

# Se tiver o arquivo deploy.tar.gz:
tar -xzf deploy.tar.gz
rm deploy.tar.gz

# Iniciar serviço
systemctl start automais-api.service
systemctl status automais-api.service
```

### 6. Verificar se está rodando
```bash
# Ver logs
journalctl -u automais-api.service -f

# Verificar porta
netstat -tlnp | grep 5000
```

## Configurar GitHub Secrets

No GitHub: **Settings → Secrets and variables → Actions**

Adicione:
- `SERVER_HOST`: `206.81.13.44`
- `SERVER_USER`: `root`
- `SERVER_PASSWORD`: Senha do usuário root
- `SERVER_PORT`: `22` (opcional)

**Importante**: Certifique-se de que o servidor permite autenticação SSH por senha. Verifique em `/etc/ssh/sshd_config` se `PasswordAuthentication yes` está habilitado.

## Comandos Úteis

```bash
# Reiniciar serviço
systemctl restart automais-api.service

# Ver status
systemctl status automais-api.service

# Ver logs
journalctl -u automais-api.service -f

# Parar serviço
systemctl stop automais-api.service
```

