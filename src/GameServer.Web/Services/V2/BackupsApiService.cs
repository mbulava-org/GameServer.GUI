using System.Net.Http.Headers;
using System.Net.Http.Json;
using GameServer.Web.Configurations;
using GameServer.Web.Models.V2;
using Microsoft.Extensions.Options;

namespace GameServer.Web.Services.V2;

public sealed class BackupsApiService(
    IHttpClientFactory httpClientFactory,
    IOptions<GameServerDockerApi> apiOptions,
    Services.Auth.JwtAuthenticationStateProvider? authStateProvider = null) : IBackupsApiService
{
    private async Task<HttpClient> CreateClientAsync()
    {
        var client = httpClientFactory.CreateClient("GameServerApi");
        var baseUri = apiOptions.Value.BaseUri?.TrimEnd('/') ?? "http://localhost:5164";
        client.BaseAddress = new Uri(baseUri + "/");

        if (authStateProvider is not null)
        {
            var token = await authStateProvider.GetTokenAsync();
            if (!string.IsNullOrWhiteSpace(token))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        return client;
    }

    public async Task<GameServerBackup> CreateBackupAsync(
        string serverId,
        CreateBackupRequest request,
        CancellationToken cancellationToken = default)
    {
        using var client = await CreateClientAsync().ConfigureAwait(false);
        var url = $"api/v2/gameservers/{Uri.EscapeDataString(serverId)}/backups";
        using var response = await client.PostAsJsonAsync(url, request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GameServerBackup>(cancellationToken).ConfigureAwait(false)
               ?? throw new InvalidOperationException("Failed to deserialize backup response.");
    }

    public async Task<IReadOnlyList<GameServerBackup>> GetBackupsAsync(
        string? serverId = null,
        CancellationToken cancellationToken = default)
    {
        using var client = await CreateClientAsync().ConfigureAwait(false);
        var url = "api/v2/backups";
        if (!string.IsNullOrWhiteSpace(serverId))
        {
            url += $"?serverId={Uri.EscapeDataString(serverId)}";
        }

        using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<GameServerBackup>>(cancellationToken).ConfigureAwait(false) ?? [];
    }

    public async Task<GameServerBackup?> GetBackupByIdAsync(
        string backupId,
        CancellationToken cancellationToken = default)
    {
        using var client = await CreateClientAsync().ConfigureAwait(false);
        var url = $"api/v2/backups/{Uri.EscapeDataString(backupId)}";
        using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GameServerBackup>(cancellationToken).ConfigureAwait(false);
    }

    public async Task<byte[]> DownloadBackupAsync(
        string backupId,
        CancellationToken cancellationToken = default)
    {
        using var client = await CreateClientAsync().ConfigureAwait(false);
        var url = $"api/v2/backups/{Uri.EscapeDataString(backupId)}/download";
        using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<GameServerBackup> ExtendBackupAsync(
        string backupId,
        ExtendBackupRequest request,
        CancellationToken cancellationToken = default)
    {
        using var client = await CreateClientAsync().ConfigureAwait(false);
        var url = $"api/v2/backups/{Uri.EscapeDataString(backupId)}/extend";
        using var response = await client.PostAsJsonAsync(url, request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GameServerBackup>(cancellationToken).ConfigureAwait(false)
               ?? throw new InvalidOperationException("Failed to deserialize backup response.");
    }

    public async Task<GameServerBackup> UpdateSharingAsync(
        string backupId,
        UpdateBackupSharingRequest request,
        CancellationToken cancellationToken = default)
    {
        using var client = await CreateClientAsync().ConfigureAwait(false);
        var url = $"api/v2/backups/{Uri.EscapeDataString(backupId)}/share";
        using var response = await client.PutAsJsonAsync(url, request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GameServerBackup>(cancellationToken).ConfigureAwait(false)
               ?? throw new InvalidOperationException("Failed to deserialize backup response.");
    }

    public async Task DeleteBackupAsync(
        string backupId,
        CancellationToken cancellationToken = default)
    {
        using var client = await CreateClientAsync().ConfigureAwait(false);
        var url = $"api/v2/backups/{Uri.EscapeDataString(backupId)}";
        using var response = await client.DeleteAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }
}
