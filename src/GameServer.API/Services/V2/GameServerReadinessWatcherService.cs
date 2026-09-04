using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using GameServer.API.Interfaces;
using GameServer.API.Repositories.V2;

namespace GameServer.API.Services.V2;

/// <summary>
/// Background log monitor that watches container log streams for a server's <c>ReadyLogPattern</c>,
/// expanding environment variables and automatically transitioning server status from 'Running' to 'Available'.
/// </summary>
public sealed class GameServerReadinessWatcherService : IGameServerReadinessWatcherService, IAsyncDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IServerLogAggregator _logAggregator;
    private readonly ILogger<GameServerReadinessWatcherService> _logger;

    private readonly ConcurrentDictionary<string, bool> _readyServers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeWatchers = new(StringComparer.OrdinalIgnoreCase);

    public GameServerReadinessWatcherService(
        IServiceProvider serviceProvider,
        IServerLogAggregator logAggregator,
        ILogger<GameServerReadinessWatcherService> logger)
    {
        _serviceProvider = serviceProvider;
        _logAggregator = logAggregator;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsServerReady(string serverId)
    {
        if (string.IsNullOrWhiteSpace(serverId)) return false;
        return _readyServers.TryGetValue(serverId, out var isReady) && isReady;
    }

    /// <inheritdoc />
    public void MarkReady(string serverId)
    {
        if (string.IsNullOrWhiteSpace(serverId)) return;
        _readyServers[serverId] = true;
        CancelWatcher(serverId);
    }

    /// <inheritdoc />
    public void ResetReadiness(string serverId)
    {
        if (string.IsNullOrWhiteSpace(serverId)) return;
        _readyServers.TryRemove(serverId, out _);
        CancelWatcher(serverId);
    }

    /// <inheritdoc />
    public async Task EnsureWatchingAsync(string serverId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);

        if (IsServerReady(serverId))
        {
            return;
        }

        if (_activeWatchers.ContainsKey(serverId))
        {
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var serverRepo = scope.ServiceProvider.GetRequiredService<IGameServerRepository>();
        var gameTypeRepo = scope.ServiceProvider.GetRequiredService<IGameTypeRepository>();

        var server = await serverRepo.GetByServerIdAsync(serverId).ConfigureAwait(false);
        if (server == null)
        {
            return;
        }

        var gameTypes = await gameTypeRepo.GetAllAsync(includeInactive: true).ConfigureAwait(false);
        var revisionPair = gameTypes
            .SelectMany(gt => gt.Revisions.Select(rev => (GameType: gt, Revision: rev)))
            .FirstOrDefault(pair => pair.Revision.Id == server.GameTypeRevisionId);

        var gameType = revisionPair.GameType;
        var revision = revisionPair.Revision;

        var pattern = revision?.ReadyLogPattern?.Trim();
        if (string.IsNullOrWhiteSpace(pattern))
        {
            // No ready log pattern configured: mark ready immediately
            MarkReady(serverId);
            if (!string.Equals(server.Status, "Available", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("GameServer {ServerId} has no ready log pattern configured. Transitioning to 'Available'.", serverId);
                server = server with { Status = "Available" };
                await serverRepo.UpdateAsync(server).ConfigureAwait(false);
            }
            return;
        }

        // Expand tokens inside the ReadyLogPattern using server properties and environment variable settings
        var expandedPattern = ExpandPattern(pattern, server, gameType, revision);
        _logger.LogInformation("Checking readiness for GameServer {ServerId} with ready pattern: '{ExpandedPattern}' (original: '{OriginalPattern}')",
            serverId, expandedPattern, pattern);

        // First, examine the entire service logs to see if the ready message was already produced
        var discovery = scope.ServiceProvider.GetService<INodeAgentDiscovery>();
        if (discovery != null)
        {
            try
            {
                List<string>? serviceLogs = null;
                if (!string.IsNullOrWhiteSpace(server.ServiceName))
                {
                    serviceLogs = await discovery.GetServiceLogsAsync(server.ServiceName, tailLines: 0).ConfigureAwait(false);
                }

                if (serviceLogs is null || serviceLogs.Count == 0)
                {
                    var monitor = scope.ServiceProvider.GetService<IServerResourceMonitor>();
                    if (monitor != null)
                    {
                        var snapshot = await monitor.GetSnapshotAsync(serverId, cancellationToken).ConfigureAwait(false);
                        var containerId = snapshot?.ContainerIds.FirstOrDefault() ?? snapshot?.RealTimeStats?.ContainerId;
                        if (!string.IsNullOrWhiteSpace(containerId))
                        {
                            serviceLogs = await discovery.GetContainerLogsAsync(containerId, tailLines: 0).ConfigureAwait(false);
                        }
                    }
                }

                if (serviceLogs is not null && serviceLogs.Count > 0)
                {
                    foreach (var logLine in serviceLogs)
                    {
                        if (MatchesPattern(logLine, expandedPattern))
                        {
                            _logger.LogInformation("GameServer {ServerId} is now Available! Found ready log pattern '{Pattern}' in existing service logs: '{LogLine}'",
                                serverId, expandedPattern, logLine);

                            MarkReady(serverId);
                            if (!string.Equals(server.Status, "Available", StringComparison.OrdinalIgnoreCase))
                            {
                                server = server with { Status = "Available" };
                                await serverRepo.UpdateAsync(server).ConfigureAwait(false);
                            }

                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to examine service logs for server {ServerId}, proceeding to live log watcher", serverId);
            }
        }

        var cts = new CancellationTokenSource();
        if (!_activeWatchers.TryAdd(serverId, cts))
        {
            cts.Dispose();
            return;
        }

        _ = Task.Run(() => WatchLogsAsync(serverId, expandedPattern, cts.Token), CancellationToken.None);
    }

    private async Task WatchLogsAsync(string serverId, string expandedPattern, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var logLine in _logAggregator.StreamLogsAsync(
                serverId,
                follow: true,
                tailLines: 500,
                timestamps: false,
                cancellationToken).ConfigureAwait(false))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (MatchesPattern(logLine, expandedPattern))
                {
                    _logger.LogInformation("GameServer {ServerId} is now Available! Matched ready log pattern '{Pattern}' on line: '{LogLine}'",
                        serverId, expandedPattern, logLine);

                    _readyServers[serverId] = true;

                    try
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var serverRepo = scope.ServiceProvider.GetRequiredService<IGameServerRepository>();
                        var server = await serverRepo.GetByServerIdAsync(serverId).ConfigureAwait(false);
                        if (server != null && !string.Equals(server.Status, "Available", StringComparison.OrdinalIgnoreCase))
                        {
                            server = server with { Status = "Available" };
                            await serverRepo.UpdateAsync(server).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to persist 'Available' status for server {ServerId}", serverId);
                    }

                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on server shutdown or reset
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while watching readiness logs for server {ServerId}", serverId);
        }
        finally
        {
            _activeWatchers.TryRemove(serverId, out var cts);
            cts?.Dispose();
        }
    }

    public static string ExpandPattern(
        string pattern,
        Models.V2.GameServer server,
        Models.V2.GameType? gameType,
        Models.V2.GameTypeRevision? revision)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(server);

        var tokenDict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        var baseTokens = ServerVariableExpander.BuildTokenValues(server, gameType, revision);
        foreach (var (k, v) in baseTokens)
        {
            tokenDict[k] = v;
        }

        foreach (var setting in server.Settings)
        {
            if (!string.IsNullOrWhiteSpace(setting.SettingKey))
            {
                tokenDict[setting.SettingKey] = setting.Value;
            }
        }

        foreach (var port in server.Ports)
        {
            tokenDict[$"PORT_{port.ContainerPort}"] = port.PublishedPort.ToString();
            tokenDict[$"CONTAINER_PORT_{port.ContainerPort}"] = port.ContainerPort.ToString();
        }

        // Support ${VAR} syntax by normalizing to {VAR}
        var normalized = Regex.Replace(pattern, @"\$\{([A-Za-z0-9_]+)\}", "{$1}");

        var expanded = ServerVariableExpander.Substitute(normalized, tokenDict);
        return expanded ?? pattern;
    }

    public static bool MatchesPattern(string logLine, string targetPattern)
    {
        if (string.IsNullOrEmpty(logLine) || string.IsNullOrEmpty(targetPattern))
        {
            return false;
        }

        if (targetPattern.Contains('*'))
        {
            try
            {
                var regexPattern = Regex.Escape(targetPattern).Replace("\\*", ".*");
                return Regex.IsMatch(logLine, regexPattern, RegexOptions.IgnoreCase);
            }
            catch
            {
                // Fall back to substring match if regex construction fails
                return logLine.Contains(targetPattern.Replace("*", ""), StringComparison.OrdinalIgnoreCase);
            }
        }

        return logLine.Contains(targetPattern, StringComparison.OrdinalIgnoreCase);
    }

    private void CancelWatcher(string serverId)
    {
        if (_activeWatchers.TryRemove(serverId, out var cts))
        {
            try
            {
                cts.Cancel();
                cts.Dispose();
            }
            catch
            {
                // Ignore cancellation errors
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        foreach (var key in _activeWatchers.Keys.ToList())
        {
            CancelWatcher(key);
        }
        _readyServers.Clear();
        return ValueTask.CompletedTask;
    }
}
