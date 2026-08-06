using Docker.DotNet.Models;
using GameServer.Docker.Models.V2;
using Microsoft.Extensions.Logging;

namespace GameServer.Docker.Services.V2.MountTypeHandlers;

/// <summary>
/// Handles local Docker named volumes. Docker creates the named volume on demand when the
/// service task starts, so no host-side provisioning is required. The concrete driver options
/// are calculated from the MountType's options into the snapshot's
/// <see cref="GameServerVolume.DriverOptionsJson"/>.
/// </summary>
public sealed class VolumeMountTypeHandler(
    ILogger<VolumeMountTypeHandler> logger)
    : IMountTypeHandler
{
    private const string DefaultDriverName = "local";

    public string MountTypeKey => "volume";

    public string? BuildDriverOptions(VolumeProvisioningSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var config = spec.Config;
        var driverOptionsJson = config.GetOption("DriverOptionsJson");
        if (spec.IsLocalLayout || string.IsNullOrWhiteSpace(driverOptionsJson))
        {
            return null;
        }

        // Replace tokens before serialization so resolved values are concrete.
        // {Target} resolves to the calculated SourcePathTemplate value (the volume name).
        var json = driverOptionsJson
            .Replace("{Source}", spec.SourceToken, StringComparison.OrdinalIgnoreCase)
            .Replace("{Target}", spec.VolumeName, StringComparison.OrdinalIgnoreCase);

        if (spec.DriverOverrides.Count == 0)
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

        foreach (var entry in spec.DriverOverrides)
        {
            opts[entry.Key] = entry.Value;
        }

        return System.Text.Json.JsonSerializer.Serialize(opts);
    }

    public Task PrepareAsync(VolumeProvisioningSpec spec, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);

        logger.LogDebug(
            "No pre-create provisioning required for named volume {VolumeName} ({ContainerPath}).",
            spec.VolumeName,
            spec.ContainerPath);

        return Task.CompletedTask;
    }

    public Mount BuildMount(GameServerVolume volume)
    {
        ArgumentNullException.ThrowIfNull(volume);

        var mount = new Mount
        {
            Type = MountTypeKey,
            Source = volume.VolumeName,
            Target = volume.ContainerPath,
            ReadOnly = volume.ReadOnly
        };

        if (!string.IsNullOrWhiteSpace(volume.DriverOptionsJson))
        {
            var driverOptions = DeserializeDriverOptions(volume.DriverOptionsJson);
            if (driverOptions is not null)
            {
                mount.VolumeOptions = new VolumeOptions
                {
                    DriverConfig = new Driver
                    {
                        Name = DefaultDriverName,
                        Options = driverOptions
                    }
                };
            }
            else
            {
                logger.LogWarning(
                    "Failed to deserialize driver options for volume {ContainerPath}; creating mount without volume options.",
                    volume.ContainerPath);
            }
        }

        return mount;
    }

    private static Dictionary<string, string>? DeserializeDriverOptions(string json)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
