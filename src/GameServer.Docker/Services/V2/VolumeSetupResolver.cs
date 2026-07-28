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
        IReadOnlyDictionary<string, string>? driverOverrides = null);

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
        IReadOnlyDictionary<string, string>? driverOverrides = null);

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
        IReadOnlyDictionary<string, string>? driverOverrides = null)
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
                    index);
            })
            .ToList();
    }

    public IReadOnlyList<GameServerVolume> ResolveForUpdate(
        string serverId,
        string gameTypeKey,
        IReadOnlyList<GameTypeVolume> revisionVolumes,
        IReadOnlyList<GameServerVolume> existingVolumes,
        string layout = "standard",
        IReadOnlyDictionary<string, string>? driverOverrides = null)
    {
        ArgumentNullException.ThrowIfNull(existingVolumes);

        var resolved = ResolveForCreate(serverId, gameTypeKey, revisionVolumes, layout, driverOverrides);
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
                    InitMode = volume.InitMode.ToString().ToLowerInvariant()
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
        int displayOrder)
    {
        var mountType = definition.MountType;
        var isLocalLayout = string.Equals(layout, "local", StringComparison.OrdinalIgnoreCase);

        var source = ResolveSourcePath(config, serverId, gameTypeKey, definition, isLocalLayout);
        var containerPath = ResolveContainerPath(config, definition);
        var driver = config.Driver.NullIfEmpty() ?? "local";
        var driverOptions = ResolveDriverOptions(config, definition, driverOverrides, isLocalLayout);

        return new GameServerVolume
        {
            GameServerId = 0,
            Usage = definition.Usage,
            ContainerPath = containerPath,
            Source = source,
            MountType = mountType,
            ReadOnly = definition.ReadOnly,
            Driver = driver,
            DriverOptionsJson = driverOptions,
            OwnerUid = definition.OwnerUid ?? config.DefaultOwnerUid,
            OwnerGid = definition.OwnerGid ?? config.DefaultOwnerGid,
            Permissions = definition.Permissions ?? config.DefaultPermissions,
            InitMode = config.DefaultInitMode,
            SeedSourcePath = null,
            IsProvisioned = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static string ResolveSourcePath(
        MountTypeConfig config,
        string serverId,
        string gameTypeKey,
        GameTypeVolume definition,
        bool isLocalLayout)
    {
        // tmpfs mounts do not have a host source path.
        if (string.Equals(definition.MountType, "tmpfs", StringComparison.OrdinalIgnoreCase))
        {
            return "tmpfs";
        }

        var template = config.SourcePathTemplate;
        var path = template
            .Replace("{gameTypeKey}", gameTypeKey, StringComparison.OrdinalIgnoreCase)
            .Replace("{serverId}", serverId, StringComparison.OrdinalIgnoreCase)
            .Replace("{Source}", definition.Source, StringComparison.OrdinalIgnoreCase)
            .Replace('\\', '/')
            .Trim('/');

        return path.StartsWith('/') ? path : "/" + path;
    }

    private static string ResolveContainerPath(MountTypeConfig config, GameTypeVolume definition)
    {
        var path = config.ContainerPathTemplate
            .Replace("{Source}", definition.Source, StringComparison.OrdinalIgnoreCase)
            .Replace('\\', '/')
            .Trim('/');

        return path.StartsWith('/') ? path : "/" + path;
    }

    private static string? ResolveDriverOptions(
        MountTypeConfig config,
        GameTypeVolume definition,
        IReadOnlyDictionary<string, string> driverOverrides,
        bool isLocalLayout)
    {
        if (isLocalLayout || string.IsNullOrWhiteSpace(config.DriverOptionsJson))
        {
            return null;
        }

        // Replace tokens before serialization so resolved values are concrete.
        var json = config.DriverOptionsJson
            .Replace("{Source}", definition.Source, StringComparison.OrdinalIgnoreCase);

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
