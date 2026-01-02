# Configuração de Secrets

## Desenvolvimento Local

1. Copie o arquivo `appsettings.Example.json` para `appsettings.Development.json`:
   ```bash
   cp src/Automais.Api/appsettings.Example.json src/Automais.Api/appsettings.Development.json
   ```

2. Edite `appsettings.Development.json` e preencha com suas credenciais locais.

3. O arquivo `appsettings.Development.json` está no `.gitignore` e não será commitado.

## Produção (GitHub Actions / Deploy)

### Configurar GitHub Secrets

1. Vá em **Settings → Secrets and variables → Actions**
2. Adicione os seguintes secrets:

   - `DB_HOST` - Host do banco de dados
   - `DB_PORT` - Porta do banco de dados
   - `DB_NAME` - Nome do banco de dados
   - `DB_USER` - Usuário do banco de dados
   - `DB_PASSWORD` - Senha do banco de dados
   - `CHIRPSTACK_API_URL` - URL da API ChirpStack
   - `CHIRPSTACK_API_TOKEN` - Token da API ChirpStack
   - `EMQX_BROKER_URL` - URL do broker EMQX
   - `EMQX_USERNAME` - Usuário do EMQX (se necessário)
   - `EMQX_PASSWORD` - Senha do EMQX (se necessário)

### GitHub Actions Workflow

No seu workflow do GitHub Actions, configure as variáveis de ambiente:

```yaml
env:
  DB_HOST: ${{ secrets.DB_HOST }}
  DB_PORT: ${{ secrets.DB_PORT }}
  DB_NAME: ${{ secrets.DB_NAME }}
  DB_USER: ${{ secrets.DB_USER }}
  DB_PASSWORD: ${{ secrets.DB_PASSWORD }}
  CHIRPSTACK_API_URL: ${{ secrets.CHIRPSTACK_API_URL }}
  CHIRPSTACK_API_TOKEN: ${{ secrets.CHIRPSTACK_API_TOKEN }}
  EMQX_BROKER_URL: ${{ secrets.EMQX_BROKER_URL }}
  EMQX_USERNAME: ${{ secrets.EMQX_USERNAME }}
  EMQX_PASSWORD: ${{ secrets.EMQX_PASSWORD }}
```

### Deploy Manual

Se estiver fazendo deploy manual, exporte as variáveis de ambiente antes de rodar:

```bash
export DB_HOST="seu-host"
export DB_PORT="25060"
export DB_NAME="defaultdb"
export DB_USER="doadmin"
export DB_PASSWORD="sua-senha"
export CHIRPSTACK_API_URL="http://srv01.automais.io:8080"
export CHIRPSTACK_API_TOKEN="seu-token"
# ... etc
```

## Formato das Variáveis

O `appsettings.json` usa o formato `${VAR_NAME}` que será substituído automaticamente pelas variáveis de ambiente.

Exemplo:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=${DB_HOST};Port=${DB_PORT};..."
  }
}
```

Será substituído por:
```
Host=db-automais-io-do-user-10042663-0.g.db.ondigitalocean.com;Port=25060;...
```

