#!/bin/bash

# Script de instalação e configuração do Nginx + Let's Encrypt
# Uso: sudo ./install-nginx-letsencrypt.sh

set -e

# Cores para output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}🚀 Instalando Nginx + Let's Encrypt para Automais.io${NC}\n"

# Verificar se está rodando como root
if [ "$EUID" -ne 0 ]; then 
    echo -e "${RED}❌ Por favor, execute como root ou com sudo${NC}"
    exit 1
fi

# Variáveis
DOMAIN="automais.io"
EMAIL=""
FRONTEND_DIR="/var/www/automais.io"
NGINX_CONFIG="/etc/nginx/sites-available/automais.io"

# Solicitar email para Let's Encrypt
read -p "Digite seu email para Let's Encrypt: " EMAIL
if [ -z "$EMAIL" ]; then
    echo -e "${RED}❌ Email é obrigatório${NC}"
    exit 1
fi

echo -e "\n${YELLOW}📦 Passo 1: Instalando Nginx...${NC}"
apt update
apt install -y nginx

echo -e "\n${YELLOW}🔒 Passo 2: Instalando Certbot...${NC}"
apt install -y certbot python3-certbot-nginx

echo -e "\n${YELLOW}📁 Passo 3: Criando diretório do frontend...${NC}"
mkdir -p "$FRONTEND_DIR"
chown -R www-data:www-data "$FRONTEND_DIR"
chmod -R 755 "$FRONTEND_DIR"

# Criar arquivo de teste
echo "<h1>Automais.io Frontend</h1><p>Configure seu build React aqui.</p>" > "$FRONTEND_DIR/index.html"
chown www-data:www-data "$FRONTEND_DIR/index.html"

echo -e "\n${YELLOW}⚙️ Passo 4: Configurando Nginx...${NC}"

# Criar configuração do Nginx
cat > "$NGINX_CONFIG" << 'EOF'
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
EOF

# Habilitar site
ln -sf "$NGINX_CONFIG" /etc/nginx/sites-enabled/

# Remover configuração padrão
rm -f /etc/nginx/sites-enabled/default

# Testar configuração
echo -e "\n${YELLOW}🧪 Testando configuração do Nginx...${NC}"
if nginx -t; then
    echo -e "${GREEN}✅ Configuração do Nginx está correta${NC}"
    systemctl reload nginx
else
    echo -e "${RED}❌ Erro na configuração do Nginx${NC}"
    exit 1
fi

echo -e "\n${YELLOW}🔐 Passo 5: Obtendo certificado SSL...${NC}"
echo -e "${YELLOW}⚠️  Certifique-se de que o domínio $DOMAIN está apontando para este servidor!${NC}"
read -p "Pressione ENTER para continuar..."

# Obter certificado SSL
certbot --nginx -d "$DOMAIN" -d "www.$DOMAIN" \
    --email "$EMAIL" \
    --agree-tos \
    --non-interactive \
    --redirect

if [ $? -eq 0 ]; then
    echo -e "${GREEN}✅ Certificado SSL obtido com sucesso!${NC}"
else
    echo -e "${RED}❌ Erro ao obter certificado SSL${NC}"
    echo -e "${YELLOW}💡 Verifique se:${NC}"
    echo -e "   - O domínio está apontando para este servidor"
    echo -e "   - As portas 80 e 443 estão abertas no firewall"
    exit 1
fi

echo -e "\n${YELLOW}🔄 Passo 6: Configurando renovação automática...${NC}"
systemctl enable certbot.timer
systemctl start certbot.timer

# Testar renovação
echo -e "\n${YELLOW}🧪 Testando renovação automática...${NC}"
certbot renew --dry-run

echo -e "\n${YELLOW}🔥 Passo 7: Configurando firewall...${NC}"
if command -v ufw &> /dev/null; then
    ufw allow 'Nginx Full'
    ufw allow 80/tcp
    ufw allow 443/tcp
    echo -e "${GREEN}✅ Regras do firewall configuradas (UFW)${NC}"
else
    echo -e "${YELLOW}⚠️  UFW não encontrado. Configure o firewall manualmente.${NC}"
fi

echo -e "\n${GREEN}✅ Instalação concluída com sucesso!${NC}\n"

echo -e "${GREEN}📋 Próximos passos:${NC}"
echo -e "1. Faça o build do frontend: ${YELLOW}cd front.io && npm run build${NC}"
echo -e "2. Envie os arquivos para: ${YELLOW}/var/www/automais.io${NC}"
echo -e "3. Ajuste as permissões: ${YELLOW}sudo chown -R www-data:www-data /var/www/automais.io${NC}"
echo -e "4. Acesse: ${YELLOW}https://$DOMAIN${NC}\n"

echo -e "${GREEN}🔍 Comandos úteis:${NC}"
echo -e "   Ver status: ${YELLOW}sudo systemctl status nginx${NC}"
echo -e "   Ver logs: ${YELLOW}sudo tail -f /var/log/nginx/automais-front-error.log${NC}"
echo -e "   Testar SSL: ${YELLOW}sudo certbot certificates${NC}"
echo -e "   Renovar SSL: ${YELLOW}sudo certbot renew${NC}\n"

