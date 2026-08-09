using System.Net.Http.Json;
using GameServer.Web.Models.V2;

namespace GameServer.Web.Services.V2;

public sealed class MountTypeConfigApiService(IHttpClientFactory httpClientFactory, Configurations.GameServerDockerApi apiOptions) : IMountTypeConfigApiService
{
    /// <summary>
    /// Gets all mount-type configurations.
    /// </summary>
    public async Task<IReadOnlyList<MountTypeConfig>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        using var response = await client.GetAsync("api/v2/mounttypeconfigs", cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<MountTypeConfig>>(cancellationToken)
            ?? throw new InvalidOperationException("The mount type config response did not contain a payload.");
    }

    /// <summary>
    /// Gets a single mount-type configuration by key.
    /// </summary>
    public async Task<MountTypeConfig> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        using var response = await client.GetAsync($"api/v2/mounttypeconfigs/{Uri.EscapeDataString(key)}", cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<MountTypeConfig>(cancellationToken)
            ?? throw new InvalidOperationException("The mount type config response did not contain a payload.");
    }

    /// <summary>
    /// Saves a mount-type configuration.
    /// </summary>
    public async Task<MountTypeConfig> SaveAsync(MountTypeConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        using var client = CreateClient();
        using var response = await client.PutAsJsonAsync($"api/v2/mounttypeconfigs/{Uri.EscapeDataString(config.Key)}", config, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<MountTypeConfig>(cancellationToken)
            ?? throw new InvalidOperationException("The save response did not contain a payload.");
    }

    /// <summary>
    /// Deletes a mount-type configuration.
    /// </summary>
    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        using var response = await client.DeleteAsync($"api/v2/mounttypeconfigs/{Uri.EscapeDataString(key)}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(apiOptions.BaseUri);
        return client;
    }
}
