using GameServer.API.Data.V2;

namespace GameServer.API.Repositories.V2;

public interface IUserRepository
{
    Task<UserEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<UserEntity?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserEntity>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<UserEntity> CreateAsync(UserEntity user, IEnumerable<int>? groupIds = null, CancellationToken cancellationToken = default);
    Task<UserEntity?> UpdateAsync(int id, Action<UserEntity> updateAction, IEnumerable<int>? groupIds = null, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task UpdateLastLoginAsync(int id, CancellationToken cancellationToken = default);
}
