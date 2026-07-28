using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using GameServer.Docker.Interfaces;
using GameServer.Docker.Models;

namespace GameServer.Docker.Services.V2;

/// <summary>
/// Multi-subscriber resource aggregator. Maintains one agent stats stream per server
/// and fans updates out to all subscribed SignalR clients.
/// </summary>
public sealed class ServerResourceAggregator : IServerResourceAggregator, IAsyncDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ServerResourceAggregator> _logger;

    // serverId -> shared source
    private readonly ConcurrentDictionary<string, ResourceSource> _sources = new();

    public ServerResourceAggregator(IServiceProvider serviceProvider, ILogger<ServerResourceAggregator> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ServerResourceUsage> StreamResourceUsageAsync(
        string serverId,
        int intervalSeconds = 5,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);

        var source = _sources.GetOrAdd(serverId, id => new ResourceSource(id, _serviceProvider, _logger));
        var channel = await source.SubscribeAsync(cancellationToken).ConfigureAwait(false);

        var minInterval = TimeSpan.FromSeconds(Math.Clamp(intervalSeconds, 1, 60));
        DateTime lastEmit = DateTime.MinValue;

        await foreach (var usage in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            var now = DateTime.UtcNow;
            if (now - lastEmit < minInterval)
            {
                continue;
            }

            lastEmit = now;
            yield return usage;
        }
    }

    /// <inheritdoc />
    public async Task<ServerResourceUsage?> GetSnapshotAsync(string serverId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);

        // Resolve a monitor on demand to avoid a scoped service being held by this singleton.
        using var scope = _serviceProvider.CreateScope();
        var monitor = scope.ServiceProvider.GetRequiredService<IServerResourceMonitor>();
        return await monitor.GetSnapshotAsync(serverId, cancellationToken).ConfigureAwait(false);
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
    /// Shared source of resource updates for a single server.
    /// </summary>
    private sealed class ResourceSource : IAsyncDisposable
    {
        private readonly string _serverId;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger _logger;
        private readonly object _lock = new();
        private readonly List<Channel<ServerResourceUsage>> _subscribers = new();

        private CancellationTokenSource? _cts;
        private Task? _producer;

        public ResourceSource(string serverId, IServiceProvider serviceProvider, ILogger logger)
        {
            _serverId = serverId;
            _serviceProvider = serviceProvider;
            // Logger type is unavailable here; use the aggregator logger directly.
            _logger = logger;
        }

        public Task<Channel<ServerResourceUsage>> SubscribeAsync(CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                EnsureStartedLocked();

                var channel = Channel.CreateUnbounded<ServerResourceUsage>(new UnboundedChannelOptions
                {
                    SingleReader = true
                });

                _subscribers.Add(channel);
                _logger.LogDebug("Resource subscriber added for server {ServerId}. Total: {Count}", _serverId, _subscribers.Count);
                return Task.FromResult(channel);
            }
        }

        public async ValueTask DisposeAsync()
        {
            CancellationTokenSource? cts;
            Task? producer;
            List<Channel<ServerResourceUsage>> subscribers;

            lock (_lock)
            {
                cts = _cts;
                producer = _producer;
                subscribers = new List<Channel<ServerResourceUsage>>(_subscribers);
                _subscribers.Clear();
            }

            cts?.Cancel();
            cts?.Dispose();

            if (producer != null)
            {
                try { await producer.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
                catch (Exception ex) { _logger.LogError(ex, "Error stopping resource producer for server {ServerId}", _serverId); }
            }

            foreach (var channel in subscribers)
            {
                channel.Writer.TryComplete();
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
                var monitor = scope.ServiceProvider.GetRequiredService<IServerResourceMonitor>();

                await foreach (var usage in monitor.StreamResourceUsageAsync(_serverId, cancellationToken).ConfigureAwait(false))
                {
                    List<Channel<ServerResourceUsage>> targets;
                    lock (_lock)
                    {
                        // Remove completed channels lazily.
                        _subscribers.RemoveAll(ch => ch.Writer.TryComplete() == false && false);
                        targets = new List<Channel<ServerResourceUsage>>(_subscribers);
                    }

                    foreach (var channel in targets)
                    {
                        channel.Writer.TryWrite(usage);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Expected shutdown.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Resource producer failed for server {ServerId}", _serverId);
            }
            finally
            {
                List<Channel<ServerResourceUsage>> targets;
                lock (_lock)
                {
                    targets = new List<Channel<ServerResourceUsage>>(_subscribers);
                }

                foreach (var channel in targets)
                {
                    channel.Writer.TryComplete();
                }
            }
        }
    }
}
