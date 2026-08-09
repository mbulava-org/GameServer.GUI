using Docker.DotNet.Models;
using GameServer.Docker.Models.V2;

namespace GameServer.Docker.Services.V2.MountTypeHandlers;

/// <summary>
/// Transient, non-persisted provisioning input for a single volume. Carries everything a
/// mount-type provider needs to (a) finalize the concrete <c>DriverOptionsJson</c> baked into
/// the persisted <see cref="GameServerVolume"/> snapshot and (b) perform the one-time host-side
/// provisioning (directory creation, ownership, permissions). None of this is stored on the
/// <see cref="GameServerVolume"/>; provisioning is a one-time operation.
/// </summary>
public sealed record VolumeProvisioningSpec
{
    /// <summary>Mount-type code (matches <see cref="MountTypeConfig.Key"/>).</summary>
    public string MountType { get; init; } = "volume";

    /// <summary>Calculated docker volume name (from the mount type's SourcePathTemplate).</summary>
    public string VolumeName { get; init; } = string.Empty;

    /// <summary>Absolute container mount target path.</summary>
    public string ContainerPath { get; init; } = string.Empty;

    /// <summary>Whether the mount is read-only.</summary>
    public bool ReadOnly { get; init; }

    /// <summary>Normalized {Source} token derived from the container source path.</summary>
    public string SourceToken { get; init; } = string.Empty;

    /// <summary>The owning server id (used for token substitution).</summary>
    public string ServerId { get; init; } = string.Empty;

    /// <summary>The game type key (used for token substitution).</summary>
    public string GameTypeKey { get; init; } = string.Empty;

    /// <summary>Mount-type configuration options (source of NfsOptions, NfsRoot, etc.).</summary>
    public MountTypeConfig Config { get; init; } = new();

    /// <summary>Whether the calling deployment is using the local binding layout.</summary>
    public bool IsLocalLayout { get; init; }

    /// <summary>Optional driver option overrides supplied by the caller.</summary>
    public IReadOnlyDictionary<string, string> DriverOverrides { get; init; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Resolved owner UID for host-side provisioning (nfs).</summary>
    public int? OwnerUid { get; init; }

    /// <summary>Resolved owner GID for host-side provisioning (nfs).</summary>
    public int? OwnerGid { get; init; }

    /// <summary>Resolved octal permissions for host-side provisioning (nfs).</summary>
    public string? Permissions { get; init; }

    /// <summary>When true, the target directory is ensured to exist before deploy (nfs).</summary>
    public bool EnsureNfsPathExists { get; init; }
}

/// <summary>
/// Handles a single mount type (matching <see cref="MountTypeConfig.Key"/>) end-to-end:
/// finalizing the concrete driver options baked into the persisted snapshot, performing any
/// one-time host-side provisioning, and translating the persisted <see cref="GameServerVolume"/>
/// snapshot into a Docker <see cref="Mount"/>. Implementations are resolved by
/// <see cref="IMountTypeHandlerFactory"/> using <see cref="MountTypeKey"/>.
/// </summary>
public interface IMountTypeHandler
{
    /// <summary>
    /// Mount-type code this handler is responsible for (e.g. <c>volume</c>, <c>nfs</c>).
    /// Matched case-insensitively against <see cref="GameServerVolume.MountType"/>.
    /// </summary>
    string MountTypeKey { get; }

    /// <summary>
    /// Calculates the concrete <c>DriverOptionsJson</c> to persist on the snapshot from the
    /// mount-type options in <paramref name="spec"/>. Returns null when the mount type needs no
    /// driver options.
    /// </summary>
    string? BuildDriverOptions(VolumeProvisioningSpec spec);

    /// <summary>
    /// Performs any one-time provisioning required before the volume can be attached to a
    /// service (for example, ensuring an NFS target directory exists with the correct ownership
    /// and permissions). Implementations that require no provisioning should complete without work.
    /// </summary>
    Task PrepareAsync(VolumeProvisioningSpec spec, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the Docker <see cref="Mount"/> for the persisted <paramref name="volume"/> snapshot,
    /// deriving the driver name and source from <see cref="GameServerVolume.VolumeName"/>,
    /// <see cref="GameServerVolume.MountType"/>, and <see cref="GameServerVolume.DriverOptionsJson"/>.
    /// </summary>
    Mount BuildMount(GameServerVolume volume);
}
