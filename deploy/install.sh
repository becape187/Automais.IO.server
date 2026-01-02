#!/bin/bash

# Script de instalação do .NET e configuração do serviço Automais.io

set -e

echo "🚀 Instalando .NET 8 SDK e Runtime..."

# Adicionar repositório Microsoft
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Atualizar pacotes
apt-get update

# Instalar .NET 8 SDK e Runtime
apt-get install -y dotnet-sdk-8.0
apt-get install -y aspnetcore-runtime-8.0

# Verificar instalação
echo "✅ .NET instalado:"
dotnet --version

echo ""
echo "📁 Criando diretórios..."
mkdir -p /root/automais.io/server.io
mkdir -p /backups/routers

echo ""
echo "📋 Copiando arquivo de serviço..."
cp automais-api.service /etc/systemd/system/

echo ""
echo "🔄 Recarregando systemd..."
systemctl daemon-reload

echo ""
echo "✅ Instalação concluída!"
echo ""
echo "Para iniciar o serviço, execute:"
echo "  systemctl start automais-api.service"
echo ""
echo "Para habilitar na inicialização:"
echo "  systemctl enable automais-api.service"
echo ""
echo "Para ver logs:"
echo "  journalctl -u automais-api.service -f"

