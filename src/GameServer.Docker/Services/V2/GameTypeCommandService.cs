using GameServer.Docker.Dtos.V2;
using GameServer.Docker.Models.V2;
using GameServer.Docker.Repositories.V2;

namespace GameServer.Docker.Services.V2;

public sealed class GameTypeCommandService(IGameTypeRepository repository)
{
    /// <summary>
    /// Creates a new V2 GameType.
    /// </summary>
    public async Task<GameTypeDetailDto> CreateAsync(SaveGameTypeRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var gameType = await repository.CreateAsync(new GameType
        {
            Key = request.Key,
            DisplayName = request.DisplayName,
            Description = request.Description,
            ImageReference = request.ImageReference,
            ThumbnailUrl = request.ThumbnailUrl,
            DocumentationUrl = request.DocumentationUrl,
            IsActive = request.IsActive
        });

        cancellationToken.ThrowIfCancellationRequested();
        return MapToDetail(gameType);
    }

    /// <summary>
    /// Updates an existing V2 GameType.
    /// </summary>
    public async Task<GameTypeDetailDto> UpdateAsync(string key, SaveGameTypeRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(key, request.Key, StringComparison.Ordinal))
        {
            throw new ArgumentException("The route key must match the payload key.", nameof(key));
        }

        var existing = await repository.GetByKeyAsync(key) ?? throw new KeyNotFoundException($"V2 GameType '{key}' was not found");

        var updated = await repository.UpdateAsync(existing with
        {
            DisplayName = request.DisplayName,
            Description = request.Description,
            ImageReference = request.ImageReference,
            ThumbnailUrl = request.ThumbnailUrl,
            DocumentationUrl = request.DocumentationUrl,
            IsActive = request.IsActive
        });

        cancellationToken.ThrowIfCancellationRequested();
        return MapToDetail(updated);
    }

    /// <summary>
    /// Adds a revision to an existing V2 GameType.
    /// </summary>
    public async Task<GameTypeRevisionDto> AddRevisionAsync(string key, SaveGameTypeRevisionRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var revision = await repository.AddRevisionAsync(key, MapToModel(request));
        return MapToDto(revision);
    }

    /// <summary>
    /// Updates an existing revision for a V2 GameType.
    /// </summary>
    public async Task<GameTypeRevisionDto> UpdateRevisionAsync(string key, int revisionId, SaveGameTypeRevisionRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var revision = await repository.UpdateRevisionAsync(key, MapToModel(request) with { Id = revisionId });
        return MapToDto(revision);
    }

    /// <summary>
    /// Marks a revision as published and optionally makes it current.
    /// </summary>
    public async Task<GameTypeRevisionDto> PublishRevisionAsync(string key, int revisionId, bool setAsCurrentRevision, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        var gameType = await repository.GetByKeyAsync(key) ?? throw new KeyNotFoundException($"V2 GameType '{key}' was not found");
        var revision = gameType.Revisions.FirstOrDefault(x => x.Id == revisionId) ?? throw new KeyNotFoundException($"V2 GameType revision '{revisionId}' was not found for '{key}'");

        var publishedRevision = await repository.UpdateRevisionAsync(key, revision with { IsPublished = true });
        if (setAsCurrentRevision)
        {
            await repository.SetCurrentRevisionAsync(key, revisionId);
        }

        return MapToDto(publishedRevision);
    }

    /// <summary>
    /// Sets the current revision for a V2 GameType.
    /// </summary>
    public async Task SetCurrentRevisionAsync(string key, int revisionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();
        await repository.SetCurrentRevisionAsync(key, revisionId);
    }

    private static GameTypeRevision MapToModel(SaveGameTypeRevisionRequestDto request)
    {
        return new GameTypeRevision
        {
            VersionTag = request.VersionTag,
            ImageDigest = request.ImageDigest,
            EnableTTY = request.EnableTTY,
            Notes = request.Notes,
            IsPublished = request.IsPublished,
            Ports = request.Ports.Select(x => new GameTypePort
            {
                Id = x.Id,
                ContainerPort = x.ContainerPort,
                Protocol = x.Protocol,
                AdvertisedPort = x.AdvertisedPort,
                Description = x.Description,
                DisplayOrder = x.DisplayOrder
            }).ToList(),
            Volumes = request.Volumes.Select(x => new GameTypeVolume
            {
                Id = x.Id,
                Source = x.Source,
                Description = x.Description,
                DisplayOrder = x.DisplayOrder,
                Usage = x.Usage
            }).ToList(),
            SettingDefinitions = request.SettingDefinitions.Select(x => new GameTypeSettingDefinition
            {
                Id = x.Id,
                SettingKey = x.SettingKey,
                DefaultValue = x.DefaultValue,
                Description = x.Description,
                DisplayOrder = x.DisplayOrder,
                Metadata = x.Metadata is null ? null : new GameTypeSettingMetadata
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
                    PortMappings = x.Metadata.PortMappings.Select(pm => new GameTypeSettingPortMapping
                    {
                        Id = pm.Id,
                        MappingRole = Enum.TryParse<GameTypeSettingPortMappingRole>(pm.MappingRole, ignoreCase: true, out var role) ? role : GameTypeSettingPortMappingRole.Primary,
                        RelationType = Enum.TryParse<GameTypeSettingPortRelationType>(pm.RelationType, ignoreCase: true, out var relationType) ? relationType : GameTypeSettingPortRelationType.Direct,
                        TargetContainerPort = pm.TargetContainerPort,
                        TargetProtocol = pm.TargetProtocol,
                        CalculationValue = pm.CalculationValue,
                        Description = pm.Description,
                        IsRequired = pm.IsRequired,
                        DisplayOrder = pm.DisplayOrder
                    }).ToList()
                }
            }).ToList(),
            WebHosts = request.WebHosts.Select(x => new GameTypeWebHost
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                PathSegment = x.PathSegment,
                ContainerPort = x.ContainerPort,
                ContainerPortVariable = x.ContainerPortVariable,
                EnabledWhen = x.EnabledWhen,
                DisplayOrder = x.DisplayOrder
            }).ToList()
        };
    }

    private static GameTypeDetailDto MapToDetail(GameType gameType)
    {
        return new GameTypeDetailDto
        {
            Id = gameType.Id,
            Key = gameType.Key,
            DisplayName = gameType.DisplayName,
            Description = gameType.Description,
            ImageReference = gameType.ImageReference,
            ThumbnailUrl = gameType.ThumbnailUrl,
            DocumentationUrl = gameType.DocumentationUrl,
            IsActive = gameType.IsActive,
            CurrentRevisionId = gameType.CurrentRevisionId,
            CreatedAt = gameType.CreatedAt,
            UpdatedAt = gameType.UpdatedAt,
            Revisions = gameType.Revisions.Select(MapToDto).ToList()
        };
    }

    private static GameTypeRevisionDto MapToDto(GameTypeRevision revision)
    {
        return new GameTypeRevisionDto
        {
            Id = revision.Id,
            VersionTag = revision.VersionTag,
            ImageDigest = revision.ImageDigest,
            EnableTTY = revision.EnableTTY,
            Notes = revision.Notes,
            IsPublished = revision.IsPublished,
            CreatedAt = revision.CreatedAt,
            Ports = revision.Ports.Select(x => new GameTypePortDto
            {
                Id = x.Id,
                ContainerPort = x.ContainerPort,
                Protocol = x.Protocol,
                AdvertisedPort = x.AdvertisedPort,
                Description = x.Description,
                DisplayOrder = x.DisplayOrder
            }).ToList(),
            Volumes = revision.Volumes.Select(x => new GameTypeVolumeDto
            {
                Id = x.Id,
                Source = x.Source,
                Description = x.Description,
                DisplayOrder = x.DisplayOrder,
                Usage = x.Usage
            }).ToList(),
            SettingDefinitions = revision.SettingDefinitions.Select(x => new GameTypeSettingDefinitionDto
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
                    PortMappings = x.Metadata.PortMappings.Select(pm => new GameTypeSettingPortMappingDto
                    {
                        Id = pm.Id,
                        MappingRole = pm.MappingRole.ToString(),
                        RelationType = pm.RelationType.ToString(),
                        TargetContainerPort = pm.TargetContainerPort,
                        TargetProtocol = pm.TargetProtocol,
                        CalculationValue = pm.CalculationValue,
                        Description = pm.Description,
                        IsRequired = pm.IsRequired,
                        DisplayOrder = pm.DisplayOrder
                    }).ToList()
                }
            }).ToList(),
            WebHosts = revision.WebHosts.Select(x => new GameTypeWebHostDto
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                PathSegment = x.PathSegment,
                ContainerPort = x.ContainerPort,
                ContainerPortVariable = x.ContainerPortVariable,
                EnabledWhen = x.EnabledWhen,
                DisplayOrder = x.DisplayOrder
            }).ToList()
        };
    }
}
