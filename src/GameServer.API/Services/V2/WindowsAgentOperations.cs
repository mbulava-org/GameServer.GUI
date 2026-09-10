using System.Net.Http.Json;
using GameServer.API.Interfaces;

namespace GameServer.API.Services.V2;

/// <summary>
/// Implements HTTP operations against Windows Host Agent endpoints.
/// </summary>
public sealed class WindowsAgentOperations(
    IHttpClientFactory httpClientFactory,
    ILogger<WindowsAgentOperations> logger) : IWindowsAgentOperations
{
    public async Task<WindowsProcessInfo?> StartServerAsync(string agentUrl, WindowsStartServerRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentUrl);
        ArgumentNullException.ThrowIfNull(request);

        using var client = CreateClient(agentUrl);
        var response = await client.PostAsJsonAsync("api/servers/start", request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            logger.LogWarning("Failed to start server {ServerId} on Windows agent {Url}. Status={StatusCode}, Error={Error}",
                request.ServerId, agentUrl, response.StatusCode, error);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<WindowsProcessInfo>(cancellationToken).ConfigureAwait(false);
    }

    public async Task<WindowsProcessInfo?> StopServerAsync(string agentUrl, string serverId, WindowsStopServerRequest? request = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);

        using var client = CreateClient(agentUrl);
        var response = await client.PostAsJsonAsync($"api/servers/{Uri.EscapeDataString(serverId)}/stop", request ?? new WindowsStopServerRequest(), cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            logger.LogWarning("Failed to stop server {ServerId} on Windows agent {Url}. Status={StatusCode}, Error={Error}",
                serverId, agentUrl, response.StatusCode, error);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<WindowsProcessInfo>(cancellationToken).ConfigureAwait(false);
    }

    public async Task<WindowsProcessInfo?> RestartServerAsync(string agentUrl, string serverId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);

        using var client = CreateClient(agentUrl);
        var response = await client.PostAsync($"api/servers/{Uri.EscapeDataString(serverId)}/restart", null, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            logger.LogWarning("Failed to restart server {ServerId} on Windows agent {Url}. Status={StatusCode}, Error={Error}",
                serverId, agentUrl, response.StatusCode, error);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<WindowsProcessInfo>(cancellationToken).ConfigureAwait(false);
    }

    public async Task<WindowsProcessInfo?> GetServerInfoAsync(string agentUrl, string serverId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);

        using var client = CreateClient(agentUrl);
        var response = await client.GetAsync($"api/servers/{Uri.EscapeDataString(serverId)}", cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<WindowsProcessInfo>(cancellationToken).ConfigureAwait(false);
    }

    public async Task<WindowsProcessStats?> GetServerStatsAsync(string agentUrl, string serverId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);

        using var client = CreateClient(agentUrl);
        var response = await client.GetAsync($"api/servers/{Uri.EscapeDataString(serverId)}/stats", cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<WindowsProcessStats>(cancellationToken).ConfigureAwait(false);
    }

    private HttpClient CreateClient(string agentUrl)
    {
        var client = httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(agentUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }
}
