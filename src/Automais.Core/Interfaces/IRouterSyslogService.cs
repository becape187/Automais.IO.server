namespace Automais.Core.Interfaces;

/// <summary>
/// Interface para serviço de recepção de logs Syslog dos Routers
/// </summary>
public interface IRouterSyslogService
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    void ProcessSyslogMessage(string message, string sourceIp);
}

