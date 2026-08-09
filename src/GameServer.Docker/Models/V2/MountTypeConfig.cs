namespace GameServer.Docker.Models.V2;

/// <summary>
/// Defines how Docker mounts of a particular type should be created.
/// Each row is keyed by the mount-type identifier (e.g. volume, bind, tmpfs)
/// and stores the options used when resolving a <see cref="GameTypeVolume"/>
/// into a concrete <see cref="GameServerVolume"/>.
/// </summary>
public sealed record MountTypeConfig
{
    /// <summary>
    /// String key that identifies this mount type. This is the primary key
    /// and is referenced by <see cref="GameTypeVolume.MountType"/>.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? Description { get; init; }

    /// <summary>
    /// Template used to compute the name of the Docker named volume created for this mount type.
    /// Since this becomes the actual volume name, it must resolve to a value that is unique per
    /// server/volume combination. Supports the same tokens as other templated options, such as
    /// {gameTypeKey}, {serverId}, and {Source}.
    /// </summary>
    public string? VolumeNameFormat { get; init; }

    /// <summary>
    /// Free-form key/value options
    /// initialization options, so the shape is intentionally open-ended rather than a fixed set
    /// of columns. Well-known keys used by <see cref="Services.V2.VolumeSetupResolver"/> include:
    /// <c>Driver</c>, <c>DriverOptionsJson</c>, <c>SourcePathTemplate</c>, <c>DefaultReadOnly</c>,
    /// <c>DefaultEnsureNfsPathExists</c>, <c>DefaultOwnerUid</c>, <c>DefaultOwnerGid</c>, and
    /// <c>DefaultPermissions</c>. Values may contain internal tokens such as {Source}, {serverId},
    /// {gameTypeKey}, and {Target} that are substituted by the resolver.
    /// </summary>
    public Dictionary<string, string>? Options { get; init; }

    public bool IsActive { get; init; } = true;

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;

    public string? GetOption(string key) =>
        Options is not null && Options.TryGetValue(key, out var value) ? value : null;
}

