using GameServer.Docker.Models.V2;
using GameServer.Docker.Repositories.V2;
using GameServer.Docker.Services.V2.MountTypeHandlers;
using Microsoft.Extensions.Logging;

namespace GameServer.Docker.Services.V2;

/// <summary>
/// Pairs a persisted <see cref="GameServerVolume"/> snapshot with the transient
/// <see cref="VolumeProvisioningSpec"/> used to provision it. The snapshot is what gets stored;
/// the spec is discarded after the one-time provisioning completes.
/// </summary>
public sealed record VolumeSetupResolution
{
    public required GameServerVolume Snapshot { get; init; }

    public required VolumeProvisioningSpec Provisioning { get; init; }
}

/// <summary>
/// Resolves GameType volume definitions and mount-type configuration templates into per-server
/// mount snapshots. Existing snapshots are never mutated; only newly introduced container
/// paths are resolved when a server is updated. The concrete driver options baked into each
/// snapshot are produced by the mount-type provider for the volume.
/// </summary>
public interface IVolumeSetupResolver
{
    /// <summary>
    /// Returns the full set of resolved snapshots (with provisioning specs) for an initial
    /// server create request.
    /// </summary>
    Task<IReadOnlyList<VolumeSetupResolution>> ResolveForCreateAsync(
        string serverId,
        string gameTypeKey,
        IReadOnlyList<GameTypeVolume> revisionVolumes,
        string layout = "standard",
        IReadOnlyDictionary<string, string>? driverOverrides = null,
        IReadOnlyDictionary<string, string?>? settingValues = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns only newly introduced resolutions not already represented by
    /// <paramref name="existingVolumes"/>. Existing snapshots are ignored.
    /// </summary>
    Task<IReadOnlyList<VolumeSetupResolution>> ResolveForUpdateAsync(
        string serverId,
        string gameTypeKey,
        IReadOnlyList<GameTypeVolume> revisionVolumes,
        IReadOnlyList<GameServerVolume> existingVolumes,
        string layout = "standard",
        IReadOnlyDictionary<string, string>? driverOverrides = null,
        IReadOnlyDictionary<string, string?>? settingValues = null,
        CancellationToken cancellationToken = default);
}

public sealed class VolumeSetupResolver(
    IMountTypeConfigRepository mountTypeConfigRepository,
    IMountTypeHandlerFactory mountTypeHandlerFactory,
    ILogger<VolumeSetupResolver> logger)
    : IVolumeSetupResolver
{
    private readonly Dictionary<string, MountTypeConfig> _cache = new(StringComparer.OrdinalIgnoreCase);

    private async Task<MountTypeConfig> GetMountTypeConfigAsync(string key)
    {
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var config = await mountTypeConfigRepository.GetByKeyAsync(key).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Mount type configuration for '{key}' was not found.");

        _cache[key] = config;
        return config;
    }

    public async Task<IReadOnlyList<VolumeSetupResolution>> ResolveForCreateAsync(
        string serverId,
        string gameTypeKey,
        IReadOnlyList<GameTypeVolume> revisionVolumes,
        string layout = "standard",
        IReadOnlyDictionary<string, string>? driverOverrides = null,
        IReadOnlyDictionary<string, string?>? settingValues = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);
        ArgumentException.ThrowIfNullOrWhiteSpace(gameTypeKey);
        ArgumentNullException.ThrowIfNull(revisionVolumes);

        var normalizedLayout = NormalizeLayout(layout);
        var overrides = driverOverrides ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var resolved = new List<VolumeSetupResolution>(revisionVolumes.Count);
        foreach (var definition in revisionVolumes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var config = await GetMountTypeConfigAsync(definition.MountType).ConfigureAwait(false);
            resolved.Add(ResolveSingle(
                config,
                serverId,
                gameTypeKey,
                definition,
                normalizedLayout,
                overrides,
                settingValues));
        }

        return resolved;
    }

    public async Task<IReadOnlyList<VolumeSetupResolution>> ResolveForUpdateAsync(
        string serverId,
        string gameTypeKey,
        IReadOnlyList<GameTypeVolume> revisionVolumes,
        IReadOnlyList<GameServerVolume> existingVolumes,
        string layout = "standard",
        IReadOnlyDictionary<string, string>? driverOverrides = null,
        IReadOnlyDictionary<string, string?>? settingValues = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(existingVolumes);

        var resolved = await ResolveForCreateAsync(
            serverId,
            gameTypeKey,
            revisionVolumes,
            layout,
            driverOverrides,
            settingValues,
            cancellationToken).ConfigureAwait(false);

        var existingPaths = existingVolumes
            .Select(v => v.ContainerPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return resolved.Where(r => !existingPaths.Contains(r.Snapshot.ContainerPath)).ToList();
    }

    private VolumeSetupResolution ResolveSingle(
        MountTypeConfig config,
        string serverId,
        string gameTypeKey,
        GameTypeVolume definition,
        string layout,
        IReadOnlyDictionary<string, string> driverOverrides,
        IReadOnlyDictionary<string, string?>? settingValues)
    {
        var mountType = definition.MountType;
        var isLocalLayout = string.Equals(layout, "local", StringComparison.OrdinalIgnoreCase);

        // {Source} token: a normalized copy of the container source path. Drop a leading '/',
        // then replace any remaining '/' with '-'.
        var sourceToken = NormalizeSourceToken(definition.Source);

        // The container path must match the container source sample exactly (leading-slash absolute).
        var containerPath = NormalizeContainerPath(definition.Source);

        // The calculated SourcePathTemplate becomes the docker volume name / folder under /data.
        var volumeName = ResolveVolumeName(config, serverId, gameTypeKey, sourceToken, definition);

        var ownerUid = ResolveOwnerValue(definition.OwnerUidVariable, definition.OwnerUid, settingValues)
            ?? ParseInt(config.GetOption("DefaultOwnerUid"));
        var ownerGid = ResolveOwnerValue(definition.OwnerGidVariable, definition.OwnerGid, settingValues)
            ?? ParseInt(config.GetOption("DefaultOwnerGid"));

        var spec = new VolumeProvisioningSpec
        {
            MountType = mountType,
            VolumeName = volumeName,
            ContainerPath = containerPath,
            ReadOnly = definition.ReadOnly,
            SourceToken = sourceToken,
            ServerId = serverId,
            GameTypeKey = gameTypeKey,
            Config = config,
            IsLocalLayout = isLocalLayout,
            DriverOverrides = driverOverrides,
            OwnerUid = ownerUid,
            OwnerGid = ownerGid,
            Permissions = definition.Permissions ?? config.GetOption("DefaultPermissions"),
            EnsureNfsPathExists = definition.EnsureNfsPathExists
        };

        // Each mount-type provider finalizes the concrete driver options baked into the snapshot.
        var handler = mountTypeHandlerFactory.GetHandler(mountType);
        var driverOptions = handler.BuildDriverOptions(spec);

        var snapshot = new GameServerVolume
        {
            GameServerId = 0,
            Usage = definition.Usage,
            ContainerPath = containerPath,
            VolumeName = volumeName,
            MountType = mountType,
            ReadOnly = definition.ReadOnly,
            DriverOptionsJson = driverOptions,
            IsProvisioned = false,
            CreatedAt = DateTime.UtcNow
        };

        return new VolumeSetupResolution { Snapshot = snapshot, Provisioning = spec };
    }

    private static int? ParseInt(string? value) =>
        int.TryParse(value, out var parsed) ? parsed : null;

    private int? ResolveOwnerValue(
        string? variableKey,
        int? explicitValue,
        IReadOnlyDictionary<string, string?>? settingValues)
    {
        if (string.IsNullOrWhiteSpace(variableKey))
        {
            return explicitValue;
        }

        if (settingValues is not null
            && settingValues.TryGetValue(variableKey, out var rawValue)
            && int.TryParse(rawValue, out var parsed))
        {
            return parsed;
        }

        logger.LogWarning(
            "Volume owner variable '{Variable}' could not be resolved to a numeric value; falling back to explicit value.",
            variableKey);

        return explicitValue;
    }

    /// <summary>
    /// Normalizes the container source path into the {Source} token value: drop a leading '/'
    /// then replace any remaining '/' with '-'.
    /// </summary>
    private static string NormalizeSourceToken(string containerSource)
    {
        var value = (containerSource ?? string.Empty).Replace('\\', '/');
        if (value.StartsWith('/'))
        {
            value = value[1..];
        }

        return value.Replace('/', '-');
    }

    /// <summary>
    /// Produces the concrete container path, matching the container source sample exactly but
    /// normalized to a leading-slash absolute path.
    /// </summary>
    private static string NormalizeContainerPath(string containerSource)
    {
        var value = (containerSource ?? string.Empty).Replace('\\', '/').Trim('/');
        return value.Length == 0 ? "/" : "/" + value;
    }

    private static string ResolveVolumeName(
        MountTypeConfig config,
        string serverId,
        string gameTypeKey,
        string sourceToken,
        GameTypeVolume definition)
    {
        // tmpfs mounts do not have a host source/volume.
        if (string.Equals(definition.MountType, "tmpfs", StringComparison.OrdinalIgnoreCase))
        {
            return "tmpfs";
        }

        // Prefer the first-class VolumeNameFormat; fall back to the legacy SourcePathTemplate option.
        var template = config.VolumeNameFormat.NullIfEmpty()
            ?? config.GetOption("SourcePathTemplate")
            ?? string.Empty;
        return template
            .Replace("{gameTypeKey}", gameTypeKey, StringComparison.OrdinalIgnoreCase)
            .Replace("{serverId}", serverId, StringComparison.OrdinalIgnoreCase)
            .Replace("{Source}", sourceToken, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeLayout(string layout)
    {
        return string.Equals(layout, "local", StringComparison.OrdinalIgnoreCase)
            ? "local"
            : "standard";
    }
}

internal static class StringExtensions
{
    public static string? NullIfEmpty(this string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
