namespace GameServer.Web.Models;

public record LoginRequest(string Username, string Password);

public record LoginResponse(
    string Token,
    int UserId,
    string Username,
    string? Email,
    string Role,
    IReadOnlyList<string> Groups,
    DateTime ExpiresAtUtc);

public record UserProfile(
    int Id,
    string Username,
    string? Email,
    string Role,
    IReadOnlyList<string> Groups,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);

public record UserModel(
    int Id,
    string Username,
    string? Email,
    string Role,
    bool IsActive,
    IReadOnlyList<string> Groups,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

public record UserDetailModel(
    int Id,
    string Username,
    string? Email,
    string Role,
    bool IsActive,
    IReadOnlyList<UserGroupMembershipModel> Groups,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? LastLoginAt);

public record UserGroupMembershipModel(int GroupId, string GroupName);

public record CreateUserRequest(
    string Username,
    string? Email,
    string Password,
    string Role,
    IReadOnlyList<int>? GroupIds);

public record UpdateUserRequest(
    string? Email,
    string? Role,
    bool? IsActive,
    string? NewPassword,
    IReadOnlyList<int>? GroupIds);

public record GroupModel(
    int Id,
    string Name,
    string? Description,
    int MemberCount,
    int ServerCount,
    DateTime CreatedAt);

public record GroupDetailModel(
    int Id,
    string Name,
    string? Description,
    IReadOnlyList<GroupMemberModel> Members,
    IReadOnlyList<GroupServerAccessModel> Servers,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record GroupMemberModel(
    int UserId,
    string Username,
    string? Email,
    string Role);

public record GroupServerAccessModel(
    int GameServerId,
    string ServerId,
    string ServerName,
    string AccessLevel);

public record CreateGroupRequest(
    string Name,
    string? Description);

public record UpdateGroupRequest(
    string Name,
    string? Description);

public record SetGroupMembersRequest(
    IReadOnlyList<int> UserIds);

public record SetGroupServersRequest(
    IReadOnlyList<ServerGroupAssignmentModel> Servers);

public record ServerGroupAssignmentModel(
    string ServerId,
    string AccessLevel);
