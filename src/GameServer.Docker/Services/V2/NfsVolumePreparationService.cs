using System.Diagnostics;
using System.Globalization;
using GameServer.Docker.Configurations;
using GameServer.Docker.Models.V2;
using Microsoft.Extensions.Options;

namespace GameServer.Docker.Services.V2;

/// <summary>
/// Prepares NFS-backed volume target folders on the primary API host before the Swarm service
/// is created or updated. The API container maps <c>BaseDataPath</c> (default <c>/data</c>) to the
/// folder above the full path of the NFS share, so the resolved target folder can be created and
/// have ownership/permissions applied here rather than on the remote agent.
/// </summary>
public interface INfsVolumePreparationService
{
    Task PrepareAsync(IReadOnlyList<GameServerVolume> volumes, CancellationToken cancellationToken = default);
}

public sealed class NfsVolumePreparationService(
    IOptions<NfsPreparationOptions> options,
    ILogger<NfsVolumePreparationService> logger)
    : INfsVolumePreparationService
{
    private readonly NfsPreparationOptions _options = options.Value;

    public async Task PrepareAsync(IReadOnlyList<GameServerVolume> volumes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(volumes);

        foreach (var volume in volumes.Where(v => v.EnsureNfsPathExists))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var targetPath = BuildTargetPath(volume);
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                continue;
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
                continue;
            }

            await ApplyOwnershipAndPermissionsAsync(targetPath, volume).ConfigureAwait(false);
        }
    }

    private string BuildTargetPath(GameServerVolume volume)
    {
        // The calculated volume name is the folder created under BaseDataPath (default /data).
        // The API container maps BaseDataPath to the folder above the NFS export root, so the
        // target folder can be created and have ownership/permissions applied here.
        var name = (volume.VolumeName ?? string.Empty)
            .Replace('\\', '/')
            .Trim('/');

        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var basePath = _options.BaseDataPath.Replace('\\', '/').TrimEnd('/');
        return $"{basePath}/{name}";
    }

    private async Task ApplyOwnershipAndPermissionsAsync(string path, GameServerVolume volume)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(volume.Permissions)
                && TryParseOctal(volume.Permissions, out var mode))
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

            if ((volume.OwnerUid.HasValue || volume.OwnerGid.HasValue) && OperatingSystem.IsLinux())
            {
                await ChownAsync(path, volume.OwnerUid, volume.OwnerGid).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to apply ownership/permissions to {Path}", path);
        }
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

    private async Task ChmodAsync(string path, int mode)
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

    private async Task ChownAsync(string path, int? uid, int? gid)
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
}
