namespace GameServer.Docker.Client.Models
{
    /// <summary>
    /// Mirrors the SignalR payload shape emitted by the server-side
    /// <see cref="GameServer.Docker.Models.ServerResourceUsage"/> model.
    /// Kept in the client assembly so the resource-monitoring client can
    /// deserialize hub messages without depending on the server project.
    /// </summary>
    public class HubResourceUsage
    {
        public string ServerId { get; set; } = string.Empty;
        public string ServiceId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public DateTime? ServiceCreatedAt { get; set; }
        public DateTime? ServiceUpdatedAt { get; set; }
        public ulong ServiceVersion { get; set; }

        public int DesiredReplicas { get; set; }
        public int RunningReplicas { get; set; }
        public int FailedTasks { get; set; }
        public int PendingTasks { get; set; }
        public int TaskCount { get; set; }

        public List<string> TaskIds { get; set; } = new();
        public List<string> ContainerIds { get; set; } = new();

        public ulong? ServiceCpuLimitPerReplica { get; set; }
        public ulong? ServiceCpuLimitTotal { get; set; }
        public ulong? ServiceCpuReservationPerReplica { get; set; }
        public ulong? ServiceCpuReservationTotal { get; set; }

        public long? ServiceMemoryLimitPerReplica { get; set; }
        public long? ServiceMemoryLimitTotal { get; set; }
        public long? ServiceMemoryReservationPerReplica { get; set; }
        public long? ServiceMemoryReservationTotal { get; set; }

        public string? UpdateState { get; set; }
        public DateTime? UpdateStartedAt { get; set; }
        public DateTime? UpdateCompletedAt { get; set; }

        public string ServiceStatus => (DesiredReplicas, RunningReplicas) switch
        {
            (0, _) => "Stopped",
            var (d, r) when r == d => "Running",
            var (d, r) when r < d => "Starting",
            var (d, r) when r > d => "Scaling Down",
            _ => "Unknown"
        };

        public ContainerStats? RealTimeStats { get; set; }
    }

    public class ContainerStats
    {
        public double CpuUsagePercent { get; set; }
        public ulong TotalCpuUsage { get; set; }
        public long MemoryUsageBytes { get; set; }
        public long MemoryLimitBytes { get; set; }
        public double MemoryUsagePercent { get; set; }
        public long NetworkRxBytes { get; set; }
        public long NetworkTxBytes { get; set; }
        public long BlockReadBytes { get; set; }
        public long BlockWriteBytes { get; set; }
        public uint OnlineCpus { get; set; }
    }
}
