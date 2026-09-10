namespace GameServer.API.Dtos.V2;

public record GroupListItemDto(
    int Id,
    string Name,
    string? Description,
    int MemberCount,
    int ServerCount,
    DateTime CreatedAt);

public record GroupDetailDto(
    int Id,
    string Name,
    string? Description,
    IReadOnlyList<GroupMemberDto> Members,
    IReadOnlyList<GroupServerAccessDto> Servers,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record GroupMemberDto(
    int UserId,
    string Username,
    string? Email,
    string Role);

public record GroupServerAccessDto(
    int GameServerId,
    string ServerId,
    string ServerName,
    string AccessLevel); // View, Edit

public record CreateGroupRequestDto(
    string Name,
    string? Description);

public record UpdateGroupRequestDto(
    string Name,
    string? Description);

public record SetGroupMembersRequestDto(
    IReadOnlyList<int> UserIds);

public record SetGroupServersRequestDto(
    IReadOnlyList<ServerGroupAssignmentDto> Servers);

public record ServerGroupAssignmentDto(
    string ServerId,
    string AccessLevel);

/// <summary>
/// A single row in the "Manage group access" dialog shown on the game server details page.
/// One row per group the current user belongs to.
/// </summary>
public record ServerGroupAccessRowDto(
    int GroupId,
    string GroupName,
    string? Description,
    string AccessLevel); // None, View, Edit

public record ServerGroupAccessAssignmentDto(
    int GroupId,
    string AccessLevel); // None, View, Edit

public record SetServerGroupAccessRequestDto(
    IReadOnlyList<ServerGroupAccessAssignmentDto> Groups);
