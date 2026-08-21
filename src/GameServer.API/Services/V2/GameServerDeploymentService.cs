using Docker.DotNet.Models;
using GameServer.API.Configurations;
using GameServer.API.Constants;
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
    ILogger<GameServerDeploymentService> logger)
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

        List<GameServerVolume> volumes;
        if (server.Volumes.Count > 0)
        {
            // Already-persisted snapshots were provisioned during the initial deploy.
            volumes = server.Volumes.ToList();
        }
        else
        {
            var resolutions = await volumeSetupResolver
                .ResolveForCreateAsync(server.ServerId, gameType.Key, revision.Volumes, volumeBindingLayout, driverOverrides: null, settingValues: BuildSettingValues(server, gameType, revision), cancellationToken)
                .ConfigureAwait(false);

            // Provision each volume (one-time host-side work for NFS targets, no-op for named
            // volumes) on the API host before asking the agent to create the service.
            await ProvisionVolumesAsync(resolutions, cancellationToken).ConfigureAwait(false);

            volumes = resolutions.Select(r => r.Snapshot).ToList();
        }

        var parameters = BuildCreateParameters(server, revision, volumes);

        var response = await serviceOperations.CreateServiceAsync(parameters, cancellationToken).ConfigureAwait(false);

        server = server with
        {
            Status = "Running",
            LastDeployedAt = DateTime.UtcNow,
            Volumes = volumes.Select(v => v with { IsProvisioned = true }).ToList()
        };

        await gameServerRepository.UpdateAsync(server).ConfigureAwait(false);

        logger.LogInformation("Deployed V2 GameServer {ServerId} as service {ServiceId}", serverId, response.ID);
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
        var newResolutions = await ResolveNewServerVolumesAsync(server, gameType, revision, layout, cancellationToken).ConfigureAwait(false);
        var newVolumes = newResolutions.Select(r => r.Snapshot).ToList();
        var allVolumes = server.Volumes.Concat(newVolumes).ToList();

        // Provision newly introduced volumes before updating the service.
        await ProvisionVolumesAsync(newResolutions, cancellationToken).ConfigureAwait(false);

        var parameters = BuildUpdateParameters(server, revision, allVolumes, imageReference);
        await serviceOperations.UpdateServiceAsync(server.ServiceName, parameters, cancellationToken).ConfigureAwait(false);

        if (newVolumes.Count > 0)
        {
            server = server with
            {
                Volumes = allVolumes
            };
            await gameServerRepository.UpdateAsync(server).ConfigureAwait(false);
        }

        logger.LogInformation("Updated V2 GameServer {ServerId} service deployment", serverId);
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

    private ServiceCreateParameters BuildCreateParameters(
        GameServerModel server,
        GameTypeRevision revision,
        IReadOnlyList<GameServerVolume> volumes)
    {
        var labels = new Dictionary<string, string>
        {
            [ServiceLabels.Managed] = ServiceLabels.ManagedValue,
            [ServiceLabels.ServerId] = server.ServerId
        };

        return new ServiceCreateParameters
        {
            Service = new ServiceSpec
            {
                Name = server.ServiceName,
                Labels = labels,
                TaskTemplate = new TaskSpec
                {
                    ContainerSpec = new ContainerSpec
                    {
                        Image = revision.ImageReference,
                        Labels = labels,
                        Mounts = BuildDockerMounts(volumes),
                        TTY = revision.EnableTTY
                    },
                    Networks = [new NetworkAttachmentConfig { Target = "gameserver_overlay" }]
                }
            }
        };
    }

    private ServiceUpdateParameters BuildUpdateParameters(
        GameServerModel server,
        GameTypeRevision revision,
        IReadOnlyList<GameServerVolume> volumes,
        string? imageReference)
    {
        return new ServiceUpdateParameters
        {
            Service = new ServiceSpec
            {
                Name = server.ServiceName,
                TaskTemplate = new TaskSpec
                {
                    ContainerSpec = new ContainerSpec
                    {
                        Image = imageReference ?? revision.ImageReference,
                        Mounts = BuildDockerMounts(volumes),
                        TTY = revision.EnableTTY
                    },
                    ForceUpdate = (ulong)DateTime.UtcNow.Ticks,
                    Networks = [new NetworkAttachmentConfig { Target = "gameserver_overlay" }]
                }
            }
        };
    }

    private List<Mount> BuildDockerMounts(IReadOnlyList<GameServerVolume> volumes)
    {
        return volumes
            .Select(volume => mountTypeHandlerFactory.GetHandler(volume.MountType).BuildMount(volume))
            .ToList();
    }
}
