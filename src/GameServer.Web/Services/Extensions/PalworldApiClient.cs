using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace GameServer.Web.Services.Extensions;

/// <summary>
/// Default implementation of <see cref="IPalworldApiClient"/>. Runs server-side
/// in <c>GameServer.Web</c>; the browser never sees the admin password. The base
/// URL is derived per request from the Docker service name so requests stay on the
/// internal Docker network. Falls back to <see cref="INodeAgentExtensionProxy"/>
/// once that is implemented for setups where the Web host cannot reach the service
/// directly.
/// </summary>
public sealed class PalworldApiClient : IPalworldApiClient
{
    public const string HttpClientName = "PalworldApi";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly INodeAgentExtensionProxy _nodeAgentProxy;
    private readonly ILogger<PalworldApiClient> _logger;

    public PalworldApiClient(
        IHttpClientFactory httpClientFactory,
        INodeAgentExtensionProxy nodeAgentProxy,
        ILogger<PalworldApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _nodeAgentProxy = nodeAgentProxy;
        _logger = logger;
    }

    public Task<PalworldServerInfo?> GetInfoAsync(PalworldRequestContext context, CancellationToken cancellationToken = default)
        => SendJsonAsync<PalworldServerInfo>(context, HttpMethod.Get, "/v1/api/info", body: null, cancellationToken);

    public Task<PalworldPlayersResponse?> GetPlayersAsync(PalworldRequestContext context, CancellationToken cancellationToken = default)
        => SendJsonAsync<PalworldPlayersResponse>(context, HttpMethod.Get, "/v1/api/players", body: null, cancellationToken);

    public Task AnnounceAsync(PalworldRequestContext context, string message, CancellationToken cancellationToken = default)
        => SendAsync(context, HttpMethod.Post, "/v1/api/announce", new { message }, cancellationToken);

    public Task KickPlayerAsync(PalworldRequestContext context, string userId, string? message, CancellationToken cancellationToken = default)
        => SendAsync(context, HttpMethod.Post, "/v1/api/kick", new { userid = userId, message = message ?? string.Empty }, cancellationToken);

    public Task BanPlayerAsync(PalworldRequestContext context, string userId, string? message, CancellationToken cancellationToken = default)
        => SendAsync(context, HttpMethod.Post, "/v1/api/ban", new { userid = userId, message = message ?? string.Empty }, cancellationToken);

    public Task SaveWorldAsync(PalworldRequestContext context, CancellationToken cancellationToken = default)
        => SendAsync(context, HttpMethod.Post, "/v1/api/save", body: null, cancellationToken);

    public Task ShutdownAsync(PalworldRequestContext context, int waitSeconds, string? message, CancellationToken cancellationToken = default)
        => SendAsync(context, HttpMethod.Post, "/v1/api/shutdown", new { waittime = waitSeconds, message = message ?? string.Empty }, cancellationToken);

    private async Task<T?> SendJsonAsync<T>(PalworldRequestContext context, HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var response = await SendCoreAsync(context, method, path, body, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false);
    }

    private async Task SendAsync(PalworldRequestContext context, HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var response = await SendCoreAsync(context, method, path, body, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private async Task<HttpResponseMessage> SendCoreAsync(PalworldRequestContext context, HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(context.ServiceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(context.AdminPassword);

        var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        var baseUri = new Uri($"http://{context.ServiceName}:{context.Port}");
        var requestUri = new Uri(baseUri, path);

        using var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"admin:{context.AdminPassword}")));

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        _logger.LogDebug("Palworld {Method} {Path} on {Service} (auth redacted)", method, path, context.ServiceName);

        try
        {
            return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Direct Palworld call failed for {Service}; attempting node-agent fallback", context.ServiceName);
            // TODO: Route through node agent once INodeAgentExtensionProxy has a real implementation.
            return await _nodeAgentProxy.SendAsync(context.ServiceName, method, path, body, cancellationToken).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// Placeholder for the future node-agent HTTP proxy that will forward extension
/// requests when the GUI cannot reach the game service directly.
/// </summary>
public interface INodeAgentExtensionProxy
{
    Task<HttpResponseMessage> SendAsync(string serviceName, HttpMethod method, string path, object? body, CancellationToken cancellationToken);
}

internal sealed class NotImplementedNodeAgentExtensionProxy : INodeAgentExtensionProxy
{
    public Task<HttpResponseMessage> SendAsync(string serviceName, HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        throw new NotImplementedException(
            "Node-agent extension proxy is not yet implemented. Configure the GUI to reach the game service directly, or implement INodeAgentExtensionProxy.");
    }
}
