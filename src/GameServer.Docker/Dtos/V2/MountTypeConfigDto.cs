namespace GameServer.Docker.Dtos.V2;

/// <summary>
/// Configuration describing how Docker mounts of a particular type are created.
/// </summary>
public sealed record MountTypeConfigDto
{
    public string Key { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? Description { get; init; }

    /// <summary>
    /// Free-form key/value options for this mount type. See
    /// <see cref="GameServer.Docker.Models.V2.MountTypeConfig.Options"/> for well-known keys.
    /// </summary>
    public Dictionary<string, string>? Options { get; init; }

    public bool IsActive { get; init; } = true;

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }
}

