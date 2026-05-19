using GameServer.Docker.Dtos.V2;
using GameServer.Docker.Models.V2;
using GameServer.Docker.Repositories.V2;
using System.Text.RegularExpressions;

namespace GameServer.Docker.Services.V2;

public sealed class GameTypeCommandService(IGameTypeRepository repository)
{
    private static readonly HashSet<string> SupportedWebHostPathVariables = new(StringComparer.OrdinalIgnoreCase)
    {
        "serverId",
        "name",
        "serviceName",
        "gameType"
    };

    private static readonly HashSet<string> SupportedWebHostPortSettingDataTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "number",
        "port"
    };

    private static readonly Regex WebHostPathVariableRegex = new("\\{(?<name>[A-Za-z][A-Za-z0-9]*)\\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

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
    /// Imports a portable V2 GameType package.
    /// </summary>
    public async Task<GameTypeDetailDto> ImportAsync(PortableGameTypePackageDto package, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(package.GameType);
        cancellationToken.ThrowIfCancellationRequested();

        var gameTypeModel = MapPortableToModel(package.GameType);
        var created = await repository.CreateAsync(gameTypeModel);

        var currentRevisionVersionTag = package.GameType.CurrentRevisionVersionTag?.Trim();
        if (!string.IsNullOrWhiteSpace(currentRevisionVersionTag))
        {
            var currentRevision = created.Revisions.FirstOrDefault(revision => string.Equals(revision.VersionTag, currentRevisionVersionTag, StringComparison.OrdinalIgnoreCase));
            if (currentRevision is null)
            {
                throw new ArgumentException($"Portable package references missing current revision version tag '{currentRevisionVersionTag}'.", nameof(package));
            }

            if (created.CurrentRevisionId != currentRevision.Id)
            {
                await repository.SetCurrentRevisionAsync(created.Key, currentRevision.Id);
                created = await repository.GetByKeyAsync(created.Key) ?? throw new InvalidOperationException("Failed to reload imported V2 GameType.");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return MapToDetail(created);
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
    /// Deletes an existing V2 GameType.
    /// </summary>
    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        await repository.DeleteAsync(key);
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
        var settingsByKey = request.SettingDefinitions
            .Where(setting => !string.IsNullOrWhiteSpace(setting.SettingKey))
            .GroupBy(setting => setting.SettingKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

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

                var expectedPort = isMultiplier
                    ? primaryMapping.TargetContainerPort * mapping.CalculationValue.Value
                    : primaryMapping.TargetContainerPort + mapping.CalculationValue.Value;

                if (mapping.TargetContainerPort != expectedPort)
                {
                    throw new ArgumentException($"Setting '{setting.SettingKey}' has default related port '{mapping.TargetContainerPort}/{targetPortIdentity.Protocol}' that does not match the calculated port '{expectedPort}/{targetPortIdentity.Protocol}'.", nameof(request));
                }
            }
        }

        foreach (var webHost in request.WebHosts)
        {
            ValidateWebHostRequest(webHost, settingsByKey, request);
        }
    }

    private static GameType MapPortableToModel(PortableGameTypeDto gameType)
    {
        ArgumentNullException.ThrowIfNull(gameType);

        if (string.IsNullOrWhiteSpace(gameType.Key))
        {
            throw new ArgumentException("Portable GameType key is required.", nameof(gameType));
        }

        if (string.IsNullOrWhiteSpace(gameType.DisplayName))
        {
            throw new ArgumentException("Portable GameType display name is required.", nameof(gameType));
        }

        if (string.IsNullOrWhiteSpace(gameType.Type))
        {
            throw new ArgumentException("Portable GameType type is required.", nameof(gameType));
        }

        var revisions = gameType.Revisions.Select(MapPortableToModel).ToList();
        var duplicateRevision = revisions
            .GroupBy(revision => new RevisionIdentity(revision.ImageReference, revision.VersionTag))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateRevision is not null)
        {
            throw new ArgumentException($"Portable GameType contains duplicate revision identity '{duplicateRevision.Key.ImageReference}:{duplicateRevision.Key.VersionTag}'.", nameof(gameType));
        }

        var currentRevisionVersionTag = gameType.CurrentRevisionVersionTag?.Trim();
        if (!string.IsNullOrWhiteSpace(currentRevisionVersionTag)
            && revisions.All(revision => !string.Equals(revision.VersionTag, currentRevisionVersionTag, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException($"Portable GameType current revision version tag '{currentRevisionVersionTag}' was not found in the package revisions.", nameof(gameType));
        }

        return new GameType
        {
            Key = gameType.Key.Trim(),
            DisplayName = gameType.DisplayName.Trim(),
            Description = gameType.Description,
            Type = gameType.Type.Trim(),
            ThumbnailUrl = gameType.ThumbnailUrl,
            DocumentationUrl = gameType.DocumentationUrl,
            IsActive = gameType.IsActive,
            Revisions = revisions
        };
    }

    private static GameTypeRevision MapPortableToModel(PortableGameTypeRevisionDto revision)
    {
        ArgumentNullException.ThrowIfNull(revision);

        var request = new SaveGameTypeRevisionRequestDto
        {
            VersionTag = revision.VersionTag,
            ImageReference = revision.ImageReference,
            ImageDigest = revision.ImageDigest,
            EnableTTY = revision.EnableTTY,
            Notes = revision.Notes,
            IsPublished = revision.IsPublished,
            Ports = revision.Ports.Select(port => new GameTypePortDto
            {
                ContainerPort = port.ContainerPort,
                Protocol = port.Protocol,
                AdvertisedPort = port.AdvertisedPort,
                Description = port.Description,
                DisplayOrder = port.DisplayOrder
            }).ToList(),
            Volumes = revision.Volumes.Select(volume => new GameTypeVolumeDto
            {
                Source = volume.Source,
                Description = volume.Description,
                DisplayOrder = volume.DisplayOrder,
                Usage = volume.Usage
            }).ToList(),
            SettingDefinitions = revision.SettingDefinitions.Select(setting => new GameTypeSettingDefinitionDto
            {
                SettingKey = setting.SettingKey,
                DefaultValue = setting.DefaultValue,
                Description = setting.Description,
                DisplayOrder = setting.DisplayOrder,
                Metadata = setting.Metadata is null ? null : new GameTypeSettingMetadataDto
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
                    PortMappings = setting.Metadata.PortMappings.Select(mapping => new GameTypeSettingPortMappingDto
                    {
                        MappingRole = mapping.MappingRole,
                        RelationType = mapping.RelationType,
                        TargetContainerPort = mapping.TargetContainerPort,
                        TargetProtocol = mapping.TargetProtocol,
                        CalculationValue = mapping.CalculationValue,
                        IsRequired = mapping.IsRequired,
                        DisplayOrder = mapping.DisplayOrder
                    }).ToList()
                }
            }).ToList(),
            WebHosts = revision.WebHosts.Select(webHost => new GameTypeWebHostDto
            {
                Name = webHost.Name,
                Description = webHost.Description,
                PathSegment = webHost.PathSegment,
                ContainerPort = webHost.ContainerPort,
                ContainerPortVariable = webHost.ContainerPortVariable,
                EnabledWhen = webHost.EnabledWhen,
                DisplayOrder = webHost.DisplayOrder
            }).ToList()
        };

        ValidateRevisionRequest(request);
        return MapToModel(request);
    }

    private sealed record RevisionIdentity(string ImageReference, string VersionTag);

    private static void ValidateWebHostRequest(GameTypeWebHostDto webHost, IReadOnlyDictionary<string, GameTypeSettingDefinitionDto> settingsByKey, SaveGameTypeRevisionRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(webHost);
        ArgumentNullException.ThrowIfNull(settingsByKey);
        ArgumentNullException.ThrowIfNull(request);

        var webHostLabel = string.IsNullOrWhiteSpace(webHost.Name) ? "(unnamed)" : webHost.Name;
        var hasStaticPort = webHost.ContainerPort.HasValue;
        var hasVariablePort = !string.IsNullOrWhiteSpace(webHost.ContainerPortVariable);

        if (!hasStaticPort && !hasVariablePort)
        {
            throw new ArgumentException($"Web Host '{webHostLabel}' must define either a static port or a port variable.", nameof(request));
        }

        if (hasStaticPort && hasVariablePort)
        {
            throw new ArgumentException($"Web Host '{webHostLabel}' cannot define both a static port and a port variable.", nameof(request));
        }

        if (hasVariablePort)
        {
            var variableName = webHost.ContainerPortVariable!.Trim();
            if (!settingsByKey.TryGetValue(variableName, out var referencedSetting))
            {
                throw new ArgumentException($"Web Host '{webHostLabel}' port variable '{variableName}' must reference an existing revision setting.", nameof(request));
            }

            if (!TryValidateWebHostPortSetting(referencedSetting, out var settingIssue))
            {
                throw new ArgumentException($"Web Host '{webHostLabel}' port variable '{variableName}' {settingIssue}", nameof(request));
            }
        }

        foreach (var issue in GetWebHostPathSegmentIssues(webHost.PathSegment))
        {
            throw new ArgumentException($"Web Host '{webHostLabel}' {issue}", nameof(request));
        }
    }

    private static bool TryValidateWebHostPortSetting(GameTypeSettingDefinitionDto setting, out string issue)
    {
        ArgumentNullException.ThrowIfNull(setting);

        if (!int.TryParse(setting.DefaultValue, out var defaultPort) || defaultPort <= 0 || defaultPort > 65535)
        {
            issue = "must have a numeric default value between 1 and 65535.";
            return false;
        }

        var dataType = setting.Metadata?.DataType?.Trim();
        if (!string.IsNullOrWhiteSpace(dataType) && !SupportedWebHostPortSettingDataTypes.Contains(dataType))
        {
            issue = "must use a setting whose DataType is 'number' or 'port'.";
            return false;
        }

        issue = string.Empty;
        return true;
    }

    private static List<string> GetWebHostPathSegmentIssues(string? pathSegment)
    {
        var issues = new List<string>();
        if (string.IsNullOrWhiteSpace(pathSegment))
        {
            return issues;
        }

        if (!string.Equals(pathSegment, pathSegment.Trim(), StringComparison.Ordinal))
        {
            issues.Add("path segment cannot start or end with whitespace.");
        }

        var trimmedPathSegment = pathSegment.Trim();
        if (trimmedPathSegment.StartsWith('/') || trimmedPathSegment.EndsWith('/'))
        {
            issues.Add("path segment cannot start or end with '/'. Use a relative path segment only.");
        }

        if (trimmedPathSegment.Contains("//", StringComparison.Ordinal))
        {
            issues.Add("path segment cannot contain empty path segments ('//').");
        }

        foreach (Match match in WebHostPathVariableRegex.Matches(trimmedPathSegment))
        {
            var variableName = match.Groups["name"].Value;
            if (!SupportedWebHostPathVariables.Contains(variableName))
            {
                var supportedVariables = string.Join(", ", SupportedWebHostPathVariables.Select(variable => $"{{{variable}}}"));
                issues.Add($"path segment uses unsupported runtime variable '{{{variableName}}}'. Supported variables: {supportedVariables}.");
            }
        }

        var literalContent = WebHostPathVariableRegex.Replace(trimmedPathSegment, string.Empty);
        if (literalContent.Contains('{') || literalContent.Contains('}'))
        {
            issues.Add("path segment contains malformed runtime variable placeholders. Use values like '{serverId}'.");
        }

        if (literalContent.Any(character => !(char.IsLower(character) || char.IsDigit(character) || character == '-' || character == '/')))
        {
            issues.Add("path segment can only contain lowercase letters, numbers, hyphens, forward slashes, and supported runtime variables.");
        }

        return issues.Distinct(StringComparer.Ordinal).ToList();
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
