using System.Diagnostics;
using System.Globalization;
using Docker.DotNet.Models;
using GameServer.Docker.Models.V2;
using Microsoft.Extensions.Logging;

namespace GameServer.Docker.Services.V2.MountTypeHandlers;

/// <summary>
/// Handles NFS-backed volumes. All configuration (device path and mount options) is calculated
/// from the MountType's options into the <see cref="GameServerVolume.DriverOptionsJson"/> snapshot.
/// One-time provisioning (directory create + ownership/permissions) is performed from the
/// transient <see cref="VolumeProvisioningSpec"/>, which carries the API-local path the primary
/// API host has mapped to the NFS root.
/// </summary>
public sealed class NfsMountTypeHandler(
    ILogger<NfsMountTypeHandler> logger)
    : IMountTypeHandler
{
    private const string NfsDriverName = "local";

    public string MountTypeKey => "nfs";

    public string? BuildDriverOptions(VolumeProvisioningSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var config = spec.Config;
        var nfsOptions = config.GetOption("NfsOptions") ?? string.Empty;
        var nfsRoot = TrimTrailingSlash(config.GetOption("NfsRoot") ?? string.Empty);
        var devicePathFormat = config.GetOption("DevicePathFormat") ?? spec.SourceToken;

        var devicePath = SubstituteTokens(devicePathFormat, spec.ServerId, spec.GameTypeKey, spec.SourceToken)
            .Trim('/');

        // Docker "local" driver NFS device: ":" prefix + server-side path under NfsRoot.
        var device = ":" + CombinePath(nfsRoot, devicePath);

        return System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["type"] = "nfs",
            ["o"] = nfsOptions,
            ["device"] = device
        });
    }

    public async Task PrepareAsync(VolumeProvisioningSpec spec, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);

        if (!spec.EnsureNfsPathExists)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var targetPath = ResolveLocalProvisioningPath(spec);
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            logger.LogWarning(
                "NFS volume {VolumeName} requested path provisioning but has no resolved local path; skipping.",
                spec.VolumeName);
            return;
        }

        try
        {
            if (!Directory.Exists(targetPath))
            {
                Directory.CreateDirectory(targetPath);
                logger.LogInformation("Created NFS target directory: {Path}", targetPath);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to create NFS target directory: {Path}", targetPath);
            return;
        }

        await ApplyOwnershipAndPermissionsAsync(targetPath, spec).ConfigureAwait(false);
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
                        Name = NfsDriverName,
                        Options = driverOptions
                    }
                };
            }
        }

        return mount;
    }

    /// <summary>
    /// Resolves the API-local provisioning path from the MountType's <c>LocalPath</c> option
    /// (which the primary API host has mapped to the NFS root) combined with the resolved
    /// device sub-path. Null when no host-side provisioning path is configured.
    /// </summary>
    private static string? ResolveLocalProvisioningPath(VolumeProvisioningSpec spec)
    {
        var config = spec.Config;
        var localRoot = TrimTrailingSlash(config.GetOption("LocalPath") ?? string.Empty);
        if (string.IsNullOrEmpty(localRoot))
        {
            return null;
        }

        var devicePathFormat = config.GetOption("DevicePathFormat") ?? spec.SourceToken;
        var devicePath = SubstituteTokens(devicePathFormat, spec.ServerId, spec.GameTypeKey, spec.SourceToken)
            .Trim('/');

        return CombinePath(localRoot, devicePath);
    }

    private async Task ApplyOwnershipAndPermissionsAsync(string path, VolumeProvisioningSpec spec)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(spec.Permissions)
                && TryParseOctal(spec.Permissions, out var mode))
            {
                if (OperatingSystem.IsLinux())
                {
                    await ChmodAsync(path, mode).ConfigureAwait(false);
                }
                else
                {
                    logger.LogDebug("Skipping chmod on non-Linux host for {Path}", path);
                }
            }

            if ((spec.OwnerUid.HasValue || spec.OwnerGid.HasValue) && OperatingSystem.IsLinux())
            {
                await ChownAsync(path, spec.OwnerUid, spec.OwnerGid).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to apply ownership/permissions to {Path}", path);
        }
    }

    private static string SubstituteTokens(string template, string serverId, string gameTypeKey, string sourceToken) =>
        template
            .Replace("{gameTypeKey}", gameTypeKey, StringComparison.OrdinalIgnoreCase)
            .Replace("{serverId}", serverId, StringComparison.OrdinalIgnoreCase)
            .Replace("{Source}", sourceToken, StringComparison.OrdinalIgnoreCase);

    private static string TrimTrailingSlash(string value) =>
        value.Replace('\\', '/').TrimEnd('/');

    private static string CombinePath(string root, string relative)
    {
        if (string.IsNullOrEmpty(root))
        {
            return relative;
        }

        return string.IsNullOrEmpty(relative) ? root : $"{root}/{relative}";
    }

    private static bool TryParseOctal(string text, out int value)
    {
        value = 0;
        foreach (var c in text)
        {
            if (c < '0' || c > '7')
            {
                value = 0;
                return false;
            }

            value = (value << 3) | (c - '0');
        }

        return true;
    }

    private static async Task ChmodAsync(string path, int mode)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "chmod",
            Arguments = $"{Convert.ToString(mode, 8)} {path}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start chmod process.");
        await process.WaitForExitAsync().ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
            throw new InvalidOperationException($"chmod failed: {error}");
        }
    }

    private static async Task ChownAsync(string path, int? uid, int? gid)
    {
        var owner = uid.HasValue ? uid.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
        if (gid.HasValue)
        {
            owner += $":{gid.Value.ToString(CultureInfo.InvariantCulture)}";
        }

        if (string.IsNullOrEmpty(owner))
        {
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "chown",
            Arguments = $"{owner} {path}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start chown process.");
        await process.WaitForExitAsync().ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
            throw new InvalidOperationException($"chown failed: {error}");
        }
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
