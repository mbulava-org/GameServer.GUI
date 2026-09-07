namespace GameServer.API.Dtos.V2;

public record LoginRequestDto(string Username, string Password);

public record LoginResponseDto(
    string Token,
    int UserId,
    string Username,
    string? Email,
    string Role,
    IReadOnlyList<string> Groups,
    DateTime ExpiresAtUtc);

public record UserProfileDto(
    int Id,
    string Username,
    string? Email,
    string Role,
    IReadOnlyList<string> Groups,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

public record ChangePasswordRequestDto(
    string CurrentPassword,
    string NewPassword);
