using System.Collections.Concurrent;
using System.Threading.Channels;

namespace GameServer.Windows.Agent.Services;

public class ProcessLogRingBuffer
{
    private readonly int _capacity;
    private readonly LinkedList<string> _buffer = new();
    private readonly object _lock = new();
    private readonly ConcurrentDictionary<Guid, Channel<string>> _subscribers = new();

    public ProcessLogRingBuffer(int capacity = 2000)
    {
        _capacity = Math.Max(100, capacity);
    }

    /// <summary>
    /// Appends a new log line, storing it in the circular buffer and broadcasting to active subscribers.
    /// </summary>
    public void Append(string line)
    {
        if (line == null) return;

        lock (_lock)
        {
            if (_buffer.Count >= _capacity)
            {
                _buffer.RemoveFirst();
            }
            _buffer.AddLast(line);
        }

        // Broadcast to all active subscribers
        foreach (var subscriber in _subscribers.Values)
        {
            subscriber.Writer.TryWrite(line);
        }
    }

    /// <summary>
    /// Gets the most recent N log lines (tail).
    /// </summary>
    public List<string> GetTail(int count = 100)
    {
        lock (_lock)
        {
            if (count <= 0 || _buffer.Count == 0)
            {
                return [];
            }

            var takeCount = Math.Min(count, _buffer.Count);
            return _buffer.TakeLast(takeCount).ToList();
        }
    }

    /// <summary>
    /// Subscribes to live log lines using an async stream.
    /// </summary>
    public async IAsyncEnumerable<string> StreamLogsAsync(
        bool includeHistory = true,
        int historyTailLines = 100,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var subscriptionId = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = true
        });

        _subscribers[subscriptionId] = channel;

        try
        {
            // Yield historical lines first if requested
            if (includeHistory && historyTailLines > 0)
            {
                var history = GetTail(historyTailLines);
                foreach (var line in history)
                {
                    yield return line;
                }
            }

            // Yield live incoming lines
            while (await channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (channel.Reader.TryRead(out var line))
                {
                    yield return line;
                }
            }
        }
        finally
        {
            _subscribers.TryRemove(subscriptionId, out _);
            channel.Writer.TryComplete();
        }
    }

    /// <summary>
    /// Clears the log buffer.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _buffer.Clear();
        }
    }
}
