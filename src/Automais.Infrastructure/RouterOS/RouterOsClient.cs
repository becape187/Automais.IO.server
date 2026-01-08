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

    private ITikConnection CreateConnection(string apiUrl, string username, string password)
    {
        var (host, port) = ParseApiUrl(apiUrl);
        var connection = ConnectionFactory.CreateConnection(TikConnectionType.Api);
        // Incluir porta no host se não for a porta padrão (8728)
        var hostWithPort = port == 8728 ? host : $"{host}:{port}";
        connection.Open(hostWithPort, username, password);
        return connection;
    }

    /// <summary>
    /// Executa uma operação com retry e timeout
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
            catch (Exception ex)
            {
                lastException = ex;
                _logger?.LogWarning(ex, "❌ Erro na operação {Operation} (tentativa {Attempt}/{MaxAttempts}): {Error}", 
                    operationName, attempt, MaxRetryAttempts, ex.Message);
            }

            // Aguardar antes de tentar novamente (backoff exponencial)
            if (attempt < MaxRetryAttempts)
            {
                var delayMs = BaseRetryDelayMs * (int)Math.Pow(2, attempt - 1);
                _logger?.LogDebug("Aguardando {DelayMs}ms antes da próxima tentativa...", delayMs);
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
                try
                {
                    using var connection = CreateConnection(apiUrl, username, password);
                    
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
                using var connection = CreateConnection(apiUrl, username, password);
                
                var systemInfo = new RouterOsSystemInfo();
                
                // Buscar informações do sistema
                var resourceCmd = connection.CreateCommand("/system/resource/print");
                var resource = resourceCmd.ExecuteList().FirstOrDefault();
                
                if (resource != null)
                {
                    systemInfo.BoardName = resource.GetResponseField("board-name");
                    systemInfo.Model = resource.GetResponseField("board-name") ?? resource.GetResponseField("platform");
                    systemInfo.SerialNumber = resource.GetResponseField("serial-number");
                    systemInfo.FirmwareVersion = resource.GetResponseField("version");
                    systemInfo.CpuLoad = resource.GetResponseField("cpu-load");
                    systemInfo.MemoryUsage = resource.GetResponseField("free-memory");
                    systemInfo.TotalMemory = resource.GetResponseField("total-memory");
                    systemInfo.Temperature = resource.GetResponseField("temperature");
                    systemInfo.Uptime = resource.GetResponseField("uptime");
                }
                
                return systemInfo;
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
            using var connection = CreateConnection(apiUrl, username, password);
            
            var cmd = connection.CreateCommand("/export");
            var result = cmd.ExecuteList();
            
            // O export retorna o conteúdo da configuração
            return string.Join("\n", result.Select(r => r.ToString()));
        }, $"ExportConfigAsync({apiUrl})", DefaultTimeoutSeconds, cancellationToken);
    }

    public async Task ImportConfigAsync(string apiUrl, string username, string password, string configContent, CancellationToken cancellationToken = default)
    {
        await ExecuteWithRetryAsync(() =>
        {
            using var connection = CreateConnection(apiUrl, username, password);
            
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
        }, $"ImportConfigAsync({apiUrl})", DefaultTimeoutSeconds, cancellationToken);
    }

    public async Task<List<RouterOsLog>> GetConfigLogsAsync(string apiUrl, string username, string password, DateTime? since = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteWithRetryAsync(() =>
            {
                using var connection = CreateConnection(apiUrl, username, password);
                
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
                        Topic = entry.GetResponseField("topics"),
                        Action = entry.GetResponseField("action"),
                        Message = entry.GetResponseField("message"),
                        User = entry.GetResponseField("user")
                    };
                    
                    if (DateTime.TryParse(entry.GetResponseField("time"), out var timestamp))
                    {
                        log.Timestamp = timestamp;
                    }
                    
                    logs.Add(log);
                }
                
                return logs;
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
            using var connection = CreateConnection(apiUrl, username, password);
            
            var cmd = connection.CreateCommand("/user/add");
            cmd.AddParameter("name", newUsername);
            cmd.AddParameter("password", newPassword);
            cmd.AddParameter("group", "full");
            cmd.ExecuteNonQuery();
            
            _logger?.LogInformation("✅ Usuário {NewUsername} criado com sucesso no RouterOS", newUsername);
        }, $"CreateUserAsync({apiUrl}, {newUsername})", DefaultTimeoutSeconds, cancellationToken);
    }

    public async Task<List<Dictionary<string, string>>> ExecuteCommandAsync(string apiUrl, string username, string password, string command, CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Executando comando RouterOS: {Command}", command);

        return await ExecuteWithRetryAsync(() =>
        {
            using var connection = CreateConnection(apiUrl, username, password);
            
            var cmd = connection.CreateCommand(command);
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
        }, $"ExecuteCommandAsync({apiUrl}, {command})", DefaultTimeoutSeconds, cancellationToken);
    }

    public async Task ExecuteCommandNoResultAsync(string apiUrl, string username, string password, string command, CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Executando comando RouterOS (sem resultado): {Command}", command);

        await ExecuteWithRetryAsync(() =>
        {
            using var connection = CreateConnection(apiUrl, username, password);
            
            var cmd = connection.CreateCommand(command);
            cmd.ExecuteNonQuery();
        }, $"ExecuteCommandNoResultAsync({apiUrl}, {command})", DefaultTimeoutSeconds, cancellationToken);
    }
}
