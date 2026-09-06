using GameServer.API.Interfaces;
using GameServer.Orchestration.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace GameServer.Orchestration.Services;

public class SignalRTerminalSessionNotifier(IHubContext<ContainerConsoleHub> hubContext) : ITerminalSessionNotifier
{
    public Task SendOutputAsync(string connectionId, string output, CancellationToken cancellationToken = default)
        => hubContext.Clients.Client(connectionId).SendAsync("Output", output, cancellationToken);

    public Task SendDisconnectedAsync(string connectionId, CancellationToken cancellationToken = default)
        => hubContext.Clients.Client(connectionId).SendAsync("Disconnected", "Shell exited", cancellationToken);

    public Task SendErrorAsync(string connectionId, string error, CancellationToken cancellationToken = default)
        => hubContext.Clients.Client(connectionId).SendAsync("Error", error, cancellationToken);
}
