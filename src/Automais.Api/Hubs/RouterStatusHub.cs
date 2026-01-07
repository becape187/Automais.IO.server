namespace Automais.Api.Hubs;

/// <summary>
/// Hub SignalR para notificações de mudança de status dos roteadores
/// Alias para o Hub do Core para manter compatibilidade
/// </summary>
public class RouterStatusHub : Automais.Core.Hubs.RouterStatusHub
{
    // O hub pode ser estendido com métodos específicos se necessário
    // Por enquanto, apenas notificações via SendAsync são usadas
}

