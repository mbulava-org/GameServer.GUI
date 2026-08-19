using System.Runtime.CompilerServices;
using GameServer.Windows.Agent.Interfaces;
using GameServer.Windows.Agent.Models;
using Microsoft.AspNetCore.SignalR;

namespace GameServer.Windows.Agent.Hubs;

public class WindowsAgentHub : Hub
{
    private readonly ILogger<WindowsAgentHub> _logger;
    private readonly IGameProcessManager _processManager;
    private readonly IWindowsResourceMonitor _resourceMonitor;

    public WindowsAgentHub(
        ILogger<WindowsAgentHub> logger,
        IGameProcessManager processManager,
        IWindowsResourceMonitor resourceMonitor)
    {
        _logger = logger;
        _processManager = processManager;
        _resourceMonitor = resourceMonitor;
    }

    /// <summary>
    /// Stream real-time logs for a specific game server.
    /// </summary>
    public async IAsyncEnumerable<string> StreamServerLogs(
        string serverId,
        bool includeHistory = true,
        int tailLines = 100,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Client {ConnectionId} started log stream for server '{ServerId}'",
            Context.ConnectionId, serverId);

        await foreach (var line in _processManager.StreamLogsAsync(serverId, includeHistory, tailLines, cancellationToken))
        {
            yield return line;
        }
    }

    /// <summary>
    /// Stream process CPU and RAM statistics for a specific game server.
    /// </summary>
    public async IAsyncEnumerable<ProcessStatsSnapshot> StreamServerStats(
        string serverId,
        int intervalSeconds = 2,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Client {ConnectionId} started stats stream for server '{ServerId}'",
            Context.ConnectionId, serverId);

        var interval = TimeSpan.FromSeconds(Math.Max(1, intervalSeconds));
        await foreach (var stats in _processManager.StreamStatsAsync(serverId, interval, cancellationToken))
        {
            yield return stats;
        }
    }

    /// <summary>
    /// Stream host-wide CPU, RAM, and Disk resource statistics.
    /// </summary>
    public async IAsyncEnumerable<HostResourceSnapshot> StreamHostStats(
        int intervalSeconds = 2,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, intervalSeconds));
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return _resourceMonitor.GetHostSnapshot();
        }
    }

    /// <summary>
    /// Snapshot of server stats.
    /// </summary>
    public ProcessStatsSnapshot? GetServerStatsSnapshot(string serverId)
    {
        return _processManager.GetStats(serverId);
    }

    /// <summary>
    /// Snapshot of recent logs.
    /// </summary>
    public ProcessLogsResponse GetServerLogs(string serverId, int tailLines = 100)
    {
        return _processManager.GetLogs(serverId, tailLines);
    }
}
