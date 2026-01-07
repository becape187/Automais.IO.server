using Microsoft.AspNetCore.SignalR;

namespace Automais.Core.Hubs;

/// <summary>
/// Hub SignalR para notificações de mudança de status dos roteadores
/// </summary>
public class RouterStatusHub : Hub
{
    // O hub pode ser estendido com métodos específicos se necessário
    // Por enquanto, apenas notificações via SendAsync são usadas
}

