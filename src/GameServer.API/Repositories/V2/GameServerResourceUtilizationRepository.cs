using GameServer.API.Data.V2;
using Microsoft.EntityFrameworkCore;

namespace GameServer.API.Repositories.V2;

public class GameServerResourceUtilizationRepository(
    GameServerV2DbContext context,
    ILogger<GameServerResourceUtilizationRepository> logger)
    : IGameServerResourceUtilizationRepository
{
    public async Task BatchInsertAsync(
        IEnumerable<GameServerResourceUtilizationEntity> records,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);

        var list = records.ToList();
        if (list.Count == 0)
        {
            return;
        }

        try
        {
            await context.ResourceUtilizations.AddRangeAsync(list, cancellationToken).ConfigureAwait(false);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogDebug("Batch inserted {Count} resource utilization records to database", list.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error batch inserting {Count} resource utilization records to database", list.Count);
            throw;
        }
    }

    public async Task<List<GameServerResourceUtilizationEntity>> GetHistoryAsync(
        string serverId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int limit = 5000,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);

        if (limit <= 0)
        {
            limit = Int32.MaxValue;
        }

        var query = context.ResourceUtilizations
            .AsNoTracking()
            .Where(r => r.ServerId == serverId);

        if (fromUtc.HasValue)
        {
            query = query.Where(r => r.Timestamp >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(r => r.Timestamp <= toUtc.Value);
        }

        return await query
            .OrderByDescending(r => r.Timestamp)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<GameServerResourceUtilizationEntity?> GetLatestAsync(
        string serverId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);

        return await context.ResourceUtilizations
            .AsNoTracking()
            .Where(r => r.ServerId == serverId)
            .OrderByDescending(r => r.Timestamp)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<GameServerCalculatedResourceHistoryResult> GetCalculatedHistoryAsync(
        string serverId,
        DateTime? fromUtc,
        DateTime? toUtc,
        int maxDataPoints,
        ResourceHistoryCalculation calculation,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);

        if (maxDataPoints <= 0)
        {
            maxDataPoints = 1;
        }

        var query = context.ResourceUtilizations
            .AsNoTracking()
            .Where(r => r.ServerId == serverId);

        if (fromUtc.HasValue)
        {
            query = query.Where(r => r.Timestamp >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(r => r.Timestamp <= toUtc.Value);
        }

        var rawSampleCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        if (rawSampleCount == 0)
        {
            return new GameServerCalculatedResourceHistoryResult
            {
                ServerId = serverId,
                MaxDataPoints = maxDataPoints,
                Calculation = calculation,
                RawSampleCount = rawSampleCount,
                Points = []
            };
        }

        var first = await query.MinAsync(r => (DateTime?)r.Timestamp, cancellationToken).ConfigureAwait(false);
        var last = await query.MaxAsync(r => (DateTime?)r.Timestamp, cancellationToken).ConfigureAwait(false);
        if (!first.HasValue || !last.HasValue)
        {
            return new GameServerCalculatedResourceHistoryResult
            {
                ServerId = serverId,
                MaxDataPoints = maxDataPoints,
                Calculation = calculation,
                RawSampleCount = 0,
                Points = []
            };
        }

        long? effectiveBucketWidthMs = null;
        if (rawSampleCount > maxDataPoints)
        {
            var durationMsInclusive = Math.Max(1d, (last.Value - first.Value).TotalMilliseconds + 1d);
            var widthMs = Math.Max(1L, (long)Math.Ceiling(durationMsInclusive / maxDataPoints));
            effectiveBucketWidthMs = widthMs;
        }

        var points = await BuildHistoryPointsAsync(
                query,
                first.Value,
                effectiveBucketWidthMs,
                calculation,
                cancellationToken)
            .ConfigureAwait(false);

        return new GameServerCalculatedResourceHistoryResult
        {
            ServerId = serverId,
            ActualFromUtc = first.Value,
            ActualToUtc = last.Value,
            EffectiveBucketWidthMs = effectiveBucketWidthMs,
            MaxDataPoints = maxDataPoints,
            Calculation = calculation,
            RawSampleCount = rawSampleCount,
            RatesIncomplete = points.Any(p => p.NetworkRxKBps is null || p.NetworkTxKBps is null || p.BlockReadKBps is null || p.BlockWriteKBps is null),
            TotalsIncomplete = points.Any(p => p.NetworkRxTotalBytes is null || p.NetworkTxTotalBytes is null || p.BlockReadTotalBytes is null || p.BlockWriteTotalBytes is null),
            Points = points
        };
    }

    private static async Task<List<GameServerCalculatedResourceHistoryPointResult>> BuildHistoryPointsAsync(
        IQueryable<GameServerResourceUtilizationEntity> query,
        DateTime firstTimestamp,
        long? bucketWidthMs,
        ResourceHistoryCalculation calculation,
        CancellationToken cancellationToken)
    {
        var results = new List<GameServerCalculatedResourceHistoryPointResult>();
        var previousByContainer = new Dictionary<string, GameServerResourceUtilizationEntity>(StringComparer.Ordinal);
        long? networkRxTotal = null;
        long? networkTxTotal = null;
        long? blockReadTotal = null;
        long? blockWriteTotal = null;
        long? activeBucketKey = null;
        var activeBucketSamples = new List<ComputedSample>();

        await foreach (var record in query
            .OrderBy(r => r.Timestamp)
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            var computed = ComputeSample(
                record,
                previousByContainer,
                ref networkRxTotal,
                ref networkTxTotal,
                ref blockReadTotal,
                ref blockWriteTotal);

            if (!bucketWidthMs.HasValue)
            {
                results.Add(BuildPoint([computed], calculation, record.Timestamp));
                continue;
            }

            var bucketKey = (long)Math.Floor((record.Timestamp - firstTimestamp).TotalMilliseconds / bucketWidthMs.Value);
            if (activeBucketKey.HasValue && bucketKey != activeBucketKey.Value)
            {
                results.Add(BuildPoint(
                    activeBucketSamples,
                    calculation,
                    firstTimestamp.AddMilliseconds(activeBucketKey.Value * bucketWidthMs.Value)));
                activeBucketSamples = [];
            }

            activeBucketKey = bucketKey;
            activeBucketSamples.Add(computed);
        }

        if (activeBucketSamples.Count > 0 && activeBucketKey.HasValue && bucketWidthMs.HasValue)
        {
            results.Add(BuildPoint(
                activeBucketSamples,
                calculation,
                firstTimestamp.AddMilliseconds(activeBucketKey.Value * bucketWidthMs.Value)));
        }

        return results;
    }

    private static GameServerCalculatedResourceHistoryPointResult BuildPoint(
        List<ComputedSample> bucketSamples,
        ResourceHistoryCalculation calculation,
        DateTime timestamp)
    {
        var records = bucketSamples.Select(s => s.Record).ToList();
        return new GameServerCalculatedResourceHistoryPointResult
        {
            Timestamp = timestamp,
            SampleCount = bucketSamples.Count,
            CpuUsagePercent = Aggregate(records.Select(r => r.CpuUsagePercent), calculation),
            MemoryUsageBytes = AggregateLong(records.Select(r => r.MemoryUsageBytes), calculation),
            MemoryLimitBytes = AggregateLong(records.Select(r => r.MemoryLimitBytes), calculation),
            MemoryUsagePercent = Aggregate(records.Select(r => r.MemoryUsagePercent), calculation),
            NetworkRxKBps = Aggregate(bucketSamples.Select(s => s.NetworkRxKBps), calculation),
            NetworkTxKBps = Aggregate(bucketSamples.Select(s => s.NetworkTxKBps), calculation),
            BlockReadKBps = Aggregate(bucketSamples.Select(s => s.BlockReadKBps), calculation),
            BlockWriteKBps = Aggregate(bucketSamples.Select(s => s.BlockWriteKBps), calculation),
            NetworkRxTotalBytes = bucketSamples.Last().NetworkRxTotalBytes,
            NetworkTxTotalBytes = bucketSamples.Last().NetworkTxTotalBytes,
            BlockReadTotalBytes = bucketSamples.Last().BlockReadTotalBytes,
            BlockWriteTotalBytes = bucketSamples.Last().BlockWriteTotalBytes
        };
    }

    private static ComputedSample ComputeSample(
        GameServerResourceUtilizationEntity record,
        Dictionary<string, GameServerResourceUtilizationEntity> previousByContainer,
        ref long? networkRxTotal,
        ref long? networkTxTotal,
        ref long? blockReadTotal,
        ref long? blockWriteTotal)
    {
        double? networkRxRate = null;
        double? networkTxRate = null;
        double? blockReadRate = null;
        double? blockWriteRate = null;

        var containerKey = string.IsNullOrWhiteSpace(record.ContainerId) ? null : record.ContainerId;
        if (!string.IsNullOrWhiteSpace(containerKey)
            && previousByContainer.TryGetValue(containerKey, out var previous))
        {
            var elapsedSeconds = (record.Timestamp - previous.Timestamp).TotalSeconds;
            if (elapsedSeconds > 0)
            {
                (networkRxRate, networkRxTotal) = CalculateRateAndTotal(previous.NetworkRxBytes, record.NetworkRxBytes, elapsedSeconds, networkRxTotal);
                (networkTxRate, networkTxTotal) = CalculateRateAndTotal(previous.NetworkTxBytes, record.NetworkTxBytes, elapsedSeconds, networkTxTotal);
                (blockReadRate, blockReadTotal) = CalculateRateAndTotal(previous.BlockReadBytes, record.BlockReadBytes, elapsedSeconds, blockReadTotal);
                (blockWriteRate, blockWriteTotal) = CalculateRateAndTotal(previous.BlockWriteBytes, record.BlockWriteBytes, elapsedSeconds, blockWriteTotal);
            }
            else
            {
                networkRxTotal = null;
                networkTxTotal = null;
                blockReadTotal = null;
                blockWriteTotal = null;
            }
        }
        else if (string.IsNullOrWhiteSpace(containerKey))
        {
            networkRxTotal = null;
            networkTxTotal = null;
            blockReadTotal = null;
            blockWriteTotal = null;
        }

        if (!string.IsNullOrWhiteSpace(containerKey))
        {
            previousByContainer[containerKey] = record;
        }

        return new ComputedSample(
            record,
            networkRxRate,
            networkTxRate,
            blockReadRate,
            blockWriteRate,
            networkRxTotal,
            networkTxTotal,
            blockReadTotal,
            blockWriteTotal);
    }

    private static (double? RateKbPerSecond, long? TotalBytes) CalculateRateAndTotal(
        long? previousCounter,
        long? currentCounter,
        double elapsedSeconds,
        long? runningTotalBytes)
    {
        if (!previousCounter.HasValue || !currentCounter.HasValue || elapsedSeconds <= 0)
        {
            return (null, null);
        }

        var delta = currentCounter.Value - previousCounter.Value;
        if (delta < 0)
        {
            return (null, null);
        }

        var total = runningTotalBytes.GetValueOrDefault();
        total += delta;
        var rate = delta / 1024d / elapsedSeconds;
        return (Math.Round(rate, 2), total);
    }

    private static double? Aggregate(IEnumerable<double?> values, ResourceHistoryCalculation calculation)
    {
        var list = values.Where(v => v.HasValue).Select(v => v!.Value).OrderBy(v => v).ToList();
        if (list.Count == 0)
        {
            return null;
        }

        return Math.Round(calculation switch
        {
            ResourceHistoryCalculation.Min => list[0],
            ResourceHistoryCalculation.Max => list[^1],
            ResourceHistoryCalculation.Median => GetMedian(list),
            _ => list.Average()
        }, 2);
    }

    private static long? AggregateLong(IEnumerable<long?> values, ResourceHistoryCalculation calculation)
    {
        var list = values.Where(v => v.HasValue).Select(v => v!.Value).OrderBy(v => v).ToList();
        if (list.Count == 0)
        {
            return null;
        }

        return calculation switch
        {
            ResourceHistoryCalculation.Min => list[0],
            ResourceHistoryCalculation.Max => list[^1],
            ResourceHistoryCalculation.Median => (long)Math.Round(GetMedian(list.Select(v => (double)v).ToList())),
            _ => (long)Math.Round(list.Average(v => (double)v))
        };
    }

    private static double GetMedian(IReadOnlyList<double> sortedValues)
    {
        if (sortedValues.Count == 0)
        {
            return 0;
        }

        var middle = sortedValues.Count / 2;
        if (sortedValues.Count % 2 == 0)
        {
            return (sortedValues[middle - 1] + sortedValues[middle]) / 2.0;
        }

        return sortedValues[middle];
    }

    private sealed record ComputedSample(
        GameServerResourceUtilizationEntity Record,
        double? NetworkRxKBps,
        double? NetworkTxKBps,
        double? BlockReadKBps,
        double? BlockWriteKBps,
        long? NetworkRxTotalBytes,
        long? NetworkTxTotalBytes,
        long? BlockReadTotalBytes,
        long? BlockWriteTotalBytes);
}
