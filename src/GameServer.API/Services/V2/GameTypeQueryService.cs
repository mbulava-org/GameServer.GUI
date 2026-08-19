using GameServer.API.Dtos.V2;
using GameServer.API.Models.V2;
using GameServer.API.Repositories.V2;

namespace GameServer.API.Services.V2;

public sealed class GameTypeQueryService(IGameTypeRepository repository)
{
    /// <summary>
    /// Gets the V2 GameType list view.
    /// </summary>
    public async Task<IReadOnlyList<GameTypeListItemDto>> GetListAsync(bool includeInactive, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var gameTypes = await repository.GetAllAsync(includeInactive);

        cancellationToken.ThrowIfCancellationRequested();
        return gameTypes.Select(MapToListItem).ToList();
    }

    /// <summary>
    /// Gets the full V2 GameType detail payload for the editor.
    /// </summary>
    public async Task<GameTypeDetailDto?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("A game type key is required.", nameof(key));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var gameType = await repository.GetByKeyAsync(key);
        if (gameType is null)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return MapToDetail(gameType);
    }

    /// <summary>
    /// Gets a portable V2 GameType package without persisted integer ids.
    /// </summary>
    public async Task<PortableGameTypePackageDto?> ExportAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("A game type key is required.", nameof(key));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var gameType = await repository.GetByKeyAsync(key);
        if (gameType is null)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        return new PortableGameTypePackageDto
        {
            GameType = MapToPortable(gameType)
        };
    }

    private static GameTypeListItemDto MapToListItem(GameType gameType)
    {
        ArgumentNullException.ThrowIfNull(gameType);

        var currentRevision = gameType.Revisions.FirstOrDefault(x => x.Id == gameType.CurrentRevisionId);

        return new GameTypeListItemDto
        {
            Id = gameType.Id,
            Key = gameType.Key,
            DisplayName = gameType.DisplayName,
            Description = gameType.Description,
            Type = gameType.Type,
            CurrentImageReference = currentRevision?.ImageReference,
            ThumbnailUrl = gameType.ThumbnailUrl,
            IsActive = gameType.IsActive,
            CurrentRevisionId = gameType.CurrentRevisionId,
            CurrentVersionTag = currentRevision?.VersionTag,
            RevisionCount = gameType.Revisions.Count,
            PublishedRevisionCount = gameType.Revisions.Count(x => x.IsPublished),
            UpdatedAt = gameType.UpdatedAt
        };
    }

    private static GameTypeDetailDto MapToDetail(GameType gameType)
    {
        ArgumentNullException.ThrowIfNull(gameType);

        return new GameTypeDetailDto
        {
            Id = gameType.Id,
            Key = gameType.Key,
            DisplayName = gameType.DisplayName,
            Description = gameType.Description,
            Type = gameType.Type,
            ThumbnailUrl = gameType.ThumbnailUrl,
            DocumentationUrl = gameType.DocumentationUrl,
            IsActive = gameType.IsActive,
            CurrentRevisionId = gameType.CurrentRevisionId,
            CreatedAt = gameType.CreatedAt,
            UpdatedAt = gameType.UpdatedAt,
            Revisions = gameType.Revisions
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.VersionTag)
                .Select(MapToDetail)
                .ToList()
        };
    }

    private static GameTypeRevisionDto MapToDetail(GameTypeRevision revision)
    {
        return new GameTypeRevisionDto
        {
            Id = revision.Id,
            VersionTag = revision.VersionTag,
            ImageReference = revision.ImageReference,
            ImageDigest = revision.ImageDigest,
            EnableTTY = revision.EnableTTY,
            Notes = revision.Notes,
            IsPublished = revision.IsPublished,
            CreatedAt = revision.CreatedAt,
            Ports = revision.Ports
                .OrderBy(x => x.DisplayOrder)
                .Select(x => new GameTypePortDto
                {
                    Id = x.Id,
                    ContainerPort = x.ContainerPort,
                    Protocol = x.Protocol,
                    AdvertisedPort = x.AdvertisedPort,
                    Description = x.Description,
                    DisplayOrder = x.DisplayOrder
                })
                .ToList(),
            Volumes = revision.Volumes
                .OrderBy(x => x.DisplayOrder)
                .Select(x => new GameTypeVolumeDto
                {
                    Id = x.Id,
                    Source = x.Source,
                    Description = x.Description,
                    DisplayOrder = x.DisplayOrder,
                    Usage = x.Usage,
                    MountType = x.MountType,
                    ReadOnly = x.ReadOnly,
                    OwnerUid = x.OwnerUid,
                    OwnerGid = x.OwnerGid,
                    OwnerUidVariable = x.OwnerUidVariable,
                    OwnerGidVariable = x.OwnerGidVariable,
                    Permissions = x.Permissions,
                    EnsureNfsPathExists = x.EnsureNfsPathExists,
                    Required = x.Required
                })
                .ToList(),
            SettingDefinitions = revision.SettingDefinitions
                .OrderBy(x => x.DisplayOrder)
                .Select(x => new GameTypeSettingDefinitionDto
                {
                    Id = x.Id,
                    SettingKey = x.SettingKey,
                    DefaultValue = x.DefaultValue,
                    Description = x.Description,
                    DisplayOrder = x.DisplayOrder,
                    Metadata = x.Metadata is null ? null : new GameTypeSettingMetadataDto
                    {
                        Id = x.Metadata.Id,
                        DataType = x.Metadata.DataType,
                        Category = x.Metadata.Category,
                        IsRequired = x.Metadata.IsRequired,
                        CannotBeEmpty = x.Metadata.CannotBeEmpty,
                        Placeholder = x.Metadata.Placeholder,
                        ValidationPattern = x.Metadata.ValidationPattern,
                        ValidationMessage = x.Metadata.ValidationMessage,
                        AutoAllocatePort = x.Metadata.AutoAllocatePort,
                        ValidateRelatedPortsAvailability = x.Metadata.ValidateRelatedPortsAvailability,
                        AllowedValuesJson = x.Metadata.AllowedValuesJson,
                        ValueMappingsJson = x.Metadata.ValueMappingsJson,
                        PortMappings = x.Metadata.PortMappings
                            .OrderBy(pm => pm.DisplayOrder)
                            .Select(pm => new GameTypeSettingPortMappingDto
                            {
                                Id = pm.Id,
                                MappingRole = pm.MappingRole.ToString(),
                                RelationType = pm.RelationType.ToString(),
                                TargetContainerPort = pm.TargetContainerPort,
                                TargetProtocol = pm.TargetProtocol,
                                CalculationValue = pm.CalculationValue,
                                IsRequired = pm.IsRequired,
                                DisplayOrder = pm.DisplayOrder
                            })
                            .ToList()
                    }
                })
                .ToList(),
            WebHosts = revision.WebHosts
                .OrderBy(x => x.DisplayOrder)
                .Select(x => new GameTypeWebHostDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    Description = x.Description,
                    PathSegment = x.PathSegment,
                    ContainerPort = x.ContainerPort,
                    ContainerPortVariable = x.ContainerPortVariable,
                    EnabledWhen = x.EnabledWhen,
                    DisplayOrder = x.DisplayOrder
                })
                .ToList()
        };
    }

    private static PortableGameTypeDto MapToPortable(GameType gameType)
    {
        ArgumentNullException.ThrowIfNull(gameType);

        var currentRevision = gameType.Revisions.FirstOrDefault(revision => revision.Id == gameType.CurrentRevisionId);

        return new PortableGameTypeDto
        {
            Key = gameType.Key,
            DisplayName = gameType.DisplayName,
            Description = gameType.Description,
            Type = gameType.Type,
            ThumbnailUrl = gameType.ThumbnailUrl,
            DocumentationUrl = gameType.DocumentationUrl,
            IsActive = gameType.IsActive,
            CurrentRevisionVersionTag = currentRevision?.VersionTag,
            Revisions = gameType.Revisions
                .OrderBy(revision => revision.CreatedAt)
                .ThenBy(revision => revision.VersionTag, StringComparer.OrdinalIgnoreCase)
                .Select(MapToPortable)
                .ToList()
        };
    }

    private static PortableGameTypeRevisionDto MapToPortable(GameTypeRevision revision)
    {
        ArgumentNullException.ThrowIfNull(revision);

        return new PortableGameTypeRevisionDto
        {
            VersionTag = revision.VersionTag,
            ImageReference = revision.ImageReference,
            ImageDigest = revision.ImageDigest,
            EnableTTY = revision.EnableTTY,
            Notes = revision.Notes,
            IsPublished = revision.IsPublished,
            Ports = revision.Ports
                .OrderBy(port => port.DisplayOrder)
                .Select(port => new PortableGameTypePortDto
                {
                    ContainerPort = port.ContainerPort,
                    Protocol = port.Protocol,
                    AdvertisedPort = port.AdvertisedPort,
                    Description = port.Description,
                    DisplayOrder = port.DisplayOrder
                })
                .ToList(),
            Volumes = revision.Volumes
                .OrderBy(volume => volume.DisplayOrder)
                .Select(volume => new PortableGameTypeVolumeDto
                {
                    Source = volume.Source,
                    Description = volume.Description,
                    DisplayOrder = volume.DisplayOrder,
                    Usage = volume.Usage,
                    MountType = volume.MountType,
                    ReadOnly = volume.ReadOnly,
                    OwnerUid = volume.OwnerUid,
                    OwnerGid = volume.OwnerGid,
                    OwnerUidVariable = volume.OwnerUidVariable,
                    OwnerGidVariable = volume.OwnerGidVariable,
                    Permissions = volume.Permissions,
                    EnsureNfsPathExists = volume.EnsureNfsPathExists,
                    Required = volume.Required
                })
                .ToList(),
            SettingDefinitions = revision.SettingDefinitions
                .OrderBy(setting => setting.DisplayOrder)
                .Select(setting => new PortableGameTypeSettingDefinitionDto
                {
                    SettingKey = setting.SettingKey,
                    DefaultValue = setting.DefaultValue,
                    Description = setting.Description,
                    DisplayOrder = setting.DisplayOrder,
                    Metadata = setting.Metadata is null ? null : new PortableGameTypeSettingMetadataDto
                    {
                        DataType = setting.Metadata.DataType,
                        Category = setting.Metadata.Category,
                        IsRequired = setting.Metadata.IsRequired,
                        CannotBeEmpty = setting.Metadata.CannotBeEmpty,
                        Placeholder = setting.Metadata.Placeholder,
                        ValidationPattern = setting.Metadata.ValidationPattern,
                        ValidationMessage = setting.Metadata.ValidationMessage,
                        AutoAllocatePort = setting.Metadata.AutoAllocatePort,
                        ValidateRelatedPortsAvailability = setting.Metadata.ValidateRelatedPortsAvailability,
                        AllowedValuesJson = setting.Metadata.AllowedValuesJson,
                        ValueMappingsJson = setting.Metadata.ValueMappingsJson,
                        PortMappings = setting.Metadata.PortMappings
                            .OrderBy(mapping => mapping.DisplayOrder)
                            .Select(mapping => new PortableGameTypeSettingPortMappingDto
                            {
                                MappingRole = mapping.MappingRole.ToString(),
                                RelationType = mapping.RelationType.ToString(),
                                TargetContainerPort = mapping.TargetContainerPort,
                                TargetProtocol = mapping.TargetProtocol,
                                CalculationValue = mapping.CalculationValue,
                                IsRequired = mapping.IsRequired,
                                DisplayOrder = mapping.DisplayOrder
                            })
                            .ToList()
                    }
                })
                .ToList(),
            WebHosts = revision.WebHosts
                .OrderBy(webHost => webHost.DisplayOrder)
                .Select(webHost => new PortableGameTypeWebHostDto
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
        };
    }
}
