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
        // Timeout de 5 segundos para não travar a API
        const int timeoutSeconds = 5;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        
        string host = string.Empty;
        int port = 0;
        
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

            (host, port) = ParseApiUrl(apiUrl);
            _logger?.LogDebug("Conectando ao RouterOS: {Host}:{Port} (timeout: {Timeout}s)", host, port, timeoutSeconds);
            
            // Tentar conectar e autenticar usando protocolo RouterOS API
            using var client = new TcpClient();
            client.ReceiveTimeout = timeoutSeconds * 1000;
            client.SendTimeout = timeoutSeconds * 1000;
            
            // Conectar com timeout
            var connectTask = client.ConnectAsync(host, port);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds), timeoutCts.Token);
            var completedTask = await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false);
            
            if (completedTask == timeoutTask)
            {
                _logger?.LogWarning("Timeout ao conectar ao RouterOS {Host}:{Port} (timeout: {Timeout}s)", host, port, timeoutSeconds);
                return false;
            }
            
            await connectTask.ConfigureAwait(false);
            
            var stream = client.GetStream();
            
            // Protocolo RouterOS API:
            // 1. Enviar palavra vazia para iniciar
            await WriteWordAsync(stream, "", timeoutCts.Token).ConfigureAwait(false);
            
            // 2. Ler palavra de autenticação inicial (geralmente "!done" ou ret)
            var initialResponse = await ReadWordAsync(stream, timeoutCts.Token).ConfigureAwait(false);
            _logger?.LogDebug("Resposta inicial RouterOS: {Response}", initialResponse);
            
            // 3. Enviar comando de login
            await WriteWordAsync(stream, "/login", timeoutCts.Token).ConfigureAwait(false);
            await WriteWordAsync(stream, $"=name={username}", timeoutCts.Token).ConfigureAwait(false);
            await WriteWordAsync(stream, $"=password={password}", timeoutCts.Token).ConfigureAwait(false);
            
            // 4. Ler todas as respostas até encontrar !done ou !trap
            var isAuthenticated = false;
            var responses = new List<string>();
            
            // Ler até 10 palavras de resposta (limite de segurança)
            for (int i = 0; i < 10; i++)
            {
                var response = await ReadWordAsync(stream, timeoutCts.Token).ConfigureAwait(false);
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
            
            try
            {
                client.Close();
            }
            catch
            {
                // Ignorar erros ao fechar
            }
            
            return isAuthenticated;
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            _logger?.LogWarning("Timeout ao testar conexão RouterOS: {ApiUrl} (timeout: {Timeout}s). A operação foi cancelada para não travar a API.", 
                apiUrl, timeoutSeconds);
            return false;
        }
        catch (TaskCanceledException)
        {
            _logger?.LogWarning("Timeout ao testar conexão RouterOS: {ApiUrl} (timeout: {Timeout}s). A operação foi cancelada para não travar a API.", 
                apiUrl, timeoutSeconds);
            return false;
        }
        catch (SocketException ex)
        {
            string errorMessage;
            var hostPort = (string.IsNullOrEmpty(host) || port == 0) ? apiUrl : $"{host}:{port}";
            
            switch (ex.SocketErrorCode)
            {
                case SocketError.ConnectionRefused:
                    if (port > 0)
                    {
                        errorMessage = $"Conexão recusada na porta {port}. Verifique se:\n" +
                            $"1. O serviço da API RouterOS está habilitado no Mikrotik: /ip service enable api\n" +
                            $"2. A porta {port} está aberta no firewall do Mikrotik\n" +
                            $"3. O IP {host} está correto e acessível via VPN\n" +
                            $"4. O firewall do Mikrotik permite conexões na porta {port}";
                    }
                    else
                    {
                        errorMessage = $"Conexão recusada em {apiUrl}. Verifique se o serviço da API RouterOS está habilitado.";
                    }
                    break;
                case SocketError.TimedOut:
                    errorMessage = $"Timeout ao conectar em {hostPort}. Verifique se o router está acessível.";
                    break;
                case SocketError.HostUnreachable:
                    errorMessage = $"Host {hostPort} não está acessível. Verifique a conectividade de rede e rotas.";
                    break;
                case SocketError.NetworkUnreachable:
                    errorMessage = $"Rede não acessível para {hostPort}. Verifique a rota de rede.";
                    break;
                default:
                    errorMessage = $"Erro de socket: {ex.SocketErrorCode} - {ex.Message}";
                    break;
            }
            
            _logger?.LogWarning(ex, "Erro de conexão ao testar RouterOS: {ApiUrl} ({HostPort}) - {ErrorMessage}", 
                apiUrl, hostPort, errorMessage);
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Erro ao testar conexão RouterOS: {ApiUrl} - {Error}", apiUrl, ex.Message);
            return false;
        }
    }
    
    private async Task<string?> ReadWordAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        try
        {
            // Verificar se foi cancelado antes de começar
            cancellationToken.ThrowIfCancellationRequested();
            
            // Ler comprimento da palavra (4 bytes, little-endian)
            var lengthBytes = new byte[4];
            var totalRead = 0;
            while (totalRead < 4)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var bytesRead = await stream.ReadAsync(lengthBytes, totalRead, 4 - totalRead, cancellationToken).ConfigureAwait(false);
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
                cancellationToken.ThrowIfCancellationRequested();
                var bytesRead = await stream.ReadAsync(wordBytes, totalRead, length - totalRead, cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                    return null;
                totalRead += bytesRead;
            }
            
            return Encoding.UTF8.GetString(wordBytes);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogDebug("Leitura de palavra RouterOS cancelada (timeout)");
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Erro ao ler palavra do RouterOS");
            return null;
        }
    }
    
    private async Task WriteWordAsync(NetworkStream stream, string word, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var wordBytes = Encoding.UTF8.GetBytes(word);
            var lengthBytes = BitConverter.GetBytes(wordBytes.Length);
            
            await stream.WriteAsync(lengthBytes, 0, 4, cancellationToken).ConfigureAwait(false);
            if (wordBytes.Length > 0)
            {
                await stream.WriteAsync(wordBytes, 0, wordBytes.Length, cancellationToken).ConfigureAwait(false);
            }
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogDebug("Escrita de palavra RouterOS cancelada (timeout)");
            throw;
        }
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

