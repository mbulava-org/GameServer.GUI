namespace GameServer.Docker.Agent.Models
{
    /// <summary>
    /// Request to create a Docker Swarm service
    /// </summary>
    public class CreateServiceRequest
    {
        public required string ServiceName { get; set; }
        public required string Image { get; set; }
        public Dictionary<string, string> Labels { get; set; } = new();
        public Dictionary<string, string> Env { get; set; } = new();
        public List<PortMapping> Ports { get; set; } = new();
        public List<MountConfig> Mounts { get; set; } = new();
        public ResourcesConfig? Resources { get; set; }
        public RestartPolicyConfig? RestartPolicy { get; set; }
        public PlacementConfig? Placement { get; set; }
        public List<string> Networks { get; set; } = new();
        public bool? TTY { get; set; }
        public List<string>? DnsNameservers { get; set; }
    }

    public class PortMapping
    {
        public uint TargetPort { get; set; }
        public uint? PublishedPort { get; set; }
        public string Protocol { get; set; } = "tcp";
        public string? PublishMode { get; set; }
    }

    public class MountConfig
    {
        public required string Type { get; set; } // bind, volume, tmpfs
        public required string Source { get; set; }
        public required string Target { get; set; }
        public bool ReadOnly { get; set; }
        public string? DriverName { get; set; }
        public Dictionary<string, string>? VolumeOptions { get; set; }
        public int? OwnerUid { get; set; }
        public int? OwnerGid { get; set; }
        public string? Permissions { get; set; }
    }

    public class ResourcesConfig
    {
        public long? MemoryBytes { get; set; }
        public long? NanoCPUs { get; set; }
    }

    public class RestartPolicyConfig
    {
        public required string Condition { get; set; } // none, on-failure, any
        public ulong? Delay { get; set; }
        public ulong? MaxAttempts { get; set; }
    }

    public class PlacementConfig
    {
        public List<string>? Constraints { get; set; }
    }

    /// <summary>
    /// Request to update a Docker Swarm service
    /// </summary>
    public class UpdateServiceRequest
    {
        public required string ServiceId { get; set; }
        public string? Image { get; set; }
        public Dictionary<string, string>? Labels { get; set; }
        public Dictionary<string, string>? Env { get; set; }
        public List<PortMapping>? Ports { get; set; }
        public List<MountConfig>? Mounts { get; set; }
        public ResourcesConfig? Resources { get; set; }
        public bool ForceUpdate { get; set; }
        public ulong? Replicas { get; set; }
        public List<string>? Networks { get; set; }
        public bool? TTY { get; set; }
        public List<string>? DnsNameservers { get; set; }
    }

    /// <summary>
    /// Response from service operations
    /// </summary>
    public class ServiceOperationResponse
    {
        public bool Success { get; set; }
        public string? ServiceId { get; set; }
        public string? Message { get; set; }
        public Dictionary<string, object>? Data { get; set; }
    }
}
