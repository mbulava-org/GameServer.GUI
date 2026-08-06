using GameServer.Docker.Interfaces;
using GameServer.Docker.Models;
using Microsoft.AspNetCore.SignalR;

namespace GameServer.Docker.Hubs
{
    /// <summary>
    /// SignalR hub for agent registration and heartbeats.
    /// Agents connect to this hub and push their state to the Primary Service.
    /// This eliminates the need for the Primary Service to query Docker Swarm.
    /// </summary>
    public class AgentRegistrationHub : Hub
    {
        private readonly IAgentRegistry _agentRegistry;
        private readonly ILogger<AgentRegistrationHub> _logger;

        public AgentRegistrationHub(
            IAgentRegistry agentRegistry,
            ILogger<AgentRegistrationHub> logger)
        {
            _agentRegistry = agentRegistry;
            _logger = logger;
        }

        /// <summary>
        /// Called by agents on initial connection to register themselves
        /// </summary>
        public async Task RegisterAgent(AgentRegistrationInfo info)
        {
            var connectionId = Context.ConnectionId;

            // The URL reported by the agent may use a Docker node hostname (e.g. "dev-docker-100")
            // that is not resolvable from the Primary Service across nodes. The SignalR connection's
            // remote IP is the agent's actual address on the shared overlay network, so use that as
            // the host to reach the agent while preserving the scheme/port it advertised.
            var remoteIp = Context.GetHttpContext()?.Connection.RemoteIpAddress;
            var resolvedUrl = RewriteHostWithRemoteIp(info.InternalUrl, remoteIp);

            if (!string.Equals(resolvedUrl, info.InternalUrl, StringComparison.Ordinal))
            {
                _logger.LogInformation(
                    "Overriding agent-reported URL {ReportedUrl} with connection IP based URL {ResolvedUrl} for Node={NodeName} ({NodeId})",
                    info.InternalUrl,
                    resolvedUrl,
                    info.NodeName,
                    info.NodeId);

                info.InternalUrl = resolvedUrl;
            }

            _logger.LogInformation(
                "Agent registration request: Node={NodeName} ({NodeId}), ConnectionId={ConnectionId}, Url={Url}",
                info.NodeName,
                info.NodeId,
                connectionId,
                info.InternalUrl);

            _agentRegistry.RegisterAgent(info, connectionId);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Rebuilds the agent URL using the SignalR connection's remote IP address as the host,
        /// preserving the scheme and port advertised by the agent. Falls back to the original URL
        /// when the remote IP is unavailable or the URL cannot be parsed.
        /// </summary>
        private static string RewriteHostWithRemoteIp(string reportedUrl, System.Net.IPAddress? remoteIp)
        {
            if (remoteIp is null || string.IsNullOrWhiteSpace(reportedUrl))
            {
                return reportedUrl;
            }

            // Normalize IPv4-mapped IPv6 addresses (e.g. ::ffff:10.0.0.5) to their IPv4 form.
            if (remoteIp.IsIPv4MappedToIPv6)
            {
                remoteIp = remoteIp.MapToIPv4();
            }

            if (System.Net.IPAddress.IsLoopback(remoteIp))
            {
                return reportedUrl;
            }

            if (!Uri.TryCreate(reportedUrl, UriKind.Absolute, out var uri))
            {
                return reportedUrl;
            }

            var host = remoteIp.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                ? $"[{remoteIp}]"
                : remoteIp.ToString();

            var builder = new UriBuilder(uri) { Host = host };
            return builder.Uri.ToString().TrimEnd('/');
        }

        /// <summary>
        /// Called by agents periodically to report their container list and health
        /// </summary>
        public async Task SendHeartbeat(AgentHeartbeatInfo heartbeat)
        {
            var connectionId = Context.ConnectionId;

            _logger.LogTrace(
                "Agent heartbeat: Node={NodeId}, ConnectionId={ConnectionId}, Containers={ContainerCount}, Health={Health}",
                heartbeat.NodeId,
                connectionId,
                heartbeat.ContainerIds.Count,
                heartbeat.Health);

            _agentRegistry.UpdateAgentContainers(connectionId, heartbeat.ContainerIds);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Called automatically by SignalR when an agent disconnects
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionId = Context.ConnectionId;

            if (exception != null)
            {
                _logger.LogWarning(exception,
                    "Agent disconnected with exception: ConnectionId={ConnectionId}",
                    connectionId);
            }
            else
            {
                _logger.LogInformation(
                    "Agent disconnected gracefully: ConnectionId={ConnectionId}",
                    connectionId);
            }

            _agentRegistry.MarkAgentDisconnected(connectionId);

            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Called automatically by SignalR when an agent connects
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation(
                "Agent connected: ConnectionId={ConnectionId}, IP={IP}",
                Context.ConnectionId,
                Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString() ?? "unknown");

            await base.OnConnectedAsync();
        }
    }
}
