using GameServer.Docker.Dtos.V2;
using GameServer.Docker.Repositories.V2;
using GameServerModel = GameServer.Docker.Models.V2.GameServer;
using GameTypeModel = GameServer.Docker.Models.V2.GameType;
using GameTypeRevisionModel = GameServer.Docker.Models.V2.GameTypeRevision;

namespace GameServer.Docker.Services.V2;

public sealed class GameServerQueryService(IGameServerRepository gameServerRepository, IGameTypeRepository gameTypeRepository)
{
    /// <summary>
    /// Gets the V2 GameServer list payload.
    /// </summary>
    public async Task<IReadOnlyList<GameServerListItemDto>> GetListAsync(bool includeDeleted = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var serversTask = gameServerRepository.GetAllAsync(includeDeleted);
        var gameTypesTask = gameTypeRepository.GetAllAsync(includeInactive: true);
        await Task.WhenAll(serversTask, gameTypesTask);

        cancellationToken.ThrowIfCancellationRequested();

        var revisionIndex = BuildRevisionIndex(await gameTypesTask.ConfigureAwait(false));
        return (await serversTask.ConfigureAwait(false))
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

        var serverTask = gameServerRepository.GetByServerIdAsync(serverId);
        var gameTypesTask = gameTypeRepository.GetAllAsync(includeInactive: true);
        await Task.WhenAll(serverTask, gameTypesTask);

        cancellationToken.ThrowIfCancellationRequested();

        var server = await serverTask.ConfigureAwait(false);
        if (server is null)
        {
            return null;
        }

        var revisionIndex = BuildRevisionIndex(await gameTypesTask.ConfigureAwait(false));
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

    private static GameServerListItemDto MapListItem(GameServerModel server, RevisionContext? revisionContext)
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
            Status = server.Status,
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
            ResolvedPorts = revisionContext?.Revision.Ports
                .OrderBy(port => port.DisplayOrder)
                .Select(MapResolvedPort)
                .ToList()
                ?? []
        };
    }

    private static GameServerDetailDto MapDetail(GameServerModel server, RevisionContext? revisionContext)
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
            Status = server.Status,
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
            ResolvedPorts = revisionContext?.Revision.Ports
                .OrderBy(port => port.DisplayOrder)
                .Select(MapResolvedPort)
                .ToList()
                ?? [],
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

    private static GameServerResolvedPortDto MapResolvedPort(GameServer.Docker.Models.V2.GameTypePort port)
    {
        ArgumentNullException.ThrowIfNull(port);

        return new GameServerResolvedPortDto
        {
            ContainerPort = port.ContainerPort,
            Protocol = port.Protocol,
            AdvertisedPort = port.AdvertisedPort,
            Description = port.Description,
            DisplayOrder = port.DisplayOrder
        };
    }

    private static GameServerResolvedVolumeDto MapServerVolume(GameServer.Docker.Models.V2.GameServerVolume volume)
    {
        ArgumentNullException.ThrowIfNull(volume);

        return new GameServerResolvedVolumeDto
        {
            Usage = volume.Usage,
            ContainerPath = volume.ContainerPath,
            Source = volume.Source,
            MountType = volume.MountType.ToString().ToLowerInvariant(),
            ReadOnly = volume.ReadOnly,
            Driver = volume.Driver,
            DriverOptionsJson = volume.DriverOptionsJson,
            OwnerUid = volume.OwnerUid,
            OwnerGid = volume.OwnerGid,
            Permissions = volume.Permissions,
            InitMode = volume.InitMode.ToString().ToLowerInvariant(),
            SeedSourcePath = volume.SeedSourcePath,
            IsProvisioned = volume.IsProvisioned,
            CreatedAt = volume.CreatedAt
        };
    }

    private sealed record RevisionContext(GameTypeModel GameType, GameTypeRevisionModel Revision);
}
