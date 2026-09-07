namespace GameServer.API.Dtos.V2;

public record UserListItemDto(
    int Id,
    string Username,
    string? Email,
    string Role,
    bool IsActive,
    IReadOnlyList<string> Groups,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

public record UserDetailDto(
    int Id,
    string Username,
    string? Email,
    string Role,
    bool IsActive,
    IReadOnlyList<UserGroupMembershipDto> Groups,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? LastLoginAt);

public record UserGroupMembershipDto(int GroupId, string GroupName);

public record CreateUserRequestDto(
    string Username,
    string? Email,
    string Password,
    string Role,
    IReadOnlyList<int>? GroupIds);

public record UpdateUserRequestDto(
    string? Email,
    string? Role,
    bool? IsActive,
    string? NewPassword,
    IReadOnlyList<int>? GroupIds);
