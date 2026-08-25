using GameServer.API.Dtos.V2;
using GameServer.API.Repositories.V2;
using GameServerModel = GameServer.API.Models.V2.GameServer;
using GameServerSettingModel = GameServer.API.Models.V2.GameServerSetting;

namespace GameServer.API.Services.V2;
 
/// <summary>
/// Handles V2 GameServer create and update operations.
/// </summary>
public sealed class GameServerCommandService(
    IGameServerRepository repository,
    GameServerQueryService queryService,
    GameServerValidationService validationService,
    GameServerSpecBuilder specBuilder,
    GameServerDeploymentService deploymentService)
{
    /// <summary>
    /// Validates a V2 GameServer request.
    /// </summary>
    public Task<GameServerValidationResultDto> ValidateAsync(SaveGameServerRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return validationService.ValidateAsync(request, cancellationToken);
    }

    /// <summary>
    /// Produces a dry-run preview of the Swarm service that would be created for a request,
    /// without persisting anything or contacting Docker.
    /// </summary>
    public async Task<GameServerDeploymentPreviewDto> PreviewAsync(SaveGameServerRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var resolution = await validationService.ResolveAsync(request, cancellationToken).ConfigureAwait(false);
        return specBuilder.Build(request, resolution);
    }

    /// <summary>
    /// Performs a point-in-time availability check for individual published ports so the
    /// editor can validate port changes as they are made.
    /// </summary>
    public Task<GameServerPortAvailabilityResultDto> CheckPortAvailabilityAsync(GameServerPortAvailabilityRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return validationService.CheckPortAvailabilityAsync(request, cancellationToken);
    }

    /// <summary>
    /// Creates a V2 GameServer.
    /// </summary>
    public async Task<GameServerDetailDto> CreateAsync(SaveGameServerRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedRequest = NormalizeForCreate(request);
        var validation = await validationService.ValidateAsync(normalizedRequest, cancellationToken).ConfigureAwait(false);
        EnsureValid(validation, nameof(request));

        var created = await repository.CreateAsync(MapToModel(normalizedRequest)).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        await deploymentService.DeployAsync(created.ServerId, normalizedRequest.VolumeBindingLayout, cancellationToken).ConfigureAwait(false);

        return await queryService.GetByServerIdAsync(created.ServerId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Failed to reload created V2 GameServer.");
    }

    /// <summary>
    /// Updates a V2 GameServer.
    /// </summary>
    public async Task<GameServerDetailDto> UpdateAsync(string serverId, SaveGameServerRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrWhiteSpace(request.ServerId)
            && !string.Equals(serverId, request.ServerId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The route server id must match the payload server id.", nameof(serverId));
        }

        var existing = await repository.GetByServerIdAsync(serverId).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"V2 GameServer '{serverId}' was not found.");

        var normalizedRequest = NormalizeForUpdate(existing, request);
        var validation = await validationService.ValidateAsync(normalizedRequest, cancellationToken).ConfigureAwait(false);
        EnsureValid(validation, nameof(request));

        var updated = await repository.UpdateAsync(MapToModel(normalizedRequest, existing.Id)).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (existing.LastDeployedAt.HasValue || string.Equals(existing.Status, "Running", StringComparison.OrdinalIgnoreCase))
        {
            await deploymentService.UpdateDeploymentAsync(updated.ServerId, volumeBindingLayout: normalizedRequest.VolumeBindingLayout, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return await queryService.GetByServerIdAsync(updated.ServerId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Failed to reload updated V2 GameServer.");
    }

    /// <summary>
    /// Starts the Swarm service for a V2 GameServer.
    /// </summary>
    public async Task<GameServerDetailDto> StartAsync(string serverId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);
        cancellationToken.ThrowIfCancellationRequested();

        await deploymentService.StartAsync(serverId, cancellationToken).ConfigureAwait(false);
        return await queryService.GetByServerIdAsync(serverId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"V2 GameServer '{serverId}' was not found.");
    }

    /// <summary>
    /// Stops the Swarm service for a V2 GameServer.
    /// </summary>
    public async Task<GameServerDetailDto> StopAsync(string serverId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);
        cancellationToken.ThrowIfCancellationRequested();

        await deploymentService.StopAsync(serverId, cancellationToken).ConfigureAwait(false);
        return await queryService.GetByServerIdAsync(serverId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"V2 GameServer '{serverId}' was not found.");
    }

    /// <summary>
    /// Restarts the Swarm service for a V2 GameServer.
    /// </summary>
    public async Task<GameServerDetailDto> RestartAsync(string serverId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);
        cancellationToken.ThrowIfCancellationRequested();

        await deploymentService.RestartAsync(serverId, cancellationToken).ConfigureAwait(false);
        return await queryService.GetByServerIdAsync(serverId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"V2 GameServer '{serverId}' was not found.");
    }

    /// <summary>
    /// Redeploys and updates the Swarm service for a V2 GameServer.
    /// </summary>
    public async Task<GameServerDetailDto> RedeployAsync(string serverId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);
        cancellationToken.ThrowIfCancellationRequested();

        await deploymentService.UpdateDeploymentAsync(serverId, cancellationToken: cancellationToken).ConfigureAwait(false);
        return await queryService.GetByServerIdAsync(serverId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"V2 GameServer '{serverId}' was not found.");
    }

    /// <summary>
    /// Deletes a V2 GameServer.
    /// </summary>
    public async Task DeleteAsync(string serverId, bool softDelete = true, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);
        cancellationToken.ThrowIfCancellationRequested();

        var existing = await repository.GetByServerIdAsync(serverId).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"V2 GameServer '{serverId}' was not found.");

        await repository.DeleteAsync(existing.ServerId, softDelete).ConfigureAwait(false);
    }

    private static SaveGameServerRequestDto NormalizeForCreate(SaveGameServerRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var serverId = string.IsNullOrWhiteSpace(request.ServerId)
            ? Guid.NewGuid().ToString("N")
            : request.ServerId.Trim();

        return request with
        {
            ServerId = serverId,
            ServiceName = string.IsNullOrWhiteSpace(request.ServiceName) ? $"gameserver-{serverId}" : request.ServiceName.Trim(),
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Stopped" : request.Status.Trim()
        };
    }

    private static SaveGameServerRequestDto NormalizeForUpdate(GameServerModel existing, SaveGameServerRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(request);

        return request with
        {
            ServerId = existing.ServerId,
            ServiceName = string.IsNullOrWhiteSpace(request.ServiceName) ? existing.ServiceName : request.ServiceName.Trim(),
            Status = string.IsNullOrWhiteSpace(request.Status) ? existing.Status : request.Status.Trim()
        };
    }

    private static GameServerModel MapToModel(SaveGameServerRequestDto request, int id = 0)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new GameServerModel
        {
            Id = id,
            ServerId = request.ServerId ?? string.Empty,
            Name = request.Name,
            Description = request.Description,
            GameTypeRevisionId = request.GameTypeRevisionId,
            ServiceName = request.ServiceName ?? string.Empty,
            Status = request.Status ?? string.Empty,
            Settings = request.Settings
                .Where(setting => !string.IsNullOrWhiteSpace(setting.SettingKey))
                .Select(setting => new GameServerSettingModel
                {
                    Id = setting.Id,
                    SettingKey = setting.SettingKey,
                    Value = setting.Value
                })
                .ToList(),
            Ports = request.Ports
                .Select(port => new GameServer.API.Models.V2.GameServerPort
                {
                    ContainerPort = port.ContainerPort,
                    Protocol = port.Protocol,
                    PublishedPort = port.PublishedPort
                })
                .ToList()
        };
    }

    private static void EnsureValid(GameServerValidationResultDto validationResult, string paramName)
    {
        ArgumentNullException.ThrowIfNull(validationResult);
        ArgumentException.ThrowIfNullOrWhiteSpace(paramName);

        var firstBlockingIssue = validationResult.Issues.FirstOrDefault(issue => issue.IsBlocking);
        if (firstBlockingIssue is not null)
        {
            throw new ArgumentException(firstBlockingIssue.Message, paramName);
        }
    }
}
