namespace GameServer.Docker.Models
{
    /// <summary>
    /// Docker Swarm Service-level resource information
    /// Contains service specifications, task states, and resource configurations
    /// Does NOT contain real-time container stats (requires Swarm Manager access only)
    /// </summary>
    public class ServerResourceUsage
    {
        // Service Identity
        public string ServerId { get; set; } = string.Empty;
        public string ServiceId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Service Lifecycle
        public DateTime? ServiceCreatedAt { get; set; }
        public DateTime? ServiceUpdatedAt { get; set; }
        public ulong ServiceVersion { get; set; }

        // Replica/Task Information
        public int DesiredReplicas { get; set; }
        public int RunningReplicas { get; set; }
        public int FailedTasks { get; set; }
        public int PendingTasks { get; set; }
        public int TaskCount { get; set; }

        // Task/Container References
        public List<string> TaskIds { get; set; } = new();
        public List<string> ContainerIds { get; set; } = new();

        // Service-level CPU Resource Specifications (NanoCPUs)
        public ulong? ServiceCpuLimitPerReplica { get; set; }
        public ulong? ServiceCpuLimitTotal { get; set; }
        public ulong? ServiceCpuReservationPerReplica { get; set; }
        public ulong? ServiceCpuReservationTotal { get; set; }

        // Service-level Memory Resource Specifications (Bytes)
        public long? ServiceMemoryLimitPerReplica { get; set; }
        public long? ServiceMemoryLimitTotal { get; set; }
        public long? ServiceMemoryReservationPerReplica { get; set; }
        public long? ServiceMemoryReservationTotal { get; set; }

        // Service Update Status
        public string? UpdateState { get; set; }
        public DateTime? UpdateStartedAt { get; set; }
        public DateTime? UpdateCompletedAt { get; set; }

        // Computed Properties for Display
        public double ReplicaHealthPercent => DesiredReplicas > 0
            ? (double)RunningReplicas / DesiredReplicas * 100.0
            : 0.0;

        public bool IsHealthy => RunningReplicas == DesiredReplicas && FailedTasks == 0;

        public string ServiceStatus => (DesiredReplicas, RunningReplicas) switch
        {
            (0, _) => "Stopped",
            var (d, r) when r == d => "Running",
            var (d, r) when r < d => "Starting",
            var (d, r) when r > d => "Scaling Down",
            _ => "Unknown"
        };

        // Real-time Container Stats (from Node Agent)
        public ContainerStats? RealTimeStats { get; set; }
        
        // Container-level data availability
        public bool HasRealTimeStats => RealTimeStats != null;
    }
}