using Docker.DotNet.Models;
using GameServer.Docker.Configurations;
using GameServer.Docker.Constants;
using GameServer.Docker.Interfaces;
using GameServer.Docker.Models.V2;
using GameServer.Docker.Repositories.V2;
using Microsoft.Extensions.Options;
using GameServerModel = GameServer.Docker.Models.V2.GameServer;

namespace GameServer.Docker.Services.V2;

/// <summary>
/// Deploys and updates V2 GameServers by resolving volume snapshots and translating them
/// into Swarm service parameters. Mounts are passed through <see cref="IServiceOperations"/>
/// so the orchestrator never calls the Docker daemon directly.
/// </summary>
public sealed class GameServerDeploymentService(
    IGameServerRepository gameServerRepository,
    IGameTypeRepository gameTypeRepository,
    IVolumeSetupResolver volumeSetupResolver,
    INfsVolumePreparationService nfsVolumePreparationService,
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

        var volumes = ResolveServerVolumes(server, gameType, revision, volumeBindingLayout);

        // Prepare NFS-backed target folders (create + ownership/permissions) on the API host
        // before asking the agent to create the service.
        await nfsVolumePreparationService.PrepareAsync(volumes, cancellationToken).ConfigureAwait(false);

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
        var newVolumes = ResolveNewServerVolumes(server, gameType, revision, layout);
        var allVolumes = server.Volumes.Concat(newVolumes).ToList();

        // Prepare newly introduced NFS-backed target folders before updating the service.
        await nfsVolumePreparationService.PrepareAsync(newVolumes, cancellationToken).ConfigureAwait(false);

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

    private List<GameServerVolume> ResolveServerVolumes(
        GameServerModel server,
        GameType gameType,
        GameTypeRevision revision,
        string layout)
    {
        if (server.Volumes.Count > 0)
        {
            return server.Volumes.ToList();
        }

        return volumeSetupResolver
            .ResolveForCreate(server.ServerId, gameType.Key, revision.Volumes, layout, driverOverrides: null, settingValues: BuildSettingValues(server))
            .ToList();
    }

    private List<GameServerVolume> ResolveNewServerVolumes(
        GameServerModel server,
        GameType gameType,
        GameTypeRevision revision,
        string layout)
    {
        return volumeSetupResolver
            .ResolveForUpdate(server.ServerId, gameType.Key, revision.Volumes, server.Volumes, layout, driverOverrides: null, settingValues: BuildSettingValues(server))
            .ToList();
    }

    private static IReadOnlyDictionary<string, string?> BuildSettingValues(GameServerModel server)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var setting in server.Settings)
        {
            if (!string.IsNullOrWhiteSpace(setting.SettingKey))
            {
                values[setting.SettingKey] = setting.Value;
            }
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
        return volumes.Select(volume =>
        {
            var mountType = volume.MountType.ToString().ToLowerInvariant();
            Mount? mount = null;

            if (!string.IsNullOrWhiteSpace(volume.DriverOptionsJson))
            {
                try
                {
                    var options = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(volume.DriverOptionsJson);
                    mount = new Mount
                    {
                        Type = mountType,
                        Source = volume.Source,
                        Target = volume.ContainerPath,
                        ReadOnly = volume.ReadOnly,
                        VolumeOptions = new VolumeOptions
                        {
                            DriverConfig = new Driver
                            {
                                Name = volume.Driver,
                                Options = options ?? []
                            }
                        }
                    };
                }
                catch (System.Text.Json.JsonException ex)
                {
                    logger.LogWarning(ex, "Failed to deserialize driver options for volume {ContainerPath}", volume.ContainerPath);
                }
            }

            mount ??= new Mount
            {
                Type = mountType,
                Source = volume.Source,
                Target = volume.ContainerPath,
                ReadOnly = volume.ReadOnly
            };

            return mount;
        }).ToList();
    }
}
