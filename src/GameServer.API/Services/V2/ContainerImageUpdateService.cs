using System.Collections.Concurrent;
using System.Security.Claims;
using GameServer.API.Dtos.V2;
using GameServer.API.Interfaces;
using GameServer.API.Repositories.V2;

namespace GameServer.API.Services.V2;

/// <summary>
/// Service that checks and applies container image tag updates against remote registry sources.
/// </summary>
public sealed class ContainerImageUpdateService : IContainerImageUpdateService
{
    private readonly IGameServerRepository _gameServerRepository;
    private readonly IGameTypeRepository _gameTypeRepository;
    private readonly IDockerRegistryService _registryService;
    private readonly GameServerDeploymentService _deploymentService;
    private readonly GameServerQueryService _queryService;
    private readonly IServerResourceMonitor? _resourceMonitor;
    private readonly INodeAgentDiscovery? _nodeAgentDiscovery;
    private readonly ILogger<ContainerImageUpdateService> _logger;

    private readonly ConcurrentDictionary<string, (ContainerImageUpdateStatusDto Status, DateTime CachedAt)> _cache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public ContainerImageUpdateService(
        IGameServerRepository gameServerRepository,
        IGameTypeRepository gameTypeRepository,
        IDockerRegistryService registryService,
        GameServerDeploymentService deploymentService,
        GameServerQueryService queryService,
        ILogger<ContainerImageUpdateService> logger,
        IServerResourceMonitor? resourceMonitor = null,
        INodeAgentDiscovery? nodeAgentDiscovery = null)
    {
        _gameServerRepository = gameServerRepository;
        _gameTypeRepository = gameTypeRepository;
        _registryService = registryService;
        _deploymentService = deploymentService;
        _queryService = queryService;
        _logger = logger;
        _resourceMonitor = resourceMonitor;
        _nodeAgentDiscovery = nodeAgentDiscovery;
    }

    /// <inheritdoc />
    public ContainerImageUpdateStatusDto? GetCachedUpdateStatus(string serverId)
    {
        if (string.IsNullOrWhiteSpace(serverId))
        {
            return null;
        }

        if (_cache.TryGetValue(serverId, out var entry) && (DateTime.UtcNow - entry.CachedAt) <= CacheTtl)
        {
            return entry.Status;
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<ContainerImageUpdateStatusDto> CheckImageUpdateAsync(string serverId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);
        cancellationToken.ThrowIfCancellationRequested();

        var server = await _gameServerRepository.GetByServerIdAsync(serverId).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"V2 GameServer '{serverId}' was not found");

        var gameTypes = await _gameTypeRepository.GetAllAsync(includeInactive: true).ConfigureAwait(false);
        var revision = gameTypes.SelectMany(gt => gt.Revisions).FirstOrDefault(r => r.Id == server.GameTypeRevisionId);

        if (revision == null || string.IsNullOrWhiteSpace(revision.ImageReference))
        {
            return new ContainerImageUpdateStatusDto
            {
                ServerId = serverId,
                ImageReference = string.Empty,
                VersionTag = string.Empty,
                IsUpdateAvailable = false,
                LastCheckedAt = DateTime.UtcNow
            };
        }

        var imageReference = revision.ImageReference;
        var versionTag = !string.IsNullOrWhiteSpace(revision.VersionTag) ? revision.VersionTag : "latest";

        // Determine current container digest
        var currentDigest = ResolveCurrentContainerDigest(server, revision);

        // Query remote registry
        var remoteDigest = await _registryService.GetRemoteDigestAsync(imageReference, versionTag, cancellationToken).ConfigureAwait(false);

        var isUpdateAvailable = false;
        if (!string.IsNullOrWhiteSpace(remoteDigest) && !string.IsNullOrWhiteSpace(currentDigest))
        {
            isUpdateAvailable = !AreDigestsEqual(remoteDigest, currentDigest);
        }

        var status = new ContainerImageUpdateStatusDto
        {
            ServerId = serverId,
            ImageReference = imageReference,
            VersionTag = versionTag,
            CurrentDigest = currentDigest,
            LatestDigest = remoteDigest,
            IsUpdateAvailable = isUpdateAvailable,
            LastCheckedAt = DateTime.UtcNow
        };

        _cache[serverId] = (status, DateTime.UtcNow);
        return status;
    }

    /// <inheritdoc />
    public async Task<GameServerDetailDto> UpdateContainerImageAsync(string serverId, ClaimsPrincipal? user = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation("Updating container image for GameServer {ServerId} to latest remote release", serverId);

        // Clear cache
        _cache.TryRemove(serverId, out _);

        // Trigger redeployment with force update
        await _deploymentService.UpdateDeploymentAsync(serverId, forceUpdate: true, cancellationToken: cancellationToken).ConfigureAwait(false);

        var updatedDetail = await _queryService.GetByServerIdAsync(serverId, user, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"V2 GameServer '{serverId}' was not found");

        return updatedDetail;
    }

    private static string? ResolveCurrentContainerDigest(
        Models.V2.GameServer server,
        Models.V2.GameTypeRevision revision)
    {
        if (!string.IsNullOrWhiteSpace(revision.ImageDigest))
        {
            return revision.ImageDigest;
        }

        return null;
    }

    private static bool AreDigestsEqual(string digestA, string digestB)
    {
        var cleanA = CleanDigest(digestA);
        var cleanB = CleanDigest(digestB);
        return string.Equals(cleanA, cleanB, StringComparison.OrdinalIgnoreCase);
    }

    private static string CleanDigest(string digest)
    {
        if (string.IsNullOrWhiteSpace(digest)) return string.Empty;
        var trimmed = digest.Trim();
        var idx = trimmed.IndexOf('@', StringComparison.Ordinal);
        if (idx >= 0)
        {
            trimmed = trimmed[(idx + 1)..];
        }
        return trimmed;
    }
}
