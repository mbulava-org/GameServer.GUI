using Docker.DotNet.Models;
using GameServer.API.Configurations;
using GameServer.API.Constants;
using GameServer.API.Dtos.V2;
using GameServer.API.Interfaces;
using GameServer.API.Models.V2;
using GameServer.API.Repositories.V2;
using GameServer.API.Services.V2.MountTypeHandlers;
using Microsoft.Extensions.Options;
using GameServerModel = GameServer.API.Models.V2.GameServer;

namespace GameServer.API.Services.V2;

/// <summary>
/// Deploys and updates V2 GameServers by resolving volume snapshots and translating them
/// into Swarm service parameters. Mounts are passed through <see cref="IServiceOperations"/>
/// so the orchestrator never calls the Docker daemon directly.
/// </summary>
public sealed class GameServerDeploymentService(
    IGameServerRepository gameServerRepository,
    IGameTypeRepository gameTypeRepository,
    IVolumeSetupResolver volumeSetupResolver,
    IMountTypeHandlerFactory mountTypeHandlerFactory,
    IServiceOperations serviceOperations,
    GameServerValidationService validationService,
    GameServerSpecBuilder specBuilder,
    ILogger<GameServerDeploymentService> logger,
    IGameServerResourceCollector? resourceCollector = null)
{
    /// <summary>
    /// Creates the Swarm service for a V2 GameServer and marks the server as deployed.
    /// </summary>
    public async Task DeployAsync(string serverId, string volumeBindingLayout = "standard", CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);
        cancellationToken.ThrowIfCancellationRequested();

        var server = await gameServerRepository.GetByServerIdAsync(serverId)
            ?? throw new KeyNotFoundException($"V2 GameServer '{serverId}' was not found");

        var (gameType, revision) = await ResolveGameTypeAndRevisionAsync(server.GameTypeRevisionId, cancellationToken).ConfigureAwait(false);

        var saveRequest = new SaveGameServerRequestDto
        {
            ServerId = server.ServerId,
            Name = server.Name,
            Description = server.Description,
            GameTypeRevisionId = server.GameTypeRevisionId,
            ServiceName = server.ServiceName,
            Status = server.Status,
            VolumeBindingLayout = volumeBindingLayout,
            Ports = server.Ports
                .Select(p => new GameServerPortDto
                {
                    ContainerPort = p.ContainerPort,
                    Protocol = p.Protocol,
                    PublishedPort = p.PublishedPort
                })
                .ToList(),
            Settings = server.Settings
                .Select(s => new GameServerSettingDto
                {
                    SettingKey = s.SettingKey,
                    Value = s.Value
                })
                .ToList()
        };

        var resolutionContext = await validationService.ResolveAsync(saveRequest, cancellationToken).ConfigureAwait(false);

        var effectiveSettings = BuildSettingValues(server, gameType, revision);
        var resolutions = await volumeSetupResolver
            .ResolveForCreateAsync(server.ServerId, gameType.Key, revision.Volumes, volumeBindingLayout, driverOverrides: null, settingValues: effectiveSettings, cancellationToken)
            .ConfigureAwait(false);

        // Provision each volume (one-time host-side work for NFS targets, no-op for named
        // volumes) on the API host before asking the agent to create the service.
        await ProvisionVolumesAsync(resolutions, cancellationToken).ConfigureAwait(false);

        var parameters = specBuilder.BuildCreateParameters(saveRequest, resolutionContext);

        var response = await serviceOperations.CreateServiceAsync(parameters, cancellationToken).ConfigureAwait(false);

        var volumes = resolutions.Select(r => r.Snapshot with { IsProvisioned = true }).ToList();

        server = server with
        {
            Status = "Preparing",
            LastDeployedAt = DateTime.UtcNow,
            Volumes = volumes
        };

        await gameServerRepository.UpdateAsync(server).ConfigureAwait(false);

        logger.LogInformation("Deployed V2 GameServer {ServerId} as service {ServiceId}", serverId, response.ID);
        _ = resourceCollector?.TriggerImmediateCollectionAsync(serverId, CancellationToken.None);
    }

    /// <summary>
    /// Starts the Swarm service for a V2 GameServer (or deploys it if it does not yet exist).
    /// </summary>
    public async Task StartAsync(string serverId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);
        cancellationToken.ThrowIfCancellationRequested();

        var server = await gameServerRepository.GetByServerIdAsync(serverId)
            ?? throw new KeyNotFoundException($"V2 GameServer '{serverId}' was not found");

        var existingServices = await serviceOperations.ListServicesAsync(serviceName: server.ServiceName, cancellationToken: cancellationToken).ConfigureAwait(false);
        var existing = existingServices.FirstOrDefault(s => string.Equals(s.Spec?.Name, server.ServiceName, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            var updateParams = new ServiceUpdateParameters
            {
                Service = new ServiceSpec
                {
                    Name = server.ServiceName,
                    Mode = new ServiceMode
                    {
                        Replicated = new ReplicatedService { Replicas = 1 }
                    }
                }
            };
            await serviceOperations.UpdateServiceAsync(server.ServiceName, updateParams, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await DeployAsync(serverId, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        server = server with { Status = existing != null ? "Starting" : "Preparing" };
        await gameServerRepository.UpdateAsync(server).ConfigureAwait(false);
        logger.LogInformation("Started V2 GameServer {ServerId} service {ServiceName}", serverId, server.ServiceName);
        _ = resourceCollector?.TriggerImmediateCollectionAsync(serverId, CancellationToken.None);
    }

    /// <summary>
    /// Stops the Swarm service for a V2 GameServer by scaling replicas to 0.
    /// </summary>
    public async Task StopAsync(string serverId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);
        cancellationToken.ThrowIfCancellationRequested();

        var server = await gameServerRepository.GetByServerIdAsync(serverId)
            ?? throw new KeyNotFoundException($"V2 GameServer '{serverId}' was not found");

        var existingServices = await serviceOperations.ListServicesAsync(serviceName: server.ServiceName, cancellationToken: cancellationToken).ConfigureAwait(false);
        var existing = existingServices.FirstOrDefault(s => string.Equals(s.Spec?.Name, server.ServiceName, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            var updateParams = new ServiceUpdateParameters
            {
                Service = new ServiceSpec
                {
                    Name = server.ServiceName,
                    Mode = new ServiceMode
                    {
                        Replicated = new ReplicatedService { Replicas = 0 }
                    }
                }
            };
            await serviceOperations.UpdateServiceAsync(server.ServiceName, updateParams, cancellationToken).ConfigureAwait(false);
        }

        server = server with { Status = "Stopped" };
        await gameServerRepository.UpdateAsync(server).ConfigureAwait(false);
        logger.LogInformation("Stopped V2 GameServer {ServerId} service {ServiceName}", serverId, server.ServiceName);
    }

    /// <summary>
    /// Restarts the Swarm service for a V2 GameServer by forcing a container recreation.
    /// </summary>
    public async Task RestartAsync(string serverId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);
        cancellationToken.ThrowIfCancellationRequested();

        var server = await gameServerRepository.GetByServerIdAsync(serverId)
            ?? throw new KeyNotFoundException($"V2 GameServer '{serverId}' was not found");

        var existingServices = await serviceOperations.ListServicesAsync(serviceName: server.ServiceName, cancellationToken: cancellationToken).ConfigureAwait(false);
        var existing = existingServices.FirstOrDefault(s => string.Equals(s.Spec?.Name, server.ServiceName, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            var saveRequest = CreateSaveRequest(server, "standard");
            var resolutionContext = await validationService.ResolveAsync(saveRequest, cancellationToken).ConfigureAwait(false);
            var desiredCreateParameters = specBuilder.BuildCreateParameters(saveRequest, resolutionContext);
            var desiredServiceSpec = desiredCreateParameters.Service;

            if (desiredServiceSpec.TaskTemplate?.ContainerSpec is not null)
            {
                desiredServiceSpec.TaskTemplate.ContainerSpec.Mounts = BuildDockerMounts(server.Volumes);
            }

            if (desiredServiceSpec.TaskTemplate != null)
            {
                desiredServiceSpec.TaskTemplate.ForceUpdate = (ulong)DateTime.UtcNow.Ticks;
            }

            var updateParams = new ServiceUpdateParameters
            {
                Service = desiredServiceSpec
            };
            await serviceOperations.UpdateServiceAsync(server.ServiceName, updateParams, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await DeployAsync(serverId, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        server = server with { Status = "Preparing" };
        await gameServerRepository.UpdateAsync(server).ConfigureAwait(false);
        logger.LogInformation("Restarted V2 GameServer {ServerId} service {ServiceName}", serverId, server.ServiceName);
        _ = resourceCollector?.TriggerImmediateCollectionAsync(serverId, CancellationToken.None);
    }

    /// <summary>
    /// Updates the Swarm service for a V2 GameServer, preserving existing volume snapshots
    /// and applying only newly introduced mounts.
    /// </summary>
    public async Task UpdateDeploymentAsync(
        string serverId,
        string? imageReference = null,
        string? volumeBindingLayout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);
        cancellationToken.ThrowIfCancellationRequested();

        var server = await gameServerRepository.GetByServerIdAsync(serverId)
            ?? throw new KeyNotFoundException($"V2 GameServer '{serverId}' was not found");

        var (gameType, revision) = await ResolveGameTypeAndRevisionAsync(server.GameTypeRevisionId, cancellationToken).ConfigureAwait(false);

        var layout = volumeBindingLayout ?? "standard";
        var saveRequest = CreateSaveRequest(server, layout);

        var resolutionContext = await validationService.ResolveAsync(saveRequest, cancellationToken).ConfigureAwait(false);

        var newResolutions = await ResolveNewServerVolumesAsync(server, gameType, revision, layout, cancellationToken).ConfigureAwait(false);
        var newVolumes = newResolutions.Select(r => r.Snapshot with { IsProvisioned = true }).ToList();
        var allVolumes = server.Volumes.Concat(newVolumes).ToList();

        // Provision newly introduced volumes before updating the service.
        await ProvisionVolumesAsync(newResolutions, cancellationToken).ConfigureAwait(false);

        // Build the desired parameters using GameServerSpecBuilder as single source of truth for Labels, Networks, Env, Ports, TTY, Mounts
        var desiredCreateParameters = specBuilder.BuildCreateParameters(saveRequest, resolutionContext);
        var desiredServiceSpec = desiredCreateParameters.Service;

        if (!string.IsNullOrWhiteSpace(imageReference) && desiredServiceSpec.TaskTemplate?.ContainerSpec is not null)
        {
            desiredServiceSpec.TaskTemplate.ContainerSpec.Image = imageReference;
        }

        if (desiredServiceSpec.TaskTemplate?.ContainerSpec is not null)
        {
            desiredServiceSpec.TaskTemplate.ContainerSpec.Mounts = BuildDockerMounts(allVolumes);
        }

        var existingServices = await serviceOperations.ListServicesAsync(serviceName: server.ServiceName, cancellationToken: cancellationToken).ConfigureAwait(false);
        var existingService = existingServices.FirstOrDefault(s => string.Equals(s.Spec?.Name, server.ServiceName, StringComparison.OrdinalIgnoreCase));

        if (existingService != null)
        {
            if (HasSpecChanged(existingService.Spec, desiredServiceSpec))
            {
                var updateParams = new ServiceUpdateParameters
                {
                    Service = desiredServiceSpec
                };
                if (updateParams.Service.TaskTemplate != null)
                {
                    updateParams.Service.TaskTemplate.ForceUpdate = (ulong)DateTime.UtcNow.Ticks;
                }
                await serviceOperations.UpdateServiceAsync(server.ServiceName, updateParams, cancellationToken).ConfigureAwait(false);
                logger.LogInformation("Updated V2 GameServer {ServerId} service deployment with new spec", serverId);
            }
            else
            {
                logger.LogInformation("V2 GameServer {ServerId} service deployment is already up to date; skipping Docker service update", serverId);
            }
        }
        else
        {
            var response = await serviceOperations.CreateServiceAsync(desiredCreateParameters, cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Created V2 GameServer {ServerId} service deployment {ServiceId}", serverId, response.ID);
        }

        server = server with
        {
            Status = "Preparing",
            LastDeployedAt = DateTime.UtcNow,
            Volumes = allVolumes
        };
        await gameServerRepository.UpdateAsync(server).ConfigureAwait(false);

        logger.LogInformation("Updated V2 GameServer {ServerId} service deployment", serverId);
        _ = resourceCollector?.TriggerImmediateCollectionAsync(serverId, CancellationToken.None);
    }

    /// <summary>
    /// Compares an existing Docker Swarm ServiceSpec with a desired ServiceSpec to determine
    /// if Labels, Networks, Environment Variables, Ports, Mounts, Image, or TTY have changed.
    /// </summary>
    public static bool HasSpecChanged(ServiceSpec? existing, ServiceSpec? desired)
    {
        if (existing is null && desired is null)
        {
            return false;
        }

        if (existing is null || desired is null)
        {
            return true;
        }

        // 1. Labels on ServiceSpec
        if (!DictionariesEqual(existing.Labels, desired.Labels))
        {
            return true;
        }

        var existingContainer = existing.TaskTemplate?.ContainerSpec;
        var desiredContainer = desired.TaskTemplate?.ContainerSpec;

        if (existingContainer is null != (desiredContainer is null))
        {
            return true;
        }

        if (existingContainer is not null && desiredContainer is not null)
        {
            // Image
            if (!string.Equals(existingContainer.Image, desiredContainer.Image, StringComparison.Ordinal))
            {
                return true;
            }

            // TTY
            if (existingContainer.TTY != desiredContainer.TTY)
            {
                return true;
            }

            // ContainerSpec Labels
            if (!DictionariesEqual(existingContainer.Labels, desiredContainer.Labels))
            {
                return true;
            }

            // Environment variables
            if (!EnvListsEqual(existingContainer.Env, desiredContainer.Env))
            {
                return true;
            }

            // Mounts
            if (!MountsEqual(existingContainer.Mounts, desiredContainer.Mounts))
            {
                return true;
            }

            // User
            if (!string.Equals(existingContainer.User, desiredContainer.User, StringComparison.Ordinal))
            {
                return true;
            }

            // DNSConfig
            if (!DNSConfigEqual(existingContainer.DNSConfig, desiredContainer.DNSConfig))
            {
                return true;
            }
        }

        // 2. Networks on TaskTemplate
        if (!NetworksEqual(existing.TaskTemplate?.Networks, desired.TaskTemplate?.Networks))
        {
            return true;
        }

        // 3. EndpointSpec Ports
        if (!PortsEqual(existing.EndpointSpec?.Ports, desired.EndpointSpec?.Ports))
        {
            return true;
        }

        return false;
    }

    private static bool DictionariesEqual(IDictionary<string, string>? a, IDictionary<string, string>? b)
    {
        var countA = a?.Count ?? 0;
        var countB = b?.Count ?? 0;
        if (countA == 0 && countB == 0) return true;
        if (countA != countB || a is null || b is null) return false;

        foreach (var (key, val) in a)
        {
            if (!b.TryGetValue(key, out var bVal) || !string.Equals(val, bVal, StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    private static bool EnvListsEqual(IList<string>? a, IList<string>? b)
    {
        var countA = a?.Count ?? 0;
        var countB = b?.Count ?? 0;
        if (countA == 0 && countB == 0) return true;
        if (countA != countB || a is null || b is null) return false;

        var setA = new HashSet<string>(a, StringComparer.Ordinal);
        var setB = new HashSet<string>(b, StringComparer.Ordinal);
        return setA.SetEquals(setB);
    }

    private static bool NetworksEqual(IList<NetworkAttachmentConfig>? a, IList<NetworkAttachmentConfig>? b)
    {
        var countA = a?.Count ?? 0;
        var countB = b?.Count ?? 0;
        if (countA == 0 && countB == 0) return true;
        if (countA != countB || a is null || b is null) return false;

        var setA = a.Select(n => n.Target ?? string.Empty).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var setB = b.Select(n => n.Target ?? string.Empty).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return setA.SetEquals(setB);
    }

    private static bool MountsEqual(IList<Mount>? a, IList<Mount>? b)
    {
        var countA = a?.Count ?? 0;
        var countB = b?.Count ?? 0;
        if (countA == 0 && countB == 0) return true;
        if (countA != countB || a is null || b is null) return false;

        var listA = a.Select(m => $"{m.Type}|{m.Source}|{m.Target}|{m.ReadOnly}").ToHashSet(StringComparer.Ordinal);
        var listB = b.Select(m => $"{m.Type}|{m.Source}|{m.Target}|{m.ReadOnly}").ToHashSet(StringComparer.Ordinal);
        return listA.SetEquals(listB);
    }

    private static bool PortsEqual(IList<PortConfig>? a, IList<PortConfig>? b)
    {
        var countA = a?.Count ?? 0;
        var countB = b?.Count ?? 0;
        if (countA == 0 && countB == 0) return true;
        if (countA != countB || a is null || b is null) return false;

        var listA = a.Select(p => $"{p.Protocol?.ToLowerInvariant()}|{p.TargetPort}|{p.PublishedPort}|{p.PublishMode}").ToHashSet(StringComparer.Ordinal);
        var listB = b.Select(p => $"{p.Protocol?.ToLowerInvariant()}|{p.TargetPort}|{p.PublishedPort}|{p.PublishMode}").ToHashSet(StringComparer.Ordinal);
        return listA.SetEquals(listB);
    }

    private static SaveGameServerRequestDto CreateSaveRequest(GameServerModel server, string volumeBindingLayout)
    {
        return new SaveGameServerRequestDto
        {
            ServerId = server.ServerId,
            Name = server.Name,
            Description = server.Description,
            GameTypeRevisionId = server.GameTypeRevisionId,
            ServiceName = server.ServiceName,
            Status = server.Status,
            VolumeBindingLayout = volumeBindingLayout,
            Ports = server.Ports
                .Select(p => new GameServerPortDto
                {
                    ContainerPort = p.ContainerPort,
                    Protocol = p.Protocol,
                    PublishedPort = p.PublishedPort
                })
                .ToList(),
            Settings = server.Settings
                .Select(s => new GameServerSettingDto
                {
                    SettingKey = s.SettingKey,
                    Value = s.Value
                })
                .ToList()
        };
    }

    private async Task<List<VolumeSetupResolution>> ResolveNewServerVolumesAsync(
        GameServerModel server,
        GameType gameType,
        GameTypeRevision revision,
        string layout,
        CancellationToken cancellationToken)
    {
        var resolved = await volumeSetupResolver
            .ResolveForUpdateAsync(server.ServerId, gameType.Key, revision.Volumes, server.Volumes, layout, driverOverrides: null, settingValues: BuildSettingValues(server, gameType, revision), cancellationToken)
            .ConfigureAwait(false);

        return resolved.ToList();
    }

    private async Task ProvisionVolumesAsync(
        IReadOnlyList<VolumeSetupResolution> resolutions,
        CancellationToken cancellationToken)
    {
        foreach (var resolution in resolutions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var handler = mountTypeHandlerFactory.GetHandler(resolution.Provisioning.MountType);
            await handler.PrepareAsync(resolution.Provisioning, cancellationToken).ConfigureAwait(false);
        }
    }

    private static IReadOnlyDictionary<string, string?> BuildSettingValues(
        GameServerModel server,
        GameType gameType,
        GameTypeRevision revision)
    {
        var tokenValues = ServerVariableExpander.BuildTokenValues(server, gameType, revision);

        var serverVariableKeys = revision.SettingDefinitions
            .Where(definition => string.Equals(definition.Metadata?.DataType, ServerVariableExpander.ServerVariableDataType, StringComparison.OrdinalIgnoreCase))
            .Select(definition => definition.SettingKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var setting in server.Settings)
        {
            if (string.IsNullOrWhiteSpace(setting.SettingKey))
            {
                continue;
            }

            values[setting.SettingKey] = serverVariableKeys.Contains(setting.SettingKey)
                ? ServerVariableExpander.Resolve(setting.Value, tokenValues)
                : setting.Value;
        }

        return values;
    }

    private async Task<(GameType GameType, GameTypeRevision Revision)> ResolveGameTypeAndRevisionAsync(
        int revisionId,
        CancellationToken cancellationToken)
    {
        var gameTypes = await gameTypeRepository.GetAllAsync(includeInactive: true).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var match = gameTypes
            .SelectMany(gt => gt.Revisions.Select(rev => (GameType: gt, Revision: rev)))
            .FirstOrDefault(t => t.Revision.Id == revisionId);

        if (match.GameType is null)
        {
            throw new InvalidOperationException($"GameTypeRevision '{revisionId}' was not found");
        }

        return match;
    }

    private List<Mount> BuildDockerMounts(IReadOnlyList<GameServerVolume> volumes)
    {
        return volumes
            .Select(volume => mountTypeHandlerFactory.GetHandler(volume.MountType).BuildMount(volume))
            .ToList();
    }

    private static bool DNSConfigEqual(DNSConfig? a, DNSConfig? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;

        var nsA = a.Nameservers ?? (IList<string>)Array.Empty<string>();
        var nsB = b.Nameservers ?? (IList<string>)Array.Empty<string>();

        return nsA.SequenceEqual(nsB, StringComparer.OrdinalIgnoreCase);
    }
}

