namespace GameServer.Docker.Agent.Models
{
    /// <summary>
    /// Health check response model
    /// </summary>
    public class HealthResponse
    {
        public string Status { get; set; } = "healthy";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string NodeName { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
    }

    /// <summary>
    /// Container statistics response model
    /// </summary>
    public class ContainerStatsResponse
    {
        public string ContainerId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public CpuStats Cpu { get; set; } = new();
        public MemoryStats Memory { get; set; } = new();
        public NetworkStats Network { get; set; } = new();
        public BlockIoStats BlockIo { get; set; } = new();
        public ulong Pids { get; set; }
    }

    public class CpuStats
    {
        public double UsagePercent { get; set; }
        public ulong TotalUsage { get; set; }
        public ulong SystemUsage { get; set; }
        public uint OnlineCpus { get; set; }
    }

    public class MemoryStats
    {
        public ulong UsageBytes { get; set; }
        public ulong LimitBytes { get; set; }
        public double UsagePercent { get; set; }
        public ulong MaxUsageBytes { get; set; }
    }

    public class NetworkStats
    {
        public long RxBytes { get; set; }
        public long TxBytes { get; set; }
    }

    public class BlockIoStats
    {
        public long ReadBytes { get; set; }
        public long WriteBytes { get; set; }
    }

    /// <summary>
    /// Container logs response model
    /// </summary>
    public class ContainerLogsResponse
    {
        public string ContainerId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public int LogLines { get; set; }
        public List<string> Logs { get; set; } = new();
    }

    /// <summary>
    /// Container inspection response model
    /// </summary>
    public class ContainerInspectResponse
    {
        public string ContainerId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public ContainerState State { get; set; } = new();
        public DateTime Created { get; set; }
        public string Image { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
    }

    public class ContainerState
    {
        public string Status { get; set; } = string.Empty;
        public bool Running { get; set; }
        public bool Paused { get; set; }
        public bool Restarting { get; set; }
        public long Pid { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime FinishedAt { get; set; }
    }

    /// <summary>
    /// List containers response model
    /// </summary>
    public class ContainerListResponse
    {
        public string NodeId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public int ContainerCount { get; set; }
        public List<ContainerSummary> Containers { get; set; } = new();
    }

    public class ContainerSummary
    {
        public string Id { get; set; } = string.Empty;
        public IList<string> Names { get; set; } = new List<string>();
        public string Image { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>
    /// Error response model
    /// </summary>
    public class ErrorResponse
    {
        public string Error { get; set; } = string.Empty;
    }
}
