using GameServer.Windows.Agent.Models;

namespace GameServer.Windows.Agent.Interfaces;

public interface IGameProcessManager
{
    Task<GameServerProcessInfo> StartServerAsync(StartServerRequest request, CancellationToken cancellationToken = default);
    Task<GameServerProcessInfo> StopServerAsync(string serverId, StopServerRequest? request = null, CancellationToken cancellationToken = default);
    Task<GameServerProcessInfo> RestartServerAsync(string serverId, CancellationToken cancellationToken = default);
    GameServerProcessInfo? GetServerInfo(string serverId);
    IReadOnlyList<GameServerProcessInfo> GetAllServers();
    ProcessLogsResponse GetLogs(string serverId, int tailLines = 100);
    IAsyncEnumerable<string> StreamLogsAsync(string serverId, bool includeHistory = true, int historyTailLines = 100, CancellationToken cancellationToken = default);
    Task<SendCommandResponse> SendCommandAsync(string serverId, SendCommandRequest request, CancellationToken cancellationToken = default);
    ProcessStatsSnapshot? GetStats(string serverId);
    IAsyncEnumerable<ProcessStatsSnapshot> StreamStatsAsync(string serverId, TimeSpan? interval = null, CancellationToken cancellationToken = default);
}
