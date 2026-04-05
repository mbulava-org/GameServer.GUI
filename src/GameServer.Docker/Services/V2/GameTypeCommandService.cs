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
            Type = request.Type,
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
            Type = request.Type,
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

        ValidateRevisionRequest(request);

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

        ValidateRevisionRequest(request);

        var revision = await repository.UpdateRevisionAsync(key, MapToModel(request) with { Id = revisionId });
        return MapToDto(revision);
    }

    private static void ValidateRevisionRequest(SaveGameTypeRevisionRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ports = request.Ports
            .Select(port => new RevisionPortIdentity(port.ContainerPort, NormalizeProtocol(port.Protocol)))
            .ToHashSet();

        foreach (var setting in request.SettingDefinitions)
        {
            if (string.IsNullOrWhiteSpace(setting.SettingKey) || setting.Metadata is null)
            {
                continue;
            }

            var isPortSetting = string.Equals(setting.Metadata.DataType, "port", StringComparison.OrdinalIgnoreCase);
            var mappings = setting.Metadata.PortMappings;
            if (isPortSetting && mappings.Count == 0)
            {
                throw new ArgumentException($"Port setting '{setting.SettingKey}' must define at least one port mapping.", nameof(request));
            }

            if (mappings.Count == 0)
            {
                continue;
            }

            var primaryMappings = mappings
                .Where(mapping => string.Equals(mapping.MappingRole, GameTypeSettingPortMappingRole.Primary.ToString(), StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (primaryMappings.Count != 1)
            {
                throw new ArgumentException($"Setting '{setting.SettingKey}' must define exactly one primary port mapping.", nameof(request));
            }

            var primaryMapping = primaryMappings[0];
            if (!string.Equals(primaryMapping.RelationType, GameTypeSettingPortRelationType.Direct.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Setting '{setting.SettingKey}' must use a direct relation for its primary port mapping.", nameof(request));
            }

            var primaryPortIdentity = new RevisionPortIdentity(primaryMapping.TargetContainerPort, NormalizeProtocol(primaryMapping.TargetProtocol));
            if (!ports.Contains(primaryPortIdentity))
            {
                throw new ArgumentException($"Setting '{setting.SettingKey}' references missing target port '{primaryMapping.TargetContainerPort}/{primaryPortIdentity.Protocol}'.", nameof(request));
            }

            var hasParsedDefaultPort = int.TryParse(setting.DefaultValue, out var defaultPort) && defaultPort > 0;
            if (isPortSetting && !hasParsedDefaultPort)
            {
                throw new ArgumentException($"Port setting '{setting.SettingKey}' must have a numeric default value before port mappings can be validated.", nameof(request));
            }

            if (isPortSetting && primaryMapping.TargetContainerPort != defaultPort)
            {
                throw new ArgumentException($"Port setting '{setting.SettingKey}' must directly map its primary rule to '{defaultPort}/{primaryPortIdentity.Protocol}'.", nameof(request));
            }

            foreach (var mapping in mappings.Where(mapping => !ReferenceEquals(mapping, primaryMapping)))
            {
                if (!string.Equals(mapping.MappingRole, GameTypeSettingPortMappingRole.Related.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException($"Setting '{setting.SettingKey}' can only use related mappings after the primary mapping.", nameof(request));
                }

                var isOffset = string.Equals(mapping.RelationType, GameTypeSettingPortRelationType.Offset.ToString(), StringComparison.OrdinalIgnoreCase);
                var isMultiplier = string.Equals(mapping.RelationType, GameTypeSettingPortRelationType.Multiplier.ToString(), StringComparison.OrdinalIgnoreCase);
                if (!isOffset && !isMultiplier)
                {
                    throw new ArgumentException($"Setting '{setting.SettingKey}' can only use offset or multiplier relations for related port mappings.", nameof(request));
                }

                if (!mapping.CalculationValue.HasValue)
                {
                    throw new ArgumentException($"Setting '{setting.SettingKey}' must define a calculation value for related port mapping '{mapping.TargetContainerPort}/{NormalizeProtocol(mapping.TargetProtocol)}'.", nameof(request));
                }

                var targetPortIdentity = new RevisionPortIdentity(mapping.TargetContainerPort, NormalizeProtocol(mapping.TargetProtocol));
                if (!ports.Contains(targetPortIdentity))
                {
                    throw new ArgumentException($"Setting '{setting.SettingKey}' references missing default related port '{mapping.TargetContainerPort}/{targetPortIdentity.Protocol}'.", nameof(request));
                }

                if (!isPortSetting)
                {
                    continue;
                }

                var expectedPort = isMultiplier
                    ? defaultPort * mapping.CalculationValue.Value
                    : defaultPort + mapping.CalculationValue.Value;

                if (mapping.TargetContainerPort != expectedPort)
                {
                    throw new ArgumentException($"Setting '{setting.SettingKey}' has default related port '{mapping.TargetContainerPort}/{targetPortIdentity.Protocol}' that does not match the calculated port '{expectedPort}/{targetPortIdentity.Protocol}'.", nameof(request));
                }
            }
        }
    }

    private static string NormalizeProtocol(string? protocol)
    {
        return string.IsNullOrWhiteSpace(protocol) ? string.Empty : protocol.Trim().ToLowerInvariant();
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
            ImageReference = request.ImageReference,
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
                        Description = null,
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
            Type = gameType.Type,
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
            ImageReference = revision.ImageReference,
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

    private sealed record RevisionPortIdentity(int ContainerPort, string Protocol);
}
