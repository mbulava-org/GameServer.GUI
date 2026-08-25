namespace GameServer.API.Dtos.V2;

public record GameServerResourceHistoryDto
{
    public long Id { get; init; }
    public string ServerId { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public double? CpuUsagePercent { get; init; }
    public long? MemoryUsageBytes { get; init; }
    public long? MemoryLimitBytes { get; init; }
    public double? MemoryUsagePercent { get; init; }
    public long? NetworkRxBytes { get; init; }
    public long? NetworkTxBytes { get; init; }
    public long? BlockReadBytes { get; init; }
    public long? BlockWriteBytes { get; init; }
    public int DesiredReplicas { get; init; }
    public int RunningReplicas { get; init; }
    public string? ContainerId { get; init; }
}
