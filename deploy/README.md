# Deploy do Automais.io Server

## Instalação Inicial no Servidor

### 1. Conectar ao servidor

```bash
ssh root@206.81.13.44
```

### 2. Instalar .NET 8

Execute o script de instalação:

```bash
cd /root/automais.io/server.io
# Copie o arquivo install.sh para o servidor primeiro
chmod +x install.sh
./install.sh
```

Ou instale manualmente:

```bash
# Adicionar repositório Microsoft
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Atualizar e instalar
apt-get update
apt-get install -y dotnet-sdk-8.0
apt-get install -y aspnetcore-runtime-8.0

# Verificar
dotnet --version
```

### 3. Configurar o serviço systemd

```bash
# Copiar arquivo de serviço
cp automais-api.service /etc/systemd/system/

# Recarregar systemd
systemctl daemon-reload

# Habilitar na inicialização
systemctl enable automais-api.service

# Iniciar serviço
systemctl start automais-api.service

# Verificar status
systemctl status automais-api.service
```

### 4. Verificar logs

```bash
# Ver logs em tempo real
journalctl -u automais-api.service -f

# Ver últimas 100 linhas
journalctl -u automais-api.service -n 100
```

## Configuração do GitHub Actions

### Secrets necessários no GitHub

Vá em **Settings → Secrets and variables → Actions** e adicione:

- `SERVER_HOST`: `206.81.13.44`
- `SERVER_USER`: `root`
- `SERVER_PASSWORD`: Senha do usuário root do servidor
- `SERVER_PORT`: `22` (opcional, padrão é 22)

**Nota**: A autenticação é feita via usuário e senha SSH. Certifique-se de que o servidor permite autenticação por senha (PasswordAuthentication yes no `/etc/ssh/sshd_config`).

## Estrutura de Diretórios no Servidor

```
/root/automais.io/
├── server.io/          # Aplicação compilada
│   ├── Automais.Api.dll
│   ├── appsettings.json
│   └── ... (outros arquivos publicados)
└── front.io/           # Frontend (se necessário)

/backups/
└── routers/            # Backups dos routers
```

## Comandos Úteis

```bash
# Reiniciar serviço
systemctl restart automais-api.service

# Parar serviço
systemctl stop automais-api.service

# Iniciar serviço
systemctl start automais-api.service

# Ver status
systemctl status automais-api.service

# Ver logs
journalctl -u automais-api.service -f

# Verificar se está rodando na porta 5000
netstat -tlnp | grep 5000
# ou
ss -tlnp | grep 5000
```

## Atualizar Variáveis de Ambiente

Edite o arquivo de serviço:

```bash
nano /etc/systemd/system/automais-api.service
```

Depois de editar, recarregue e reinicie:

```bash
systemctl daemon-reload
systemctl restart automais-api.service
```

## Troubleshooting

### Serviço não inicia

```bash
# Ver logs detalhados
journalctl -u automais-api.service -n 50 --no-pager

# Verificar se o .NET está instalado
dotnet --version

# Verificar se o arquivo DLL existe
ls -la /root/automais.io/server.io/Automais.Api.dll
```

### Porta já em uso

```bash
# Verificar o que está usando a porta 5000
lsof -i :5000
# ou
netstat -tlnp | grep 5000

# Matar processo se necessário
kill -9 <PID>
```

### Permissões

```bash
# Garantir que o diretório tem as permissões corretas
chown -R root:root /root/automais.io/server.io
chmod +x /root/automais.io/server.io/Automais.Api.dll
```

