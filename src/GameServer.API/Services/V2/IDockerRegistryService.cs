namespace GameServer.API.Services.V2;

/// <summary>
/// Service for querying remote OCI/Docker registries to inspect tag digests.
/// </summary>
public interface IDockerRegistryService
{
    /// <summary>
    /// Gets the manifest digest (e.g., sha256:...) of a remote image tag from its source registry.
    /// </summary>
    /// <param name="imageReference">The image reference (e.g. "vinanrra/7dtd-server" or "ghcr.io/user/repo").</param>
    /// <param name="tag">The version tag (e.g. "latest" or "1.2.3").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The remote SHA256 digest string if found, or null if unreachable.</returns>
    Task<string?> GetRemoteDigestAsync(string imageReference, string? tag = null, CancellationToken cancellationToken = default);
}
