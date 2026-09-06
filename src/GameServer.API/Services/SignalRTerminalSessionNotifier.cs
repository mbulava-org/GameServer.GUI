using GameServer.API.Hubs;
using GameServer.API.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace GameServer.API.Services
{
    public class SignalRTerminalSessionNotifier : ITerminalSessionNotifier
    {
        private readonly IHubContext<ContainerConsoleHub> _hubContext;

        public SignalRTerminalSessionNotifier(IHubContext<ContainerConsoleHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public Task SendOutputAsync(string connectionId, string output, CancellationToken cancellationToken = default)
        {
            return _hubContext.Clients.Client(connectionId).SendAsync("Output", output, cancellationToken);
        }

        public Task SendDisconnectedAsync(string connectionId, CancellationToken cancellationToken = default)
        {
            return _hubContext.Clients.Client(connectionId).SendAsync("Disconnected", "Shell exited", cancellationToken);
        }

        public Task SendErrorAsync(string connectionId, string error, CancellationToken cancellationToken = default)
        {
            return _hubContext.Clients.Client(connectionId).SendAsync("Error", error, cancellationToken);
        }
    }
}
