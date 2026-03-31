using System.Net.Http.Json;
using GameServer.Web.Models.V2;
using Microsoft.Extensions.Options;

namespace GameServer.Web.Services.V2;

public sealed class GameTypeV2ApiService(IHttpClientFactory httpClientFactory, IOptions<Configurations.GameServerDockerApi> apiOptions)
{
    /// <summary>
    /// Gets the V2 GameType list.
    /// </summary>
    public async Task<IReadOnlyList<GameTypeListItem>> GetListAsync(bool includeInactive, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        using var response = await client.GetAsync($"api/v2/gametypes?includeInactive={includeInactive}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<List<GameTypeListItem>>(cancellationToken);
        return payload ?? [];
    }

    /// <summary>
    /// Gets the V2 GameType detail payload.
    /// </summary>
    public async Task<GameTypeDetail?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("A game type key is required.", nameof(key));
        }

        using var client = CreateClient();
        using var response = await client.GetAsync($"api/v2/gametypes/{Uri.EscapeDataString(key)}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GameTypeDetail>(cancellationToken);
    }

    /// <summary>
    /// Creates a V2 GameType.
    /// </summary>
    public async Task<GameTypeDetail> CreateAsync(SaveGameTypeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var client = CreateClient();
        using var response = await client.PostAsJsonAsync("api/v2/gametypes", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<GameTypeDetail>(cancellationToken)
            ?? throw new InvalidOperationException("The V2 create response did not contain a game type payload.");
    }

    /// <summary>
    /// Updates a V2 GameType.
    /// </summary>
    public async Task<GameTypeDetail> UpdateAsync(string key, SaveGameTypeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(request);

        using var client = CreateClient();
        using var response = await client.PutAsJsonAsync($"api/v2/gametypes/{Uri.EscapeDataString(key)}", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<GameTypeDetail>(cancellationToken)
            ?? throw new InvalidOperationException("The V2 update response did not contain a game type payload.");
    }

    /// <summary>
    /// Adds a revision to a V2 GameType.
    /// </summary>
    public async Task<GameTypeRevision> AddRevisionAsync(string key, SaveGameTypeRevisionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(request);

        using var client = CreateClient();
        using var response = await client.PostAsJsonAsync($"api/v2/gametypes/{Uri.EscapeDataString(key)}/revisions", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<GameTypeRevision>(cancellationToken)
            ?? throw new InvalidOperationException("The V2 add-revision response did not contain a revision payload.");
    }

    /// <summary>
    /// Updates a revision for a V2 GameType.
    /// </summary>
    public async Task<GameTypeRevision> UpdateRevisionAsync(string key, int revisionId, SaveGameTypeRevisionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(request);

        using var client = CreateClient();
        using var response = await client.PutAsJsonAsync($"api/v2/gametypes/{Uri.EscapeDataString(key)}/revisions/{revisionId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<GameTypeRevision>(cancellationToken)
            ?? throw new InvalidOperationException("The V2 update-revision response did not contain a revision payload.");
    }

    /// <summary>
    /// Publishes a revision for a V2 GameType.
    /// </summary>
    public async Task<GameTypeRevision> PublishRevisionAsync(string key, int revisionId, bool setAsCurrentRevision, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        using var client = CreateClient();
        using var response = await client.PostAsJsonAsync(
            $"api/v2/gametypes/{Uri.EscapeDataString(key)}/revisions/{revisionId}/publish",
            new PublishRevisionRequest { SetAsCurrentRevision = setAsCurrentRevision },
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<GameTypeRevision>(cancellationToken)
            ?? throw new InvalidOperationException("The V2 publish-revision response did not contain a revision payload.");
    }

    /// <summary>
    /// Sets the current revision for a V2 GameType.
    /// </summary>
    public async Task SetCurrentRevisionAsync(string key, int revisionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        using var client = CreateClient();
        using var response = await client.PostAsync($"api/v2/gametypes/{Uri.EscapeDataString(key)}/revisions/{revisionId}/set-current", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Detects Docker setup data for a V2 GameType tag.
    /// </summary>
    public async Task<GameTypeSetupDetectionResult> DetectSetupAsync(string key, string versionTag, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(versionTag);

        using var client = CreateClient();
        using var response = await client.PostAsJsonAsync(
            $"api/v2/gametypes/{Uri.EscapeDataString(key)}/detection/scan-tag",
            new DetectGameTypeSetupRequest { VersionTag = versionTag },
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<GameTypeSetupDetectionResult>(cancellationToken)
            ?? throw new InvalidOperationException("The V2 detection response did not contain a payload.");
    }

    /// <summary>
    /// Compares detected Docker setup data to a selected V2 GameType revision.
    /// </summary>
    public async Task<GameTypeSetupComparisonResult> CompareSetupAsync(string key, string versionTag, int revisionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(versionTag);

        using var client = CreateClient();
        using var response = await client.PostAsJsonAsync(
            $"api/v2/gametypes/{Uri.EscapeDataString(key)}/detection/compare",
            new CompareGameTypeSetupRequest
            {
                VersionTag = versionTag,
                RevisionId = revisionId
            },
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<GameTypeSetupComparisonResult>(cancellationToken)
            ?? throw new InvalidOperationException("The V2 comparison response did not contain a payload.");
    }

    private HttpClient CreateClient()
    {
        var baseUri = apiOptions.Value.BaseUri;
        if (string.IsNullOrWhiteSpace(baseUri))
        {
            throw new InvalidOperationException("GameServerDockerApi:BaseUri must be configured.");
        }

        var client = httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(baseUri, UriKind.Absolute);
        return client;
    }
}
