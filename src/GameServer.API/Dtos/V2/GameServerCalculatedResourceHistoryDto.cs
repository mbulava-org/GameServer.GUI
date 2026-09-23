namespace GameServer.API.Dtos.V2;

public record GameServerCalculatedResourceHistoryDto
{
    public string ServerId { get; init; } = string.Empty;
    public DateTime? ActualFromUtc { get; init; }
    public DateTime? ActualToUtc { get; init; }
    public long? EffectiveBucketWidthMs { get; init; }
    public int MaxDataPoints { get; init; }
    public string Calculation { get; init; } = "avg";
    public int RawSampleCount { get; init; }
    public bool RatesIncomplete { get; init; }
    public bool TotalsIncomplete { get; init; }
    public IReadOnlyList<GameServerCalculatedResourceHistoryPointDto> Points { get; init; } = [];
}

public record GameServerCalculatedResourceHistoryPointDto
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
