using Microsoft.AspNetCore.SignalR;
using GameServer.Docker.Interfaces;
using System.Collections.Concurrent;

namespace GameServer.Docker.Hubs
{
    /// <summary>
    /// SignalR Hub for real-time container console access.
    /// Provides bidirectional communication between clients and container consoles via Node Agents.
    /// </summary>
    public class ContainerConsoleHub : Hub
    {
        private readonly ILogger<ContainerConsoleHub> _logger;
        private readonly INodeAgentDiscovery _nodeAgentDiscovery;
        private readonly IHttpClientFactory _httpClientFactory;
        
        // Track active console sessions per connection
        private static readonly ConcurrentDictionary<string, ConsoleSession> _activeSessions = new();

        public ContainerConsoleHub(
            ILogger<ContainerConsoleHub> logger,
            INodeAgentDiscovery nodeAgentDiscovery,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _nodeAgentDiscovery = nodeAgentDiscovery;
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Client connects to a container's console
        /// </summary>
        public async Task<bool> AttachToContainer(string containerId)
        {
            var connectionId = Context.ConnectionId;
            _logger.LogInformation("Client {ConnectionId} requesting console attach to container {ContainerId}", 
                connectionId, containerId);

            try
            {
                // Find the node agent for this container
                var agent = await _nodeAgentDiscovery.GetAgentForContainerAsync(containerId);
                if (agent == null)
                {
                    _logger.LogWarning("No agent found for container {ContainerId}", containerId);
                    await Clients.Caller.SendAsync("Error", $"Container {containerId} not found or agent unavailable");
                    return false;
                }

                // Create WebSocket connection to agent
                var wsUrl = agent.InternalUrl.Replace("http://", "ws://").Replace("https://", "wss://");
                var agentWsUrl = $"{wsUrl}/containers/{containerId}/attach/ws";

                var session = new ConsoleSession
                {
                    ConnectionId = connectionId,
                    ContainerId = containerId,
                    AgentUrl = agent.InternalUrl,
                    WebSocketUrl = agentWsUrl
                };

                // Start WebSocket connection to agent
                var clientWebSocket = new System.Net.WebSockets.ClientWebSocket();
                await clientWebSocket.ConnectAsync(new Uri(agentWsUrl), Context.ConnectionAborted);

                session.AgentWebSocket = clientWebSocket;
                _activeSessions[connectionId] = session;

                _logger.LogInformation("Console session established for {ContainerId} via {AgentUrl}", 
                    containerId, agent.InternalUrl);

                // Start forwarding messages from agent to client
                _ = Task.Run(async () => await ForwardAgentMessagesToClientAsync(session));

                await Clients.Caller.SendAsync("Connected", containerId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error attaching to container {ContainerId}", containerId);
                await Clients.Caller.SendAsync("Error", $"Failed to attach: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Client sends input to the container console
        /// </summary>
        public async Task SendInput(string input)
        {
            var connectionId = Context.ConnectionId;
            
            if (!_activeSessions.TryGetValue(connectionId, out var session))
            {
                _logger.LogWarning("No active session for connection {ConnectionId}", connectionId);
                return;
            }

            try
            {
                if (session.AgentWebSocket?.State == System.Net.WebSockets.WebSocketState.Open)
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(input);
                    await session.AgentWebSocket.SendAsync(
                        new ArraySegment<byte>(bytes),
                        System.Net.WebSockets.WebSocketMessageType.Text,
                        true,
                        Context.ConnectionAborted);

                    _logger.LogTrace("Sent {Length} bytes to container {ContainerId}", bytes.Length, session.ContainerId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending input to container {ContainerId}", session.ContainerId);
                await Clients.Caller.SendAsync("Error", $"Failed to send input: {ex.Message}");
            }
        }

        /// <summary>
        /// Execute a command in the container
        /// </summary>
        public async Task<string> ExecCommand(string containerId, string command, string[] args)
        {
            _logger.LogInformation("Executing command '{Command}' in container {ContainerId}", command, containerId);

            try
            {
                var agent = await _nodeAgentDiscovery.GetAgentForContainerAsync(containerId);
                if (agent == null)
                {
                    return "ERROR: Container not found or agent unavailable";
                }

                var httpClient = _httpClientFactory.CreateClient();
                httpClient.BaseAddress = new Uri(agent.InternalUrl);

                var request = new
                {
                    Cmd = new[] { command }.Concat(args ?? Array.Empty<string>()).ToArray(),
                    AttachStdout = true,
                    AttachStderr = true
                };

                var response = await httpClient.PostAsJsonAsync($"/containers/{containerId}/exec", request);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadAsStringAsync();
                await Clients.Caller.SendAsync("CommandOutput", result);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing command in container {ContainerId}", containerId);
                return $"ERROR: {ex.Message}";
            }
        }

        /// <summary>
        /// Disconnect from container console
        /// </summary>
        public async Task Disconnect()
        {
            await CleanupSession(Context.ConnectionId);
        }

        /// <summary>
        /// Called when client disconnects
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await CleanupSession(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Forward messages from agent WebSocket to SignalR client
        /// </summary>
        private async Task ForwardAgentMessagesToClientAsync(ConsoleSession session)
        {
            var buffer = new byte[4096];
            var webSocket = session.AgentWebSocket;

            try
            {
                while (webSocket?.State == System.Net.WebSockets.WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        CancellationToken.None);

                    if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Text)
                    {
                        var message = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                        await Clients.Client(session.ConnectionId).SendAsync("Output", message);
                    }
                    else if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Close)
                    {
                        _logger.LogInformation("Agent closed WebSocket for container {ContainerId}", session.ContainerId);
                        await Clients.Client(session.ConnectionId).SendAsync("Disconnected", "Agent closed connection");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding messages from agent for container {ContainerId}", session.ContainerId);
                await Clients.Client(session.ConnectionId).SendAsync("Error", $"Connection error: {ex.Message}");
            }
            finally
            {
                await CleanupSession(session.ConnectionId);
            }
        }

        /// <summary>
        /// Cleanup session resources
        /// </summary>
        private async Task CleanupSession(string connectionId)
        {
            if (_activeSessions.TryRemove(connectionId, out var session))
            {
                _logger.LogInformation("Cleaning up console session for connection {ConnectionId}, container {ContainerId}",
                    connectionId, session.ContainerId);

                if (session.AgentWebSocket != null)
                {
                    try
                    {
                        if (session.AgentWebSocket.State == System.Net.WebSockets.WebSocketState.Open)
                        {
                            await session.AgentWebSocket.CloseAsync(
                                System.Net.WebSockets.WebSocketCloseStatus.NormalClosure,
                                "Client disconnected",
                                CancellationToken.None);
                        }
                        session.AgentWebSocket.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error closing agent WebSocket");
                    }
                }
            }
        }

        /// <summary>
        /// Session tracking
        /// </summary>
        private class ConsoleSession
        {
            public string ConnectionId { get; set; } = string.Empty;
            public string ContainerId { get; set; } = string.Empty;
            public string AgentUrl { get; set; } = string.Empty;
            public string WebSocketUrl { get; set; } = string.Empty;
            public System.Net.WebSockets.ClientWebSocket? AgentWebSocket { get; set; }
            public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
        }
    }
}
