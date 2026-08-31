namespace GameServer.API.Dtos.V2;

/// <summary>
/// Configuration describing how Docker mounts of a particular type are created.
/// </summary>
public sealed record MountTypeConfigDto
{
    public string Key { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? Description { get; init; }

    /// <summary>
    /// Template used to compute the name of the Docker named volume created for this mount type.
    /// Must resolve to a value that is unique per server/volume combination.
    /// </summary>
    public string? VolumeNameFormat { get; init; }

    /// <summary>
    /// Free-form key/value options for this mount type.
    /// <see cref="GameServer.API.Models.V2.MountTypeConfig.Options"/> for well-known keys.
    /// </summary>
    public Dictionary<string, string>? Options { get; init; }

    public bool IsActive { get; init; } = true;

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }
}

