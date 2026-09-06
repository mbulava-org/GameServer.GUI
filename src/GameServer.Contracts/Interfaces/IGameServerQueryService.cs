using GameServer.API.Dtos.V2;

namespace GameServer.API.Interfaces
{
    public interface IGameServerQueryService
    {
        Task<IReadOnlyList<GameServerListItemDto>> GetListAsync(bool includeDeleted = false, CancellationToken cancellationToken = default);
        Task<GameServerDetailDto?> GetByServerIdAsync(string serverId, CancellationToken cancellationToken = default);
    }
}
