#!/bin/bash
# Script para testar se as variáveis de ambiente estão sendo carregadas corretamente

echo "=== Testando variáveis de ambiente do serviço automais-api ==="
echo ""

# Verificar se o serviço existe
if ! systemctl list-unit-files | grep -q automais-api.service; then
    echo "❌ Serviço automais-api.service não encontrado!"
    exit 1
fi

echo "1. Variáveis de ambiente definidas no serviço:"
systemctl show automais-api.service | grep -E "^Environment=" | sed 's/DB_PASSWORD=.*/DB_PASSWORD=***/' | sed 's/CHIRPSTACK_API_TOKEN=.*/CHIRPSTACK_API_TOKEN=***/'
echo ""

echo "2. Testando substituição de variáveis (simulando o que o .NET faz):"
DB_HOST=$(systemctl show automais-api.service | grep "DB_HOST=" | cut -d'=' -f2)
DB_PORT=$(systemctl show automais-api.service | grep "DB_PORT=" | cut -d'=' -f2)
DB_NAME=$(systemctl show automais-api.service | grep "DB_NAME=" | cut -d'=' -f2)
DB_USER=$(systemctl show automais-api.service | grep "DB_USER=" | cut -d'=' -f2)
DB_PASSWORD=$(systemctl show automais-api.service | grep "DB_PASSWORD=" | cut -d'=' -f2)

if [ -z "$DB_HOST" ]; then
    echo "❌ DB_HOST não está definida!"
else
    echo "✅ DB_HOST=$DB_HOST"
fi

if [ -z "$DB_PORT" ]; then
    echo "❌ DB_PORT não está definida!"
else
    echo "✅ DB_PORT=$DB_PORT"
fi

if [ -z "$DB_NAME" ]; then
    echo "❌ DB_NAME não está definida!"
else
    echo "✅ DB_NAME=$DB_NAME"
fi

if [ -z "$DB_USER" ]; then
    echo "❌ DB_USER não está definida!"
else
    echo "✅ DB_USER=$DB_USER"
fi

if [ -z "$DB_PASSWORD" ]; then
    echo "❌ DB_PASSWORD não está definida!"
else
    echo "✅ DB_PASSWORD=***"
fi

echo ""
echo "3. Connection string resultante seria:"
echo "Host=${DB_HOST};Port=${DB_PORT};Database=${DB_NAME};Username=${DB_USER};Password=***;Ssl Mode=Require"
echo ""

echo "4. Verificando se o serviço está rodando:"
if systemctl is-active --quiet automais-api.service; then
    echo "✅ Serviço está ATIVO"
else
    echo "❌ Serviço NÃO está ativo"
    echo "   Status: $(systemctl is-active automais-api.service)"
fi
echo ""

echo "5. Últimas 20 linhas dos logs:"
journalctl -u automais-api.service -n 20 --no-pager
echo ""

echo "=== Fim do teste ==="

