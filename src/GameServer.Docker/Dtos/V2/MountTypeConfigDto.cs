namespace GameServer.Docker.Dtos.V2;

/// <summary>
/// Configuration describing how Docker mounts of a particular type are created.
/// </summary>
public sealed record MountTypeConfigDto
{
    public string Key { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string Driver { get; init; } = string.Empty;

    /// <summary>
    /// JSON-serialized dictionary of default driver options. Tokens such as
    /// {Source}, {serverId}, and {gameTypeKey} may be replaced by the resolver.
    /// </summary>
    public string? DriverOptionsJson { get; init; }

    /// <summary>
    /// Template for the host/source path. Tokens: {Source}, {serverId}, {gameTypeKey}.
    /// </summary>
    public string SourcePathTemplate { get; init; } = string.Empty;

    /// <summary>
    /// Template for the container target path. Tokens: {Source}.
    /// </summary>
    public string ContainerPathTemplate { get; init; } = "{Source}";

    public bool DefaultReadOnly { get; init; }

    public string DefaultInitMode { get; init; } = "none";

    public int? DefaultOwnerUid { get; init; }

    public int? DefaultOwnerGid { get; init; }

    public string? DefaultPermissions { get; init; }

    public bool IsActive { get; init; } = true;

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }
}
