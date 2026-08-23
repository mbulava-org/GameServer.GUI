using System.Net.Http.Headers;
using System.Net.Http.Json;
using GameServer.Web.Configurations;
using GameServer.Web.Models.V2;
using Microsoft.Extensions.Options;

namespace GameServer.Web.Services.V2;

public sealed class GameServerFilesApiService(IHttpClientFactory httpClientFactory, IOptions<GameServerDockerApi> apiOptions)
    : IGameServerFilesApiService
{
    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient();
        var baseUri = apiOptions.Value.BaseUri?.TrimEnd('/') ?? "http://localhost:5164";
        client.BaseAddress = new Uri(baseUri + "/");
        return client;
    }

    public async Task<IReadOnlyList<FileItem>> ListFilesAsync(
        string serverId,
        string volumePath,
        string? subPath = null,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var url = $"api/v2/gameservers/{Uri.EscapeDataString(serverId)}/files?volumePath={Uri.EscapeDataString(volumePath)}";
        if (!string.IsNullOrWhiteSpace(subPath))
        {
            url += $"&subPath={Uri.EscapeDataString(subPath)}";
        }

        using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<FileItem>>(cancellationToken).ConfigureAwait(false) ?? [];
    }

    public async Task<string> GetContentAsync(
        string serverId,
        string volumePath,
        string subPath,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var url = $"api/v2/gameservers/{Uri.EscapeDataString(serverId)}/files/content?volumePath={Uri.EscapeDataString(volumePath)}&subPath={Uri.EscapeDataString(subPath)}";

        using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveContentAsync(
        string serverId,
        string volumePath,
        string subPath,
        string content,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var url = $"api/v2/gameservers/{Uri.EscapeDataString(serverId)}/files/content?volumePath={Uri.EscapeDataString(volumePath)}&subPath={Uri.EscapeDataString(subPath)}";

        using var response = await client.PutAsJsonAsync(url, new { Content = content }, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task<byte[]> DownloadAsync(
        string serverId,
        string volumePath,
        string subPath,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var url = $"api/v2/gameservers/{Uri.EscapeDataString(serverId)}/files/download?volumePath={Uri.EscapeDataString(volumePath)}&subPath={Uri.EscapeDataString(subPath)}";

        using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UploadAsync(
        string serverId,
        string volumePath,
        string? subPath,
        string fileName,
        Stream contentStream,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var url = $"api/v2/gameservers/{Uri.EscapeDataString(serverId)}/files/upload?volumePath={Uri.EscapeDataString(volumePath)}";
        if (!string.IsNullOrWhiteSpace(subPath))
        {
            url += $"&subPath={Uri.EscapeDataString(subPath)}";
        }

        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(contentStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(streamContent, "file", fileName);

        using var response = await client.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task CreateDirectoryAsync(
        string serverId,
        string volumePath,
        string subPath,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var url = $"api/v2/gameservers/{Uri.EscapeDataString(serverId)}/files/directory?volumePath={Uri.EscapeDataString(volumePath)}&subPath={Uri.EscapeDataString(subPath)}";

        using var response = await client.PostAsync(url, content: null, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(
        string serverId,
        string volumePath,
        string subPath,
        bool recursive = false,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var url = $"api/v2/gameservers/{Uri.EscapeDataString(serverId)}/files?volumePath={Uri.EscapeDataString(volumePath)}&subPath={Uri.EscapeDataString(subPath)}&recursive={recursive}";

        using var response = await client.DeleteAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }
}
