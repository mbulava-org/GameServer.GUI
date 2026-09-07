using GameServer.Web.Models;

namespace GameServer.Web.Services.Auth;

public interface IAuthApiService
{
    Task<(bool Success, string? Error, LoginResponse? Response)> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<UserProfile?> GetCurrentUserAsync(CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserModel>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<UserDetailModel?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error, UserDetailModel? User)> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error, UserDetailModel? User)> UpdateUserAsync(int id, UpdateUserRequest request, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> DeleteUserAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GroupModel>> GetGroupsAsync(CancellationToken cancellationToken = default);
    Task<GroupDetailModel?> GetGroupByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error, GroupDetailModel? Group)> CreateGroupAsync(CreateGroupRequest request, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error, GroupDetailModel? Group)> UpdateGroupAsync(int id, UpdateGroupRequest request, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> DeleteGroupAsync(int id, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error, GroupDetailModel? Group)> SetGroupMembersAsync(int id, IReadOnlyList<int> userIds, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error, GroupDetailModel? Group)> SetGroupServersAsync(int id, IReadOnlyList<ServerGroupAssignmentModel> servers, CancellationToken cancellationToken = default);
}
