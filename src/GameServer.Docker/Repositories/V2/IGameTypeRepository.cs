using GameServer.Docker.Models.V2;

namespace GameServer.Docker.Repositories.V2;

public interface IGameTypeRepository
{
    Task InitializeDatabaseAsync();

    Task<List<GameType>> GetAllAsync(bool includeInactive = false);

    Task<GameType?> GetByKeyAsync(string key);

    Task<GameType?> GetByIdAsync(int id);

    Task<GameType> CreateAsync(GameType gameType);

    Task<GameType> UpdateAsync(GameType gameType);

    Task DeleteAsync(string key);

    Task<GameTypeRevision> AddRevisionAsync(string gameTypeKey, GameTypeRevision revision);

    Task<GameTypeRevision> UpdateRevisionAsync(string gameTypeKey, GameTypeRevision revision);

    Task SetCurrentRevisionAsync(string gameTypeKey, int revisionId);
}
