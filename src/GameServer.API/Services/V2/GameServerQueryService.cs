using GameServer.API.Dtos.V2;
using GameServer.API.Repositories.V2;
using GameServerModel = GameServer.API.Models.V2.GameServer;
using GameTypeModel = GameServer.API.Models.V2.GameType;
using GameTypeRevisionModel = GameServer.API.Models.V2.GameTypeRevision;

namespace GameServer.API.Services.V2;

public sealed class GameServerQueryService(
    IGameServerRepository gameServerRepository,
    IGameTypeRepository gameTypeRepository,
    IGameServerResourceCollector? resourceCollector = null,
    Interfaces.IGameServerReadinessWatcherService? readinessWatcher = null)
{
    /// <summary>
    /// Gets the V2 GameServer list payload.
    /// </summary>
    public async Task<IReadOnlyList<GameServerListItemDto>> GetListAsync(bool includeDeleted = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var servers = await gameServerRepository.GetAllAsync(includeDeleted).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var gameTypes = await gameTypeRepository.GetAllAsync(includeInactive: true).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var revisionIndex = BuildRevisionIndex(gameTypes);
        return servers
            .Select(server => MapListItem(server, ResolveRevisionContext(server.GameTypeRevisionId, revisionIndex)))
            .ToList();
    }

    /// <summary>
    /// Gets the V2 GameServer detail payload for a specific server id.
    /// </summary>
    public async Task<GameServerDetailDto?> GetByServerIdAsync(string serverId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);
        cancellationToken.ThrowIfCancellationRequested();

        var server = await gameServerRepository.GetByServerIdAsync(serverId).ConfigureAwait(false);
        if (server is null)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var gameTypes = await gameTypeRepository.GetAllAsync(includeInactive: true).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var revisionIndex = BuildRevisionIndex(gameTypes);
        return MapDetail(server, ResolveRevisionContext(server.GameTypeRevisionId, revisionIndex));
    }

    private static Dictionary<int, RevisionContext> BuildRevisionIndex(IEnumerable<GameTypeModel> gameTypes)
    {
        ArgumentNullException.ThrowIfNull(gameTypes);

        return gameTypes
            .SelectMany(gameType => gameType.Revisions.Select(revision => new RevisionContext(gameType, revision)))
            .ToDictionary(context => context.Revision.Id);
    }

    private static RevisionContext? ResolveRevisionContext(int revisionId, IReadOnlyDictionary<int, RevisionContext> revisionIndex)
    {
        ArgumentNullException.ThrowIfNull(revisionIndex);
        return revisionIndex.TryGetValue(revisionId, out var context) ? context : null;
    }

    private string ResolveEffectiveStatus(string serverId, string fallbackStatus)
    {
        if (readinessWatcher?.IsServerReady(serverId) == true)
        {
            return "Available";
        }

        if (resourceCollector == null) return fallbackStatus;
        var cached = resourceCollector.GetCachedUsage(serverId);
        if (cached == null || string.IsNullOrWhiteSpace(cached.ServiceStatus))
        {
            return fallbackStatus;
        }

        if (string.Equals(fallbackStatus, "Available", StringComparison.OrdinalIgnoreCase)
            && string.Equals(cached.ServiceStatus, "Running", StringComparison.OrdinalIgnoreCase))
        {
            return "Available";
        }

        return cached.ServiceStatus;
    }

    private GameServerListItemDto MapListItem(GameServerModel server, RevisionContext? revisionContext)
    {
        ArgumentNullException.ThrowIfNull(server);

        return new GameServerListItemDto
        {
            Id = server.Id,
            ServerId = server.ServerId,
            Name = server.Name,
            Description = server.Description,
            GameTypeRevisionId = server.GameTypeRevisionId,
            ServiceName = server.ServiceName,
            Status = ResolveEffectiveStatus(server.ServerId, server.Status),
            CreatedAt = server.CreatedAt,
            UpdatedAt = server.UpdatedAt,
            LastDeployedAt = server.LastDeployedAt,
            LastSeenAt = server.LastSeenAt,
            IsDeleted = server.IsDeleted,
            GameTypeKey = revisionContext?.GameType.Key,
            GameTypeDisplayName = revisionContext?.GameType.DisplayName,
            GameTypeThumbnailUrl = revisionContext?.GameType.ThumbnailUrl,
            RevisionVersionTag = revisionContext?.Revision.VersionTag,
            RevisionImageReference = revisionContext?.Revision.ImageReference,
            Ports = server.Ports
                .OrderBy(port => port.ContainerPort)
                .Select(port => new GameServerPortDto
                {
                    ContainerPort = port.ContainerPort,
                    Protocol = port.Protocol,
                    PublishedPort = port.PublishedPort
                })
                .ToList(),
            ResolvedPorts = MapResolvedPorts(server, revisionContext)
        };
    }

    private GameServerDetailDto MapDetail(GameServerModel server, RevisionContext? revisionContext)
    {
        ArgumentNullException.ThrowIfNull(server);

        return new GameServerDetailDto
        {
            Id = server.Id,
            ServerId = server.ServerId,
            Name = server.Name,
            Description = server.Description,
            GameTypeRevisionId = server.GameTypeRevisionId,
            ServiceName = server.ServiceName,
            Status = ResolveEffectiveStatus(server.ServerId, server.Status),
            CreatedAt = server.CreatedAt,
            UpdatedAt = server.UpdatedAt,
            LastDeployedAt = server.LastDeployedAt,
            LastSeenAt = server.LastSeenAt,
            IsDeleted = server.IsDeleted,
            GameTypeKey = revisionContext?.GameType.Key,
            GameTypeDisplayName = revisionContext?.GameType.DisplayName,
            GameTypeDescription = revisionContext?.GameType.Description,
            GameTypeThumbnailUrl = revisionContext?.GameType.ThumbnailUrl,
            RevisionVersionTag = revisionContext?.Revision.VersionTag,
            RevisionImageReference = revisionContext?.Revision.ImageReference,
            Settings = server.Settings
                .OrderBy(setting => setting.SettingKey, StringComparer.OrdinalIgnoreCase)
                .Select(setting => new GameServerSettingDto
                {
                    Id = setting.Id,
                    SettingKey = setting.SettingKey,
                    Value = setting.Value
                })
                .ToList(),
            Ports = server.Ports
                .OrderBy(port => port.ContainerPort)
                .Select(port => new GameServerPortDto
                {
                    ContainerPort = port.ContainerPort,
                    Protocol = port.Protocol,
                    PublishedPort = port.PublishedPort
                })
                .ToList(),
            ResolvedPorts = MapResolvedPorts(server, revisionContext),
            ResolvedVolumes = server.Volumes
                .OrderBy(volume => volume.CreatedAt)
                .Select(MapServerVolume)
                .ToList(),
            ResolvedWebHosts = revisionContext?.Revision.WebHosts
                .OrderBy(webHost => webHost.DisplayOrder)
                .Select(webHost => new GameServerResolvedWebHostDto
                {
                    Name = webHost.Name,
                    Description = webHost.Description,
                    PathSegment = webHost.PathSegment,
                    ContainerPort = webHost.ContainerPort,
                    ContainerPortVariable = webHost.ContainerPortVariable,
                    EnabledWhen = webHost.EnabledWhen,
                    DisplayOrder = webHost.DisplayOrder
                })
                .ToList()
                ?? [],
            DockerVolumeOptions = [],
            NetworkOptions = [],
            ConfigurationRules = []
        };
    }

    private static List<GameServerResolvedPortDto> MapResolvedPorts(GameServerModel server, RevisionContext? revisionContext)
    {
        if (revisionContext?.Revision?.Ports == null)
        {
            return [];
        }

        var savedPorts = server.Ports ?? [];

        return revisionContext.Revision.Ports
            .OrderBy(port => port.DisplayOrder)
            .Select(port =>
            {
                var savedPort = savedPorts.FirstOrDefault(p =>
                    p.ContainerPort == port.ContainerPort && string.Equals(p.Protocol, port.Protocol, StringComparison.OrdinalIgnoreCase))
                    ?? savedPorts.FirstOrDefault(p => string.Equals(p.Protocol, port.Protocol, StringComparison.OrdinalIgnoreCase));

                var publishedPort = (savedPort != null && savedPort.PublishedPort > 0) ? savedPort.PublishedPort : port.ContainerPort;

                return new GameServerResolvedPortDto
                {
                    ContainerPort = port.ContainerPort,
                    Protocol = port.Protocol,
                    PublishedPort = publishedPort,
                    AdvertisedPort = port.AdvertisedPort,
                    Description = port.Description,
                    DisplayOrder = port.DisplayOrder
                };
            })
            .ToList();
    }

    private static GameServerResolvedVolumeDto MapServerVolume(GameServer.API.Models.V2.GameServerVolume volume)
    {
        ArgumentNullException.ThrowIfNull(volume);

        return new GameServerResolvedVolumeDto
        {
            Usage = volume.Usage,
            VolumeName = volume.VolumeName,
            ContainerPath = volume.ContainerPath,
            MountType = volume.MountType.ToString().ToLowerInvariant(),
            ReadOnly = volume.ReadOnly,
            DriverOptionsJson = volume.DriverOptionsJson,
            IsProvisioned = volume.IsProvisioned,
            CreatedAt = volume.CreatedAt
        };
    }

    private sealed record RevisionContext(GameTypeModel GameType, GameTypeRevisionModel Revision);
}
