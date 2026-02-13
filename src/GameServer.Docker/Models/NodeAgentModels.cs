namespace GameServer.Docker.Models
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
