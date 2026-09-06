namespace GameServer.API.Dtos.V2;

public sealed record FileItemDto
{
    public required string Name { get; init; }

    public required string Path { get; init; }

    public long Size { get; init; }

    public bool IsDirectory { get; init; }

    public DateTime LastModified { get; init; }

    public string? Extension { get; init; }

    public string? Permissions { get; init; }
}
