# 🚀 Instalação do .NET 8 no Ubuntu Linux

Este guia mostra como instalar o .NET 8 SDK e Runtime no Ubuntu Linux.

## 📋 Pré-requisitos

- Ubuntu 20.04, 22.04 ou 24.04
- Acesso root ou sudo
- Conexão com a internet

## 🔧 Método 1: Instalação via Microsoft Repository (Recomendado)

### Passo 1: Adicionar o repositório Microsoft

```bash
# Atualizar lista de pacotes
sudo apt update

# Instalar dependências
sudo apt install -y wget apt-transport-https

# Adicionar chave GPG da Microsoft
wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb
```

### Passo 2: Instalar .NET 8 SDK

```bash
# Atualizar lista de pacotes após adicionar repositório
sudo apt update

# Instalar .NET 8 SDK (inclui runtime)
sudo apt install -y dotnet-sdk-8.0
```

### Passo 3: Verificar instalação

```bash
# Verificar versão do .NET
dotnet --version

# Deve mostrar algo como: 8.0.xxx

# Verificar SDKs instalados
dotnet --list-sdks

# Verificar runtimes instalados
dotnet --list-runtimes
```

## 🔧 Método 2: Instalação apenas do Runtime (para produção)

Se você só precisa executar aplicações (não desenvolver), instale apenas o runtime:

```bash
# Instalar .NET 8 Runtime (ASP.NET Core)
sudo apt install -y aspnetcore-runtime-8.0

# Ou apenas o runtime básico
sudo apt install -y dotnet-runtime-8.0
```

## 🔧 Método 3: Instalação via Snap (Alternativa)

```bash
# Instalar via snap
sudo snap install dotnet-sdk --classic --channel=8.0

# Verificar
dotnet --version
```

## ✅ Verificação da Instalação

Execute os seguintes comandos para verificar:

```bash
# Versão do .NET
dotnet --version

# SDKs instalados
dotnet --list-sdks

# Runtimes instalados
dotnet --list-runtimes

# Informações do sistema
dotnet --info
```

**Saída esperada:**
```
.NET SDK:
 Version:           8.0.xxx
 Commit:             xxxxxxxx

Runtime Environment:
 OS Name:     ubuntu
 OS Version:  22.04
 OS Platform: Linux
 RID:         linux-x64
 Base Path:   /usr/share/dotnet/sdk/8.0.xxx/
```

## 🎯 Configuração para o Projeto Automais.io

### Verificar se o .NET 8 está instalado

```bash
dotnet --version
# Deve retornar: 8.0.xxx ou superior
```

### Instalar ferramentas do Entity Framework

```bash
# Instalar ferramentas do EF Core globalmente
dotnet tool install --global dotnet-ef

# Verificar instalação
dotnet ef --version
```

### Testar compilação do projeto

```bash
# Navegar até o diretório do projeto
cd /root/automais.io/server.io/src/Automais.Api

# Restaurar dependências
dotnet restore

# Compilar projeto
dotnet build

# Executar (opcional, para teste)
dotnet run
```

## 🔄 Atualização do .NET

Para atualizar para uma versão mais recente do .NET 8:

```bash
# Atualizar lista de pacotes
sudo apt update

# Atualizar .NET SDK
sudo apt upgrade dotnet-sdk-8.0

# Verificar nova versão
dotnet --version
```

## 🗑️ Desinstalação

Se precisar remover o .NET:

```bash
# Remover .NET SDK
sudo apt remove dotnet-sdk-8.0

# Remover repositório Microsoft (opcional)
sudo rm /etc/apt/sources.list.d/microsoft-prod.list
sudo apt update
```

## 🐛 Troubleshooting

### Problema: "dotnet: command not found"

**Solução:**
```bash
# Verificar se o PATH está configurado
echo $PATH

# Adicionar ao PATH (se necessário)
export PATH=$PATH:/usr/share/dotnet

# Para tornar permanente, adicionar ao ~/.bashrc ou ~/.profile
echo 'export PATH=$PATH:/usr/share/dotnet' >> ~/.bashrc
source ~/.bashrc
```

### Problema: Erro de permissão

**Solução:**
```bash
# Verificar permissões
ls -la /usr/share/dotnet

# Se necessário, corrigir permissões
sudo chown -R root:root /usr/share/dotnet
```

### Problema: Versão antiga instalada

**Solução:**
```bash
# Remover versão antiga
sudo apt remove dotnet-sdk-7.0  # ou versão antiga

# Instalar versão 8.0
sudo apt install dotnet-sdk-8.0
```

## 📚 Recursos Adicionais

- **Documentação oficial**: https://learn.microsoft.com/dotnet/core/install/linux-ubuntu
- **Downloads**: https://dotnet.microsoft.com/download/dotnet/8.0
- **Changelog**: https://github.com/dotnet/core/blob/main/release-notes/8.0/README.md

## ✅ Checklist de Instalação

- [ ] .NET 8 SDK instalado (`dotnet --version` retorna 8.0.x)
- [ ] Ferramentas EF Core instaladas (`dotnet ef --version`)
- [ ] Projeto compila sem erros (`dotnet build`)
- [ ] Aplicação executa corretamente (`dotnet run`)

---

**Nota**: Para produção, recomenda-se usar apenas o **ASP.NET Core Runtime** ao invés do SDK completo, para reduzir o tamanho da instalação.

