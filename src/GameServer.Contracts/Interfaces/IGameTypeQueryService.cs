using GameServer.API.Dtos.V2;

namespace GameServer.API.Interfaces
{
    public interface IGameTypeQueryService
    {
        Task<IReadOnlyList<GameTypeListItemDto>> GetListAsync(bool includeInactive, CancellationToken cancellationToken = default);
        Task<GameTypeDetailDto?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
        Task<PortableGameTypePackageDto?> ExportAsync(string key, CancellationToken cancellationToken = default);
    }
}
