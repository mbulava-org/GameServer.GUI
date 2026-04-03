using Docker.DotNet;
using GameServer.Docker.Dtos.V2;
using GameServer.Docker.Repositories.V2;
using DockerModels = global::Docker.DotNet.Models;

namespace GameServer.Docker.Services.V2.Detection;

public sealed class GameTypeSetupDetectionService(IGameTypeRepository repository, IDockerClient? dockerClient, ILogger<GameTypeSetupDetectionService> logger)
{
    /// <summary>
    /// Detects Docker image setup data for a V2 GameType and tag.
    /// </summary>
    public async Task<GameTypeSetupDetectionResultDto> DetectAsync(string key, DetectGameTypeSetupRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.VersionTag);
        cancellationToken.ThrowIfCancellationRequested();

        var gameType = await repository.GetByKeyAsync(key) ?? throw new KeyNotFoundException($"V2 GameType '{key}' was not found");
        if (dockerClient is null)
        {
            throw new InvalidOperationException("Docker image detection requires direct Docker access from the Primary Service.");
        }

        return await DetectAsync(gameType, request.VersionTag, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Compares detected Docker setup data to a selected V2 GameType revision.
    /// </summary>
    public async Task<GameTypeSetupComparisonResultDto> CompareAsync(string key, CompareGameTypeSetupRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.VersionTag);
        cancellationToken.ThrowIfCancellationRequested();

        var gameType = await repository.GetByKeyAsync(key) ?? throw new KeyNotFoundException($"V2 GameType '{key}' was not found");
        var revision = gameType.Revisions.FirstOrDefault(x => x.Id == request.RevisionId)
            ?? throw new KeyNotFoundException($"V2 GameType revision '{request.RevisionId}' was not found for '{key}'");

        var detection = await DetectAsync(gameType, request.VersionTag, cancellationToken).ConfigureAwait(false);

        var revisionPorts = revision.Ports
            .Select(x => $"{x.ContainerPort}/{x.Protocol.ToLowerInvariant()}")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        var detectedPorts = detection.Ports
            .Select(x => $"{x.ContainerPort}/{x.Protocol.ToLowerInvariant()}")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        var revisionVolumes = revision.Volumes
            .Select(x => x.Description ?? x.Source)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var detectedVolumes = detection.Volumes
            .Select(x => x.ContainerPath)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var revisionSettings = revision.SettingDefinitions
            .ToDictionary(x => x.SettingKey, x => x.DefaultValue, StringComparer.OrdinalIgnoreCase);

        var detectedSettings = detection.Settings
            .ToDictionary(x => x.Key, x => x.DefaultValue, StringComparer.OrdinalIgnoreCase);

        var changedSettings = revisionSettings.Keys
            .Intersect(detectedSettings.Keys, StringComparer.OrdinalIgnoreCase)
            .Where(x => !string.Equals(revisionSettings[x], detectedSettings[x], StringComparison.Ordinal))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Select(x => new ChangedSettingDto
            {
                Key = x,
                RevisionValue = revisionSettings[x],
                DetectedValue = detectedSettings[x]
            })
            .ToList();

        var addedPorts = detectedPorts.Except(revisionPorts, StringComparer.Ordinal).ToList();
        var removedPorts = revisionPorts.Except(detectedPorts, StringComparer.Ordinal).ToList();
        var addedVolumes = detectedVolumes.Except(revisionVolumes, StringComparer.OrdinalIgnoreCase).ToList();
        var removedVolumes = revisionVolumes.Except(detectedVolumes, StringComparer.OrdinalIgnoreCase).ToList();
        var addedSettings = detectedSettings.Keys.Except(revisionSettings.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        var removedSettings = revisionSettings.Keys.Except(detectedSettings.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();

        var digestChanged = !string.Equals(revision.ImageDigest, detection.ImageDigest, StringComparison.Ordinal);

        return new GameTypeSetupComparisonResultDto
        {
            Detection = detection,
            RevisionId = revision.Id,
            RevisionVersionTag = revision.VersionTag,
            HasChanges = digestChanged || addedPorts.Count > 0 || removedPorts.Count > 0 || addedVolumes.Count > 0 || removedVolumes.Count > 0 || addedSettings.Count > 0 || removedSettings.Count > 0 || changedSettings.Count > 0,
            DigestChanged = digestChanged,
            AddedPorts = addedPorts,
            RemovedPorts = removedPorts,
            AddedVolumes = addedVolumes,
            RemovedVolumes = removedVolumes,
            AddedSettings = addedSettings,
            RemovedSettings = removedSettings,
            ChangedSettings = changedSettings
        };
    }

    private async Task<GameTypeSetupDetectionResultDto> DetectAsync(Models.V2.GameType gameType, string versionTag, CancellationToken cancellationToken)
    {
        if (dockerClient is null)
        {
            throw new InvalidOperationException("Docker image detection requires direct Docker access from the Primary Service.");
        }

        var imageReferenceWithTag = $"{gameType.ImageReference}:{versionTag}";
        logger.LogInformation("Detecting Docker setup for V2 GameType {GameTypeKey} using image {ImageReferenceWithTag}", gameType.Key, imageReferenceWithTag);

        var image = await dockerClient.Images.InspectImageAsync(imageReferenceWithTag, cancellationToken).ConfigureAwait(false);
        var config = image.Config;

        var detectedPorts = GetDetectedPorts(config?.ExposedPorts);

        return new GameTypeSetupDetectionResultDto
        {
            ImageReference = gameType.ImageReference,
            VersionTag = versionTag,
            ImageDigest = GetImageDigest(gameType.ImageReference, image.RepoDigests),
            Ports = detectedPorts,
            Settings = GetDetectedSettings(config?.Env, detectedPorts),
            Volumes = GetDetectedVolumes(config?.Volumes)
        };
    }

    private static string? GetImageDigest(string imageReference, IList<string>? repoDigests)
    {
        if (repoDigests is null || repoDigests.Count == 0)
        {
            return null;
        }

        var matchingDigest = repoDigests.FirstOrDefault(x => x.StartsWith($"{imageReference}@", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(matchingDigest))
        {
            var separatorIndex = matchingDigest.IndexOf('@');
            return separatorIndex >= 0 && separatorIndex < matchingDigest.Length - 1
                ? matchingDigest[(separatorIndex + 1)..]
                : matchingDigest;
        }

        return repoDigests[0];
    }

    private static List<DetectedPortDto> GetDetectedPorts(IDictionary<string, DockerModels.EmptyStruct>? exposedPorts)
    {
        if (exposedPorts is null || exposedPorts.Count == 0)
        {
            return [];
        }

        return exposedPorts.Keys
            .Select(ParsePort)
            .Where(x => x is not null)
            .Select(x => x!)
            .OrderBy(x => x.ContainerPort)
            .ThenBy(x => x.Protocol)
            .ToList();
    }

    private static DetectedPortDto? ParsePort(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var segments = value.Split('/');
        if (segments.Length != 2 || !int.TryParse(segments[0], out var port))
        {
            return null;
        }

        return new DetectedPortDto
        {
            ContainerPort = port,
            Protocol = segments[1]
        };
    }

    private static List<DetectedSettingDto> GetDetectedSettings(IList<string>? environmentVariables, IReadOnlyList<DetectedPortDto> detectedPorts)
    {
        if (environmentVariables is null || environmentVariables.Count == 0)
        {
            return [];
        }

        return environmentVariables
            .Select(x => ParseEnvironmentVariable(x, detectedPorts))
            .Where(x => x is not null)
            .Select(x => x!)
            .OrderBy(x => x.Key)
            .ToList();
    }

    private static DetectedSettingDto? ParseEnvironmentVariable(string? environmentVariable, IReadOnlyList<DetectedPortDto> detectedPorts)
    {
        if (string.IsNullOrWhiteSpace(environmentVariable))
        {
            return null;
        }

        var separatorIndex = environmentVariable.IndexOf('=');
        if (separatorIndex < 0)
        {
            return new DetectedSettingDto { Key = environmentVariable, PortMappings = InferDetectedPortMappings(environmentVariable, null, detectedPorts) };
        }

        var key = environmentVariable[..separatorIndex];
        var defaultValue = separatorIndex == environmentVariable.Length - 1 ? string.Empty : environmentVariable[(separatorIndex + 1)..];

        return new DetectedSettingDto
        {
            Key = key,
            DefaultValue = defaultValue,
            PortMappings = InferDetectedPortMappings(key, defaultValue, detectedPorts)
        };
    }

    private static List<DetectedSettingPortMappingDto> InferDetectedPortMappings(string key, string? defaultValue, IReadOnlyList<DetectedPortDto> detectedPorts)
    {
        if (!LooksLikePortSetting(key) || !int.TryParse(defaultValue, out var defaultPort) || defaultPort <= 0 || detectedPorts.Count == 0)
        {
            return [];
        }

        var matchingPorts = detectedPorts
            .Where(port => port.ContainerPort == defaultPort)
            .OrderBy(port => GetProtocolSortOrder(port.Protocol))
            .ThenBy(port => port.Protocol, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (matchingPorts.Count == 0)
        {
            return [];
        }

        var inferredMappings = new List<DetectedSettingPortMappingDto>();

        inferredMappings.Add(new DetectedSettingPortMappingDto
        {
            MappingRole = Models.V2.GameTypeSettingPortMappingRole.Primary.ToString(),
            RelationType = Models.V2.GameTypeSettingPortRelationType.Direct.ToString(),
            TargetContainerPort = matchingPorts[0].ContainerPort,
            TargetProtocol = matchingPorts[0].Protocol,
            Description = $"Detected primary mapping for setting '{key}'.",
            IsRequired = false
        });

        foreach (var additionalMatch in matchingPorts.Skip(1))
        {
            inferredMappings.Add(new DetectedSettingPortMappingDto
            {
                MappingRole = Models.V2.GameTypeSettingPortMappingRole.Related.ToString(),
                RelationType = Models.V2.GameTypeSettingPortRelationType.Direct.ToString(),
                TargetContainerPort = additionalMatch.ContainerPort,
                TargetProtocol = additionalMatch.Protocol,
                Description = $"Detected related mapping for setting '{key}' on protocol '{additionalMatch.Protocol}'.",
                IsRequired = false
            });
        }

        if (IsPrimaryPortSetting(key))
        {
            foreach (var relatedPort in detectedPorts
                .Where(port => port.ContainerPort != defaultPort)
                .OrderBy(port => Math.Abs(port.ContainerPort - defaultPort))
                .ThenBy(port => GetProtocolSortOrder(port.Protocol))
                .ThenBy(port => port.Protocol, StringComparer.OrdinalIgnoreCase))
            {
                inferredMappings.Add(new DetectedSettingPortMappingDto
                {
                    MappingRole = Models.V2.GameTypeSettingPortMappingRole.Related.ToString(),
                    RelationType = Models.V2.GameTypeSettingPortRelationType.Offset.ToString(),
                    TargetContainerPort = relatedPort.ContainerPort,
                    TargetProtocol = relatedPort.Protocol,
                    CalculationValue = relatedPort.ContainerPort - defaultPort,
                    Description = $"Detected related exposed port for setting '{key}'.",
                    IsRequired = false
                });
            }
        }

        return inferredMappings;
    }

    private static bool LooksLikePortSetting(string key)
    {
        return !string.IsNullOrWhiteSpace(key)
            && key.Contains("PORT", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPrimaryPortSetting(string key)
    {
        var normalizedKey = key.Trim().Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

        return normalizedKey is "PORT" or "SERVERPORT" or "GAMEPORT" or "DEFAULTPORT" or "SERVICEPORT" or "PRIMARYPORT";
    }

    private static int GetProtocolSortOrder(string protocol)
    {
        return string.Equals(protocol, "tcp", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
    }

    private static List<DetectedVolumeDto> GetDetectedVolumes(IDictionary<string, DockerModels.EmptyStruct>? volumes)
    {
        if (volumes is null || volumes.Count == 0)
        {
            return [];
        }

        return volumes.Keys
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Select(x => new DetectedVolumeDto { ContainerPath = x })
            .ToList();
    }
}
