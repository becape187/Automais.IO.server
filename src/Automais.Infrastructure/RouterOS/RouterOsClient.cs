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

            return await Task.Run(() =>
            {
                using var connection = CreateConnection(apiUrl, username, password);
                
                var systemInfo = new RouterOsSystemInfo();
                
                // Buscar informações do sistema
                var resourceCmd = connection.CreateCommand("/system/resource/print");
                var resource = resourceCmd.ExecuteList().FirstOrDefault();
                
                if (resource != null)
                {
                    systemInfo.Model = resource.GetResponseField("board-name") ?? resource.GetResponseField("platform");
                    systemInfo.SerialNumber = resource.GetResponseField("serial-number");
                    systemInfo.FirmwareVersion = resource.GetResponseField("version");
                    systemInfo.CpuLoad = resource.GetResponseField("cpu-load");
                    systemInfo.MemoryUsage = resource.GetResponseField("free-memory");
                    systemInfo.Uptime = resource.GetResponseField("uptime");
                }
                
                return systemInfo;
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Erro ao buscar informações do sistema RouterOS");
            return new RouterOsSystemInfo();
        }
    }

    public async Task<string> ExportConfigAsync(string apiUrl, string username, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            return await Task.Run(() =>
            {
                using var connection = CreateConnection(apiUrl, username, password);
                
                var cmd = connection.CreateCommand("/export");
                var result = cmd.ExecuteList();
                
                // O export retorna o conteúdo da configuração
                return string.Join("\n", result.Select(r => r.ToString()));
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Erro ao exportar configuração RouterOS");
            throw;
        }
    }

    public async Task ImportConfigAsync(string apiUrl, string username, string password, string configContent, CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.Run(() =>
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
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Erro ao importar configuração RouterOS");
            throw;
        }
    }

    public async Task<List<RouterOsLog>> GetConfigLogsAsync(string apiUrl, string username, string password, DateTime? since = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return await Task.Run(() =>
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
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Erro ao buscar logs do RouterOS");
            return new List<RouterOsLog>();
        }
    }

    public async Task CreateUserAsync(string apiUrl, string username, string password, string newUsername, string newPassword, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation("Criando usuário {NewUsername} no RouterOS via {ApiUrl}", newUsername, apiUrl);

            await Task.Run(() =>
            {
                using var connection = CreateConnection(apiUrl, username, password);
                
                var cmd = connection.CreateCommand("/user/add");
                cmd.AddParameter("name", newUsername);
                cmd.AddParameter("password", newPassword);
                cmd.AddParameter("group", "full");
                cmd.ExecuteNonQuery();
                
                _logger?.LogInformation("✅ Usuário {NewUsername} criado com sucesso no RouterOS", newUsername);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Erro ao criar usuário {NewUsername} no RouterOS", newUsername);
            throw;
        }
    }

    public async Task<List<Dictionary<string, string>>> ExecuteCommandAsync(string apiUrl, string username, string password, string command, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogDebug("Executando comando RouterOS: {Command}", command);

            return await Task.Run(() =>
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
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Erro ao executar comando RouterOS: {Command}", command);
            throw;
        }
    }

    public async Task ExecuteCommandNoResultAsync(string apiUrl, string username, string password, string command, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogDebug("Executando comando RouterOS (sem resultado): {Command}", command);

            await Task.Run(() =>
            {
                using var connection = CreateConnection(apiUrl, username, password);
                
                var cmd = connection.CreateCommand(command);
                cmd.ExecuteNonQuery();
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Erro ao executar comando RouterOS: {Command}", command);
            throw;
        }
    }
}
