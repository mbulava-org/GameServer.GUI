using GameServer.Docker.Configurations;
using GameServer.Docker.Interfaces;
using GameServer.Docker.Models;
using System.Collections.Concurrent;

namespace GameServer.Docker.Services
{
    /// <summary>
    /// In-memory registry for agents discovered through UDP announcements.
    /// </summary>
    public sealed class UdpAgentRegistryService : IUdpAgentRegistry
    {
        private readonly ILogger<UdpAgentRegistryService> _logger;
        private readonly UdpAgentDiscoveryOptions _options;
        private readonly ConcurrentDictionary<string, UdpAgentEntry> _agentsByNode = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, string> _containerToNode = new(StringComparer.Ordinal);

        public UdpAgentRegistryService(
            ILogger<UdpAgentRegistryService> logger,
            UdpAgentDiscoveryOptions options)
        {
            _logger = logger;
            _options = options;
        }

        /// <summary>
        /// Adds or refreshes an agent announcement in the registry.
        /// </summary>
        public void UpsertAnnouncement(UdpAgentAnnouncement announcement)
        {
            ArgumentNullException.ThrowIfNull(announcement);

            if (string.IsNullOrWhiteSpace(announcement.NodeId) ||
                string.IsNullOrWhiteSpace(announcement.NodeName) ||
                string.IsNullOrWhiteSpace(announcement.InternalUrl))
            {
                _logger.LogDebug("Ignoring UDP announcement with missing identity fields");
                return;
            }

            var now = DateTimeOffset.UtcNow;
            var announcementTime = announcement.Timestamp == default ? now : announcement.Timestamp;
            var expiresAt = announcementTime.AddSeconds(Math.Max(1, _options.AnnouncementTtlSeconds));

            var endpoint = new NodeAgentEndpoint
            {
                NodeId = announcement.NodeId,
                NodeName = announcement.NodeName,
                InternalUrl = announcement.InternalUrl,
                IsManagerNode = announcement.IsManagerNode,
                IsHealthy = true,
                DiscoveredAt = announcementTime.UtcDateTime,
                LastHeartbeat = announcementTime.UtcDateTime
            };

            _agentsByNode[announcement.NodeId] = new UdpAgentEntry(endpoint, expiresAt, announcement.ContainerIds);

            var currentContainers = announcement.ContainerIds
                .Where(containerId => !string.IsNullOrWhiteSpace(containerId))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            foreach (var existingContainer in _containerToNode.Where(kvp => kvp.Value == announcement.NodeId).Select(kvp => kvp.Key).ToList())
            {
                _containerToNode.TryRemove(existingContainer, out _);
            }

            foreach (var containerId in currentContainers)
            {
                _containerToNode[containerId] = announcement.NodeId;
            }

            _logger.LogTrace(
                "UDP agent announcement stored: Node={NodeName} ({NodeId}), Containers={ContainerCount}, Url={Url}, ExpiresAt={ExpiresAt}",
                announcement.NodeName,
                announcement.NodeId,
                currentContainers.Count,
                announcement.InternalUrl,
                expiresAt);
        }

        /// <summary>
        /// Gets the UDP-discovered agent that currently owns a container.
        /// </summary>
        public NodeAgentEndpoint? GetAgentForContainer(string containerId)
        {
            if (string.IsNullOrWhiteSpace(containerId))
            {
                return null;
            }

            RemoveExpired(DateTimeOffset.UtcNow);

            if (_containerToNode.TryGetValue(containerId, out var nodeId) &&
                _agentsByNode.TryGetValue(nodeId, out var entry))
            {
                return entry.Endpoint;
            }

            return null;
        }

        /// <summary>
        /// Gets all currently active UDP-discovered agents.
        /// </summary>
        public IReadOnlyCollection<NodeAgentEndpoint> GetAllAgents()
        {
            RemoveExpired(DateTimeOffset.UtcNow);
            return _agentsByNode.Values.Select(entry => entry.Endpoint).ToList();
        }

        /// <summary>
        /// Removes expired UDP-discovered agents and their container mappings.
        /// </summary>
        public void RemoveExpired(DateTimeOffset now)
        {
            var expiredNodeIds = _agentsByNode
                .Where(kvp => kvp.Value.ExpiresAt <= now)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var nodeId in expiredNodeIds)
            {
                if (!_agentsByNode.TryRemove(nodeId, out var removedEntry))
                {
                    continue;
                }

                foreach (var containerId in _containerToNode.Where(kvp => kvp.Value == nodeId).Select(kvp => kvp.Key).ToList())
                {
                    _containerToNode.TryRemove(containerId, out _);
                }

                _logger.LogDebug(
                    "Removed expired UDP agent: Node={NodeName} ({NodeId}), Url={Url}",
                    removedEntry.Endpoint.NodeName,
                    nodeId,
                    removedEntry.Endpoint.InternalUrl);
            }
        }

        private sealed record UdpAgentEntry(NodeAgentEndpoint Endpoint, DateTimeOffset ExpiresAt, IReadOnlyCollection<string> ContainerIds);
    }
}
