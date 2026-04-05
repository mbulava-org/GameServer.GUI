using GameServer.Docker.Dtos.V2;
using GameServer.Docker.Interfaces;
using GameServer.Docker.Models;
using GameServer.Docker.Repositories.V2;
using System.Net;
using System.Net.Http.Json;

namespace GameServer.Docker.Services.V2.Detection;

public sealed class GameTypeSetupDetectionService(
    IGameTypeRepository repository,
    IAgentRegistry agentRegistry,
    IHttpClientFactory httpClientFactory,
    ILogger<GameTypeSetupDetectionService> logger)
{
    /// <summary>
    /// Detects Docker image setup data for a V2 GameType and tag.
    /// </summary>
    public async Task<GameTypeSetupDetectionResultDto> DetectAsync(string key, DetectGameTypeSetupRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ImageReference);
        cancellationToken.ThrowIfCancellationRequested();

        var gameType = await repository.GetByKeyAsync(key) ?? throw new KeyNotFoundException($"V2 GameType '{key}' was not found");

        return await DetectAsync(gameType, request.ImageReference, request.VersionTag, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Compares detected Docker setup data to a selected V2 GameType revision.
    /// </summary>
    public async Task<GameTypeSetupComparisonResultDto> CompareAsync(string key, CompareGameTypeSetupRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ImageReference);
        cancellationToken.ThrowIfCancellationRequested();

        var gameType = await repository.GetByKeyAsync(key) ?? throw new KeyNotFoundException($"V2 GameType '{key}' was not found");
        var revision = gameType.Revisions.FirstOrDefault(x => x.Id == request.RevisionId)
            ?? throw new KeyNotFoundException($"V2 GameType revision '{request.RevisionId}' was not found for '{key}'");

        var detection = await DetectAsync(gameType, request.ImageReference, request.VersionTag, cancellationToken).ConfigureAwait(false);

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

    private async Task<GameTypeSetupDetectionResultDto> DetectAsync(Models.V2.GameType gameType, string imageReference, string? versionTag, CancellationToken cancellationToken)
    {
        var normalizedVersionTag = NormalizeVersionTag(imageReference, versionTag);
        var repositoryReference = RemoveTag(imageReference);
        var imageReferenceWithTag = string.IsNullOrWhiteSpace(normalizedVersionTag)
            ? imageReference
            : $"{repositoryReference}:{normalizedVersionTag}";
        logger.LogInformation("Detecting Docker setup for V2 GameType {GameTypeKey} using image {ImageReferenceWithTag} via node agents", gameType.Key, imageReferenceWithTag);

        var image = await InspectImageViaAgentAsync(imageReferenceWithTag, cancellationToken).ConfigureAwait(false);

        var detectedPorts = GetDetectedPorts(image.ExposedPorts);

        return new GameTypeSetupDetectionResultDto
        {
            ImageReference = repositoryReference,
            VersionTag = normalizedVersionTag,
            ImageDigest = GetImageDigest(repositoryReference, image.RepoDigests),
            Ports = detectedPorts,
            Settings = GetDetectedSettings(image.EnvironmentVariables, detectedPorts),
            Volumes = GetDetectedVolumes(image.VolumePaths)
        };
    }

    private async Task<AgentImageInspectResponse> InspectImageViaAgentAsync(string imageReferenceWithTag, CancellationToken cancellationToken)
    {
        var agents = agentRegistry.GetHealthyAgents()
            .OrderByDescending(agent => agent.IsManagerNode)
            .ThenBy(agent => agent.NodeName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (agents.Count == 0)
        {
            throw new InvalidOperationException("Docker image detection requires at least one healthy node agent.");
        }

        var failures = new List<string>();
        foreach (var agent in agents)
        {
            try
            {
                return await InspectImageOnAgentAsync(agent, imageReferenceWithTag, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "Failed to reach node agent {NodeName} for image inspection of {ImageReference}", agent.NodeName, imageReferenceWithTag);
                failures.Add($"{agent.NodeName}: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "Node agent {NodeName} could not inspect image {ImageReference}", agent.NodeName, imageReferenceWithTag);
                failures.Add($"{agent.NodeName}: {ex.Message}");
            }
        }

        throw new InvalidOperationException(
            $"Docker image '{imageReferenceWithTag}' could not be inspected by any healthy node agent. {string.Join(" | ", failures)}");
    }

    private async Task<AgentImageInspectResponse> InspectImageOnAgentAsync(NodeAgentEndpoint agent, string imageReferenceWithTag, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentException.ThrowIfNullOrWhiteSpace(imageReferenceWithTag);

        var httpClient = httpClientFactory.CreateClient();
        var requestUri = new Uri(new Uri(agent.InternalUrl), "/api/images/inspect");

        var response = await httpClient.PostAsJsonAsync(
            requestUri,
            new AgentInspectImageRequest { ImageReference = imageReferenceWithTag, PullIfMissing = true },
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorMessage = await ReadAgentErrorAsync(response, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(errorMessage);
        }

        var inspection = await response.Content.ReadFromJsonAsync<AgentImageInspectResponse>(cancellationToken).ConfigureAwait(false);
        if (inspection is null)
        {
            throw new InvalidOperationException($"Node agent '{agent.NodeName}' returned an empty image inspection response.");
        }

        logger.LogInformation("Inspected image {ImageReference} via node agent {NodeName}", imageReferenceWithTag, agent.NodeName);
        return inspection;
    }

    private static async Task<string> ReadAgentErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var responseBody = response.Content is null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return $"Node agent returned {(int)response.StatusCode} ({response.ReasonPhrase}).";
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(responseBody);
            if (document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                if (document.RootElement.TryGetProperty("error", out var errorProperty) && errorProperty.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    return errorProperty.GetString() ?? responseBody;
                }

                if (document.RootElement.TryGetProperty("detail", out var detailProperty) && detailProperty.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    return detailProperty.GetString() ?? responseBody;
                }

                if (document.RootElement.TryGetProperty("title", out var titleProperty) && titleProperty.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    return titleProperty.GetString() ?? responseBody;
                }
            }
        }
        catch (System.Text.Json.JsonException)
        {
        }

        return responseBody;
    }

    private static string? NormalizeVersionTag(string imageReference, string? versionTag)
    {
        if (!string.IsNullOrWhiteSpace(versionTag))
        {
            return versionTag.Trim();
        }

        var separatorIndex = imageReference.LastIndexOf(':');
        var slashIndex = imageReference.LastIndexOf('/');
        return separatorIndex > slashIndex && separatorIndex < imageReference.Length - 1
            ? imageReference[(separatorIndex + 1)..]
            : string.Empty;
    }

    private static string RemoveTag(string imageReference)
    {
        var separatorIndex = imageReference.LastIndexOf(':');
        var slashIndex = imageReference.LastIndexOf('/');
        return separatorIndex > slashIndex
            ? imageReference[..separatorIndex]
            : imageReference;
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

    private static List<DetectedPortDto> GetDetectedPorts(IReadOnlyList<string>? exposedPorts)
    {
        if (exposedPorts is null || exposedPorts.Count == 0)
        {
            return [];
        }

        return exposedPorts
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
            IsRequired = false
        });

        foreach (var additionalMatch in matchingPorts.Skip(1))
        {
            inferredMappings.Add(new DetectedSettingPortMappingDto
            {
                MappingRole = Models.V2.GameTypeSettingPortMappingRole.Related.ToString(),
                RelationType = Models.V2.GameTypeSettingPortRelationType.Offset.ToString(),
                TargetContainerPort = additionalMatch.ContainerPort,
                TargetProtocol = additionalMatch.Protocol,
                CalculationValue = additionalMatch.ContainerPort - defaultPort,
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

    private static List<DetectedVolumeDto> GetDetectedVolumes(IReadOnlyList<string>? volumes)
    {
        if (volumes is null || volumes.Count == 0)
        {
            return [];
        }

        return volumes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Select(x => new DetectedVolumeDto { ContainerPath = x })
            .ToList();
    }

    private sealed record AgentInspectImageRequest
    {
        public string ImageReference { get; init; } = string.Empty;
        public bool PullIfMissing { get; init; }
    }

    private sealed record AgentImageInspectResponse
    {
        public List<string> RepoDigests { get; init; } = [];
        public List<string> EnvironmentVariables { get; init; } = [];
        public List<string> ExposedPorts { get; init; } = [];
        public List<string> VolumePaths { get; init; } = [];
    }
}
