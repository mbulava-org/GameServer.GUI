using Docker.DotNet;
using Docker.DotNet.Models;
using GameServer.Docker.Interfaces;
using GameServer.Docker.Models;

namespace GameServer.Docker.Services
{
    /// <summary>
    /// Service for monitoring game server resources.
    /// Combines Swarm Service-level data (specifications, replicas, tasks) with real-time container stats from Node Agents.
    /// 
    /// Real-time container stats (CPU%, memory%, network I/O, etc.) are ONLY available via Node Agents.
    /// Service-level data (limits, reservations, replica counts) comes from Docker Swarm Manager.
    /// </summary>
    public class GameServerResourceMonitorService : IGameServerResourceMonitor
    {
        private readonly ILogger<GameServerResourceMonitorService> _logger;
        private readonly DockerServiceHelper _dockerServiceHelper;
        private readonly INodeAgentDiscovery _nodeAgentDiscovery;

        public GameServerResourceMonitorService(
            ILogger<GameServerResourceMonitorService> logger,
            DockerServiceHelper dockerServiceHelper,
            INodeAgentDiscovery nodeAgentDiscovery)
        {
            _logger = logger;
            _dockerServiceHelper = dockerServiceHelper;
            _nodeAgentDiscovery = nodeAgentDiscovery;
        }

        public async IAsyncEnumerable<ServerResourceUsage> StreamResourceUsageAsync(
            string serverId, 
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Starting resource stream for server {ServerId}", serverId);

            // Get initial service information
            var serviceId = await _dockerServiceHelper.GetGameServerServiceIdAsync(serverId);
            if (string.IsNullOrEmpty(serviceId))
            {
                _logger.LogWarning("No service found for server {ServerId}", serverId);
                yield break;
            }

            var service = await _dockerServiceHelper.GetSwarmServiceByServiceId(serviceId);
            if (service == null)
            {
                _logger.LogWarning("Could not inspect service {ServiceId}", serviceId);
                yield break;
            }

            // Get initial task information
            var tasks = await _dockerServiceHelper.GetTasksForSwarmServiceAsync(serviceId);
            var runningTasks = tasks.Where(t => t.Status?.State == TaskState.Running).ToList();
            
            if (!runningTasks.Any())
            {
                _logger.LogWarning("No running tasks found for server {ServerId}", serverId);
                yield break;
            }

            // Get the first running container to stream stats from
            var containerId = runningTasks.FirstOrDefault()?.Status?.ContainerStatus?.ContainerID;
            if (string.IsNullOrEmpty(containerId))
            {
                _logger.LogWarning("No container ID found for server {ServerId}", serverId);
                yield break;
            }

            _logger.LogInformation("Starting stats stream from Node Agent for container {ContainerId} (server {ServerId})", 
                containerId, serverId);

            // Use a channel to convert the stream to an async enumerable with service context
            var channel = System.Threading.Channels.Channel.CreateUnbounded<Models.ContainerStats>();

            // Create a background task to stream stats from the Node Agent
            var streamTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var stats in _nodeAgentDiscovery.StreamContainerStatsAsync(containerId, cancellationToken))
                    {
                        await channel.Writer.WriteAsync(stats, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug("Stats stream cancelled for container {ContainerId}", containerId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error streaming stats from Node Agent for container {ContainerId}", containerId);
                }
                finally
                {
                    channel.Writer.Complete();
                }
            }, cancellationToken);

            // Stream results as they arrive
            await foreach (var containerStats in channel.Reader.ReadAllAsync(cancellationToken))
            {
                // Refresh service-level data periodically (every 30 seconds)
                // This is much less expensive than refreshing on every stats update
                var timeSinceServiceRefresh = DateTime.UtcNow - service.UpdatedAt;
                if (timeSinceServiceRefresh > TimeSpan.FromSeconds(30))
                {
                    try
                    {
                        service = await _dockerServiceHelper.GetSwarmServiceByServiceId(serviceId);
                        tasks = await _dockerServiceHelper.GetTasksForSwarmServiceAsync(serviceId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error refreshing service data for {ServiceId}", serviceId);
                    }
                }

                // Build resource info with latest stats
                var resourceInfo = BuildServiceResourceInfo(serverId, service, tasks);
                resourceInfo.RealTimeStats = containerStats;

                yield return resourceInfo;
            }

            await streamTask;
        }

        public async Task<Models.ServerResourceUsage?> GetResourceUsageAsync(string serverId)
        {
            _logger.LogDebug("Getting resource info for server {ServerId}", serverId);

            try
            {
                var serviceId = await _dockerServiceHelper.GetGameServerServiceIdAsync(serverId);
                if (string.IsNullOrEmpty(serviceId))
                {
                    _logger.LogWarning("No service found for server {ServerId}", serverId);
                    return null;
                }

                // Get service specification from Swarm Manager (limits, reservations, replica config)
                _logger.LogTrace("Fetching service-level data from Swarm Manager for service {ServiceId}", serviceId);
                var service = await _dockerServiceHelper.GetSwarmServiceByServiceId(serviceId);
                if (service == null)
                {
                    _logger.LogWarning("Could not inspect service {ServiceId}", serviceId);
                    return null;
                }

                // Get task information from Swarm Manager (task states, container IDs)
                var tasks = await _dockerServiceHelper.GetTasksForSwarmServiceAsync(serviceId);

                // Build service-level resource info (from Swarm)
                var resourceInfo = BuildServiceResourceInfo(serverId, service, tasks);

                // Try to get real-time container stats from Node Agent
                // This provides actual CPU%, memory usage, network I/O, etc.
                // We only need stats from one container since all replicas should have similar stats
                // Use parallel execution with cancellation to stop once first result is found
                try
                {
                    using var cts = new CancellationTokenSource();
                    var statsLock = new object();
                    var statsFound = false;
                    
                    await Parallel.ForEachAsync(resourceInfo.ContainerIds, 
                        new ParallelOptions { CancellationToken = cts.Token },
                        async (containerId, cancellationToken) =>
                    {
                        // Early exit check - optimization to reduce unnecessary API calls
                        // Note: There's a small TOCTOU window, but the inner lock ensures correct behavior
                        bool shouldSkip;
                        lock (statsLock)
                        {
                            shouldSkip = statsFound;
                        }
                        if (shouldSkip)
                            return;
                            
                        if (!string.IsNullOrEmpty(containerId))
                        {
                            _logger.LogDebug("Fetching real-time container stats via Node Agent for container {ContainerId}", containerId);
                            var containerStats = await _nodeAgentDiscovery.GetContainerStatsAsync(containerId);
                            if (containerStats != null)
                            {
                                // Use lock to ensure only one thread sets the value
                                lock (statsLock)
                                {
                                    if (!statsFound)
                                    {
                                        resourceInfo.RealTimeStats = containerStats;
                                        statsFound = true;
                                        _logger.LogInformation("Successfully retrieved real-time stats for server {ServerId} (CPU: {Cpu:F2}%, Memory: {Memory:F2}%)",
                                            serverId, containerStats.CpuUsagePercent, containerStats.MemoryUsagePercent);
                                        // Cancel remaining operations
                                        cts.Cancel();
                                    }
                                }
                            }
                            else
                            {
                                _logger.LogDebug("No real-time stats available from Node Agent for container {ContainerId}, trying next container", containerId);
                            }
                        }
                    });
                }
                catch (OperationCanceledException)
                {
                    // Expected when we successfully cancel after finding first result
                    // No action needed - this is a success case
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not fetch real-time stats from Node Agent for server {ServerId}, returning service-level data only", serverId);
                }
                
                if (resourceInfo.RealTimeStats == null)
                {
                    _logger.LogWarning("No real-time stats available from Node Agent for server {ServerId}. Returning service-level data only", serverId);
                }

                return resourceInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting resource info for server {ServerId}", serverId);
                return null;
            }
        }

        



        #region Private Helper Methods


        private Models.ServerResourceUsage BuildServiceResourceInfo(
            string serverId,
            SwarmService service,
            List<TaskResponse> tasks)
        {
            var resourceInfo = new Models.ServerResourceUsage
            {
                ServerId = serverId,
                ServiceId = service.ID,
                Timestamp = DateTime.UtcNow
            };

            // Extract service specification
            var spec = service.Spec;
            var resources = spec?.TaskTemplate?.Resources;
            var limits = resources?.Limits;
            var reservations = resources?.Reservations;

            // Get replica configuration
            var desiredReplicas = spec?.Mode?.Replicated?.Replicas ?? 0;
            resourceInfo.DesiredReplicas = (int)desiredReplicas;

            // Count tasks by state (TaskState is an enum)
            var runningTasks = tasks.Where(t => t.Status?.State == TaskState.Running).ToList();
            var failedTasks = tasks.Where(t => t.Status?.State == TaskState.Failed).Count();
            var pendingTasks = tasks.Where(t => t.Status?.State == TaskState.Pending || 
                                                  t.Status?.State == TaskState.Preparing ||
                                                  t.Status?.State == TaskState.Starting).Count();

            resourceInfo.RunningReplicas = runningTasks.Count;
            resourceInfo.FailedTasks = failedTasks;
            resourceInfo.PendingTasks = pendingTasks;
            resourceInfo.TaskCount = tasks.Count;

            // Extract task IDs for running tasks
            resourceInfo.TaskIds = runningTasks
                .Select(t => t.ID)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();

            resourceInfo.ContainerIds = runningTasks
                .Select(t => t.Status?.ContainerStatus?.ContainerID)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList()!;

            // Service-level CPU specifications (per replica) - NanoCPUs is long, convert to ulong
            if (limits?.NanoCPUs > 0)
            {
                resourceInfo.ServiceCpuLimitPerReplica = (ulong)limits.NanoCPUs;
                resourceInfo.ServiceCpuLimitTotal = (ulong)limits.NanoCPUs * (ulong)desiredReplicas;
            }

            if (reservations?.NanoCPUs > 0)
            {
                resourceInfo.ServiceCpuReservationPerReplica = (ulong)reservations.NanoCPUs;
                resourceInfo.ServiceCpuReservationTotal = (ulong)reservations.NanoCPUs * (ulong)desiredReplicas;
            }

            // Service-level Memory specifications (per replica)
            if (limits?.MemoryBytes > 0)
            {
                resourceInfo.ServiceMemoryLimitPerReplica = limits.MemoryBytes;
                resourceInfo.ServiceMemoryLimitTotal = limits.MemoryBytes * (long)desiredReplicas;
            }

            if (reservations?.MemoryBytes > 0)
            {
                resourceInfo.ServiceMemoryReservationPerReplica = reservations.MemoryBytes;
                resourceInfo.ServiceMemoryReservationTotal = reservations.MemoryBytes * (long)desiredReplicas;
            }

            // Service update status
            if (service.UpdateStatus != null)
            {
                resourceInfo.UpdateState = service.UpdateStatus.State?.ToString();
                resourceInfo.UpdateStartedAt = service.UpdateStatus.StartedAt;
                resourceInfo.UpdateCompletedAt = service.UpdateStatus.CompletedAt;
            }

            // Service creation and update times
            resourceInfo.ServiceCreatedAt = service.CreatedAt;
            resourceInfo.ServiceUpdatedAt = service.UpdatedAt;

            // Service version for optimistic locking
            resourceInfo.ServiceVersion = service.Version?.Index ?? 0;

            return resourceInfo;
        }

        #endregion
    }
}