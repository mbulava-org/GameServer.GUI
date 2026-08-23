using System.Text.Json;
using System.Text.Json.Serialization;
using Docker.DotNet.Models;
using GameServer.API.Configurations;
using GameServer.API.Constants;
using GameServer.API.Dtos.V2;
using GameServer.API.Models.V2;

namespace GameServer.API.Services.V2;

/// <summary>
/// Builds the complete Swarm <see cref="ServiceCreateParameters"/> for a V2 GameServer from an
/// already-resolved <see cref="GameServerResolutionContext"/>.
/// <para>
/// This is the single source of truth for what a deployed service looks like: labels, networks,
/// environment variables, published ports and mounts. It is currently consumed by the deployment
/// preview; <see cref="GameServerDeploymentService"/> will be rewired to it in a follow-up pass.
/// </para>
/// </summary>
public sealed class GameServerSpecBuilder
{
    private static readonly JsonSerializerOptions RawJsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly NetworkOptions _networkOptions;

    public GameServerSpecBuilder(NetworkOptions networkOptions)
    {
        _networkOptions = networkOptions ?? throw new ArgumentNullException(nameof(networkOptions));
    }

    /// <summary>
    /// Builds the Docker Swarm <see cref="ServiceCreateParameters"/> for a save request resolution.
    /// </summary>
    public ServiceCreateParameters BuildCreateParameters(SaveGameServerRequestDto request, GameServerResolutionContext resolution)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(resolution);

        if (resolution.Revision is null)
        {
            throw new InvalidOperationException("The selected GameType revision could not be resolved.");
        }

        var revision = resolution.Revision;
        var gameTypeKey = resolution.GameType?.Key ?? "unknown";
        var serverId = string.IsNullOrWhiteSpace(request.ServerId) ? "<generated-on-save>" : request.ServerId;
        var serviceName = string.IsNullOrWhiteSpace(request.ServiceName)
            ? $"{gameTypeKey}-{serverId}"
            : request.ServiceName;

        var environment = BuildEnvironment(request, resolution);
        var ports = BuildPorts(resolution.Result.ResolvedPorts, resolution.Revision);
        var volumes = BuildVolumes(resolution.Result.ResolvedVolumes, []);

        var labels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ServiceLabels.Managed] = ServiceLabels.ManagedValue,
            [ServiceLabels.ServerId] = serverId
        };

        var networks = new List<GameServerPreviewNetworkDto>();

        // Attach the GameServer network only when one is configured.
        if (!string.IsNullOrWhiteSpace(_networkOptions.NetworkName))
        {
            networks.Add(new()
            {
                Name = _networkOptions.NetworkName,
                Driver = "overlay",
                Description = "Shared overlay network for all managed game server services."
            });
        }

        // Attach the load balancer network only when one or more web hosts are enabled.
        var hasEnabledWebHosts = resolution.Result.ResolvedWebHosts.Count > 0;
        if (hasEnabledWebHosts && !string.IsNullOrWhiteSpace(_networkOptions.LoadBalancerNetwork))
        {
            networks.Add(new()
            {
                Name = _networkOptions.LoadBalancerNetwork,
                Driver = "overlay",
                Description = "Load balancer network for reverse proxy discovery of web hosts."
            });
        }

        return new ServiceCreateParameters
        {
            Service = new ServiceSpec
            {
                Name = serviceName,
                Labels = labels,
                TaskTemplate = new TaskSpec
                {
                    ContainerSpec = new ContainerSpec
                    {
                        Image = !string.IsNullOrWhiteSpace(revision.VersionTag) ? $"{revision.ImageReference}:{revision.VersionTag}" : revision.ImageReference,
                        Labels = labels,
                        Env = environment.Select(entry => $"{entry.Key}={entry.Value}").ToList(),
                        Mounts = volumes.Select(ToMount).ToList(),
                        TTY = revision.EnableTTY
                    },
                    Networks = networks
                        .Select(network => new NetworkAttachmentConfig { Target = network.Name })
                        .ToList()
                },
                EndpointSpec = new EndpointSpec
                {
                    Ports = ports
                        .Where(port => port.Published)
                        .Select(port => new PortConfig
                        {
                            Protocol = port.Protocol,
                            TargetPort = (uint)port.ContainerPort,
                            PublishedPort = (uint)port.PublishedPort,
                            PublishMode = "ingress"
                        })
                        .ToList()
                }
            }
        };
    }

    /// <summary>
    /// Produces the deployment preview for a save request resolution.
    /// </summary>
    public GameServerDeploymentPreviewDto Build(SaveGameServerRequestDto request, GameServerResolutionContext resolution)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(resolution);

        var notices = new List<string>();

        if (resolution.Revision is null)
        {
            notices.Add("The selected GameType revision could not be resolved, so no service spec could be generated.");
            return new GameServerDeploymentPreviewDto
            {
                Issues = resolution.Result.Issues,
                Notices = notices
            };
        }

        var revision = resolution.Revision;
        var gameTypeKey = resolution.GameType?.Key ?? "unknown";
        var serverId = string.IsNullOrWhiteSpace(request.ServerId) ? "<generated-on-save>" : request.ServerId;
        var serviceName = string.IsNullOrWhiteSpace(request.ServiceName)
            ? $"{gameTypeKey}-{serverId}"
            : request.ServiceName;

        var environment = BuildEnvironment(request, resolution);
        var ports = BuildPorts(resolution.Result.ResolvedPorts, resolution.Revision);
        var volumes = BuildVolumes(resolution.Result.ResolvedVolumes, notices);

        var labels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ServiceLabels.Managed] = ServiceLabels.ManagedValue,
            [ServiceLabels.ServerId] = serverId
        };

        var networks = new List<GameServerPreviewNetworkDto>();

        // Attach the GameServer network only when one is configured.
        if (!string.IsNullOrWhiteSpace(_networkOptions.NetworkName))
        {
            networks.Add(new()
            {
                Name = _networkOptions.NetworkName,
                Driver = "overlay",
                Description = "Shared overlay network for all managed game server services."
            });
        }

        // Attach the load balancer network only when one or more web hosts are enabled.
        var hasEnabledWebHosts = resolution.Result.ResolvedWebHosts.Count > 0;
        if (hasEnabledWebHosts && !string.IsNullOrWhiteSpace(_networkOptions.LoadBalancerNetwork))
        {
            networks.Add(new()
            {
                Name = _networkOptions.LoadBalancerNetwork,
                Driver = "overlay",
                Description = "Load balancer network for reverse proxy discovery of web hosts."
            });
        }

        var parameters = BuildCreateParameters(request, resolution);

        if (environment.Count == 0)
        {
            notices.Add("The selected revision defines no settings, so no environment variables will be set.");
        }

        if (ports.Count == 0)
        {
            notices.Add("The selected revision defines no ports.");
        }

        return new GameServerDeploymentPreviewDto
        {
            ServiceName = serviceName,
            ServerId = serverId,
            GameTypeKey = gameTypeKey,
            ImageReference = revision.ImageReference,
            VersionTag = revision.VersionTag,
            EnableTTY = revision.EnableTTY,
            VolumeBindingLayout = request.VolumeBindingLayout,
            Labels = labels,
            Networks = networks,
            EnvironmentVariables = environment,
            Ports = ports,
            Volumes = volumes,
            Issues = resolution.Result.Issues,
            Notices = notices,
            RawServiceSpecJson = JsonSerializer.Serialize(parameters, RawJsonOptions)
        };
    }

    private static List<GameServerPreviewEnvironmentVariableDto> BuildEnvironment(SaveGameServerRequestDto request, GameServerResolutionContext resolution)
    {
        var revision = resolution.Revision;
        if (revision is null)
        {
            return [];
        }

        var tokens = BuildPreviewTokens(request, resolution);

        return revision.SettingDefinitions
            .Where(definition => !string.IsNullOrWhiteSpace(definition.SettingKey))
            .OrderBy(definition => definition.DisplayOrder)
            .ThenBy(definition => definition.SettingKey, StringComparer.OrdinalIgnoreCase)
            .Select(definition =>
            {
                resolution.EffectiveSettings.TryGetValue(definition.SettingKey, out var effectiveValue);

                var dataType = definition.Metadata?.DataType;
                var isServerVariable = string.Equals(dataType, ServerVariableExpander.ServerVariableDataType, StringComparison.OrdinalIgnoreCase);
                var rawValue = effectiveValue;

                if (isServerVariable)
                {
                    effectiveValue = ServerVariableExpander.Resolve(effectiveValue, tokens);
                }

                return new GameServerPreviewEnvironmentVariableDto
                {
                    Key = definition.SettingKey,
                    Value = effectiveValue,
                    RawValue = rawValue,
                    DataType = dataType,
                    Category = definition.Metadata?.Category,
                    IsExpanded = isServerVariable && !string.Equals(rawValue, effectiveValue, StringComparison.Ordinal),
                    UsesDefault = effectiveValue is not null && string.Equals(effectiveValue, definition.DefaultValue, StringComparison.Ordinal)
                };
            })
            .ToList();
    }

    private static Dictionary<string, string?> BuildPreviewTokens(SaveGameServerRequestDto request, GameServerResolutionContext resolution)
    {
        var gameTypeKey = resolution.GameType?.Key;
        var serverId = string.IsNullOrWhiteSpace(request.ServerId) ? "<generated-on-save>" : request.ServerId;
        var serviceName = string.IsNullOrWhiteSpace(request.ServiceName)
            ? $"{gameTypeKey ?? "unknown"}-{serverId}"
            : request.ServiceName;

        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ServerId"] = serverId,
            ["Name"] = request.Name,
            ["ServiceName"] = serviceName,
            ["Description"] = request.Description,
            ["Status"] = request.Status,
            ["GameTypeKey"] = gameTypeKey,
            ["RevisionVersionTag"] = resolution.Revision?.VersionTag,
            ["RevisionImageReference"] = resolution.Revision?.ImageReference
        };
    }

    private static List<GameServerPreviewPortDto> BuildPorts(
        IReadOnlyList<GameServerResolvedPortDto> resolvedPorts,
        GameTypeRevision? revision)
    {
        var revisionPorts = revision?.Ports.OrderBy(p => p.DisplayOrder).ToList() ?? [];

        return resolvedPorts
            .OrderBy(port => port.DisplayOrder)
            .Select((resolvedPort, index) =>
            {
                var revisionPort = revisionPorts.FirstOrDefault(p => p.DisplayOrder == resolvedPort.DisplayOrder)
                    ?? (index < revisionPorts.Count ? revisionPorts[index] : null)
                    ?? revisionPorts.FirstOrDefault(p => string.Equals(p.Protocol, resolvedPort.Protocol, StringComparison.OrdinalIgnoreCase));

                var containerPort = revisionPort?.ContainerPort ?? resolvedPort.ContainerPort;
                var publishedPort = resolvedPort.ContainerPort;

                return new GameServerPreviewPortDto
                {
                    ContainerPort = containerPort,
                    PublishedPort = publishedPort,
                    Protocol = string.IsNullOrWhiteSpace(resolvedPort.Protocol) ? "tcp" : resolvedPort.Protocol.ToLowerInvariant(),
                    Published = resolvedPort.AdvertisedPort,
                    PublishMode = resolvedPort.AdvertisedPort ? "ingress" : "not published",
                    Description = resolvedPort.Description
                };
            })
            .ToList();
    }

    private static List<GameServerPreviewVolumeDto> BuildVolumes(
        IReadOnlyList<GameServerResolvedVolumeDto> resolvedVolumes,
        List<string> notices)
    {
        if (resolvedVolumes.Count == 0)
        {
            notices.Add("Volume/mount resolution is currently unavailable, so no mounts can be previewed. This is expected while mount-type configuration is being validated.");
            return [];
        }

        return resolvedVolumes
            .Select(volume => new GameServerPreviewVolumeDto
            {
                Usage = volume.Usage,
                VolumeName = volume.VolumeName,
                ContainerPath = volume.ContainerPath,
                MountType = volume.MountType,
                ReadOnly = volume.ReadOnly,
                DriverOptionsJson = volume.DriverOptionsJson,
                OwnerUid = volume.OwnerUid,
                OwnerGid = volume.OwnerGid,
                Permissions = volume.Permissions
            })
            .ToList();
    }

    private static Mount ToMount(GameServerPreviewVolumeDto volume)
    {
        var mount = new Mount
        {
            Type = string.Equals(volume.MountType, "bind", StringComparison.OrdinalIgnoreCase)
                ? "bind"
                : string.Equals(volume.MountType, "tmpfs", StringComparison.OrdinalIgnoreCase)
                    ? "tmpfs"
                    : "volume",
            Source = volume.VolumeName,
            Target = volume.ContainerPath,
            ReadOnly = volume.ReadOnly
        };

        if (string.IsNullOrWhiteSpace(volume.DriverOptionsJson))
        {
            return mount;
        }

        try
        {
            var options = JsonSerializer.Deserialize<Dictionary<string, string>>(volume.DriverOptionsJson);
            mount.VolumeOptions = new VolumeOptions
            {
                DriverConfig = new Driver
                {
                    Name = "local",
                    Options = options ?? []
                }
            };
        }
        catch (JsonException)
        {
            // Malformed driver options are already surfaced as validation issues.
        }

        return mount;
    }
}
