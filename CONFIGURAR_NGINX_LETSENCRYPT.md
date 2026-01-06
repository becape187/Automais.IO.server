# 🌐 Configuração Nginx + Let's Encrypt para Frontend

Este guia mostra como configurar o Nginx com SSL (Let's Encrypt) para servir o frontend React do Automais.io.

## 📋 Pré-requisitos

- Ubuntu 20.04, 22.04 ou 24.04
- Domínio configurado apontando para o servidor (ex: `automais.io`)
- Acesso root ou sudo
- Portas 80 e 443 abertas no firewall

## 🔧 Passo 1: Instalar Nginx

```bash
# Atualizar lista de pacotes
sudo apt update

# Instalar Nginx
sudo apt install -y nginx

# Verificar status
sudo systemctl status nginx

# Habilitar Nginx para iniciar no boot
sudo systemctl enable nginx
```

## 🔒 Passo 2: Instalar Certbot (Let's Encrypt)

```bash
# Instalar Certbot e plugin do Nginx
sudo apt install -y certbot python3-certbot-nginx

# Verificar instalação
certbot --version
```

## 📁 Passo 3: Preparar Diretório do Frontend

```bash
# Criar diretório para o frontend
sudo mkdir -p /var/www/automais.io

# Dar permissões corretas
sudo chown -R $USER:$USER /var/www/automais.io
sudo chmod -R 755 /var/www/automais.io

# Criar arquivo de teste (opcional, para testar)
echo "<h1>Automais.io Frontend</h1>" | sudo tee /var/www/automais.io/index.html
```

## ⚙️ Passo 4: Configurar Nginx

### 4.1 Criar arquivo de configuração

```bash
# Criar arquivo de configuração
sudo nano /etc/nginx/sites-available/automais.io
```

### 4.2 Configuração inicial (HTTP - antes do SSL)

Cole o seguinte conteúdo:

```nginx
server {
    listen 80;
    listen [::]:80;
    server_name automais.io www.automais.io;

    # Root do React build
    root /var/www/automais.io;
    index index.html;

    # Logs
    access_log /var/log/nginx/automais-front-access.log;
    error_log /var/log/nginx/automais-front-error.log;

    # Gzip compression
    gzip on;
    gzip_vary on;
    gzip_min_length 1024;
    gzip_types text/plain text/css text/xml text/javascript 
               application/x-javascript application/javascript 
               application/xml+rss application/json;

    # Proxy para API backend
    location /api {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
        
        # Timeouts
        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;
    }

    # Cache para assets estáticos
    location ~* \.(jpg|jpeg|png|gif|ico|css|js|svg|woff|woff2|ttf|eot)$ {
        expires 1y;
        add_header Cache-Control "public, immutable";
    }

    # React Router - SPA fallback
    location / {
        try_files $uri $uri/ /index.html;
    }
}
```

### 4.3 Habilitar site

```bash
# Criar link simbólico
sudo ln -s /etc/nginx/sites-available/automais.io /etc/nginx/sites-enabled/

# Remover configuração padrão (opcional)
sudo rm /etc/nginx/sites-enabled/default

# Testar configuração
sudo nginx -t

# Se tudo estiver OK, recarregar Nginx
sudo systemctl reload nginx
```

## 🔐 Passo 5: Configurar SSL com Let's Encrypt

### 5.1 Obter certificado SSL

```bash
# Obter certificado SSL (substitua pelo seu email)
sudo certbot --nginx -d automais.io -d www.automais.io --email seu-email@exemplo.com --agree-tos --non-interactive

# OU modo interativo (recomendado na primeira vez)
sudo certbot --nginx -d automais.io -d www.automais.io
```

O Certbot irá:
- Verificar o domínio
- Obter o certificado SSL
- Configurar automaticamente o Nginx para usar HTTPS
- Configurar redirecionamento HTTP → HTTPS

### 5.2 Verificar renovação automática

```bash
# Testar renovação (dry-run)
sudo certbot renew --dry-run

# Verificar timer do systemd
sudo systemctl status certbot.timer

# Habilitar renovação automática (já vem habilitado por padrão)
sudo systemctl enable certbot.timer
```

## ✅ Passo 6: Verificar Configuração Final

Após o Certbot, sua configuração deve estar assim:

```bash
# Ver configuração final
sudo cat /etc/nginx/sites-available/automais.io
```

O Certbot terá adicionado automaticamente:
- Bloco `server` para HTTPS (porta 443)
- Certificados SSL
- Redirecionamento HTTP → HTTPS

## 🚀 Passo 7: Deploy do Frontend

### 7.1 Build do Frontend React

No seu ambiente de desenvolvimento:

```bash
# Navegar até o diretório do frontend
cd front.io

# Instalar dependências
npm install

# Build para produção
npm run build

# O build será gerado em: front.io/dist
```

### 7.2 Upload para o servidor

```bash
# Usando SCP (do seu computador local)
scp -r front.io/dist/* usuario@servidor:/var/www/automais.io/

# OU usando rsync (mais eficiente)
rsync -avz --delete front.io/dist/ usuario@servidor:/var/www/automais.io/

# OU manualmente via SFTP/FTP
```

### 7.3 Ajustar permissões no servidor

```bash
# Ajustar permissões
sudo chown -R www-data:www-data /var/www/automais.io
sudo chmod -R 755 /var/www/automais.io

# Recarregar Nginx
sudo systemctl reload nginx
```

## 📝 Script de Deploy Automatizado

Crie um script para facilitar o deploy:

```bash
# Criar script
sudo nano /usr/local/bin/deploy-frontend.sh
```

Cole o seguinte:

```bash
#!/bin/bash

# Script de deploy do frontend Automais.io
# Uso: sudo /usr/local/bin/deploy-frontend.sh

set -e

FRONTEND_DIR="/var/www/automais.io"
BACKUP_DIR="/var/backups/automais.io"
DATE=$(date +%Y%m%d_%H%M%S)

echo "🚀 Iniciando deploy do frontend..."

# Criar backup
if [ -d "$FRONTEND_DIR" ]; then
    echo "📦 Criando backup..."
    sudo mkdir -p "$BACKUP_DIR"
    sudo cp -r "$FRONTEND_DIR" "$BACKUP_DIR/frontend_$DATE"
    echo "✅ Backup criado em: $BACKUP_DIR/frontend_$DATE"
fi

# Aguardar upload manual ou usar rsync
echo "⏳ Aguardando upload dos arquivos..."
echo "   Use: rsync -avz --delete front.io/dist/ usuario@servidor:/var/www/automais.io/"
read -p "Pressione ENTER após fazer o upload..."

# Ajustar permissões
echo "🔧 Ajustando permissões..."
sudo chown -R www-data:www-data "$FRONTEND_DIR"
sudo chmod -R 755 "$FRONTEND_DIR"

# Recarregar Nginx
echo "🔄 Recarregando Nginx..."
sudo systemctl reload nginx

echo "✅ Deploy concluído com sucesso!"
echo "🌐 Acesse: https://automais.io"
```

Tornar executável:

```bash
sudo chmod +x /usr/local/bin/deploy-frontend.sh
```

## 🔍 Verificação e Testes

### Testar configuração Nginx

```bash
# Verificar sintaxe
sudo nginx -t

# Ver status
sudo systemctl status nginx

# Ver logs
sudo tail -f /var/log/nginx/automais-front-access.log
sudo tail -f /var/log/nginx/automais-front-error.log
```

### Testar SSL

```bash
# Verificar certificado SSL
sudo certbot certificates

# Testar renovação
sudo certbot renew --dry-run

# Verificar SSL online
# Acesse: https://www.ssllabs.com/ssltest/analyze.html?d=automais.io
```

### Testar acesso

```bash
# Testar HTTP (deve redirecionar para HTTPS)
curl -I http://automais.io

# Testar HTTPS
curl -I https://automais.io

# Testar API
curl https://automais.io/api/health
```

## 🔥 Configuração do Firewall

```bash
# Se estiver usando UFW
sudo ufw allow 'Nginx Full'
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw status

# Se estiver usando iptables
sudo iptables -A INPUT -p tcp --dport 80 -j ACCEPT
sudo iptables -A INPUT -p tcp --dport 443 -j ACCEPT
```

## 🛠️ Troubleshooting

### Problema: Nginx não inicia

```bash
# Verificar logs de erro
sudo journalctl -u nginx -n 50

# Verificar sintaxe
sudo nginx -t

# Verificar se a porta está em uso
sudo netstat -tulpn | grep :80
sudo netstat -tulpn | grep :443
```

### Problema: Certificado SSL não funciona

```bash
# Verificar certificados
sudo certbot certificates

# Renovar manualmente
sudo certbot renew

# Verificar logs do Certbot
sudo tail -f /var/log/letsencrypt/letsencrypt.log
```

### Problema: Frontend não carrega

```bash
# Verificar permissões
ls -la /var/www/automais.io

# Verificar se index.html existe
ls -la /var/www/automais.io/index.html

# Verificar logs do Nginx
sudo tail -f /var/log/nginx/automais-front-error.log
```

### Problema: API não responde

```bash
# Verificar se a API está rodando
sudo systemctl status automais-api

# Verificar se a porta 5000 está acessível
curl http://localhost:5000/api/health

# Verificar configuração do proxy no Nginx
sudo cat /etc/nginx/sites-available/automais.io | grep -A 10 "location /api"
```

## 📊 Monitoramento

### Ver estatísticas do Nginx

```bash
# Ver requisições em tempo real
sudo tail -f /var/log/nginx/automais-front-access.log | awk '{print $1}' | sort | uniq -c | sort -rn

# Ver erros
sudo tail -f /var/log/nginx/automais-front-error.log
```

### Renovação automática do SSL

O Certbot já configura renovação automática, mas você pode verificar:

```bash
# Ver status do timer
sudo systemctl status certbot.timer

# Ver quando será a próxima renovação
sudo systemctl list-timers certbot.timer
```

## 🔄 Atualização da Configuração

Se precisar atualizar a configuração do Nginx:

```bash
# Editar configuração
sudo nano /etc/nginx/sites-available/automais.io

# Testar
sudo nginx -t

# Recarregar (sem downtime)
sudo systemctl reload nginx

# OU reiniciar (com pequeno downtime)
sudo systemctl restart nginx
```

## 📚 Recursos Adicionais

- **Nginx Documentation**: https://nginx.org/en/docs/
- **Certbot Documentation**: https://certbot.eff.org/
- **Let's Encrypt**: https://letsencrypt.org/
- **SSL Labs Test**: https://www.ssllabs.com/ssltest/

## ✅ Checklist de Configuração

- [ ] Nginx instalado e rodando
- [ ] Certbot instalado
- [ ] Domínio apontando para o servidor
- [ ] Certificado SSL obtido e configurado
- [ ] Redirecionamento HTTP → HTTPS funcionando
- [ ] Frontend buildado e enviado para `/var/www/automais.io`
- [ ] Permissões corretas configuradas
- [ ] API backend acessível via proxy `/api`
- [ ] Firewall configurado (portas 80 e 443)
- [ ] Renovação automática do SSL configurada
- [ ] Logs sendo monitorados
- [ ] Testes de acesso realizados

---

**Nota**: Lembre-se de atualizar a variável `API_URL` no frontend para apontar para `https://automais.io/api` em produção.

