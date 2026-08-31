namespace GameServer.API.Models
{
    /// <summary>
    /// Response for agent discovery endpoint
    /// </summary>
    public class AgentDiscoveryResponse
    {
        public DateTime Timestamp { get; set; }
        public int AgentCount { get; set; }
        public List<AgentInfo> Agents { get; set; } = new();
    }

    /// <summary>
    /// Information about a discovered agent
    /// </summary>
    public class AgentInfo
    {
        public string NodeId { get; set; } = string.Empty;
        public string NodeName { get; set; } = string.Empty;
        public string TaskId { get; set; } = string.Empty;
        public string InternalUrl { get; set; } = string.Empty;
        public bool IsHealthy { get; set; }
        public DateTime DiscoveredAt { get; set; }
    }
}
