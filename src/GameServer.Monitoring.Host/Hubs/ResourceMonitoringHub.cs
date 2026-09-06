using Microsoft.AspNetCore.SignalR;
using GameServer.API.Interfaces;
using System.Collections.Concurrent;

namespace GameServer.Monitoring.Host.Hubs;

/// <summary>
/// SignalR Hub for real-time server resource monitoring.
/// Moved from GameServer.API in Phase 2 so Monitoring service owns this stream.
/// Streams resource usage updates (CPU, memory, network, etc.) to connected clients.
/// </summary>
public class ResourceMonitoringHub : Hub
{
    private readonly ILogger<ResourceMonitoringHub> _logger;
    private readonly IServerResourceAggregator _resourceAggregator;
    private static readonly ConcurrentDictionary<string, MonitoringSession> _activeSessions = new();
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens = new();

    public ResourceMonitoringHub(
        ILogger<ResourceMonitoringHub> logger,
        IServerResourceAggregator resourceAggregator)
    {
        _logger = logger;
        _resourceAggregator = resourceAggregator;
    }

    /// <summary>Subscribe to resource updates for a specific server.</summary>
    public async Task SubscribeToServer(string serverId, int intervalSeconds = 5)
    {
        var connectionId = Context.ConnectionId;

        if (intervalSeconds < 1) intervalSeconds = 1;
        if (intervalSeconds > 60) intervalSeconds = 60;

        _logger.LogInformation("Client {ConnectionId} subscribing to server {ServerId} with {Interval}s interval",
            connectionId, serverId, intervalSeconds);

        var session = new MonitoringSession
        {
            ConnectionId = connectionId,
            ServerId = serverId,
            IntervalSeconds = intervalSeconds,
            StartedAt = DateTime.UtcNow
        };

        _activeSessions[connectionId] = session;
        var cts = new CancellationTokenSource();
        _cancellationTokens[connectionId] = cts;

        try
        {
            await Clients.Caller.SendAsync("Subscribed", serverId, intervalSeconds);
            var clientProxy = Clients.Client(connectionId);
            _ = Task.Run(async () => await StreamResourceUpdatesAsync(session, clientProxy, cts.Token));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subscribing to server {ServerId}", serverId);
            try { await Clients.Caller.SendAsync("Error", $"Failed to subscribe: {ex.Message}"); }
            catch (ObjectDisposedException) { }
        }
    }

    /// <summary>Subscribe to resource updates for multiple servers.</summary>
    public async Task SubscribeToMultipleServers(string[] serverIds, int intervalSeconds = 5)
    {
        var connectionId = Context.ConnectionId;

        if (serverIds == null || serverIds.Length == 0)
        {
            await Clients.Caller.SendAsync("Error", "No server IDs provided");
            return;
        }

        if (intervalSeconds < 1) intervalSeconds = 1;
        if (intervalSeconds > 60) intervalSeconds = 60;

        _logger.LogInformation("Client {ConnectionId} subscribing to {Count} servers with {Interval}s interval",
            connectionId, serverIds.Length, intervalSeconds);

        var session = new MonitoringSession
        {
            ConnectionId = connectionId,
            ServerIds = serverIds.ToList(),
            IntervalSeconds = intervalSeconds,
            StartedAt = DateTime.UtcNow
        };

        _activeSessions[connectionId] = session;
        var cts = new CancellationTokenSource();
        _cancellationTokens[connectionId] = cts;

        try
        {
            await Clients.Caller.SendAsync("SubscribedMultiple", serverIds, intervalSeconds);
            var clientProxy = Clients.Client(connectionId);
            _ = Task.Run(async () => await StreamMultipleResourceUpdatesAsync(session, clientProxy, cts.Token));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subscribing to multiple servers");
            try { await Clients.Caller.SendAsync("Error", $"Failed to subscribe: {ex.Message}"); }
            catch (ObjectDisposedException) { }
        }
    }

    /// <summary>Get a single resource snapshot without subscribing.</summary>
    public async Task<GameServer.API.Models.ServerResourceUsage?> GetSnapshot(string serverId)
    {
        _logger.LogDebug("Client {ConnectionId} requesting snapshot for server {ServerId}", Context.ConnectionId, serverId);
        try
        {
            return await _resourceAggregator.GetSnapshotAsync(serverId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting snapshot for server {ServerId}", serverId);
            await Clients.Caller.SendAsync("Error", $"Failed to get snapshot: {ex.Message}");
            return null;
        }
    }

    /// <summary>Unsubscribe from current monitoring session.</summary>
    public async Task Unsubscribe()
    {
        await CleanupSession(Context.ConnectionId);
        await Clients.Caller.SendAsync("Unsubscribed");
    }

    /// <summary>Update the monitoring interval for current session.</summary>
    public async Task UpdateInterval(int intervalSeconds)
    {
        if (intervalSeconds < 1 || intervalSeconds > 60)
        {
            await Clients.Caller.SendAsync("Error", "Interval must be between 1 and 60 seconds");
            return;
        }

        if (_activeSessions.TryGetValue(Context.ConnectionId, out var session))
        {
            session.IntervalSeconds = intervalSeconds;
            _logger.LogInformation("Updated interval for {ConnectionId} to {Interval}s", Context.ConnectionId, intervalSeconds);
            await Clients.Caller.SendAsync("IntervalUpdated", intervalSeconds);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await CleanupSession(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    private async Task StreamResourceUpdatesAsync(MonitoringSession session, IClientProxy clientProxy, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting resource stream for server {ServerId} on {ConnectionId}", session.ServerId, session.ConnectionId);
        try
        {
            var updateCounter = 0;
            await foreach (var usage in _resourceAggregator.StreamResourceUsageAsync(session.ServerId!, session.IntervalSeconds, cancellationToken))
            {
                updateCounter++;
                try
                {
                    await clientProxy.SendAsync("ResourceUpdate", usage, cancellationToken);
                    _logger.LogTrace("Sent resource update #{Count} for {ServerId} to {ConnectionId}", updateCounter, session.ServerId, session.ConnectionId);
                }
                catch (ObjectDisposedException)
                {
                    _logger.LogInformation("Client {ConnectionId} disconnected, stopping stream for {ServerId}", session.ConnectionId, session.ServerId);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending resource update to {ConnectionId}", session.ConnectionId);
                    try { await clientProxy.SendAsync("Error", $"Error sending update: {ex.Message}", cancellationToken); }
                    catch (ObjectDisposedException) { break; }
                }
            }
        }
        catch (OperationCanceledException) { _logger.LogInformation("Resource monitoring cancelled for {ConnectionId}", session.ConnectionId); }
        catch (Exception ex) { _logger.LogError(ex, "Unexpected error in resource stream for {ConnectionId}", session.ConnectionId); }
    }

    private async Task StreamMultipleResourceUpdatesAsync(MonitoringSession session, IClientProxy clientProxy, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting resource stream for {Count} servers on {ConnectionId}", session.ServerIds?.Count ?? 0, session.ConnectionId);
        try
        {
            var updateQueue = System.Threading.Channels.Channel.CreateUnbounded<GameServer.API.Models.ServerResourceUsage>();

            var streamingTasks = (session.ServerIds ?? []).Select(serverId => Task.Run(async () =>
            {
                try
                {
                    await foreach (var usage in _resourceAggregator.StreamResourceUsageAsync(serverId, session.IntervalSeconds, cancellationToken))
                        await updateQueue.Writer.WriteAsync(usage, cancellationToken);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { _logger.LogWarning(ex, "Error streaming resources for server {ServerId}", serverId); }
            }, cancellationToken)).ToList();

            var lastSentTime = DateTime.UtcNow;
            var batchUpdates = new Dictionary<string, GameServer.API.Models.ServerResourceUsage>();

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                    if (await updateQueue.Reader.WaitToReadAsync(linkedCts.Token))
                        while (updateQueue.Reader.TryRead(out var usage))
                            batchUpdates[usage.ServerId] = usage;
                }
                catch (OperationCanceledException) { }

                if (DateTime.UtcNow - lastSentTime >= TimeSpan.FromSeconds(session.IntervalSeconds) && batchUpdates.Count > 0)
                {
                    try
                    {
                        var updates = batchUpdates.Values.ToList();
                        await clientProxy.SendAsync("ResourceUpdateBatch", updates, cancellationToken);
                        lastSentTime = DateTime.UtcNow;
                        _logger.LogTrace("Sent batch of {Count} resource updates to {ConnectionId}", updates.Count, session.ConnectionId);
                        batchUpdates.Clear();
                    }
                    catch (ObjectDisposedException)
                    {
                        _logger.LogInformation("Client {ConnectionId} disconnected, stopping multi-server stream", session.ConnectionId);
                        break;
                    }
                    catch (Exception ex) { _logger.LogError(ex, "Error sending batch update to {ConnectionId}", session.ConnectionId); }
                }
            }
        }
        catch (OperationCanceledException) { _logger.LogInformation("Multi-server monitoring cancelled for {ConnectionId}", session.ConnectionId); }
        catch (Exception ex) { _logger.LogError(ex, "Unexpected error in multi-server stream for {ConnectionId}", session.ConnectionId); }
    }

    private async Task CleanupSession(string connectionId)
    {
        if (_activeSessions.TryRemove(connectionId, out _))
        {
            _logger.LogInformation("Cleaning up monitoring session for {ConnectionId}", connectionId);
            if (_cancellationTokens.TryRemove(connectionId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
        }
        await Task.CompletedTask;
    }

    private class MonitoringSession
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string? ServerId { get; set; }
        public List<string>? ServerIds { get; set; }
        public int IntervalSeconds { get; set; } = 5;
        public DateTime StartedAt { get; set; }
    }
}
