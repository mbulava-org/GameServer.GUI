using Microsoft.AspNetCore.SignalR;
using GameServer.API.Services;

namespace GameServer.API.Hubs
{
    /// <summary>
    /// SignalR Hub for real-time container terminal access.
    /// Delegates session management to TerminalSessionManager service.
    /// </summary>
    public class ContainerConsoleHub : Hub
    {
        private readonly ILogger<ContainerConsoleHub> _logger;
        private readonly TerminalSessionManager _sessionManager;

        public ContainerConsoleHub(
            ILogger<ContainerConsoleHub> logger,
            TerminalSessionManager sessionManager)
        {
            _logger = logger;
            _sessionManager = sessionManager;
        }

        /// <summary>
        /// Start an interactive exec session (e.g., /bin/sh) in the container
        /// </summary>
        public async Task<bool> StartExecSession(string containerId, string shell = "/bin/sh")
        {
            var connectionId = Context.ConnectionId;
            _logger.LogInformation("Client {ConnectionId} requesting terminal for container {ContainerId} (shell={Shell})",
                connectionId, containerId, shell);

            try
            {
                var (success, error) = await _sessionManager.StartSessionAsync(connectionId, containerId, shell);
                
                if (success)
                {
                    await Clients.Caller.SendAsync("SessionStarted", connectionId);
                    await Clients.Caller.SendAsync("Connected", containerId);
                    return true;
                }
                else
                {
                    await Clients.Caller.SendAsync("Error", error ?? "Failed to start session");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting terminal session for {ContainerId}", containerId);
                await Clients.Caller.SendAsync("Error", $"Failed to start terminal: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Client sends input to the container terminal
        /// </summary>
        public async Task SendInput(string sessionId, string input)
        {
            // sessionId is actually connectionId in our case
            await _sessionManager.SendInputAsync(Context.ConnectionId, input);
        }

        /// <summary>
        /// Disconnect from terminal
        /// </summary>
        public async Task Disconnect()
        {
            await _sessionManager.CloseSessionAsync(Context.ConnectionId);
        }

        /// <summary>
        /// Called when client disconnects
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("Client {ConnectionId} disconnected", Context.ConnectionId);
            await _sessionManager.CloseSessionAsync(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
