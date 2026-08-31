using GameServerModel = GameServer.API.Models.V2.GameServer;

namespace GameServer.API.Repositories.V2;

public interface IGameServerRepository
{
    Task<List<GameServerModel>> GetAllAsync(bool includeDeleted = false);

    Task<GameServerModel?> GetByIdAsync(int id);

    Task<GameServerModel?> GetByServerIdAsync(string serverId);

    Task<GameServerModel> CreateAsync(GameServerModel server);

    Task<GameServerModel> UpdateAsync(GameServerModel server);

    Task DeleteAsync(string serverId, bool softDelete = true);
}
