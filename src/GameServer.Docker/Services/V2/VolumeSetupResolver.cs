using GameServer.Docker.Models.V2;
using GameServer.Docker.Repositories.V2;
using Microsoft.Extensions.Logging;

namespace GameServer.Docker.Services.V2;

/// <summary>
/// Resolves GameType volume definitions and mount-type configuration templates into per-server
/// mount snapshots. Existing snapshots are never mutated; only newly introduced container
/// paths are resolved when a server is updated.
/// Note: BuildMountConfigs returns anonymous objects rather than referencing the Agent project
/// directly to avoid an assembly reference cycle.
/// </summary>
public interface IVolumeSetupResolver
{
    /// <summary>
    /// Returns the full set of resolved <see cref="GameServerVolume"/> snapshots for an initial
    /// server create request.
    /// </summary>
    IReadOnlyList<GameServerVolume> ResolveForCreate(
        string serverId,
        string gameTypeKey,
        IReadOnlyList<GameTypeVolume> revisionVolumes,
        string layout = "standard",
        IReadOnlyDictionary<string, string>? driverOverrides = null,
        IReadOnlyDictionary<string, string?>? settingValues = null);

    /// <summary>
    /// Returns only newly introduced <see cref="GameServerVolume"/> snapshots not already
    /// represented by <paramref name="existingVolumes"/>. Existing snapshots are ignored.
    /// </summary>
    IReadOnlyList<GameServerVolume> ResolveForUpdate(
        string serverId,
        string gameTypeKey,
        IReadOnlyList<GameTypeVolume> revisionVolumes,
        IReadOnlyList<GameServerVolume> existingVolumes,
        string layout = "standard",
        IReadOnlyDictionary<string, string>? driverOverrides = null,
        IReadOnlyDictionary<string, string?>? settingValues = null);

    /// <summary>
    /// Transforms resolved snapshots into agent mount requests.
    /// </summary>
    IReadOnlyList<object> BuildMountConfigs(IReadOnlyList<GameServerVolume> volumes);
}

public sealed class VolumeSetupResolver(
    IMountTypeConfigRepository mountTypeConfigRepository,
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

    public IReadOnlyList<GameServerVolume> ResolveForCreate(
        string serverId,
        string gameTypeKey,
        IReadOnlyList<GameTypeVolume> revisionVolumes,
        string layout = "standard",
        IReadOnlyDictionary<string, string>? driverOverrides = null,
        IReadOnlyDictionary<string, string?>? settingValues = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);
        ArgumentException.ThrowIfNullOrWhiteSpace(gameTypeKey);
        ArgumentNullException.ThrowIfNull(revisionVolumes);

        var effectiveLayout = NormalizeLayout(layout);
        var effectiveOverrides = driverOverrides ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return revisionVolumes
            .OrderBy(v => v.DisplayOrder)
            .ThenBy(v => v.Source)
            .Select((definition, index) =>
            {
                var config = GetMountTypeConfigAsync(definition.MountType).GetAwaiter().GetResult();
                return ResolveSingle(
                    config,
                    serverId,
                    gameTypeKey,
                    definition,
                    effectiveLayout,
                    effectiveOverrides,
                    index,
                    settingValues);
            })
            .ToList();
    }

    public IReadOnlyList<GameServerVolume> ResolveForUpdate(
        string serverId,
        string gameTypeKey,
        IReadOnlyList<GameTypeVolume> revisionVolumes,
        IReadOnlyList<GameServerVolume> existingVolumes,
        string layout = "standard",
        IReadOnlyDictionary<string, string>? driverOverrides = null,
        IReadOnlyDictionary<string, string?>? settingValues = null)
    {
        ArgumentNullException.ThrowIfNull(existingVolumes);

        var resolved = ResolveForCreate(serverId, gameTypeKey, revisionVolumes, layout, driverOverrides, settingValues);
        var existingPaths = existingVolumes
            .Select(v => v.ContainerPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return resolved.Where(r => !existingPaths.Contains(r.ContainerPath)).ToList();
    }

    public IReadOnlyList<object> BuildMountConfigs(IReadOnlyList<GameServerVolume> volumes)
    {
        ArgumentNullException.ThrowIfNull(volumes);

        return volumes
            .Select(volume =>
            {
                var mountType = volume.MountType.ToString().ToLowerInvariant();
                var driverName = (string?)null;
                if (string.Equals(mountType, "volume", StringComparison.OrdinalIgnoreCase))
                {
                    // Swarm named-volume driver is separate from mount config options.
                    driverName = volume.Driver;
                }

                return (object)new
                {
                    Type = mountType,
                    Source = volume.Source,
                    Target = volume.ContainerPath,
                    ReadOnly = volume.ReadOnly,
                    DriverName = driverName,
                    VolumeOptions = !string.IsNullOrWhiteSpace(volume.DriverOptionsJson)
                        ? DeserializeDriverOptions(volume.DriverOptionsJson)
                        : null,
                    OwnerUid = volume.OwnerUid,
                    OwnerGid = volume.OwnerGid,
                    Permissions = volume.Permissions,
                    EnsureNfsPathExists = volume.EnsureNfsPathExists
                };
            })
            .ToList();
    }

    private GameServerVolume ResolveSingle(
        MountTypeConfig config,
        string serverId,
        string gameTypeKey,
        GameTypeVolume definition,
        string layout,
        IReadOnlyDictionary<string, string> driverOverrides,
        int displayOrder,
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

        var source = volumeName;
        var driver = config.GetOption("Driver").NullIfEmpty() ?? "local";
        var driverOptions = ResolveDriverOptions(config, sourceToken, volumeName, driverOverrides, isLocalLayout);

        var ownerUid = ResolveOwnerValue(definition.OwnerUidVariable, definition.OwnerUid, settingValues)
            ?? ParseInt(config.GetOption("DefaultOwnerUid"));
        var ownerGid = ResolveOwnerValue(definition.OwnerGidVariable, definition.OwnerGid, settingValues)
            ?? ParseInt(config.GetOption("DefaultOwnerGid"));

        return new GameServerVolume
        {
            GameServerId = 0,
            Usage = definition.Usage,
            ContainerPath = containerPath,
            Source = source,
            VolumeName = volumeName,
            MountType = mountType,
            ReadOnly = definition.ReadOnly,
            Driver = driver,
            DriverOptionsJson = driverOptions,
            OwnerUid = ownerUid,
            OwnerGid = ownerGid,
            Permissions = definition.Permissions ?? config.GetOption("DefaultPermissions"),
            EnsureNfsPathExists = definition.EnsureNfsPathExists,
            IsProvisioned = false,
            CreatedAt = DateTime.UtcNow
        };
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

        var template = config.GetOption("SourcePathTemplate") ?? string.Empty;
        return template
            .Replace("{gameTypeKey}", gameTypeKey, StringComparison.OrdinalIgnoreCase)
            .Replace("{serverId}", serverId, StringComparison.OrdinalIgnoreCase)
            .Replace("{Source}", sourceToken, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveDriverOptions(
        MountTypeConfig config,
        string sourceToken,
        string volumeName,
        IReadOnlyDictionary<string, string> driverOverrides,
        bool isLocalLayout)
    {
        var driverOptionsJson = config.GetOption("DriverOptionsJson");
        if (isLocalLayout || string.IsNullOrWhiteSpace(driverOptionsJson))
        {
            return null;
        }

        // Replace tokens before serialization so resolved values are concrete.
        // {Target} resolves to the calculated SourcePathTemplate value (the volume name).
        var json = driverOptionsJson
            .Replace("{Source}", sourceToken, StringComparison.OrdinalIgnoreCase)
            .Replace("{Target}", volumeName, StringComparison.OrdinalIgnoreCase);

        if (driverOverrides.Count == 0)
        {
            return json;
        }

        Dictionary<string, string>? opts;
        try
        {
            opts = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (System.Text.Json.JsonException)
        {
            return json;
        }

        foreach (var entry in driverOverrides)
        {
            opts[entry.Key] = entry.Value;
        }

        return System.Text.Json.JsonSerializer.Serialize(opts);
    }

    private static string NormalizeLayout(string layout)
    {
        return string.Equals(layout, "local", StringComparison.OrdinalIgnoreCase)
            ? "local"
            : "standard";
    }

    private static Dictionary<string, string>? DeserializeDriverOptions(string json)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        }
        catch (System.Text.Json.JsonException ex)
        {
            // Log and return null; validation should prevent invalid JSON reaching here.
            return null;
        }
    }
}

internal static class StringExtensions
{
    public static string? NullIfEmpty(this string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
