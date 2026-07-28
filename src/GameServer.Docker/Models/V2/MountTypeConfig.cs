namespace GameServer.Docker.Models.V2;

/// <summary>
/// Defines how Docker mounts of a particular type should be created.
/// Each row is keyed by the mount-type identifier (e.g. volume, bind, tmpfs)
/// and stores the templates and defaults used when resolving a <see cref="GameTypeVolume"/>
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
    /// Default Docker driver to use for mounts of this type (e.g. local, vieux-sshfs, rexray/ebs).
    /// </summary>
    public string Driver { get; init; } = string.Empty;

    /// <summary>
    /// JSON-serialized dictionary of default driver options for this mount type.
    /// Tokens such as {Source}, {serverId}, and {gameTypeKey} may be replaced
    /// by the resolver before the volume is created.
    /// </summary>
    public string? DriverOptionsJson { get; init; }

    /// <summary>
    /// Template for resolving the host/source path. Tokens: {Source}, {serverId}, {gameTypeKey}.
    /// </summary>
    public string SourcePathTemplate { get; init; } = string.Empty;

    /// <summary>
    /// Template for the container target path. Tokens: {Source}.
    /// </summary>
    public string ContainerPathTemplate { get; init; } = "{Source}";

    public bool DefaultReadOnly { get; init; }

    public VolumeInitMode DefaultInitMode { get; init; } = VolumeInitMode.None;

    public int? DefaultOwnerUid { get; init; }

    public int? DefaultOwnerGid { get; init; }

    public string? DefaultPermissions { get; init; }

    public bool IsActive { get; init; } = true;

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
}
