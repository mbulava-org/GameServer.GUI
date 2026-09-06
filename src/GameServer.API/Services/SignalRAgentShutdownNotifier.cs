using GameServer.API.Hubs;
using GameServer.API.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace GameServer.API.Services
{
    public class SignalRAgentShutdownNotifier : IAgentShutdownNotifier
    {
        private readonly IHubContext<AgentRegistrationHub> _hubContext;

        public SignalRAgentShutdownNotifier(IHubContext<AgentRegistrationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public Task NotifyPrimaryServiceShuttingDownAsync(CancellationToken cancellationToken = default)
        {
            return _hubContext.Clients.All.SendCoreAsync(
                "PrimaryServiceShuttingDown",
                ["Primary Service is shutting down."],
                cancellationToken);
        }
    }
}
