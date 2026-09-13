using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GameServer.API.Services.V2;

/// <summary>
/// Queries remote OCI and Docker Registry v2 endpoints to inspect tag digests.
/// </summary>
public sealed class DockerRegistryService : IDockerRegistryService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DockerRegistryService> _logger;

    private const string AcceptManifestHeaders =
        "application/vnd.docker.distribution.manifest.v2+json, " +
        "application/vnd.docker.distribution.manifest.list.v2+json, " +
        "application/vnd.oci.image.manifest.v1+json, " +
        "application/vnd.oci.image.index.v1+json";

    public DockerRegistryService(
        IHttpClientFactory httpClientFactory,
        ILogger<DockerRegistryService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string?> GetRemoteDigestAsync(string imageReference, string? tag = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageReference))
        {
            return null;
        }

        var (registryHost, repository, resolvedTag) = ParseImageReference(imageReference, tag);

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);

            var manifestUrl = $"https://{registryHost}/v2/{repository}/manifests/{resolvedTag}";

            var request = new HttpRequestMessage(HttpMethod.Get, manifestUrl);
            request.Headers.Add("Accept", AcceptManifestHeaders);

            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && response.Headers.WwwAuthenticate.Any())
            {
                var token = await AcquireBearerTokenAsync(client, response.Headers.WwwAuthenticate.First(), registryHost, repository, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(token))
                {
                    request = new HttpRequestMessage(HttpMethod.Get, manifestUrl);
                    request.Headers.Add("Accept", AcceptManifestHeaders);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                }
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Registry response for {Host}/{Repo}:{Tag} returned status {StatusCode}", registryHost, repository, resolvedTag, response.StatusCode);
                return null;
            }

            // Docker-Content-Digest header
            if (response.Headers.TryGetValues("Docker-Content-Digest", out var digestValues))
            {
                var digest = digestValues.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d))?.Trim();
                if (!string.IsNullOrWhiteSpace(digest))
                {
                    return digest;
                }
            }

            // Fallback: Compute SHA256 of manifest body
            var contentBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (contentBytes.Length > 0)
            {
                var hash = SHA256.HashData(contentBytes);
                return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve remote image digest for {Registry}/{Repository}:{Tag}", registryHost, repository, resolvedTag);
            return null;
        }
    }

    public static (string RegistryHost, string Repository, string Tag) ParseImageReference(string imageReference, string? explicitTag = null)
    {
        var cleaned = imageReference.Trim();

        // Strip http:// or https:// if present
        if (cleaned.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[8..];
        }
        else if (cleaned.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[7..];
        }

        // Strip query / fragment
        var qIdx = cleaned.IndexOf('?');
        if (qIdx >= 0) cleaned = cleaned[..qIdx];
        var fIdx = cleaned.IndexOf('#');
        if (fIdx >= 0) cleaned = cleaned[..fIdx];

        cleaned = cleaned.Trim('/');

        string tag;
        var digestIdx = cleaned.IndexOf('@', StringComparison.Ordinal);
        if (digestIdx >= 0)
        {
            cleaned = cleaned[..digestIdx];
        }

        var colonIdx = cleaned.LastIndexOf(':');
        var slashIdx = cleaned.LastIndexOf('/');

        if (colonIdx > slashIdx)
        {
            tag = cleaned[(colonIdx + 1)..];
            cleaned = cleaned[..colonIdx];
        }
        else
        {
            tag = !string.IsNullOrWhiteSpace(explicitTag) ? explicitTag.Trim() : "latest";
        }

        if (!string.IsNullOrWhiteSpace(explicitTag))
        {
            tag = explicitTag.Trim();
        }

        // Determine registry host vs repository path
        string registryHost;
        string repository;

        var firstSlash = cleaned.IndexOf('/');
        if (firstSlash > 0)
        {
            var potentialDomain = cleaned[..firstSlash];
            if (potentialDomain.Contains('.') || potentialDomain.Contains(':') || string.Equals(potentialDomain, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                registryHost = potentialDomain;
                repository = cleaned[(firstSlash + 1)..];

                if (string.Equals(registryHost, "docker.io", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(registryHost, "index.docker.io", StringComparison.OrdinalIgnoreCase))
                {
                    registryHost = "registry-1.docker.io";
                    if (!repository.Contains('/'))
                    {
                        repository = $"library/{repository}";
                    }
                }
            }
            else
            {
                // Docker Hub user/repo
                registryHost = "registry-1.docker.io";
                repository = cleaned;
            }
        }
        else
        {
            // Official library image on Docker Hub
            registryHost = "registry-1.docker.io";
            repository = $"library/{cleaned}";
        }

        return (registryHost, repository, string.IsNullOrWhiteSpace(tag) ? "latest" : tag);
    }

    private async Task<string?> AcquireBearerTokenAsync(
        HttpClient client,
        AuthenticationHeaderValue authHeader,
        string registryHost,
        string repository,
        CancellationToken cancellationToken)
    {
        try
        {
            var parameter = authHeader.Parameter;
            if (string.IsNullOrWhiteSpace(parameter))
            {
                return null;
            }

            var realmMatch = Regex.Match(parameter, @"realm=""([^""]+)""");
            var serviceMatch = Regex.Match(parameter, @"service=""([^""]+)""");
            var scopeMatch = Regex.Match(parameter, @"scope=""([^""]+)""");

            var realm = realmMatch.Success ? realmMatch.Groups[1].Value : null;
            if (string.IsNullOrWhiteSpace(realm))
            {
                return null;
            }

            var service = serviceMatch.Success ? serviceMatch.Groups[1].Value : registryHost;
            var scope = scopeMatch.Success ? scopeMatch.Groups[1].Value : $"repository:{repository}:pull";

            var tokenUrl = $"{realm}?service={Uri.EscapeDataString(service)}&scope={Uri.EscapeDataString(scope)}";
            var response = await client.GetAsync(tokenUrl, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), cancellationToken: cancellationToken).ConfigureAwait(false);
            if (doc.RootElement.TryGetProperty("token", out var tokenProp))
            {
                return tokenProp.GetString();
            }
            if (doc.RootElement.TryGetProperty("access_token", out var accessTokenProp))
            {
                return accessTokenProp.GetString();
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to authenticate with registry realm for {Host}", registryHost);
            return null;
        }
    }
}
