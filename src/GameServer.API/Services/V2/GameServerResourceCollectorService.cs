using System.Collections.Concurrent;
using GameServer.API.Data.V2;
using GameServer.API.Interfaces;
using GameServer.API.Models;
using GameServer.API.Repositories.V2;
using GameServer.API.Services;

namespace GameServer.API.Services.V2;

/// <summary>
/// Background service that continuously captures resource usage from all managed GameServers,
/// buffers the metric history in memory, periodically batch-inserts records to the database,
/// allows immediate on-demand sampling when servers start/restart, and ensures all cached
/// data is flushed to the database on graceful application shutdown.
/// </summary>
public sealed class GameServerResourceCollectorService : BackgroundService, IGameServerResourceCollector
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDatabaseReadinessGate _readinessGate;
    private readonly ILogger<GameServerResourceCollectorService> _logger;

    private readonly ConcurrentQueue<GameServerResourceUtilizationEntity> _writeBuffer = new();
    private readonly ConcurrentDictionary<string, ServerResourceUsage> _cachedUsage = new();
    private readonly SemaphoreSlim _flushLock = new(1, 1);

    private readonly TimeSpan _collectionInterval = TimeSpan.FromSeconds(10);
    private readonly TimeSpan _flushInterval = TimeSpan.FromSeconds(15);
    private const int MaxBatchSize = 100;

    public GameServerResourceCollectorService(
        IServiceProvider serviceProvider,
        IDatabaseReadinessGate readinessGate,
        ILogger<GameServerResourceCollectorService> logger)
    {
        _serviceProvider = serviceProvider;
        _readinessGate = readinessGate;
        _logger = logger;
    }

    /// <inheritdoc />
    public ServerResourceUsage? GetCachedUsage(string serverId)
    {
        if (string.IsNullOrWhiteSpace(serverId))
        {
            return null;
        }

        return _cachedUsage.TryGetValue(serverId, out var usage) ? usage : null;
    }

    /// <inheritdoc />
    public async Task TriggerImmediateCollectionAsync(string serverId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(serverId))
        {
            return;
        }

        _logger.LogInformation("Immediate resource collection triggered for server {ServerId}", serverId);
        await CollectServerUsageAsync(serverId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        await FlushBufferToDatabaseAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("GameServerResourceCollectorService starting up...");

        // Wait for database initialization before executing startup sampling and background loops
        await _readinessGate.WaitUntilReadyAsync(stoppingToken).ConfigureAwait(false);

        _logger.LogInformation("Database is ready. Initiating startup resource collection for all managed servers...");

        // Run background collection and flushing loops concurrently
        var collectionLoopTask = RunCollectionLoopAsync(stoppingToken);
        var flushLoopTask = RunFlushLoopAsync(stoppingToken);

        await Task.WhenAll(collectionLoopTask, flushLoopTask).ConfigureAwait(false);
    }

    private async Task RunCollectionLoopAsync(CancellationToken stoppingToken)
    {
        // Initial collection on startup
        await CollectAllManagedServersAsync(stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_collectionInterval, stoppingToken).ConfigureAwait(false);
                await CollectAllManagedServersAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in periodic resource collection loop");
            }
        }
    }

    private async Task RunFlushLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_flushInterval, stoppingToken).ConfigureAwait(false);
                await FlushBufferToDatabaseAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in periodic database flush loop");
            }
        }
    }

    private async Task CollectAllManagedServersAsync(CancellationToken cancellationToken)
    {
        try
        {
            List<string> serverIds;
            using (var scope = _serviceProvider.CreateScope())
            {
                var serverRepo = scope.ServiceProvider.GetRequiredService<IGameServerRepository>();
                var servers = await serverRepo.GetAllAsync(includeDeleted: false).ConfigureAwait(false);
                serverIds = servers
                    .Where(s => !string.IsNullOrWhiteSpace(s.ServerId))
                    .Select(s => s.ServerId)
                    .ToList();
            }

            if (serverIds.Count == 0)
            {
                return;
            }

            _logger.LogDebug("Collecting resource metrics for {Count} managed servers", serverIds.Count);

            var tasks = serverIds.Select(id => CollectServerUsageAsync(id, cancellationToken));
            await Task.WhenAll(tasks).ConfigureAwait(false);

            // Flush if write buffer grew large
            if (_writeBuffer.Count >= MaxBatchSize)
            {
                await FlushBufferToDatabaseAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutdown requested
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error collecting resource metrics for managed servers");
        }
    }

    private async Task CollectServerUsageAsync(string serverId, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var monitor = scope.ServiceProvider.GetRequiredService<IServerResourceMonitor>();

            var usage = await monitor.GetSnapshotAsync(serverId, cancellationToken).ConfigureAwait(false);
            if (usage == null)
            {
                return;
            }

            // Update in-memory local cache
            _cachedUsage[serverId] = usage;

            // Synchronize status back to the repository if it changed based on live Swarm service tasks
            if (!string.IsNullOrWhiteSpace(usage.ServiceStatus))
            {
                try
                {
                    var serverRepo = scope.ServiceProvider.GetRequiredService<IGameServerRepository>();
                    var server = await serverRepo.GetByServerIdAsync(serverId).ConfigureAwait(false);
                    if (server != null && !string.Equals(server.Status, usage.ServiceStatus, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Syncing GameServer {ServerId} status from '{OldStatus}' to '{NewStatus}'",
                            serverId, server.Status, usage.ServiceStatus);
                        server = server with { Status = usage.ServiceStatus };
                        await serverRepo.UpdateAsync(server).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to sync status for server {ServerId}", serverId);
                }
            }

            // Only persist to database if the server has active containers and real-time metrics
            if (!usage.HasRealTimeStats || usage.RunningReplicas == 0)
            {
                _logger.LogTrace("Skipping metric persistence for server {ServerId}: server is stopped or lacks real-time container stats", serverId);
                return;
            }

            // Ensure we aren't saving empty stats records where all metrics are null
            if (!usage.CpuUsagePercent.HasValue && !usage.MemoryUsageBytes.HasValue)
            {
                _logger.LogTrace("Skipping metric persistence for server {ServerId}: metric values are empty", serverId);
                return;
            }

            // Map to entity for DB persistence
            var entity = new GameServerResourceUtilizationEntity
            {
                ServerId = usage.ServerId,
                Timestamp = usage.Timestamp,
                CpuUsagePercent = usage.CpuUsagePercent,
                MemoryUsageBytes = usage.MemoryUsageBytes,
                MemoryLimitBytes = usage.MemoryLimitBytes,
                MemoryUsagePercent = usage.MemoryUsagePercent,
                NetworkRxBytes = usage.NetworkRxBytes,
                NetworkTxBytes = usage.NetworkTxBytes,
                BlockReadBytes = usage.BlockReadBytes,
                BlockWriteBytes = usage.BlockWriteBytes,
                DesiredReplicas = usage.DesiredReplicas,
                RunningReplicas = usage.RunningReplicas,
                ContainerId = usage.ContainerId
            };

            _writeBuffer.Enqueue(entity);
            _logger.LogTrace("Enqueued resource utilization sample for server {ServerId} (Buffer size: {Count})",
                serverId, _writeBuffer.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect resource snapshot for server {ServerId}", serverId);
        }
    }

    private async Task FlushBufferToDatabaseAsync(CancellationToken cancellationToken)
    {
        if (_writeBuffer.IsEmpty)
        {
            return;
        }

        await _flushLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var recordsToInsert = new List<GameServerResourceUtilizationEntity>();
            while (_writeBuffer.TryDequeue(out var record))
            {
                recordsToInsert.Add(record);
            }

            if (recordsToInsert.Count == 0)
            {
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IGameServerResourceUtilizationRepository>();

            await repo.BatchInsertAsync(recordsToInsert, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Successfully flushed {Count} resource metrics to the database", recordsToInsert.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Handled during StopAsync
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush resource utilization buffer to the database");
        }
        finally
        {
            _flushLock.Release();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("GameServerResourceCollectorService is stopping. Flushing remaining cached metrics to database...");

        try
        {
            // Execute base background service shutdown
            await base.StopAsync(cancellationToken).ConfigureAwait(false);

            // Flush any remaining records in the buffer
            if (!_writeBuffer.IsEmpty)
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                await FlushBufferToDatabaseAsync(linkedCts.Token).ConfigureAwait(false);
            }

            _logger.LogInformation("GameServerResourceCollectorService completed shutdown and flushed all cached metrics.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while flushing metrics during GameServerResourceCollectorService shutdown");
        }
    }
}
