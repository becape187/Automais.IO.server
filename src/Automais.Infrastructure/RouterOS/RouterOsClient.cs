using Automais.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using System.Text;

namespace Automais.Infrastructure.RouterOS;

/// <summary>
/// Cliente para comunicação com API RouterOS
/// Implementação básica usando API protocol do RouterOS
/// </summary>
public class RouterOsClient : IRouterOsClient
{
    private readonly ILogger<RouterOsClient>? _logger;

    public RouterOsClient(ILogger<RouterOsClient>? logger = null)
    {
        _logger = logger;
    }

    public async Task<bool> TestConnectionAsync(string apiUrl, string username, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(apiUrl))
            {
                _logger?.LogWarning("API URL está vazia");
                return false;
            }

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                _logger?.LogWarning("Username ou password estão vazios");
                return false;
            }

            var (host, port) = ParseApiUrl(apiUrl);
            _logger?.LogDebug("Conectando ao RouterOS: {Host}:{Port}", host, port);
            
            // Tentar conectar e autenticar usando protocolo RouterOS API
            using var client = new TcpClient();
            client.ReceiveTimeout = 5000;
            client.SendTimeout = 5000;
            await client.ConnectAsync(host, port);
            
            var stream = client.GetStream();
            
            // Protocolo RouterOS API:
            // 1. Enviar palavra vazia para iniciar
            await WriteWordAsync(stream, "", cancellationToken);
            
            // 2. Ler palavra de autenticação inicial (geralmente "!done" ou ret)
            var initialResponse = await ReadWordAsync(stream, cancellationToken);
            _logger?.LogDebug("Resposta inicial RouterOS: {Response}", initialResponse);
            
            // 3. Enviar comando de login
            await WriteWordAsync(stream, "/login", cancellationToken);
            await WriteWordAsync(stream, $"=name={username}", cancellationToken);
            await WriteWordAsync(stream, $"=password={password}", cancellationToken);
            
            // 4. Ler todas as respostas até encontrar !done ou !trap
            var isAuthenticated = false;
            var responses = new List<string>();
            
            // Ler até 10 palavras de resposta (limite de segurança)
            for (int i = 0; i < 10; i++)
            {
                var response = await ReadWordAsync(stream, cancellationToken);
                if (response == null) break;
                
                responses.Add(response);
                _logger?.LogDebug("Resposta RouterOS [{Index}]: {Response}", i, response);
                
                if (response.StartsWith("!done"))
                {
                    isAuthenticated = true;
                    break;
                }
                if (response.StartsWith("!trap"))
                {
                    isAuthenticated = false;
                    break;
                }
            }
            
            if (!isAuthenticated)
            {
                _logger?.LogWarning("Falha na autenticação RouterOS para {Username} em {Host}:{Port}. Respostas: {Responses}", 
                    username, host, port, string.Join(", ", responses));
            }
            else
            {
                _logger?.LogInformation("Autenticação RouterOS bem-sucedida para {Username} em {Host}:{Port}", username, host, port);
            }
            
            client.Close();
            return isAuthenticated;
        }
        catch (SocketException ex)
        {
            _logger?.LogWarning(ex, "Erro de conexão ao testar RouterOS: {ApiUrl} - {Error}", apiUrl, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Erro ao testar conexão RouterOS: {ApiUrl}", apiUrl);
            return false;
        }
    }
    
    private async Task<string?> ReadWordAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        try
        {
            // Ler comprimento da palavra (4 bytes, little-endian)
            var lengthBytes = new byte[4];
            var totalRead = 0;
            while (totalRead < 4)
            {
                var bytesRead = await stream.ReadAsync(lengthBytes, totalRead, 4 - totalRead, cancellationToken);
                if (bytesRead == 0)
                    return null;
                totalRead += bytesRead;
            }
            
            var length = BitConverter.ToInt32(lengthBytes, 0);
            if (length == 0)
                return "";
            
            if (length > 8192) // Limite de segurança
            {
                _logger?.LogWarning("Palavra RouterOS muito grande: {Length} bytes", length);
                return null;
            }
            
            // Ler a palavra
            var wordBytes = new byte[length];
            totalRead = 0;
            while (totalRead < length)
            {
                var bytesRead = await stream.ReadAsync(wordBytes, totalRead, length - totalRead, cancellationToken);
                if (bytesRead == 0)
                    return null;
                totalRead += bytesRead;
            }
            
            return Encoding.UTF8.GetString(wordBytes);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Erro ao ler palavra do RouterOS");
            return null;
        }
    }
    
    private async Task WriteWordAsync(NetworkStream stream, string word, CancellationToken cancellationToken)
    {
        var wordBytes = Encoding.UTF8.GetBytes(word);
        var lengthBytes = BitConverter.GetBytes(wordBytes.Length);
        
        await stream.WriteAsync(lengthBytes, 0, 4, cancellationToken);
        if (wordBytes.Length > 0)
        {
            await stream.WriteAsync(wordBytes, 0, wordBytes.Length, cancellationToken);
        }
        await stream.FlushAsync(cancellationToken);
    }

    public async Task<RouterOsSystemInfo> GetSystemInfoAsync(string apiUrl, string username, string password, CancellationToken cancellationToken = default)
    {
        // TODO: Implementar usando biblioteca RouterOS API (ex: RouterOS.API)
        // Por enquanto, tenta buscar informações básicas via comandos
        // Comandos RouterOS:
        // /system/identity/print - retorna nome
        // /system/resource/print - retorna model, version, serial-number, etc
        
        _logger?.LogInformation("Buscando informações do sistema RouterOS via {ApiUrl}", apiUrl);
        
        try
        {
            // Tentar buscar informações via comandos genéricos
            // Por enquanto retorna objeto vazio - precisa implementar com biblioteca RouterOS.API
            var systemInfo = new RouterOsSystemInfo();
            
            // Quando implementar com biblioteca RouterOS.API:
            // var resource = await ExecuteCommandAsync(apiUrl, username, password, "/system/resource/print", cancellationToken);
            // systemInfo.Model = resource.FirstOrDefault()?.GetValueOrDefault("board-name");
            // systemInfo.SerialNumber = resource.FirstOrDefault()?.GetValueOrDefault("serial-number");
            // systemInfo.FirmwareVersion = resource.FirstOrDefault()?.GetValueOrDefault("version");
            
            await Task.CompletedTask;
            return systemInfo;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Erro ao buscar informações do sistema RouterOS");
            // Retornar objeto vazio em caso de erro - não falhar
            return new RouterOsSystemInfo();
        }
    }

    public async Task<string> ExportConfigAsync(string apiUrl, string username, string password, CancellationToken cancellationToken = default)
    {
        // TODO: Implementar exportação via API RouterOS
        // Comando: /export file=temp_export
        // Ou usar biblioteca RouterOS.API para executar comandos
        
        await Task.CompletedTask;
        throw new NotImplementedException("ExportConfigAsync precisa ser implementado com biblioteca RouterOS.API");
    }

    public async Task ImportConfigAsync(string apiUrl, string username, string password, string configContent, CancellationToken cancellationToken = default)
    {
        // TODO: Implementar importação via API RouterOS
        // Comando: /import file-name=backup.rsc
        // Ou executar comandos sequencialmente
        
        await Task.CompletedTask;
        throw new NotImplementedException("ImportConfigAsync precisa ser implementado com biblioteca RouterOS.API");
    }

    public async Task<List<RouterOsLog>> GetConfigLogsAsync(string apiUrl, string username, string password, DateTime? since = null, CancellationToken cancellationToken = default)
    {
        // TODO: Implementar busca de logs via API RouterOS
        // Comando: /log/print where topics~"config"
        
        await Task.CompletedTask;
        return new List<RouterOsLog>();
    }

    public async Task CreateUserAsync(string apiUrl, string username, string password, string newUsername, string newPassword, CancellationToken cancellationToken = default)
    {
        // TODO: Implementar criação de usuário via API RouterOS
        // Usar biblioteca RouterOS.API ou executar comandos via API protocol
        // Comando: /user/add name=<newUsername> password=<newPassword> group=full
        
        _logger?.LogInformation("Criando usuário {NewUsername} no RouterOS via {ApiUrl}", newUsername, apiUrl);
        
        // Por enquanto apenas loga - precisa implementar com biblioteca RouterOS.API
        await Task.CompletedTask;
        
        _logger?.LogWarning("CreateUserAsync ainda não está implementado completamente. " +
            "Precisa usar biblioteca RouterOS.API para executar: /user/add name={NewUsername} password=*** group=full", newUsername);
        
        // TODO: Implementar quando tiver biblioteca RouterOS.API
        // throw new NotImplementedException("CreateUserAsync precisa ser implementado com biblioteca RouterOS.API");
    }

    public async Task<List<Dictionary<string, string>>> ExecuteCommandAsync(string apiUrl, string username, string password, string command, CancellationToken cancellationToken = default)
    {
        // TODO: Implementar usando biblioteca RouterOS API (ex: RouterOS.API ou Mikrotik.API)
        // Por enquanto retorna lista vazia - precisa implementar protocolo RouterOS API
        _logger?.LogWarning("ExecuteCommandAsync ainda não está completamente implementado. Comando: {Command}", command);
        
        await Task.CompletedTask;
        
        // Placeholder - retornar lista vazia por enquanto
        // Quando implementar, usar biblioteca RouterOS.API para executar comandos
        return new List<Dictionary<string, string>>();
    }

    public async Task ExecuteCommandNoResultAsync(string apiUrl, string username, string password, string command, CancellationToken cancellationToken = default)
    {
        // TODO: Implementar usando biblioteca RouterOS API
        _logger?.LogWarning("ExecuteCommandNoResultAsync ainda não está completamente implementado. Comando: {Command}", command);
        
        await Task.CompletedTask;
        
        // Placeholder - quando implementar, usar biblioteca RouterOS.API
    }

    private (string host, int port) ParseApiUrl(string apiUrl)
    {
        // Formato esperado: "192.168.1.1:8728", "http://192.168.1.1:8728", ou "10.100.1.50:8728"
        if (string.IsNullOrWhiteSpace(apiUrl))
            throw new ArgumentException("API URL não pode ser vazia", nameof(apiUrl));

        // Remover protocolo se houver
        var url = apiUrl.Trim();
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            url = url.Substring(7);
        }
        else if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = url.Substring(8);
        }

        // Separar host e porta
        var lastColonIndex = url.LastIndexOf(':');
        if (lastColonIndex < 0)
        {
            // Se não tem porta, usar padrão 8728
            return (url, 8728);
        }

        var host = url.Substring(0, lastColonIndex);
        var portStr = url.Substring(lastColonIndex + 1);
        
        if (!int.TryParse(portStr, out var port))
            throw new ArgumentException($"Porta inválida na API URL: {portStr}", nameof(apiUrl));

        if (port < 1 || port > 65535)
            throw new ArgumentException($"Porta fora do range válido (1-65535): {port}", nameof(apiUrl));

        return (host, port);
    }
}

