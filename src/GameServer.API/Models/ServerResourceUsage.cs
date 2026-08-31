namespace GameServer.API.Models
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
        public int PreparingTasks { get; set; }
        public int StartingTasks { get; set; }
        public int TaskCount { get; set; }
        public string? LatestTaskState { get; set; }
        public string? LatestTaskMessage { get; set; }
        public string? LatestTaskError { get; set; }

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

        public string ServiceStatus
        {
            get
            {
                if (DesiredReplicas == 0)
                {
                    return "Stopped";
                }

                if (RunningReplicas == DesiredReplicas && DesiredReplicas > 0)
                {
                    return "Running";
                }

                if (RunningReplicas > DesiredReplicas && DesiredReplicas > 0)
                {
                    return "Scaling Down";
                }

                if (PreparingTasks > 0)
                {
                    return "Preparing";
                }

                if (StartingTasks > 0 || PendingTasks > 0)
                {
                    return "Starting";
                }

                if (FailedTasks > 0 && RunningReplicas == 0)
                {
                    return "Failed";
                }

                if (RunningReplicas < DesiredReplicas)
                {
                    return "Starting";
                }

                if (RunningReplicas > DesiredReplicas)
                {
                    return "Scaling Down";
                }

                return "Unknown";
            }
        }

        // Real-time Container Stats (from Node Agent)
        public ContainerStats? RealTimeStats { get; set; }
        
        // Container-level data availability
        public bool HasRealTimeStats => RealTimeStats != null;

        // Flat properties mirrored for Client deserialization
        public double? CpuUsagePercent => RealTimeStats?.CpuUsagePercent;
        public long? MemoryUsageBytes => RealTimeStats != null ? (long)RealTimeStats.MemoryUsageBytes : null;
        public long? MemoryLimitBytes => RealTimeStats != null ? (long)RealTimeStats.MemoryLimitBytes : ServiceMemoryLimitPerReplica;
        public double? MemoryUsagePercent => RealTimeStats?.MemoryUsagePercent;
        public long? NetworkRxBytes => RealTimeStats?.NetworkRxBytes;
        public long? NetworkTxBytes => RealTimeStats?.NetworkTxBytes;
        public long? BlockReadBytes => RealTimeStats?.BlockReadBytes;
        public long? BlockWriteBytes => RealTimeStats?.BlockWriteBytes;
        public string? ContainerId => RealTimeStats?.ContainerId ?? ContainerIds.FirstOrDefault();
        public string? NodeName => null;
        public int? Replicas => DesiredReplicas;
        public int? HealthyReplicas => RunningReplicas;
    }
}