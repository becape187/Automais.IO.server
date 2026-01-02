# Instruções para Instalar o Serviço Systemd no Servidor

## Arquivo de Serviço

O arquivo `automais-api.service` está localizado em `deploy/automais-api.service`.

## Instalação Manual (se necessário)

Se precisar instalar manualmente no servidor:

### 1. Copiar o arquivo de serviço

```bash
# No servidor (206.81.13.44)
sudo cp /tmp/automais-api.service /etc/systemd/system/automais-api.service
```

Ou se você já tiver o arquivo localmente:

```bash
# No seu computador local
scp deploy/automais-api.service root@206.81.13.44:/tmp/

# No servidor
sudo cp /tmp/automais-api.service /etc/systemd/system/automais-api.service
```

### 2. Recarregar systemd

```bash
sudo systemctl daemon-reload
```

### 3. Habilitar o serviço (para iniciar automaticamente no boot)

```bash
sudo systemctl enable automais-api.service
```

### 4. Iniciar o serviço

```bash
sudo systemctl start automais-api.service
```

### 5. Verificar status

```bash
sudo systemctl status automais-api.service
```

### 6. Ver logs

```bash
# Logs em tempo real
sudo journalctl -u automais-api.service -f

# Últimas 100 linhas
sudo journalctl -u automais-api.service -n 100

# Logs desde hoje
sudo journalctl -u automais-api.service --since today
```

## Comandos Úteis

```bash
# Parar o serviço
sudo systemctl stop automais-api.service

# Reiniciar o serviço
sudo systemctl restart automais-api.service

# Desabilitar inicialização automática
sudo systemctl disable automais-api.service

# Verificar se está rodando
sudo systemctl is-active automais-api.service

# Verificar se está habilitado
sudo systemctl is-enabled automais-api.service
```

## Configuração do Serviço

O arquivo de serviço está configurado para:

- **Usuário**: `root`
- **Diretório de trabalho**: `/root/automais.io/server.io`
- **Executável**: `/usr/bin/dotnet /root/automais.io/server.io/Automais.Api.dll`
- **Porta**: `5000` (http://0.0.0.0:5000)
- **Ambiente**: `Production`
- **Reinício automático**: Sim (após 10 segundos em caso de falha)

## Variáveis de Ambiente

As variáveis de ambiente estão configuradas diretamente no arquivo de serviço. Se precisar alterar:

1. Edite o arquivo: `sudo nano /etc/systemd/system/automais-api.service`
2. Recarregue: `sudo systemctl daemon-reload`
3. Reinicie: `sudo systemctl restart automais-api.service`

## Troubleshooting

### Serviço não inicia

```bash
# Verificar erros
sudo journalctl -u automais-api.service -n 50 --no-pager

# Verificar se o .NET está instalado
dotnet --version

# Verificar se o arquivo DLL existe
ls -la /root/automais.io/server.io/Automais.Api.dll
```

### Porta já em uso

```bash
# Verificar qual processo está usando a porta 5000
sudo lsof -i :5000
# ou
sudo netstat -tulpn | grep 5000
```

### Permissões

```bash
# Verificar permissões do diretório
ls -la /root/automais.io/server.io/

# Se necessário, ajustar permissões
chmod +x /root/automais.io/server.io/Automais.Api.dll
```

