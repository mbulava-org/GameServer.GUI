using Microsoft.AspNetCore.SignalR;

namespace GameServer.Docker.Interfaces;

/// <summary>
/// Aggregates real-time container log streams from Node Agents and fans them out
/// to multiple SignalR subscribers. A single agent stream is maintained per server;
/// all clients viewing the same server share the same underlying stream.
/// </summary>
public interface IServerLogAggregator
{
    /// <summary>
    /// Returns an asynchronous sequence of log lines for the requested server.
    /// The sequence is backed by a shared agent stream; multiple callers receive
    /// the same data without opening separate agent connections.
    /// </summary>
    /// <param name="serverId">Server ID to stream logs for</param>
    /// <param name="follow">Continuously stream new logs</param>
    /// <param name="tailLines">Number of recent lines to include</param>
    /// <param name="timestamps">Include timestamps</param>
    /// <param name="cancellationToken">Cancellation token</param>
    IAsyncEnumerable<string> StreamLogsAsync(
        string serverId,
        bool follow = true,
        int tailLines = 100,
        bool timestamps = true,
        CancellationToken cancellationToken = default);
}
