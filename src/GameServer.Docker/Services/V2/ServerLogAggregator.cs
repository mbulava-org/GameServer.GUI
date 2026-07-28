using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using GameServer.Docker.Interfaces;
using GameServer.Docker.Services;

namespace GameServer.Docker.Services.V2;

/// <summary>
/// Multi-subscriber log aggregator. Maintains one agent log stream per server
/// and fans log lines out to all subscribed SignalR clients.
/// </summary>
public sealed class ServerLogAggregator : IServerLogAggregator, IAsyncDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ServerLogAggregator> _logger;

    // serverId -> shared source
    private readonly ConcurrentDictionary<string, LogSource> _sources = new();

    public ServerLogAggregator(IServiceProvider serviceProvider, ILogger<ServerLogAggregator> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> StreamLogsAsync(
        string serverId,
        bool follow = true,
        int tailLines = 100,
        bool timestamps = true,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);

        var source = _sources.GetOrAdd(serverId, id => new LogSource(id, _serviceProvider, _logger));
        var channel = await source.SubscribeAsync(cancellationToken).ConfigureAwait(false);

        await foreach (var line in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return line;
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var source in _sources.Values)
        {
            await source.DisposeAsync().ConfigureAwait(false);
        }

        _sources.Clear();
    }

    /// <summary>
    /// Shared source of log lines for a single server.
    /// </summary>
    private sealed class LogSource : IAsyncDisposable
    {
        private readonly string _serverId;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger _logger;
        private readonly object _lock = new();
        private readonly List<Channel<string>> _subscribers = new();

        private CancellationTokenSource? _cts;
        private Task? _producer;
        private int _activeSubscriptionCount;

        public LogSource(string serverId, IServiceProvider serviceProvider, ILogger logger)
        {
            _serverId = serverId;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public Task<Channel<string>> SubscribeAsync(CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                EnsureStartedLocked();

                var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
                {
                    SingleReader = true
                });

                channel.Reader.Completion.ContinueWith(_ => Unsubscribe(channel), TaskScheduler.Default);
                _subscribers.Add(channel);
                Interlocked.Increment(ref _activeSubscriptionCount);
                _logger.LogDebug("Log subscriber added for server {ServerId}. Total: {Count}", _serverId, _subscribers.Count);
                return Task.FromResult(channel);
            }
        }

        public async ValueTask DisposeAsync()
        {
            CancellationTokenSource? cts;
            Task? producer;
            List<Channel<string>> subscribers;

            lock (_lock)
            {
                cts = _cts;
                producer = _producer;
                subscribers = new List<Channel<string>>(_subscribers);
                _subscribers.Clear();
                _activeSubscriptionCount = 0;
            }

            cts?.Cancel();
            cts?.Dispose();

            if (producer != null)
            {
                try { await producer.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
                catch (Exception ex) { _logger.LogError(ex, "Error stopping log producer for server {ServerId}", _serverId); }
            }

            foreach (var channel in subscribers)
            {
                channel.Writer.TryComplete();
            }
        }

        private void Unsubscribe(Channel<string> channel)
        {
            lock (_lock)
            {
                _subscribers.Remove(channel);
                Interlocked.Decrement(ref _activeSubscriptionCount);
                _logger.LogDebug("Log subscriber removed for server {ServerId}. Remaining: {Count}", _serverId, _subscribers.Count);

                if (_subscribers.Count == 0)
                {
                    _cts?.Cancel();
                }
            }
        }

        private void EnsureStartedLocked()
        {
            if (_producer != null)
            {
                return;
            }

            _cts = new CancellationTokenSource();
            _producer = Task.Run(() => RunProducerAsync(_cts.Token));
        }

        private async Task RunProducerAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var discovery = scope.ServiceProvider.GetRequiredService<INodeAgentDiscovery>();
                var nodeAgentClient = scope.ServiceProvider.GetRequiredService<NodeAgentClient>();
                var queryService = scope.ServiceProvider.GetRequiredService<GameServerQueryService>();
                var server = await queryService.GetByServerIdAsync(_serverId, cancellationToken).ConfigureAwait(false);

                if (server is null)
                {
                    _logger.LogWarning("Cannot aggregate logs: server {ServerId} not found", _serverId);
                    return;
                }

                var agent = await discovery.GetAgentForServerAsync(_serverId).ConfigureAwait(false);
                if (agent is null)
                {
                    _logger.LogWarning("Cannot aggregate logs: no agent for server {ServerId}", _serverId);
                    return;
                }

                var containerId = await Hubs.ServerLogsHub.ResolveContainerIdAsync(agent, _serverId, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(containerId))
                {
                    _logger.LogWarning("Cannot aggregate logs: no container for server {ServerId} on agent {AgentUrl}", _serverId, agent.InternalUrl);
                    return;
                }

                await foreach (var line in nodeAgentClient.StreamContainerLogsAsync(
                    agent.InternalUrl,
                    containerId,
                    follow: true,
                    tailLines: 100,
                    timestamps: true,
                    cancellationToken).ConfigureAwait(false))
                {
                    List<Channel<string>> targets;
                    lock (_lock)
                    {
                        targets = new List<Channel<string>>(_subscribers);
                    }

                    foreach (var channel in targets)
                    {
                        channel.Writer.TryWrite(line);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Expected shutdown.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Log producer failed for server {ServerId}", _serverId);
            }
            finally
            {
                List<Channel<string>> targets;
                lock (_lock)
                {
                    targets = new List<Channel<string>>(_subscribers);
                }

                foreach (var channel in targets)
                {
                    channel.Writer.TryComplete();
                }
            }
        }
    }
}
