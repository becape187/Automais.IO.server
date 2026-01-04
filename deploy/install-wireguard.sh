#!/bin/bash

# Script de instalação rápida do WireGuard no Linux
# Execute como root ou com sudo

set -e

echo "🔧 Instalando WireGuard..."

# Detectar distribuição
if [ -f /etc/os-release ]; then
    . /etc/os-release
    OS=$ID
else
    echo "❌ Não foi possível detectar a distribuição Linux"
    exit 1
fi

# Instalar WireGuard
if [ "$OS" = "ubuntu" ] || [ "$OS" = "debian" ]; then
    echo "📦 Atualizando pacotes (Ubuntu/Debian)..."
    apt update
    apt install -y wireguard wireguard-tools
elif [ "$OS" = "centos" ] || [ "$OS" = "rhel" ] || [ "$OS" = "fedora" ]; then
    echo "📦 Instalando WireGuard (CentOS/RHEL/Fedora)..."
    if [ "$OS" = "centos" ] || [ "$OS" = "rhel" ]; then
        yum install -y epel-release
        yum install -y wireguard-tools
    else
        dnf install -y wireguard-tools
    fi
else
    echo "⚠️ Distribuição não suportada automaticamente. Instale manualmente."
    exit 1
fi

# Verificar instalação
if ! command -v wg &> /dev/null; then
    echo "❌ WireGuard não foi instalado corretamente"
    exit 1
fi

echo "✅ WireGuard instalado: $(wg --version)"

# Habilitar IP forwarding
echo "🌐 Habilitando IP forwarding..."
echo "net.ipv4.ip_forward=1" >> /etc/sysctl.conf
echo "net.ipv6.conf.all.forwarding=1" >> /etc/sysctl.conf
sysctl -p

# Criar diretório de configurações
echo "📁 Criando diretório de configurações..."
mkdir -p /etc/wireguard
chmod 700 /etc/wireguard

# Configurar firewall (UFW)
if command -v ufw &> /dev/null; then
    echo "🔥 Configurando firewall (UFW)..."
    ufw allow 51820/udp comment "WireGuard"
    echo "✅ Porta 51820/udp permitida no UFW"
fi

# Configurar firewall (iptables)
if command -v iptables &> /dev/null; then
    echo "🔥 Configurando firewall (iptables)..."
    iptables -A INPUT -p udp --dport 51820 -j ACCEPT
    
    # Salvar regras iptables
    if command -v netfilter-persistent &> /dev/null; then
        netfilter-persistent save
    elif [ -f /etc/redhat-release ]; then
        service iptables save 2>/dev/null || true
    fi
    echo "✅ Porta 51820/udp permitida no iptables"
fi

# Verificar se API roda como root (não precisa configurar permissões)
echo "🔐 Verificando permissões..."
if [ "$EUID" -eq 0 ]; then
    echo "✅ Executando como root - permissões OK"
else
    echo "⚠️ Executando como usuário normal"
    echo "   Se a API não rodar como root, configure permissões:"
    echo "   sudo usermod -aG wireguard <usuario-api>"
fi

echo ""
echo "✅ Instalação concluída!"
echo ""
echo "📝 Próximos passos:"
echo "   1. Teste a instalação: wg --version"
echo "   2. Crie uma interface de teste: wg-quick up wg0-test"
echo "   3. Configure a API para usar os comandos wg e wg-quick"
echo ""
echo "📚 Documentação completa: INSTALACAO_WIREGUARD_LINUX.md"

