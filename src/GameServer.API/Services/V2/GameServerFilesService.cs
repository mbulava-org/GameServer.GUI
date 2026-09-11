using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using GameServer.API.Dtos.V2;
using GameServer.API.Interfaces;
using GameServer.API.Models;
using GameServer.API.Models.V2;
using GameServer.API.Repositories.V2;
using Microsoft.Extensions.Logging;

namespace GameServer.API.Services.V2;

public sealed class GameServerFilesService(
    IGameServerRepository gameServerRepository,
    IMountTypeConfigRepository mountTypeConfigRepository,
    INodeAgentDiscovery nodeAgentDiscovery,
    IHttpClientFactory httpClientFactory,
    ILogger<GameServerFilesService> logger,
    IServerResourceMonitor? serverResourceMonitor = null,
    IGameTypeRepository? gameTypeRepository = null)
    : IGameServerFilesService
{
    public async Task<IReadOnlyList<FileItemDto>> ListFilesAsync(
        string serverId,
        string volumeContainerPath,
        string? subPath = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (agent, containerId) = await ResolveAgentAndContainerAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (agent != null && !string.IsNullOrWhiteSpace(containerId))
        {
            var targetPath = CombineContainerPath(volumeContainerPath, subPath);
            var client = httpClientFactory.CreateClient();
            var url = $"{agent.InternalUrl.TrimEnd('/')}/containers/{containerId}/files?path={Uri.EscapeDataString(targetPath)}";

            var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var files = await response.Content.ReadFromJsonAsync<List<FileItemDto>>(cancellationToken: cancellationToken).ConfigureAwait(false);
                if (files is not null && files.Count > 0)
                {
                    var normalizedVolume = (volumeContainerPath ?? "/").Replace('\\', '/').TrimEnd('/');
                    return files.Select(f =>
                    {
                        var p = (f.Path ?? string.Empty).Replace('\\', '/');
                        if (!string.IsNullOrEmpty(normalizedVolume) && p.StartsWith(normalizedVolume + "/", StringComparison.OrdinalIgnoreCase))
                        {
                            p = p[(normalizedVolume.Length + 1)..];
                        }
                        else if (!string.IsNullOrEmpty(normalizedVolume) && p.StartsWith(normalizedVolume, StringComparison.OrdinalIgnoreCase))
                        {
                            p = p[normalizedVolume.Length..].TrimStart('/');
                        }
                        else
                        {
                            p = p.TrimStart('/');
                        }

                        return f with { Path = p };
                    }).ToList();
                }

                return [];
            }

            logger.LogWarning("Agent {AgentUrl} returned status {StatusCode} when listing files for container {ContainerId} at {Path}", agent.InternalUrl, response.StatusCode, containerId, targetPath);
        }

        // Fallback to local NFS storage path if container is not running
        var (localPath, _) = await TryResolveLocalNfsPathAsync(serverId, volumeContainerPath, subPath, cancellationToken);
        if (!string.IsNullOrWhiteSpace(localPath) && Directory.Exists(localPath))
        {
            return ListFilesFromLocalDisk(localPath, volumeContainerPath, subPath);
        }

        logger.LogDebug("No active container or local NFS directory found for server {ServerId} at volume {VolumePath}", serverId, volumeContainerPath);
        return [];
    }

    private static IReadOnlyList<FileItemDto> ListFilesFromLocalDisk(string localDirectoryPath, string volumeContainerPath, string? subPath)
    {
        var dirInfo = new DirectoryInfo(localDirectoryPath);
        if (!dirInfo.Exists)
        {
            return [];
        }

        var results = new List<FileItemDto>();
        var cleanSub = string.IsNullOrWhiteSpace(subPath) ? string.Empty : subPath.Replace('\\', '/').Trim('/');

        foreach (var dir in dirInfo.GetDirectories())
        {
            var itemRelativePath = string.IsNullOrEmpty(cleanSub) ? dir.Name : $"{cleanSub}/{dir.Name}";
            results.Add(new FileItemDto
            {
                Name = dir.Name,
                Path = itemRelativePath,
                IsDirectory = true,
                Size = 0,
                LastModified = dir.LastWriteTimeUtc,
                Extension = null,
                Permissions = "drwxr-xr-x"
            });
        }

        foreach (var file in dirInfo.GetFiles())
        {
            var itemRelativePath = string.IsNullOrEmpty(cleanSub) ? file.Name : $"{cleanSub}/{file.Name}";
            results.Add(new FileItemDto
            {
                Name = file.Name,
                Path = itemRelativePath,
                IsDirectory = false,
                Size = file.Length,
                LastModified = file.LastWriteTimeUtc,
                Extension = file.Extension,
                Permissions = "-rw-r--r--"
            });
        }

        return results.OrderByDescending(f => f.IsDirectory).ThenBy(f => f.Name).ToList();
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> GetFileStreamAsync(
        string serverId,
        string volumeContainerPath,
        string subPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (agent, containerId) = await ResolveAgentAndContainerAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (agent != null && !string.IsNullOrWhiteSpace(containerId))
        {
            var targetPath = CombineContainerPath(volumeContainerPath, subPath);
            var client = httpClientFactory.CreateClient();
            var url = $"{agent.InternalUrl.TrimEnd('/')}/containers/{containerId}/files/download?path={Uri.EscapeDataString(targetPath)}";

            var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                    ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                    ?? Path.GetFileName(targetPath);

                return (stream, contentType, fileName);
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new FileNotFoundException($"File not found in container at: {targetPath}");
            }
        }

        // Fallback to local NFS storage path
        var (localPath, _) = await TryResolveLocalNfsPathAsync(serverId, volumeContainerPath, subPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(localPath))
        {
            throw new InvalidOperationException($"Game server '{serverId}' is not currently running and local volume path could not be resolved.");
        }

        if (File.Exists(localPath))
        {
            var stream = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var fileName = Path.GetFileName(localPath);
            return (stream, "application/octet-stream", fileName);
        }

        throw new FileNotFoundException($"File not found at volume path: {subPath}");
    }

    public async Task<string> GetFileContentTextAsync(
        string serverId,
        string volumeContainerPath,
        string subPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (agent, containerId) = await ResolveAgentAndContainerAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (agent != null && !string.IsNullOrWhiteSpace(containerId))
        {
            var targetPath = CombineContainerPath(volumeContainerPath, subPath);
            var client = httpClientFactory.CreateClient();
            var url = $"{agent.InternalUrl.TrimEnd('/')}/containers/{containerId}/files/content?path={Uri.EscapeDataString(targetPath)}";

            var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new FileNotFoundException($"File not found in container at: {targetPath}");
            }
        }

        // Fallback to local NFS storage path
        var (resolvedPath, _) = await TryResolveLocalNfsPathAsync(serverId, volumeContainerPath, subPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(resolvedPath))
        {
            throw new InvalidOperationException($"Game server '{serverId}' is not currently running and local volume path could not be resolved.");
        }

        if (File.Exists(resolvedPath))
        {
            return await File.ReadAllTextAsync(resolvedPath, cancellationToken).ConfigureAwait(false);
        }

        throw new FileNotFoundException($"File not found at volume path: {subPath}");
    }

    public async Task SaveFileContentTextAsync(
        string serverId,
        string volumeContainerPath,
        string subPath,
        string content,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (agent, containerId) = await ResolveAgentAndContainerAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (agent != null && !string.IsNullOrWhiteSpace(containerId))
        {
            var targetPath = CombineContainerPath(volumeContainerPath, subPath);
            var client = httpClientFactory.CreateClient();
            var url = $"{agent.InternalUrl.TrimEnd('/')}/containers/{containerId}/files/content?path={Uri.EscapeDataString(targetPath)}";

            var response = await client.PutAsJsonAsync(url, new { Content = content }, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Saved text file via agent to container {ContainerId} at {Path}", containerId, targetPath);
                return;
            }

            throw new HttpRequestException($"Agent returned status code {response.StatusCode} when saving {targetPath}");
        }

        // Fallback to local NFS storage path
        var (localPath, mountConfig) = await TryResolveLocalNfsPathAsync(serverId, volumeContainerPath, subPath, cancellationToken);
        if (!string.IsNullOrWhiteSpace(localPath))
        {
            var dir = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await PreserveOwnershipAndPermissionsAsync(
                localPath,
                async () => await File.WriteAllTextAsync(localPath, content, cancellationToken),
                mountConfig?.GetOption("DefaultOwnerUid"),
                mountConfig?.GetOption("DefaultOwnerGid"),
                mountConfig?.GetOption("DefaultPermissions")
            );

            logger.LogInformation("Saved text file directly to local NFS storage at {Path} (ownership preserved)", localPath);
            return;
        }

        throw new InvalidOperationException($"Game server '{serverId}' is not currently running and local volume path could not be resolved.");
    }

    public async Task UploadFileAsync(
        string serverId,
        string volumeContainerPath,
        string? subPath,
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        cancellationToken.ThrowIfCancellationRequested();

        var safeFileName = Path.GetFileName(fileName);
        var (agent, containerId) = await ResolveAgentAndContainerAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (agent != null && !string.IsNullOrWhiteSpace(containerId))
        {
            var targetDir = CombineContainerPath(volumeContainerPath, subPath);
            var client = httpClientFactory.CreateClient();
            var url = $"{agent.InternalUrl.TrimEnd('/')}/containers/{containerId}/files/upload?path={Uri.EscapeDataString(targetDir)}";

            using var form = new MultipartFormDataContent();
            using var streamContent = new StreamContent(content);
            form.Add(streamContent, "file", safeFileName);

            var response = await client.PostAsync(url, form, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Uploaded file {FileName} via agent to container {ContainerId} at {Dir}", safeFileName, containerId, targetDir);
                return;
            }

            throw new HttpRequestException($"Agent returned status code {response.StatusCode} when uploading {safeFileName} to {targetDir}");
        }

        // Fallback to local NFS storage path
        var (localDir, mountConfig) = await TryResolveLocalNfsPathAsync(serverId, volumeContainerPath, subPath, cancellationToken);
        if (!string.IsNullOrWhiteSpace(localDir))
        {
            if (!Directory.Exists(localDir))
            {
                Directory.CreateDirectory(localDir);
            }

            var destinationFilePath = Path.Combine(localDir, safeFileName);

            await PreserveOwnershipAndPermissionsAsync(
                destinationFilePath,
                async () =>
                {
                    using var destination = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write);
                    await content.CopyToAsync(destination, cancellationToken);
                },
                mountConfig?.GetOption("DefaultOwnerUid"),
                mountConfig?.GetOption("DefaultOwnerGid"),
                mountConfig?.GetOption("DefaultPermissions")
            );

            logger.LogInformation("Uploaded file {FileName} directly to local NFS storage at {Dir} (ownership preserved)", safeFileName, localDir);
            return;
        }

        throw new InvalidOperationException($"Game server '{serverId}' is not currently running and local volume path could not be resolved.");
    }

    public async Task CreateDirectoryAsync(
        string serverId,
        string volumeContainerPath,
        string subPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (agent, containerId) = await ResolveAgentAndContainerAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (agent != null && !string.IsNullOrWhiteSpace(containerId))
        {
            var targetDir = CombineContainerPath(volumeContainerPath, subPath);
            var client = httpClientFactory.CreateClient();
            var url = $"{agent.InternalUrl.TrimEnd('/')}/containers/{containerId}/files/directory?path={Uri.EscapeDataString(targetDir)}";

            var response = await client.PostAsync(url, null, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Created directory {Path} via agent in container {ContainerId}", targetDir, containerId);
                return;
            }

            throw new HttpRequestException($"Agent returned status code {response.StatusCode} when creating directory {targetDir}");
        }

        // Fallback to local NFS storage path
        var (localDir, mountConfig) = await TryResolveLocalNfsPathAsync(serverId, volumeContainerPath, subPath, cancellationToken);
        if (!string.IsNullOrWhiteSpace(localDir))
        {
            await PreserveOwnershipAndPermissionsAsync(
                localDir,
                () =>
                {
                    Directory.CreateDirectory(localDir);
                    return Task.CompletedTask;
                },
                mountConfig?.GetOption("DefaultOwnerUid"),
                mountConfig?.GetOption("DefaultOwnerGid"),
                mountConfig?.GetOption("DefaultPermissions")
            );

            logger.LogInformation("Created directory directly on local NFS storage at {Path} (ownership preserved)", localDir);
            return;
        }

        throw new InvalidOperationException($"Game server '{serverId}' is not currently running and local volume path could not be resolved.");
    }

    public async Task DeleteFileOrDirectoryAsync(
        string serverId,
        string volumeContainerPath,
        string subPath,
        bool recursive = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (agent, containerId) = await ResolveAgentAndContainerAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (agent != null && !string.IsNullOrWhiteSpace(containerId))
        {
            var targetPath = CombineContainerPath(volumeContainerPath, subPath);
            var client = httpClientFactory.CreateClient();
            var url = $"{agent.InternalUrl.TrimEnd('/')}/containers/{containerId}/files?path={Uri.EscapeDataString(targetPath)}&recursive={recursive}";

            var response = await client.DeleteAsync(url, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Deleted item {Path} (recursive={Recursive}) via agent in container {ContainerId}", targetPath, recursive, containerId);
                return;
            }

            throw new HttpRequestException($"Agent returned status code {response.StatusCode} when deleting {targetPath}");
        }

        // Fallback to local NFS storage path
        var (localPath, _) = await TryResolveLocalNfsPathAsync(serverId, volumeContainerPath, subPath, cancellationToken);
        if (!string.IsNullOrWhiteSpace(localPath))
        {
            if (File.Exists(localPath))
            {
                File.Delete(localPath);
                logger.LogInformation("Deleted file directly from local NFS storage at {Path}", localPath);
                return;
            }
            if (Directory.Exists(localPath))
            {
                Directory.Delete(localPath, recursive);
                logger.LogInformation("Deleted directory directly from local NFS storage at {Path}", localPath);
                return;
            }

            throw new FileNotFoundException($"File or directory not found at: {localPath}");
        }

        throw new InvalidOperationException($"Game server '{serverId}' is not currently running and local volume path could not be resolved.");
    }

    private async Task<(string? LocalPath, MountTypeConfig? Config)> TryResolveLocalNfsPathAsync(
        string serverId,
        string volumeContainerPath,
        string? subPath,
        CancellationToken ct)
    {
        try
        {
            var server = await gameServerRepository.GetByServerIdAsync(serverId);
            if (server == null) return (null, null);

            var volume = server.Volumes.FirstOrDefault(v =>
                string.Equals(v.ContainerPath.TrimEnd('/'), volumeContainerPath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
            if (volume == null) return (null, null);

            var mountConfig = await mountTypeConfigRepository.GetByKeyAsync(volume.MountType, ct);
            if (mountConfig == null) return (null, null);

            var localRoot = mountConfig.GetOption("LocalPath")?.Replace('\\', '/').TrimEnd('/');
            if (string.IsNullOrWhiteSpace(localRoot)) return (null, mountConfig);

            var sourceToken = (volume.ContainerPath ?? string.Empty).Replace('\\', '/').TrimStart('/').Replace('/', '-');
            var devicePathFormat = mountConfig.GetOption("DevicePathFormat") ?? sourceToken;
            var gameTypeKey = "gametype";
            if (gameTypeRepository != null)
            {
                var allTypes = await gameTypeRepository.GetAllAsync();
                var match = allTypes.FirstOrDefault(gt => gt.Revisions.Any(r => r.Id == server.GameTypeRevisionId));
                if (match != null)
                {
                    gameTypeKey = match.Key;
                }
            }

            var devicePath = devicePathFormat
                .Replace("{gameTypeKey}", gameTypeKey, StringComparison.OrdinalIgnoreCase)
                .Replace("{serverId}", server.ServerId, StringComparison.OrdinalIgnoreCase)
                .Replace("{Source}", sourceToken, StringComparison.OrdinalIgnoreCase)
                .Trim('/');

            var fullPath = $"{localRoot}/{devicePath}";

            if (!string.IsNullOrWhiteSpace(subPath))
            {
                var cleanSub = subPath.Replace('\\', '/').Trim('/');
                fullPath = $"{fullPath}/{cleanSub}";
            }

            return (fullPath, mountConfig);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not resolve local NFS path for server {ServerId}, volume {VolumePath}", serverId, volumeContainerPath);
            return (null, null);
        }
    }

    private static async Task PreserveOwnershipAndPermissionsAsync(
        string path,
        Func<Task> writeAction,
        string? defaultOwnerUid = null,
        string? defaultOwnerGid = null,
        string? defaultPermissions = null)
    {
        int? uid = null;
        int? gid = null;
        int? mode = null;

        if (OperatingSystem.IsLinux())
        {
            try
            {
                if (File.Exists(path) || Directory.Exists(path))
                {
                    (uid, gid, mode) = await GetLinuxOwnerAndModeAsync(path);
                }
                else
                {
                    var parent = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
                    {
                        (uid, gid, mode) = await GetLinuxOwnerAndModeAsync(parent);
                    }
                }
            }
            catch { }
        }

        await writeAction();

        if (OperatingSystem.IsLinux())
        {
            try
            {
                var finalUid = uid ?? (int.TryParse(defaultOwnerUid, out var u) ? u : null);
                var finalGid = gid ?? (int.TryParse(defaultOwnerGid, out var g) ? g : null);
                var finalMode = mode ?? (TryParseOctal(defaultPermissions, out var m) ? m : null);

                if (finalUid.HasValue || finalGid.HasValue)
                {
                    await ChownAsync(path, finalUid, finalGid);
                }
                if (finalMode.HasValue)
                {
                    await ChmodAsync(path, finalMode.Value);
                }
            }
            catch { }
        }
    }

    private static async Task<(int? Uid, int? Gid, int? Mode)> GetLinuxOwnerAndModeAsync(string path)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "stat",
                Arguments = $"-c \"%u %g %a\" \"{path}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(startInfo);
            if (proc == null) return (null, null, null);

            var output = (await proc.StandardOutput.ReadToEndAsync()).Trim();
            await proc.WaitForExitAsync();

            if (proc.ExitCode == 0)
            {
                var parts = output.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 3)
                {
                    int.TryParse(parts[0], out var u);
                    int.TryParse(parts[1], out var g);
                    TryParseOctal(parts[2], out var m);
                    return (u, g, m);
                }
            }
        }
        catch { }

        return (null, null, null);
    }

    private static async Task ChownAsync(string path, int? uid, int? gid)
    {
        try
        {
            var owner = uid.HasValue ? uid.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
            if (gid.HasValue)
            {
                owner += $":{gid.Value.ToString(CultureInfo.InvariantCulture)}";
            }

            if (string.IsNullOrEmpty(owner)) return;

            var startInfo = new ProcessStartInfo
            {
                FileName = "chown",
                Arguments = $"\"{owner}\" \"{path}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(startInfo);
            if (proc != null)
            {
                await proc.WaitForExitAsync();
            }
        }
        catch { }
    }

    private static async Task ChmodAsync(string path, int mode)
    {
        try
        {
            var octal = Convert.ToString(mode, 8);
            var startInfo = new ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"{octal} \"{path}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(startInfo);
            if (proc != null)
            {
                await proc.WaitForExitAsync();
            }
        }
        catch { }
    }

    private static bool TryParseOctal(string? text, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        foreach (var c in text.Trim())
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

    private async Task<(NodeAgentEndpoint? Agent, string? ContainerId)> ResolveAgentAndContainerAsync(
        string serverId,
        CancellationToken cancellationToken)
    {
        if (serverResourceMonitor != null)
        {
            try
            {
                var snapshot = await serverResourceMonitor.GetSnapshotAsync(serverId, cancellationToken).ConfigureAwait(false);
                var containerId = snapshot?.ContainerIds.FirstOrDefault() ?? snapshot?.RealTimeStats?.ContainerId;
                if (!string.IsNullOrWhiteSpace(containerId))
                {
                    var agent = await nodeAgentDiscovery.GetAgentForContainerAsync(containerId).ConfigureAwait(false)
                        ?? await nodeAgentDiscovery.GetAgentForServerAsync(serverId).ConfigureAwait(false);
                    if (agent != null)
                    {
                        return (agent, containerId);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Could not resolve container from resource monitor for server {ServerId}", serverId);
            }
        }

        var fallbackAgent = await nodeAgentDiscovery.GetAgentForServerAsync(serverId).ConfigureAwait(false);
        return (fallbackAgent, null);
    }

    public static string CombineContainerPath(string volumeContainerPath, string? subPath)
    {
        var normalizedVolume = (volumeContainerPath ?? "/").Replace('\\', '/').TrimEnd('/');
        if (!normalizedVolume.StartsWith('/'))
        {
            normalizedVolume = "/" + normalizedVolume;
        }

        if (string.IsNullOrWhiteSpace(subPath))
        {
            return normalizedVolume;
        }

        var normalizedSub = subPath.Replace('\\', '/').Trim('/');
        var volumeWithoutSlash = normalizedVolume.TrimStart('/');

        if (normalizedSub.Equals(volumeWithoutSlash, StringComparison.OrdinalIgnoreCase))
        {
            return normalizedVolume;
        }
        if (normalizedSub.StartsWith(volumeWithoutSlash + "/", StringComparison.OrdinalIgnoreCase))
        {
            return "/" + normalizedSub;
        }

        return $"{normalizedVolume}/{normalizedSub}";
    }
}
