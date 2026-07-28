using System.Net.Http.Json;
using GameServer.Web.Models.V2;

namespace GameServer.Web.Services.V2;

public sealed class GameServerV2ApiService(IHttpClientFactory httpClientFactory, Configurations.GameServerDockerApi apiOptions)
{
    /// <summary>
    /// Gets the V2 GameServer list.
    /// </summary>
    public async Task<IReadOnlyList<GameServerListItem>> GetListAsync(bool includeDeleted = false, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        using var response = await client.GetAsync($"api/v2/gameservers?includeDeleted={includeDeleted}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<List<GameServerListItem>>(cancellationToken);
        return payload ?? [];
    }

    /// <summary>
    /// Gets the V2 GameServer detail payload.
    /// </summary>
    public async Task<GameServerDetail?> GetByServerIdAsync(string serverId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);

        using var client = CreateClient();
        using var response = await client.GetAsync($"api/v2/gameservers/{Uri.EscapeDataString(serverId)}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GameServerDetail>(cancellationToken);
    }

    /// <summary>
    /// Validates a V2 GameServer request.
    /// </summary>
    public async Task<GameServerValidationResult> ValidateAsync(SaveGameServerRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var client = CreateClient();
        using var response = await client.PostAsJsonAsync("api/v2/gameservers/validate", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<GameServerValidationResult>(cancellationToken)
            ?? throw new InvalidOperationException("The V2 GameServer validation response did not contain a payload.");
    }

    /// <summary>
    /// Creates a V2 GameServer.
    /// </summary>
    public async Task<GameServerDetail> CreateAsync(SaveGameServerRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var client = CreateClient();
        using var response = await client.PostAsJsonAsync("api/v2/gameservers", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<GameServerDetail>(cancellationToken)
            ?? throw new InvalidOperationException("The V2 GameServer create response did not contain a payload.");
    }

    /// <summary>
    /// Updates a V2 GameServer.
    /// </summary>
    public async Task<GameServerDetail> UpdateAsync(string serverId, SaveGameServerRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);
        ArgumentNullException.ThrowIfNull(request);

        using var client = CreateClient();
        using var response = await client.PutAsJsonAsync($"api/v2/gameservers/{Uri.EscapeDataString(serverId)}", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<GameServerDetail>(cancellationToken)
            ?? throw new InvalidOperationException("The V2 GameServer update response did not contain a payload.");
    }

    private HttpClient CreateClient()
    {
        var baseUri = apiOptions.BaseUri;
        if (string.IsNullOrWhiteSpace(baseUri))
        {
            throw new InvalidOperationException("GameServerDockerApi:BaseUri must be configured.");
        }

        var client = httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(baseUri);
        return client;
    }
}
