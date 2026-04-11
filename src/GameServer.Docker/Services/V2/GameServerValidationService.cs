using System.Text.Json;
using System.Text.RegularExpressions;
using GameServer.Docker.Constants;
using GameServer.Docker.Dtos.V2;
using GameServer.Docker.Interfaces;
using GameServer.Docker.Repositories.V2;
using Docker.DotNet.Models;
using GameTypeModel = GameServer.Docker.Models.V2.GameType;
using GameTypeRevisionModel = GameServer.Docker.Models.V2.GameTypeRevision;
using GameTypeSettingDefinitionModel = GameServer.Docker.Models.V2.GameTypeSettingDefinition;
using GameTypeSettingMetadataModel = GameServer.Docker.Models.V2.GameTypeSettingMetadata;
using GameTypeSettingPortMappingModel = GameServer.Docker.Models.V2.GameTypeSettingPortMapping;
using GameTypeWebHostModel = GameServer.Docker.Models.V2.GameTypeWebHost;
using Microsoft.Extensions.Options;

namespace GameServer.Docker.Services.V2;

/// <summary>
/// Validates V2 GameServer save requests and derives effective runtime configuration.
/// </summary>
public sealed class GameServerValidationService(
    IGameTypeRepository gameTypeRepository,
    IServiceOperations serviceOperations,
    IOptions<GameServer.Docker.Configurations.PortAllocation> portAllocationOptions)
{
    private readonly GameServer.Docker.Configurations.PortAllocation portAllocation = portAllocationOptions.Value;

    /// <summary>
    /// Validates a V2 GameServer request and returns effective derived configuration.
    /// </summary>
    public async Task<GameServerValidationResultDto> ValidateAsync(SaveGameServerRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var issues = new List<GameServerValidationIssueDto>();
        ValidateCoreFields(request, issues);
        ValidateConfigurationOptions(request.DockerVolumeOptions, "docker-volume-options", issues);
        ValidateConfigurationOptions(request.NetworkOptions, "network-options", issues);

        var gameTypes = await gameTypeRepository.GetAllAsync(includeInactive: true).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var revisionContext = ResolveRevisionContext(request.GameTypeRevisionId, gameTypes);
        if (revisionContext is null)
        {
            issues.Add(CreateIssue("RevisionNotFound", $"GameTypeRevision '{request.GameTypeRevisionId}' was not found.", "gameTypeRevisionId"));
            return CreateResult(issues, request.DockerVolumeOptions, request.NetworkOptions);
        }

        var effectiveSettings = BuildEffectiveSettings(request, revisionContext.Revision, issues);
        var resolvedPorts = ResolvePorts(revisionContext.Revision, effectiveSettings, issues);
        ValidateResolvedPorts(resolvedPorts, request.ServerId, issues, cancellationToken);
        var resolvedVolumes = ResolveVolumes(revisionContext.Revision);
        var resolvedWebHosts = ResolveWebHosts(revisionContext.Revision.WebHosts, effectiveSettings, issues);

        await ValidateResolvedPortsAsync(resolvedPorts, request.ServerId, issues, cancellationToken).ConfigureAwait(false);

        return new GameServerValidationResultDto
        {
            IsValid = issues.All(issue => !issue.IsBlocking),
            Issues = issues,
            ResolvedPorts = resolvedPorts,
            ResolvedVolumes = resolvedVolumes,
            ResolvedWebHosts = resolvedWebHosts,
            DockerVolumeOptions = request.DockerVolumeOptions,
            NetworkOptions = request.NetworkOptions
        };
    }

    private static void ValidateCoreFields(SaveGameServerRequestDto request, List<GameServerValidationIssueDto> issues)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(issues);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            issues.Add(CreateIssue("ServerNameRequired", "Server name is required.", "name"));
        }

        if (request.GameTypeRevisionId <= 0)
        {
            issues.Add(CreateIssue("RevisionRequired", "A GameType revision must be selected.", "gameTypeRevisionId"));
        }
    }

    private static void ValidateConfigurationOptions(
        IEnumerable<GameServerConfigurationOptionDto> options,
        string scope,
        List<GameServerValidationIssueDto> issues)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(issues);

        foreach (var option in options)
        {
            if (option.Required && string.IsNullOrWhiteSpace(option.Value))
            {
                var label = string.IsNullOrWhiteSpace(option.DisplayName) ? option.Key : option.DisplayName;
                issues.Add(CreateIssue("ConfigurationOptionRequired", $"Configuration option '{label}' is required.", $"{scope}:{option.Key}"));
            }
        }
    }

    private static RevisionContext? ResolveRevisionContext(int revisionId, IEnumerable<GameTypeModel> gameTypes)
    {
        ArgumentNullException.ThrowIfNull(gameTypes);

        return gameTypes
            .SelectMany(gameType => gameType.Revisions.Select(revision => new RevisionContext(gameType, revision)))
            .FirstOrDefault(context => context.Revision.Id == revisionId);
    }

    private static Dictionary<string, string?> BuildEffectiveSettings(
        SaveGameServerRequestDto request,
        GameTypeRevisionModel revision,
        List<GameServerValidationIssueDto> issues)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentNullException.ThrowIfNull(issues);

        var settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var duplicateKeys = request.Settings
            .Where(setting => !string.IsNullOrWhiteSpace(setting.SettingKey))
            .GroupBy(setting => setting.SettingKey, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        foreach (var duplicateKey in duplicateKeys)
        {
            issues.Add(CreateIssue("DuplicateSetting", $"Setting '{duplicateKey}' is defined more than once.", $"settings:{duplicateKey}"));
        }

        foreach (var setting in request.Settings.Where(setting => !string.IsNullOrWhiteSpace(setting.SettingKey)))
        {
            settings[setting.SettingKey] = setting.Value;
        }

        var revisionDefinitions = revision.SettingDefinitions
            .Where(definition => !string.IsNullOrWhiteSpace(definition.SettingKey))
            .ToDictionary(definition => definition.SettingKey, StringComparer.OrdinalIgnoreCase);

        foreach (var requestSetting in request.Settings.Where(setting => !string.IsNullOrWhiteSpace(setting.SettingKey)))
        {
            if (!revisionDefinitions.ContainsKey(requestSetting.SettingKey))
            {
                issues.Add(CreateIssue("UnknownSetting", $"Setting '{requestSetting.SettingKey}' is not defined by the selected GameType revision.", $"settings:{requestSetting.SettingKey}"));
            }
        }

        foreach (var definition in revision.SettingDefinitions)
        {
            if (string.IsNullOrWhiteSpace(definition.SettingKey))
            {
                continue;
            }

            settings.TryGetValue(definition.SettingKey, out var requestValue);
            var effectiveValue = requestValue ?? definition.DefaultValue;
            settings[definition.SettingKey] = effectiveValue;
            ValidateSettingValue(definition, effectiveValue, issues);
        }

        return settings;
    }

    private static void ValidateSettingValue(GameTypeSettingDefinitionModel definition, string? value, List<GameServerValidationIssueDto> issues)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(issues);

        var metadata = definition.Metadata;
        if (metadata is null)
        {
            return;
        }

        var scope = $"settings:{definition.SettingKey}";
        var hasValue = !string.IsNullOrWhiteSpace(value);

        if (metadata.IsRequired && !hasValue)
        {
            issues.Add(CreateIssue("SettingRequired", $"Setting '{definition.SettingKey}' is required.", scope));
            return;
        }

        if (metadata.CannotBeEmpty && !hasValue)
        {
            issues.Add(CreateIssue("SettingEmpty", $"Setting '{definition.SettingKey}' cannot be empty.", scope));
            return;
        }

        if (!hasValue)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(metadata.ValidationPattern))
        {
            try
            {
                if (!Regex.IsMatch(value!, metadata.ValidationPattern))
                {
                    issues.Add(CreateIssue("PatternMismatch", metadata.ValidationMessage ?? $"Setting '{definition.SettingKey}' does not match the required pattern.", scope));
                }
            }
            catch (ArgumentException)
            {
                issues.Add(CreateIssue("PatternInvalid", $"Setting '{definition.SettingKey}' has an invalid validation pattern.", scope));
            }
        }

        switch (NormalizeDataType(metadata.DataType))
        {
            case "boolean":
                if (!bool.TryParse(value, out _))
                {
                    issues.Add(CreateIssue("BooleanInvalid", $"Setting '{definition.SettingKey}' must be true or false.", scope));
                }
                break;
            case "number":
                if (!double.TryParse(value, out _))
                {
                    issues.Add(CreateIssue("NumberInvalid", $"Setting '{definition.SettingKey}' must be numeric.", scope));
                }
                break;
            case "enum":
                ValidateEnumValue(definition.SettingKey, metadata, value!, issues, scope);
                break;
            case "port":
                if (!int.TryParse(value, out var port) || port <= 0 || port > 65535)
                {
                    issues.Add(CreateIssue("PortInvalid", $"Setting '{definition.SettingKey}' must be a valid port between 1 and 65535.", scope));
                }
                break;
        }
    }

    private static void ValidateEnumValue(
        string settingKey,
        GameTypeSettingMetadataModel metadata,
        string value,
        List<GameServerValidationIssueDto> issues,
        string scope)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(issues);
        ArgumentException.ThrowIfNullOrWhiteSpace(settingKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        if (string.IsNullOrWhiteSpace(metadata.AllowedValuesJson))
        {
            return;
        }

        try
        {
            var allowedValues = JsonSerializer.Deserialize<List<string>>(metadata.AllowedValuesJson) ?? [];
            if (allowedValues.Count > 0 && allowedValues.All(candidate => !string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(CreateIssue("EnumInvalid", $"Setting '{settingKey}' must be one of: {string.Join(", ", allowedValues)}.", scope));
            }
        }
        catch (JsonException)
        {
            issues.Add(CreateIssue("EnumMetadataInvalid", $"Setting '{settingKey}' has invalid allowed-values metadata.", scope));
        }
    }

    private static List<GameServerResolvedPortDto> ResolvePorts(
        GameTypeRevisionModel revision,
        IReadOnlyDictionary<string, string?> effectiveSettings,
        List<GameServerValidationIssueDto> issues)
    {
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentNullException.ThrowIfNull(effectiveSettings);
        ArgumentNullException.ThrowIfNull(issues);

        var resolvedPorts = revision.Ports
            .OrderBy(port => port.DisplayOrder)
            .Select(port => new GameServerResolvedPortDto
            {
                ContainerPort = port.ContainerPort,
                Protocol = port.Protocol,
                AdvertisedPort = port.AdvertisedPort,
                Description = port.Description,
                DisplayOrder = port.DisplayOrder
            })
            .ToList();

        var portLookup = revision.Ports
            .OrderBy(port => port.DisplayOrder)
            .Select((definition, index) => new { definition, index })
            .ToDictionary(item => BuildPortKey(item.definition.ContainerPort, item.definition.Protocol), item => item.index, StringComparer.OrdinalIgnoreCase);

        foreach (var definition in revision.SettingDefinitions)
        {
            if (definition.Metadata?.PortMappings.Count is not > 0)
            {
                continue;
            }

            if (!effectiveSettings.TryGetValue(definition.SettingKey, out var effectiveValue) || string.IsNullOrWhiteSpace(effectiveValue))
            {
                continue;
            }

            if (!int.TryParse(effectiveValue, out var basePort) || basePort <= 0 || basePort > 65535)
            {
                issues.Add(CreateIssue("PortSettingInvalid", $"Setting '{definition.SettingKey}' must resolve to a valid port before mapped ports can be derived.", $"settings:{definition.SettingKey}"));
                continue;
            }

            var primaryMapping = definition.Metadata.PortMappings.FirstOrDefault(mapping => mapping.MappingRole == GameServer.Docker.Models.V2.GameTypeSettingPortMappingRole.Primary);
            if (primaryMapping is null)
            {
                issues.Add(CreateIssue("PrimaryPortMappingMissing", $"Setting '{definition.SettingKey}' must define a primary port mapping.", $"settings:{definition.SettingKey}"));
                continue;
            }

            foreach (var mapping in definition.Metadata.PortMappings)
            {
                if (!portLookup.TryGetValue(BuildPortKey(mapping.TargetContainerPort, mapping.TargetProtocol), out var resolvedPortIndex))
                {
                    var referenceLabel = mapping.MappingRole == GameServer.Docker.Models.V2.GameTypeSettingPortMappingRole.Related
                        ? "default related port"
                        : "target port";
                    issues.Add(CreateIssue("MappedPortMissing", $"Setting '{definition.SettingKey}' references missing {referenceLabel} '{mapping.TargetContainerPort}/{mapping.TargetProtocol}'.", $"settings:{definition.SettingKey}"));
                    continue;
                }

                var derivedPort = DerivePortValue(mapping, primaryMapping, basePort, definition.SettingKey, issues);
                if (!derivedPort.HasValue)
                {
                    continue;
                }

                resolvedPorts[resolvedPortIndex] = resolvedPorts[resolvedPortIndex] with { ContainerPort = derivedPort.Value };
            }
        }

        return resolvedPorts;
    }

    private static int? DerivePortValue(
        GameTypeSettingPortMappingModel mapping,
        GameTypeSettingPortMappingModel primaryMapping,
        int basePort,
        string settingKey,
        List<GameServerValidationIssueDto> issues)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentNullException.ThrowIfNull(primaryMapping);
        ArgumentNullException.ThrowIfNull(issues);
        ArgumentException.ThrowIfNullOrWhiteSpace(settingKey);

        int derivedPort;
        if (mapping.MappingRole == GameServer.Docker.Models.V2.GameTypeSettingPortMappingRole.Primary)
        {
            if (mapping.RelationType != GameServer.Docker.Models.V2.GameTypeSettingPortRelationType.Direct)
            {
                issues.Add(CreateIssue("PrimaryMappingInvalid", $"Setting '{settingKey}' must use a direct relation for the primary mapping.", $"settings:{settingKey}"));
                return null;
            }

            derivedPort = basePort;
        }
        else
        {
            if (!mapping.CalculationValue.HasValue)
            {
                issues.Add(CreateIssue("RelatedMappingInvalid", $"Setting '{settingKey}' must define a calculation value for related mappings.", $"settings:{settingKey}"));
                return null;
            }

            derivedPort = mapping.RelationType switch
            {
                GameServer.Docker.Models.V2.GameTypeSettingPortRelationType.Offset => basePort + mapping.CalculationValue.Value,
                GameServer.Docker.Models.V2.GameTypeSettingPortRelationType.Multiplier => basePort * mapping.CalculationValue.Value,
                _ => int.MinValue
            };

            if (derivedPort == int.MinValue)
            {
                issues.Add(CreateIssue("RelatedMappingTypeInvalid", $"Setting '{settingKey}' can only use offset or multiplier related mappings.", $"settings:{settingKey}"));
                return null;
            }
        }

        if (derivedPort <= 0 || derivedPort > 65535)
        {
            issues.Add(CreateIssue("DerivedPortInvalid", $"Setting '{settingKey}' resolves to invalid port '{derivedPort}/{mapping.TargetProtocol}'.", $"settings:{settingKey}"));
            return null;
        }

        return derivedPort;
    }

    private static void ValidateResolvedPorts(
        IReadOnlyList<GameServerResolvedPortDto> resolvedPorts,
        string? currentServerId,
        List<GameServerValidationIssueDto> issues,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolvedPorts);
        ArgumentNullException.ThrowIfNull(issues);
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var port in resolvedPorts)
        {
            if (port.ContainerPort < 1024 || port.ContainerPort > 65535)
            {
                issues.Add(CreateIssue("ResolvedPortRangeInvalid", $"Resolved port '{port.ContainerPort}/{port.Protocol}' is outside the allowed range.", $"ports:{port.ContainerPort}/{port.Protocol}"));
            }
        }

        var duplicates = resolvedPorts
            .GroupBy(port => BuildPortKey(port.ContainerPort, port.Protocol), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        foreach (var duplicate in duplicates)
        {
            issues.Add(CreateIssue("ResolvedPortDuplicate", $"Resolved port '{duplicate}' is duplicated within the server configuration.", $"ports:{duplicate}"));
        }
    }

    private async Task ValidateResolvedPortsAsync(
        IReadOnlyList<GameServerResolvedPortDto> resolvedPorts,
        string? currentServerId,
        List<GameServerValidationIssueDto> issues,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolvedPorts);
        ArgumentNullException.ThrowIfNull(issues);
        cancellationToken.ThrowIfCancellationRequested();

        var services = await serviceOperations.ListServicesAsync($"{ServiceLabels.Managed}={ServiceLabels.ManagedValue}", cancellationToken: cancellationToken).ConfigureAwait(false);
        var occupiedPorts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var service in services)
        {
            var serviceServerId = TryGetServiceServerId(service);
            if (!string.IsNullOrWhiteSpace(currentServerId)
                && string.Equals(serviceServerId, currentServerId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var endpointPort in service.Endpoint?.Ports ?? [])
            {
                occupiedPorts.Add(BuildPortKey((int)endpointPort.PublishedPort, endpointPort.Protocol));
            }
        }

        foreach (var port in resolvedPorts)
        {
            var scope = $"ports:{port.ContainerPort}/{port.Protocol}";
            if (port.ContainerPort < portAllocation.StartPort || port.ContainerPort > portAllocation.EndPort)
            {
                issues.Add(CreateIssue("PortOutsideAllocationRange", $"Resolved port '{port.ContainerPort}/{port.Protocol}' is outside the configured allocation range.", scope));
                continue;
            }

            if (occupiedPorts.Contains(BuildPortKey(port.ContainerPort, port.Protocol)))
            {
                issues.Add(CreateIssue("PortUnavailable", $"Resolved port '{port.ContainerPort}/{port.Protocol}' is already in use by another managed server.", scope));
            }
        }
    }

    private static List<GameServerResolvedVolumeDto> ResolveVolumes(GameTypeRevisionModel revision)
    {
        ArgumentNullException.ThrowIfNull(revision);

        return revision.Volumes
            .OrderBy(volume => volume.DisplayOrder)
            .Select(volume => new GameServerResolvedVolumeDto
            {
                Source = volume.Source,
                Description = volume.Description,
                DisplayOrder = volume.DisplayOrder,
                Usage = volume.Usage
            })
            .ToList();
    }

    private static List<GameServerResolvedWebHostDto> ResolveWebHosts(
        IEnumerable<GameTypeWebHostModel> webHosts,
        IReadOnlyDictionary<string, string?> effectiveSettings,
        List<GameServerValidationIssueDto> issues)
    {
        ArgumentNullException.ThrowIfNull(webHosts);
        ArgumentNullException.ThrowIfNull(effectiveSettings);
        ArgumentNullException.ThrowIfNull(issues);

        var resolved = new List<GameServerResolvedWebHostDto>();
        foreach (var webHost in webHosts.OrderBy(host => host.DisplayOrder))
        {
            if (!IsWebHostEnabled(webHost, effectiveSettings))
            {
                continue;
            }

            int? resolvedPort = webHost.ContainerPort;
            if (!string.IsNullOrWhiteSpace(webHost.ContainerPortVariable))
            {
                if (!effectiveSettings.TryGetValue(webHost.ContainerPortVariable, out var configuredValue)
                    || !int.TryParse(configuredValue, out var parsedPort)
                    || parsedPort <= 0
                    || parsedPort > 65535)
                {
                    issues.Add(CreateIssue("WebHostPortInvalid", $"Web Host '{webHost.Name}' requires a valid port from setting '{webHost.ContainerPortVariable}'.", $"webHosts:{webHost.Name}"));
                    continue;
                }

                resolvedPort = parsedPort;
            }

            if (!resolvedPort.HasValue)
            {
                issues.Add(CreateIssue("WebHostPortMissing", $"Web Host '{webHost.Name}' must define a fixed port or port variable.", $"webHosts:{webHost.Name}"));
                continue;
            }

            resolved.Add(new GameServerResolvedWebHostDto
            {
                Name = webHost.Name,
                Description = webHost.Description,
                PathSegment = webHost.PathSegment,
                ContainerPort = resolvedPort,
                ContainerPortVariable = webHost.ContainerPortVariable,
                EnabledWhen = webHost.EnabledWhen,
                DisplayOrder = webHost.DisplayOrder
            });
        }

        return resolved;
    }

    private static bool IsWebHostEnabled(GameTypeWebHostModel webHost, IReadOnlyDictionary<string, string?> effectiveSettings)
    {
        ArgumentNullException.ThrowIfNull(webHost);
        ArgumentNullException.ThrowIfNull(effectiveSettings);

        if (string.IsNullOrWhiteSpace(webHost.EnabledWhen))
        {
            return true;
        }

        var condition = webHost.EnabledWhen.Trim();
        var isNegated = condition.Contains("!=", StringComparison.Ordinal);
        var separator = isNegated ? "!=" : "=";
        var parts = condition.Split(separator, 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        effectiveSettings.TryGetValue(parts[0], out var actualValue);
        var matches = string.Equals(actualValue ?? string.Empty, parts[1], StringComparison.OrdinalIgnoreCase);
        return isNegated ? !matches : matches;
    }

    private static GameServerValidationIssueDto CreateIssue(string code, string message, string scope, bool isBlocking = true)
    {
        return new GameServerValidationIssueDto
        {
            Code = code,
            Message = message,
            Scope = scope,
            Severity = isBlocking ? "Error" : "Warning",
            IsBlocking = isBlocking
        };
    }

    private static string NormalizeDataType(string? dataType)
    {
        return string.IsNullOrWhiteSpace(dataType) ? string.Empty : dataType.Trim().ToLowerInvariant();
    }

    private static string BuildPortKey(int port, string? protocol)
    {
        return $"{port}/{(protocol ?? string.Empty).Trim().ToLowerInvariant()}";
    }

    private static string? TryGetServiceServerId(SwarmService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        return service.Spec?.Labels is not null && service.Spec.Labels.TryGetValue(ServiceLabels.ServerId, out var serverId)
            ? serverId
            : null;
    }

    private static GameServerValidationResultDto CreateResult(
        List<GameServerValidationIssueDto> issues,
        List<GameServerConfigurationOptionDto> dockerVolumeOptions,
        List<GameServerConfigurationOptionDto> networkOptions)
    {
        return new GameServerValidationResultDto
        {
            IsValid = issues.All(issue => !issue.IsBlocking),
            Issues = issues,
            ResolvedPorts = [],
            ResolvedVolumes = [],
            ResolvedWebHosts = [],
            DockerVolumeOptions = dockerVolumeOptions,
            NetworkOptions = networkOptions
        };
    }

    private sealed record RevisionContext(GameTypeModel GameType, GameTypeRevisionModel Revision);
}
