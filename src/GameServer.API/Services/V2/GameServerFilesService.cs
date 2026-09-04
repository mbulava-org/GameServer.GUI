using System.Net.Http.Json;
using GameServer.API.Dtos.V2;
using GameServer.API.Interfaces;
using GameServer.API.Models;
using GameServer.API.Repositories.V2;
using Microsoft.Extensions.Logging;

namespace GameServer.API.Services.V2;

public sealed class GameServerFilesService(
    IGameServerRepository gameServerRepository,
    INodeAgentDiscovery nodeAgentDiscovery,
    IHttpClientFactory httpClientFactory,
    ILogger<GameServerFilesService> logger,
    IServerResourceMonitor? serverResourceMonitor = null)
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
        if (agent == null || string.IsNullOrWhiteSpace(containerId))
        {
            logger.LogDebug("No active container found for server {ServerId} when listing files; returning empty list", serverId);
            return [];
        }

        var targetPath = CombineContainerPath(volumeContainerPath, subPath);
        var client = httpClientFactory.CreateClient();
        var url = $"{agent.InternalUrl.TrimEnd('/')}/containers/{containerId}/files?path={Uri.EscapeDataString(targetPath)}";

        var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Agent {AgentUrl} returned status {StatusCode} when listing files for container {ContainerId} at {Path}", agent.InternalUrl, response.StatusCode, containerId, targetPath);
            return [];
        }

        var files = await response.Content.ReadFromJsonAsync<List<FileItemDto>>(cancellationToken: cancellationToken).ConfigureAwait(false);
        return files ?? [];
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> GetFileStreamAsync(
        string serverId,
        string volumeContainerPath,
        string subPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (agent, containerId) = await ResolveAgentAndContainerAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (agent == null || string.IsNullOrWhiteSpace(containerId))
        {
            throw new InvalidOperationException($"Game server '{serverId}' is not currently running. The server must be active to access container files.");
        }

        var targetPath = CombineContainerPath(volumeContainerPath, subPath);
        var client = httpClientFactory.CreateClient();
        var url = $"{agent.InternalUrl.TrimEnd('/')}/containers/{containerId}/files/download?path={Uri.EscapeDataString(targetPath)}";

        var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new FileNotFoundException($"File not found in container at: {targetPath}");
            }

            throw new HttpRequestException($"Agent returned status code {response.StatusCode} when downloading {targetPath}");
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
            ?? Path.GetFileName(targetPath);

        return (stream, contentType, fileName);
    }

    public async Task<string> GetFileContentTextAsync(
        string serverId,
        string volumeContainerPath,
        string subPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (agent, containerId) = await ResolveAgentAndContainerAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (agent == null || string.IsNullOrWhiteSpace(containerId))
        {
            throw new InvalidOperationException($"Game server '{serverId}' is not currently running. The server must be active to access container files.");
        }

        var targetPath = CombineContainerPath(volumeContainerPath, subPath);
        var client = httpClientFactory.CreateClient();
        var url = $"{agent.InternalUrl.TrimEnd('/')}/containers/{containerId}/files/content?path={Uri.EscapeDataString(targetPath)}";

        var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new FileNotFoundException($"File not found in container at: {targetPath}");
            }

            throw new HttpRequestException($"Agent returned status code {response.StatusCode} when reading {targetPath}");
        }

        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
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
        if (agent == null || string.IsNullOrWhiteSpace(containerId))
        {
            throw new InvalidOperationException($"Game server '{serverId}' is not currently running. The server must be active to access container files.");
        }

        var targetPath = CombineContainerPath(volumeContainerPath, subPath);
        var client = httpClientFactory.CreateClient();
        var url = $"{agent.InternalUrl.TrimEnd('/')}/containers/{containerId}/files/content?path={Uri.EscapeDataString(targetPath)}";

        var response = await client.PutAsJsonAsync(url, new { Content = content }, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Agent returned status code {response.StatusCode} when saving {targetPath}");
        }

        logger.LogInformation("Saved text file via agent to container {ContainerId} at {Path}", containerId, targetPath);
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
        if (agent == null || string.IsNullOrWhiteSpace(containerId))
        {
            throw new InvalidOperationException($"Game server '{serverId}' is not currently running. The server must be active to access container files.");
        }

        var targetDir = CombineContainerPath(volumeContainerPath, subPath);
        var client = httpClientFactory.CreateClient();
        var url = $"{agent.InternalUrl.TrimEnd('/')}/containers/{containerId}/files/upload?path={Uri.EscapeDataString(targetDir)}";

        using var form = new MultipartFormDataContent();
        using var streamContent = new StreamContent(content);
        form.Add(streamContent, "file", safeFileName);

        var response = await client.PostAsync(url, form, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Agent returned status code {response.StatusCode} when uploading {safeFileName} to {targetDir}");
        }

        logger.LogInformation("Uploaded file {FileName} via agent to container {ContainerId} at {Dir}", safeFileName, containerId, targetDir);
    }

    public async Task CreateDirectoryAsync(
        string serverId,
        string volumeContainerPath,
        string subPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (agent, containerId) = await ResolveAgentAndContainerAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (agent == null || string.IsNullOrWhiteSpace(containerId))
        {
            throw new InvalidOperationException($"Game server '{serverId}' is not currently running. The server must be active to access container files.");
        }

        var targetDir = CombineContainerPath(volumeContainerPath, subPath);
        var client = httpClientFactory.CreateClient();
        var url = $"{agent.InternalUrl.TrimEnd('/')}/containers/{containerId}/files/directory?path={Uri.EscapeDataString(targetDir)}";

        var response = await client.PostAsync(url, null, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Agent returned status code {response.StatusCode} when creating directory {targetDir}");
        }

        logger.LogInformation("Created directory {Path} via agent in container {ContainerId}", targetDir, containerId);
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
        if (agent == null || string.IsNullOrWhiteSpace(containerId))
        {
            throw new InvalidOperationException($"Game server '{serverId}' is not currently running. The server must be active to access container files.");
        }

        var targetPath = CombineContainerPath(volumeContainerPath, subPath);
        var client = httpClientFactory.CreateClient();
        var url = $"{agent.InternalUrl.TrimEnd('/')}/containers/{containerId}/files?path={Uri.EscapeDataString(targetPath)}&recursive={recursive}";

        var response = await client.DeleteAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Agent returned status code {response.StatusCode} when deleting {targetPath}");
        }

        logger.LogInformation("Deleted item {Path} (recursive={Recursive}) via agent in container {ContainerId}", targetPath, recursive, containerId);
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

    private static string CombineContainerPath(string volumeContainerPath, string? subPath)
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
        return $"{normalizedVolume}/{normalizedSub}";
    }
}
