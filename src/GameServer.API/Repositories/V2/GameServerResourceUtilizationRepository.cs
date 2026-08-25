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
        int limit = 500,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);

        if (limit <= 0)
        {
            limit = 500;
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
}
