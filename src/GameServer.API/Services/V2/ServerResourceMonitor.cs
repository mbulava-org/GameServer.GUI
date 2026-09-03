using Docker.DotNet.Models;
using GameServer.API.Interfaces;
using GameServer.API.Models;

namespace GameServer.API.Services.V2;

/// <summary>
/// V2-compatible resource monitor that aggregates Docker Swarm service state
/// with real-time container statistics from Node Agents.
/// </summary>
public sealed class ServerResourceMonitor : IServerResourceMonitor
{
    private readonly IServiceOperations _serviceOperations;
    private readonly INodeAgentDiscovery _nodeAgentDiscovery;
    private readonly GameServerQueryService _gameServerQueryService;
    private readonly ILogger<ServerResourceMonitor> _logger;

    public ServerResourceMonitor(
        IServiceOperations serviceOperations,
        INodeAgentDiscovery nodeAgentDiscovery,
        GameServerQueryService gameServerQueryService,
        ILogger<ServerResourceMonitor> logger)
    {
        _serviceOperations = serviceOperations;
        _nodeAgentDiscovery = nodeAgentDiscovery;
        _gameServerQueryService = gameServerQueryService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ServerResourceUsage> StreamResourceUsageAsync(
        string serverId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);

        var server = await _gameServerQueryService.GetByServerIdAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (server == null)
        {
            _logger.LogWarning("Cannot stream resources: server {ServerId} not found in V2 repository", serverId);
            yield break;
        }

        var serviceName = server.ServiceName;

        while (!cancellationToken.IsCancellationRequested)
        {
            var usage = await GetSnapshotInternalAsync(serverId, serviceName, cancellationToken).ConfigureAwait(false);
            if (usage != null)
            {
                yield return usage;
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
        }
    }

    /// <inheritdoc />
    public async Task<ServerResourceUsage?> GetSnapshotAsync(string serverId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);

        var server = await _gameServerQueryService.GetByServerIdAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (server == null)
        {
            _logger.LogWarning("Cannot get resource snapshot: server {ServerId} not found in V2 repository", serverId);
            return null;
        }

        return await GetSnapshotInternalAsync(serverId, server.ServiceName, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ServerResourceUsage?> GetSnapshotInternalAsync(
        string serverId,
        string serviceName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var services = await _serviceOperations.ListServicesAsync(
                serviceName: serviceName,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var service = services.FirstOrDefault();
            if (service == null)
            {
                _logger.LogDebug("Service {ServiceName} for server {ServerId} not found", serviceName, serverId);
                return null;
            }

            var tasks = await _serviceOperations.ListTasksAsync(
                new TasksListParameters
                {
                    Filters = new Dictionary<string, IDictionary<string, bool>>
                    {
                        ["service"] = new Dictionary<string, bool> { [service.ID] = true },
                        ["desired-state"] = new Dictionary<string, bool> { ["running"] = true }
                    }
                },
                cancellationToken).ConfigureAwait(false);

            var preparingTasks = tasks.Count(t => t.Status?.State is TaskState.Preparing or TaskState.Allocated or TaskState.Assigned or TaskState.Accepted or TaskState.New);
            var startingTasks = tasks.Count(t => t.Status?.State is TaskState.Starting or TaskState.Ready);
            var pendingTasks = tasks.Count(t => t.Status?.State is TaskState.Pending);
            var runningTasks = tasks.Where(t => t.Status?.State == TaskState.Running).ToList();
            var failedTasks = tasks.Count(t => t.Status?.State is TaskState.Failed or TaskState.Rejected or TaskState.Orphaned);
            var runningContainerIds = runningTasks
                .Select(t => t.Status?.ContainerStatus?.ContainerID)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList()!;

            var latestTask = tasks.OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt).FirstOrDefault();

            var usage = new ServerResourceUsage
            {
                ServerId = serverId,
                ServiceId = service.ID,
                Timestamp = DateTime.UtcNow,
                ServiceCreatedAt = service.CreatedAt,
                ServiceUpdatedAt = service.UpdatedAt,
                ServiceVersion = service.Version?.Index ?? 0,
                DesiredReplicas = (int)(service.Spec?.Mode?.Replicated?.Replicas ?? 0),
                RunningReplicas = runningTasks.Count,
                PreparingTasks = preparingTasks,
                StartingTasks = startingTasks,
                PendingTasks = pendingTasks,
                FailedTasks = failedTasks,
                TaskCount = tasks.Count,
                TaskIds = tasks.Select(t => t.ID ?? string.Empty).ToList(),
                ContainerIds = runningContainerIds,
                LatestTaskState = latestTask?.Status?.State.ToString(),
                LatestTaskMessage = latestTask?.Status?.Message,
                LatestTaskError = latestTask?.Status?.Err,
                ServiceCpuLimitPerReplica = service.Spec?.TaskTemplate?.Resources?.Limits?.NanoCPUs is long limitNanos && limitNanos > 0
                    ? (ulong)limitNanos
                    : null,
                ServiceCpuReservationPerReplica = service.Spec?.TaskTemplate?.Resources?.Reservations?.NanoCPUs is long reservationNanos && reservationNanos > 0
                    ? (ulong)reservationNanos
                    : null,
                ServiceMemoryLimitPerReplica = service.Spec?.TaskTemplate?.Resources?.Limits?.MemoryBytes,
                ServiceMemoryReservationPerReplica = service.Spec?.TaskTemplate?.Resources?.Reservations?.MemoryBytes
            };

            await TryAttachRealTimeStatsAsync(usage, runningContainerIds, cancellationToken).ConfigureAwait(false);

            return usage;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("manager agent", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Manager agent not currently available for resource snapshot of server {ServerId}: {Message}", serverId, ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting resource snapshot for server {ServerId}", serverId);
            return null;
        }
    }

    private async Task TryAttachRealTimeStatsAsync(
        ServerResourceUsage usage,
        IReadOnlyList<string> runningContainerIds,
        CancellationToken cancellationToken)
    {
        if (runningContainerIds.Count == 0)
        {
            return;
        }

        // Prefer the first running container's real-time stats for the snapshot.
        foreach (var containerId in runningContainerIds)
        {
            try
            {
                var stats = await _nodeAgentDiscovery.GetContainerStatsAsync(containerId).ConfigureAwait(false);
                if (stats != null)
                {
                    usage.RealTimeStats = stats;
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get real-time stats for container {ContainerId} on server {ServerId}",
                    containerId, usage.ServerId);
            }
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        // Fallback: try streaming the most recent container for a single sample.
        var lastContainerId = runningContainerIds[^1];
        try
        {
            await foreach (var streamedStats in _nodeAgentDiscovery.StreamContainerStatsAsync(lastContainerId, cancellationToken).ConfigureAwait(false))
            {
                usage.RealTimeStats = streamedStats;
                return;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when no data is available or cancellation is requested.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to stream real-time stats for container {ContainerId} on server {ServerId}",
                lastContainerId, usage.ServerId);
        }
    }
}
