using GameServer.API.Data.V2;

namespace GameServer.API.Repositories.V2;

public interface IGameServerResourceUtilizationRepository
{
    Task BatchInsertAsync(IEnumerable<GameServerResourceUtilizationEntity> records, CancellationToken cancellationToken = default);

    Task<List<GameServerResourceUtilizationEntity>> GetHistoryAsync(
        string serverId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int limit = 5000,
        CancellationToken cancellationToken = default);

    Task<GameServerResourceUtilizationEntity?> GetLatestAsync(
        string serverId,
        CancellationToken cancellationToken = default);
}
