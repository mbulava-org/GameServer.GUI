using Microsoft.AspNetCore.SignalR;
using GameServer.Docker.Hubs;
using GameServer.Docker.Interfaces;
using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace GameServer.Docker.Services
{
    /// <summary>
    /// Long-lived service that manages terminal sessions and WebSocket connections
    /// </summary>
    public class TerminalSessionManager
    {
        private readonly ILogger<TerminalSessionManager> _logger;
        private readonly IHubContext<ContainerConsoleHub> _hubContext;
        private readonly INodeAgentDiscovery _nodeAgentDiscovery;
        private readonly ConcurrentDictionary<string, TerminalSession> _sessions = new();

        public TerminalSessionManager(
            ILogger<TerminalSessionManager> logger,
            IHubContext<ContainerConsoleHub> hubContext,
            INodeAgentDiscovery nodeAgentDiscovery)
        {
            _logger = logger;
            _hubContext = hubContext;
            _nodeAgentDiscovery = nodeAgentDiscovery;
        }

        public async Task<(bool Success, string? Error)> StartSessionAsync(
            string connectionId, 
            string containerId, 
            string shell = "/bin/sh")
        {
            try
            {
                _logger.LogInformation("Starting terminal session for connection {ConnectionId}, container {ContainerId}", 
                    connectionId, containerId);

                // Find the agent
                var agent = await _nodeAgentDiscovery.GetAgentForContainerAsync(containerId);
                if (agent == null)
                {
                    return (false, "Container not found or agent unavailable");
                }

                // Build WebSocket URL
                var wsUrl = agent.InternalUrl.Replace("http://", "ws://").Replace("https://", "wss://");
                var shellCmd = Uri.EscapeDataString(shell);
                var agentWsUrl = $"{wsUrl}/containers/{containerId}/exec/ws?cmd={shellCmd}&tty=true";

                _logger.LogInformation("Connecting to agent at {Url}", agentWsUrl);

                // Create and connect WebSocket
                var clientWebSocket = new ClientWebSocket();
                await clientWebSocket.ConnectAsync(new Uri(agentWsUrl), CancellationToken.None);

                _logger.LogInformation("WebSocket connected for connection {ConnectionId}", connectionId);

                // Create session
                var session = new TerminalSession
                {
                    ConnectionId = connectionId,
                    ContainerId = containerId,
                    Shell = shell,
                    AgentUrl = agent.InternalUrl,
                    WebSocket = clientWebSocket,
                    StartTime = DateTime.UtcNow
                };

                _sessions[connectionId] = session;

                // Start forwarding in background (fire and forget)
                _ = Task.Run(async () => await ForwardMessagesAsync(session));

                _logger.LogInformation("Terminal session started successfully for {ConnectionId}", connectionId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start terminal session for {ConnectionId}", connectionId);
                return (false, ex.Message);
            }
        }

        public async Task SendInputAsync(string connectionId, string input)
        {
            if (!_sessions.TryGetValue(connectionId, out var session))
            {
                _logger.LogWarning("No session found for connection {ConnectionId}", connectionId);
                return;
            }

            try
            {
                if (session.WebSocket.State == WebSocketState.Open)
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(input);
                    await session.WebSocket.SendAsync(
                        new ArraySegment<byte>(bytes),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None);

                    _logger.LogTrace("Sent {Length} bytes to container for connection {ConnectionId}", 
                        bytes.Length, connectionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending input for connection {ConnectionId}", connectionId);
                await CloseSessionAsync(connectionId);
            }
        }

        public async Task CloseSessionAsync(string connectionId)
        {
            if (_sessions.TryRemove(connectionId, out var session))
            {
                _logger.LogInformation("Closing terminal session for {ConnectionId}", connectionId);

                try
                {
                    if (session.WebSocket.State == WebSocketState.Open)
                    {
                        await session.WebSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Client disconnected",
                            CancellationToken.None);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error closing WebSocket for {ConnectionId}", connectionId);
                }
                finally
                {
                    session.WebSocket.Dispose();
                }
            }
        }

        private async Task ForwardMessagesAsync(TerminalSession session)
        {
            var buffer = new byte[8192];
            var connectionId = session.ConnectionId;

            _logger.LogInformation("Started message forwarding for connection {ConnectionId}", connectionId);

            try
            {
                while (session.WebSocket.State == WebSocketState.Open)
                {
                    var result = await session.WebSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                        
                        // Send to SignalR client
                        await _hubContext.Clients.Client(connectionId).SendAsync("Output", message);
                        
                        _logger.LogTrace("Forwarded {Length} bytes to client {ConnectionId}", 
                            result.Count, connectionId);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogInformation("Agent closed WebSocket for {ConnectionId}", connectionId);
                        await _hubContext.Clients.Client(connectionId).SendAsync("Disconnected", "Shell exited");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in message forwarding for {ConnectionId}", connectionId);
                
                try
                {
                    await _hubContext.Clients.Client(connectionId).SendAsync("Error", $"Connection error: {ex.Message}");
                }
                catch
                {
                    // Client might be gone
                }
            }
            finally
            {
                _logger.LogInformation("Message forwarding ended for {ConnectionId}", connectionId);
                await CloseSessionAsync(connectionId);
            }
        }

        public class TerminalSession
        {
            public string ConnectionId { get; set; } = "";
            public string ContainerId { get; set; } = "";
            public string Shell { get; set; } = "";
            public string AgentUrl { get; set; } = "";
            public ClientWebSocket WebSocket { get; set; } = null!;
            public DateTime StartTime { get; set; }
        }
    }
}
