#!/bin/bash

# Script completo de setup do servidor
# Execute este script no servidor para configurar tudo

set -e

echo "🚀 Configurando servidor Automais.io..."

# 1. Instalar .NET 8
echo ""
echo "📦 Instalando .NET 8..."
if ! command -v dotnet &> /dev/null; then
    wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
    dpkg -i packages-microsoft-prod.deb
    rm packages-microsoft-prod.deb
    apt-get update
    apt-get install -y dotnet-sdk-8.0 aspnetcore-runtime-8.0
    echo "✅ .NET 8 instalado: $(dotnet --version)"
else
    echo "✅ .NET já instalado: $(dotnet --version)"
fi

# 2. Criar diretórios
echo ""
echo "📁 Criando diretórios..."
mkdir -p /root/automais.io/server.io
mkdir -p /backups/routers
echo "✅ Diretórios criados"

# 3. Configurar serviço systemd
echo ""
echo "⚙️ Configurando serviço systemd..."
if [ -f "automais-api.service" ]; then
    cp automais-api.service /etc/systemd/system/
    systemctl daemon-reload
    echo "✅ Serviço configurado"
else
    echo "⚠️ Arquivo automais-api.service não encontrado. Copie-o manualmente."
fi

# 4. Verificar estrutura
echo ""
echo "📋 Estrutura de diretórios:"
echo "  /root/automais.io/server.io/ - Aplicação"
echo "  /backups/routers/ - Backups dos routers"

echo ""
echo "✅ Setup concluído!"
echo ""
echo "Próximos passos:"
echo "  1. Faça o deploy da aplicação (via GitHub Actions ou manualmente)"
echo "  2. Inicie o serviço: systemctl start automais-api.service"
echo "  3. Habilite na inicialização: systemctl enable automais-api.service"
echo "  4. Verifique logs: journalctl -u automais-api.service -f"

