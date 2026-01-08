using Automais.Core.Interfaces;
using Microsoft.Extensions.Logging;
using tik4net;
using tik4net.Objects;
using tik4net.Objects.System;

namespace Automais.Infrastructure.RouterOS;

/// <summary>
/// Cliente para comunicação com API RouterOS usando biblioteca tik4net
/// </summary>
public class RouterOsClient : IRouterOsClient
{
    private readonly ILogger<RouterOsClient>? _logger;
    private const int DefaultTimeoutSeconds = 30;
    private const int MaxRetryAttempts = 3;
    private const int BaseRetryDelayMs = 500;

    public RouterOsClient(ILogger<RouterOsClient>? logger = null)
    {
        _logger = logger;
    }

    private (string host, int port) ParseApiUrl(string apiUrl)
    {
        if (string.IsNullOrWhiteSpace(apiUrl))
            throw new ArgumentException("API URL não pode ser vazia", nameof(apiUrl));

        var url = apiUrl.Trim();
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            url = url.Substring(7);
        }
        else if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = url.Substring(8);
        }

        var lastColonIndex = url.LastIndexOf(':');
        if (lastColonIndex < 0)
        {
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

    /// <summary>
    /// Cria conexão com RouterOS de forma síncrona (mantido para compatibilidade)
    /// </summary>
    private ITikConnection CreateConnection(string apiUrl, string username, string password)
    {
        var (host, port) = ParseApiUrl(apiUrl);
        var connection = ConnectionFactory.CreateConnection(TikConnectionType.Api);
        
        try
        {
            // Incluir porta no host se não for a porta padrão (8728)
            var hostWithPort = port == 8728 ? host : $"{host}:{port}";
            connection.Open(hostWithPort, username, password);
            
            // Verificar se a conexão está realmente aberta
            if (!connection.IsOpened)
            {
                connection.Close();
                throw new InvalidOperationException("Conexão RouterOS não foi aberta corretamente");
            }
            
            return connection;
        }
        catch
        {
            // Garantir que a conexão seja fechada em caso de erro
            try
            {
                if (connection != null && connection.IsOpened)
                {
                    connection.Close();
                }
            }
            catch
            {
                // Ignorar erros ao fechar conexão corrompida
            }
            throw;
        }
    }
    
    /// <summary>
    /// Cria conexão com RouterOS de forma assíncrona e protegida com timeout
    /// </summary>
    private async Task<ITikConnection> CreateConnectionAsync(
        string apiUrl, 
        string username, 
        string password, 
        int timeoutSeconds = DefaultTimeoutSeconds,
        CancellationToken cancellationToken = default)
    {
        var (host, port) = ParseApiUrl(apiUrl);
        var hostWithPort = port == 8728 ? host : $"{host}:{port}";
        
        // Executar criação de conexão em thread separada com timeout
        return await Task.Run(() =>
        {
            ITikConnection? connection = null;
            try
            {
                connection = ConnectionFactory.CreateConnection(TikConnectionType.Api);
                
                // Abrir conexão (operação síncrona, mas executada em thread separada)
                connection.Open(hostWithPort, username, password);
                
                // Verificar se a conexão está realmente aberta
                if (!connection.IsOpened)
                {
                    connection.Close();
                    throw new InvalidOperationException("Conexão RouterOS não foi aberta corretamente");
                }
                
                return connection;
            }
            catch
            {
                // Garantir que a conexão seja fechada em caso de erro
                try
                {
                    if (connection != null && connection.IsOpened)
                    {
                        connection.Close();
                    }
                }
                catch
                {
                    // Ignorar erros ao fechar conexão corrompida
                }
                throw;
            }
        }, cancellationToken).WaitAsync(TimeSpan.FromSeconds(timeoutSeconds), cancellationToken);
    }

    /// <summary>
    /// Verifica se uma conexão está válida e aberta
    /// </summary>
    private bool IsConnectionValid(ITikConnection? connection)
    {
        try
        {
            return connection != null && connection.IsOpened;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Fecha uma conexão de forma segura, tratando exceções
    /// </summary>
    private void SafeCloseConnection(ITikConnection? connection)
    {
        if (connection == null)
            return;

        try
        {
            if (connection.IsOpened)
            {
                connection.Close();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Erro ao fechar conexão RouterOS (pode estar já fechada)");
        }
    }

    /// <summary>
    /// Executa uma operação com retry e timeout
    /// Cada tentativa cria uma nova conexão para evitar reutilização de conexões corrompidas
    /// </summary>
    private async Task<T> ExecuteWithRetryAsync<T>(
        Func<T> operation,
        string operationName,
        int timeoutSeconds = DefaultTimeoutSeconds,
        CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;
        
        for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
        {
            try
            {
                _logger?.LogDebug("Executando {Operation} (tentativa {Attempt}/{MaxAttempts})", 
                    operationName, attempt, MaxRetryAttempts);

                // Cada tentativa executa a operação completa (que cria uma nova conexão)
                var task = Task.Run(operation, cancellationToken);
                var result = await task.WaitAsync(TimeSpan.FromSeconds(timeoutSeconds), cancellationToken);
                
                if (attempt > 1)
                {
                    _logger?.LogInformation("✅ {Operation} bem-sucedida na tentativa {Attempt}", 
                        operationName, attempt);
                }
                
                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger?.LogWarning("Operação {Operation} cancelada", operationName);
                throw;
            }
            catch (TimeoutException ex)
            {
                lastException = ex;
                _logger?.LogWarning(ex, "⏱️ Timeout na operação {Operation} (tentativa {Attempt}/{MaxAttempts}, timeout: {Timeout}s)", 
                    operationName, attempt, MaxRetryAttempts, timeoutSeconds);
            }
            catch (tik4net.TikConnectionException ex)
            {
                // Erro específico de conexão - precisa de mais tempo para reconectar
                lastException = ex;
                _logger?.LogWarning(ex, "🔌 Erro de conexão RouterOS na operação {Operation} (tentativa {Attempt}/{MaxAttempts}): {Error}", 
                    operationName, attempt, MaxRetryAttempts, ex.Message);
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger?.LogWarning(ex, "❌ Erro na operação {Operation} (tentativa {Attempt}/{MaxAttempts}): {Error}", 
                    operationName, attempt, MaxRetryAttempts, ex.Message);
            }

            // Aguardar antes de tentar novamente (backoff exponencial)
            // Para erros de conexão, aguardar um pouco mais para dar tempo ao router se recuperar
            if (attempt < MaxRetryAttempts)
            {
                var baseDelay = lastException is tik4net.TikConnectionException 
                    ? BaseRetryDelayMs * 2  // Delay maior para erros de conexão
                    : BaseRetryDelayMs;
                    
                var delayMs = baseDelay * (int)Math.Pow(2, attempt - 1);
                _logger?.LogDebug("Aguardando {DelayMs}ms antes da próxima tentativa (router pode estar se recuperando)...", delayMs);
                await Task.Delay(delayMs, cancellationToken);
            }
        }

        _logger?.LogError(lastException, "❌ Falha definitiva na operação {Operation} após {MaxAttempts} tentativas", 
            operationName, MaxRetryAttempts);
        throw new InvalidOperationException(
            $"Falha ao executar {operationName} após {MaxRetryAttempts} tentativas", lastException);
    }

    /// <summary>
    /// Executa uma operação sem retorno com retry e timeout
    /// </summary>
    private async Task ExecuteWithRetryAsync(
        Action operation,
        string operationName,
        int timeoutSeconds = DefaultTimeoutSeconds,
        CancellationToken cancellationToken = default)
    {
        await ExecuteWithRetryAsync<object?>(() =>
        {
            operation();
            return null;
        }, operationName, timeoutSeconds, cancellationToken);
    }

    /// <summary>
    /// Obtém um campo de resposta de forma segura, retornando null se o campo não existir
    /// </summary>
    private static string? GetResponseFieldSafe(ITikReSentence sentence, string fieldName)
    {
        if (sentence == null || sentence.Words == null)
            return null;

        return sentence.Words.TryGetValue(fieldName, out var value) ? value : null;
    }

    public async Task<bool> TestConnectionAsync(string apiUrl, string username, string password, CancellationToken cancellationToken = default)
    {
        const int timeoutSeconds = 5;
        
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
            _logger?.LogDebug("Testando conexão RouterOS: {Host}:{Port} (timeout: {Timeout}s)", host, port, timeoutSeconds);

            // Executar em thread separada para não travar a API
            return await Task.Run(async () =>
            {
                ITikConnection? connection = null;
                try
                {
                    connection = CreateConnection(apiUrl, username, password);
                    
                    // Testar conexão executando um comando simples
                    var cmd = connection.CreateCommand("/system/identity/print");
                    cmd.ExecuteScalar();
                    
                    _logger?.LogInformation("✅ Conexão RouterOS bem-sucedida para {Username} em {Host}:{Port}", username, host, port);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "❌ Falha ao conectar RouterOS {Host}:{Port}: {Error}", host, port, ex.Message);
                    return false;
                }
                finally
                {
                    SafeCloseConnection(connection);
                }
            }, cancellationToken).WaitAsync(TimeSpan.FromSeconds(timeoutSeconds), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("Timeout ao testar conexão RouterOS: {ApiUrl} (timeout: {Timeout}s)", apiUrl, timeoutSeconds);
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Erro ao testar conexão RouterOS: {ApiUrl} - {Error}", apiUrl, ex.Message);
            return false;
        }
    }

    public async Task<RouterOsSystemInfo> GetSystemInfoAsync(string apiUrl, string username, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation("Buscando informações do sistema RouterOS via {ApiUrl}", apiUrl);

            return await ExecuteWithRetryAsync(() =>
            {
                ITikConnection? connection = null;
                try
                {
                    connection = CreateConnection(apiUrl, username, password);
                    
                    var systemInfo = new RouterOsSystemInfo();
                    
                    // Buscar informações do sistema
                    var resourceCmd = connection.CreateCommand("/system/resource/print");
                    var resource = resourceCmd.ExecuteList().FirstOrDefault();
                    
                    if (resource != null)
                    {
                        // Usar GetResponseFieldSafe para campos opcionais que podem não existir
                        systemInfo.BoardName = GetResponseFieldSafe(resource, "board-name");
                        systemInfo.Model = GetResponseFieldSafe(resource, "board-name") ?? GetResponseFieldSafe(resource, "platform");
                        systemInfo.SerialNumber = GetResponseFieldSafe(resource, "serial-number");
                        systemInfo.FirmwareVersion = GetResponseFieldSafe(resource, "version");
                        systemInfo.CpuLoad = GetResponseFieldSafe(resource, "cpu-load");
                        systemInfo.MemoryUsage = GetResponseFieldSafe(resource, "free-memory");
                        systemInfo.TotalMemory = GetResponseFieldSafe(resource, "total-memory");
                        systemInfo.Temperature = GetResponseFieldSafe(resource, "temperature");
                        systemInfo.Uptime = GetResponseFieldSafe(resource, "uptime");
                    }
                    
                    return systemInfo;
                }
                finally
                {
                    SafeCloseConnection(connection);
                }
            }, $"GetSystemInfoAsync({apiUrl})", DefaultTimeoutSeconds, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Erro ao buscar informações do sistema RouterOS");
            return new RouterOsSystemInfo();
        }
    }

    public async Task<string> ExportConfigAsync(string apiUrl, string username, string password, CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(() =>
        {
            ITikConnection? connection = null;
            try
            {
                connection = CreateConnection(apiUrl, username, password);
                
                var cmd = connection.CreateCommand("/export");
                var result = cmd.ExecuteList();
                
                // O export retorna o conteúdo da configuração
                return string.Join("\n", result.Select(r => r.ToString()));
            }
            finally
            {
                SafeCloseConnection(connection);
            }
        }, $"ExportConfigAsync({apiUrl})", DefaultTimeoutSeconds, cancellationToken);
    }

    public async Task ImportConfigAsync(string apiUrl, string username, string password, string configContent, CancellationToken cancellationToken = default)
    {
        await ExecuteWithRetryAsync(() =>
        {
            ITikConnection? connection = null;
            try
            {
                connection = CreateConnection(apiUrl, username, password);
                
                // Dividir configuração em linhas e executar cada comando
                var lines = configContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("#"))
                        continue;
                    
                    try
                    {
                        var cmd = connection.CreateCommand(trimmedLine);
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Erro ao executar linha de configuração: {Line}", trimmedLine);
                        // Continuar com as próximas linhas
                    }
                }
            }
            finally
            {
                SafeCloseConnection(connection);
            }
        }, $"ImportConfigAsync({apiUrl})", DefaultTimeoutSeconds, cancellationToken);
    }

    public async Task<List<RouterOsLog>> GetConfigLogsAsync(string apiUrl, string username, string password, DateTime? since = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteWithRetryAsync(() =>
            {
                ITikConnection? connection = null;
                try
                {
                    connection = CreateConnection(apiUrl, username, password);
                    
                    var logs = new List<RouterOsLog>();
                    var cmd = connection.CreateCommand("/log/print");
                    
                    if (since.HasValue)
                    {
                        cmd.AddParameter("?since", since.Value.ToString("yyyy-MM-dd HH:mm:ss"));
                    }
                    
                    var logEntries = cmd.ExecuteList();
                    
                    foreach (var entry in logEntries)
                    {
                        var log = new RouterOsLog
                        {
                            Topic = GetResponseFieldSafe(entry, "topics"),
                            Action = GetResponseFieldSafe(entry, "action"),
                            Message = GetResponseFieldSafe(entry, "message"),
                            User = GetResponseFieldSafe(entry, "user")
                        };
                        
                        if (DateTime.TryParse(GetResponseFieldSafe(entry, "time") ?? "", out var timestamp))
                        {
                            log.Timestamp = timestamp;
                        }
                        
                        logs.Add(log);
                    }
                    
                    return logs;
                }
                finally
                {
                    SafeCloseConnection(connection);
                }
            }, $"GetConfigLogsAsync({apiUrl})", DefaultTimeoutSeconds, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Erro ao buscar logs do RouterOS");
            return new List<RouterOsLog>();
        }
    }

    public async Task CreateUserAsync(string apiUrl, string username, string password, string newUsername, string newPassword, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Criando usuário {NewUsername} no RouterOS via {ApiUrl}", newUsername, apiUrl);

        await ExecuteWithRetryAsync(() =>
        {
            ITikConnection? connection = null;
            try
            {
                connection = CreateConnection(apiUrl, username, password);
                
                var cmd = connection.CreateCommand("/user/add");
                cmd.AddParameter("name", newUsername);
                cmd.AddParameter("password", newPassword);
                cmd.AddParameter("group", "full");
                cmd.ExecuteNonQuery();
                
                _logger?.LogInformation("✅ Usuário {NewUsername} criado com sucesso no RouterOS", newUsername);
            }
            finally
            {
                SafeCloseConnection(connection);
            }
        }, $"CreateUserAsync({apiUrl}, {newUsername})", DefaultTimeoutSeconds, cancellationToken);
    }

    /// <summary>
    /// Faz o parsing de um comando RouterOS e separa o caminho do comando dos parâmetros
    /// Exemplo: "/ip/firewall/filter/print chain=output where action=drop"
    /// Retorna: (commandPath: "/ip/firewall/filter/print", parameters: ["chain=output", "where", "action=drop"])
    /// </summary>
    private (string commandPath, List<string> parameters) ParseRouterOsCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            throw new ArgumentException("Comando não pode ser vazio", nameof(command));

        var trimmed = command.Trim();
        var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length == 0)
            throw new ArgumentException("Comando inválido", nameof(command));

        var commandPath = parts[0];
        var parameters = new List<string>();

        for (int i = 1; i < parts.Length; i++)
        {
            parameters.Add(parts[i]);
        }

        return (commandPath, parameters);
    }

    /// <summary>
    /// Adiciona parâmetros ao comando RouterOS usando a sintaxe correta da API
    /// Suporta formatos como:
    /// - chain=output (parâmetro normal)
    /// - where action=drop (filtro com where)
    /// - ?action=drop (filtro direto com ?)
    /// </summary>
    private void AddParametersToCommand(ITikCommand cmd, List<string> parameters)
    {
        if (parameters == null || parameters.Count == 0)
            return;

        for (int i = 0; i < parameters.Count; i++)
        {
            var param = parameters[i].Trim();
            
            if (string.IsNullOrWhiteSpace(param))
                continue;

            // Tratar parâmetros "where" que indicam filtros
            // Formato: "where action=drop" ou "where chain=output"
            if (param.Equals("where", StringComparison.OrdinalIgnoreCase))
            {
                // O próximo parâmetro deve ser o filtro (ex: "action=drop")
                if (i + 1 < parameters.Count)
                {
                    var filterParam = parameters[i + 1].Trim();
                    var filterParts = filterParam.Split('=', 2);
                    
                    if (filterParts.Length == 2)
                    {
                        // Adicionar como query parameter com "?" (ex: ?action=drop)
                        var filterName = filterParts[0].Trim();
                        var filterValue = filterParts[1].Trim();
                        cmd.AddParameter($"?{filterName}", filterValue);
                        i++; // Pular o próximo parâmetro já que foi processado
                    }
                    else
                    {
                        // Se não tem "=", pode ser um operador lógico (and, or) - pular
                        i++;
                    }
                }
            }
            // Tratar parâmetros que já começam com "?" (filtros diretos)
            // Formato: "?action=drop"
            else if (param.StartsWith("?", StringComparison.Ordinal))
            {
                var filterParam = param.Substring(1); // Remover o "?"
                var filterParts = filterParam.Split('=', 2);
                
                if (filterParts.Length == 2)
                {
                    var filterName = filterParts[0].Trim();
                    var filterValue = filterParts[1].Trim();
                    cmd.AddParameter($"?{filterName}", filterValue);
                }
            }
            // Tratar parâmetros normais (ex: "chain=output")
            else if (param.Contains('='))
            {
                var paramParts = param.Split('=', 2);
                if (paramParts.Length == 2)
                {
                    var paramName = paramParts[0].Trim();
                    var paramValue = paramParts[1].Trim();
                    cmd.AddParameter(paramName, paramValue);
                }
            }
            // Tratar parâmetros sem valor (flags ou operadores lógicos)
            else
            {
                // Se for "and" ou "or", não adicionar como parâmetro
                // (esses são tratados automaticamente pela API quando há múltiplos ?param=value)
                if (!param.Equals("and", StringComparison.OrdinalIgnoreCase) && 
                    !param.Equals("or", StringComparison.OrdinalIgnoreCase))
                {
                    cmd.AddParameter(param, string.Empty);
                }
            }
        }
    }

    public async Task<List<Dictionary<string, string>>> ExecuteCommandAsync(string apiUrl, string username, string password, string command, CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Executando comando RouterOS: {Command}", command);

        return await ExecuteWithRetryAsync(() =>
        {
            ITikConnection? connection = null;
            try
            {
                connection = CreateConnection(apiUrl, username, password);
                
                // Fazer parsing do comando para separar caminho dos parâmetros
                var (commandPath, parameters) = ParseRouterOsCommand(command);
                
                _logger?.LogDebug("Comando parseado - Path: {Path}, Parâmetros: {Params}", 
                    commandPath, string.Join(" ", parameters));
                
                var cmd = connection.CreateCommand(commandPath);
                
                // Adicionar parâmetros ao comando
                AddParametersToCommand(cmd, parameters);
                
                var results = cmd.ExecuteList();
                
                var resultList = new List<Dictionary<string, string>>();
                
                foreach (var result in results)
                {
                    var dict = new Dictionary<string, string>();
                    // Words é um IDictionary<string, string> que contém todos os atributos
                    foreach (var kvp in result.Words)
                    {
                        dict[kvp.Key] = kvp.Value ?? string.Empty;
                    }
                    resultList.Add(dict);
                }
                
                return resultList;
            }
            finally
            {
                SafeCloseConnection(connection);
            }
        }, $"ExecuteCommandAsync({apiUrl}, {command})", DefaultTimeoutSeconds, cancellationToken);
    }

    public async Task ExecuteCommandNoResultAsync(string apiUrl, string username, string password, string command, CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Executando comando RouterOS (sem resultado): {Command}", command);

        await ExecuteWithRetryAsync(() =>
        {
            ITikConnection? connection = null;
            try
            {
                connection = CreateConnection(apiUrl, username, password);
                
                // Fazer parsing do comando para separar caminho dos parâmetros
                var (commandPath, parameters) = ParseRouterOsCommand(command);
                
                _logger?.LogDebug("Comando parseado - Path: {Path}, Parâmetros: {Params}", 
                    commandPath, string.Join(" ", parameters));
                
                var cmd = connection.CreateCommand(commandPath);
                
                // Adicionar parâmetros ao comando
                AddParametersToCommand(cmd, parameters);
                
                cmd.ExecuteNonQuery();
            }
            finally
            {
                SafeCloseConnection(connection);
            }
        }, $"ExecuteCommandNoResultAsync({apiUrl}, {command})", DefaultTimeoutSeconds, cancellationToken);
    }
}
