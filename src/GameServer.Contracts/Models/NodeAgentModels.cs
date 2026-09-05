namespace GameServer.API.Models
{
    /// <summary>
    /// Represents a node agent endpoint for container-level operations
    /// </summary>
    public class NodeAgentEndpoint
    {
        public string NodeId { get; set; } = string.Empty;
        public string NodeName { get; set; } = string.Empty;
        public string TaskId { get; set; } = string.Empty;
        public string ContainerId { get; set; } = string.Empty;
        public string InternalUrl { get; set; } = string.Empty;
        public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;
        public bool IsHealthy { get; set; } = true;

        /// <summary>
        /// SignalR connection ID when agent is connected via registration
        /// </summary>
        public string? ConnectionId { get; set; }

        /// <summary>
        /// Last time a heartbeat was received from this agent
        /// </summary>
        public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether this agent is running on a Docker Swarm manager node
        /// </summary>
        public bool IsManagerNode { get; set; }
    }

    /// <summary>
    /// Information sent by agent during initial registration
    /// </summary>
    public class AgentRegistrationInfo
    {
        public string NodeId { get; set; } = string.Empty;
        public string NodeName { get; set; } = string.Empty;
        public string InternalUrl { get; set; } = string.Empty;
        public List<string> Capabilities { get; set; } = new();
        public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether this agent is running on a Docker Swarm manager node
        /// Only manager nodes can perform service-level operations
        /// </summary>
        public bool IsManagerNode { get; set; }
    }

    /// <summary>
    /// Heartbeat information sent periodically by agents
    /// </summary>
    public class AgentHeartbeatInfo
    {
        public string NodeId { get; set; } = string.Empty;
        public List<string> ContainerIds { get; set; } = new();
        public string Health { get; set; } = "healthy";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Real-time container statistics from node agent
    /// </summary>
    public class ContainerStats
    {
        public string ContainerId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        
        // CPU
        public double CpuUsagePercent { get; set; }
        public ulong CpuTotalUsage { get; set; }
        public ulong CpuSystemUsage { get; set; }
        public uint OnlineCpus { get; set; }
        
        // Memory
        public ulong MemoryUsageBytes { get; set; }
        public ulong MemoryLimitBytes { get; set; }
        public double MemoryUsagePercent { get; set; }
        public ulong MemoryMaxUsageBytes { get; set; }
        
        // Network
        public long NetworkRxBytes { get; set; }
        public long NetworkTxBytes { get; set; }
        
        // Block I/O
        public long BlockReadBytes { get; set; }
        public long BlockWriteBytes { get; set; }
        
        // Processes
        public ulong Pids { get; set; }
    }
}
