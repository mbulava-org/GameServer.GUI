using GameServer.API.Data.V2;
using GameServer.API.Dtos.V2;

namespace GameServer.API.Repositories.V2;

public interface IGroupRepository
{
    Task<GroupEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<GroupEntity?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GroupEntity>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<GroupEntity> CreateAsync(GroupEntity group, CancellationToken cancellationToken = default);
    Task<GroupEntity?> UpdateAsync(int id, string name, string? description, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task SetMembersAsync(int groupId, IEnumerable<int> userIds, CancellationToken cancellationToken = default);
    Task SetServerAccessAsync(int groupId, IEnumerable<ServerGroupAssignmentDto> servers, CancellationToken cancellationToken = default);
    Task AddOrUpdateServerAccessAsync(int groupId, string serverId, string accessLevel, CancellationToken cancellationToken = default);
    Task RemoveServerAccessAsync(int groupId, string serverId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetUserGroupIdsAsync(int userId, CancellationToken cancellationToken = default);
}
