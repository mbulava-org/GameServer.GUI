namespace GameServer.API.Repositories.V2;

public enum ResourceHistoryCalculation
{
    Min,
    Max,
    Avg,
    Median
}

public sealed record GameServerCalculatedResourceHistoryResult
{
    public string ServerId { get; init; } = string.Empty;
    public DateTime? ActualFromUtc { get; init; }
    public DateTime? ActualToUtc { get; init; }
    public long? EffectiveBucketWidthMs { get; init; }
    public int MaxDataPoints { get; init; }
    public ResourceHistoryCalculation Calculation { get; init; } = ResourceHistoryCalculation.Avg;
    public int RawSampleCount { get; init; }
    public bool RatesIncomplete { get; init; }
    public bool TotalsIncomplete { get; init; }
    public IReadOnlyList<GameServerCalculatedResourceHistoryPointResult> Points { get; init; } = [];
}

public sealed record GameServerCalculatedResourceHistoryPointResult
{
    public DateTime Timestamp { get; init; }
    public int SampleCount { get; init; }

    public double? CpuUsagePercent { get; init; }
    public long? MemoryUsageBytes { get; init; }
    public long? MemoryLimitBytes { get; init; }
    public double? MemoryUsagePercent { get; init; }

    public double? NetworkRxKBps { get; init; }
    public double? NetworkTxKBps { get; init; }
    public double? BlockReadKBps { get; init; }
    public double? BlockWriteKBps { get; init; }

    public long? NetworkRxTotalBytes { get; init; }
    public long? NetworkTxTotalBytes { get; init; }
    public long? BlockReadTotalBytes { get; init; }
    public long? BlockWriteTotalBytes { get; init; }
}
