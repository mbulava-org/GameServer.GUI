namespace GameServer.API.Interfaces;

/// <summary>
/// Defines operations executed against remote Windows Host Agents.
/// </summary>
public interface IWindowsAgentOperations
{
    /// <summary>
    /// Starts a game server process on the specified Windows Agent.
    /// </summary>
    Task<WindowsProcessInfo?> StartServerAsync(string agentUrl, WindowsStartServerRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops a game server process on the specified Windows Agent.
    /// </summary>
    Task<WindowsProcessInfo?> StopServerAsync(string agentUrl, string serverId, WindowsStopServerRequest? request = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restarts a game server process on the specified Windows Agent.
    /// </summary>
    Task<WindowsProcessInfo?> RestartServerAsync(string agentUrl, string serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets process details and status for a game server on the specified Windows Agent.
    /// </summary>
    Task<WindowsProcessInfo?> GetServerInfoAsync(string agentUrl, string serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets point-in-time process metrics for a game server on the specified Windows Agent.
    /// </summary>
    Task<WindowsProcessStats?> GetServerStatsAsync(string agentUrl, string serverId, CancellationToken cancellationToken = default);
}

public sealed record WindowsStartServerRequest
{
    public string ServerId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? GameTypeKey { get; init; }
    public uint? SteamAppId { get; init; }
    public string? InstallDirectory { get; init; }
    public string ExecutablePath { get; init; } = string.Empty;
    public string? Arguments { get; init; }
    public string? WorkingDirectory { get; init; }
    public Dictionary<string, string>? EnvironmentVariables { get; init; }
    public bool AutoRestart { get; init; } = true;
    public int? RconPort { get; init; }
    public string? RconPassword { get; init; }
}

public sealed record WindowsStopServerRequest
{
    public int? GracefulTimeoutSeconds { get; init; }
    public bool Force { get; init; }
}

public sealed record WindowsProcessInfo
{
    public string ServerId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int? ProcessId { get; init; }
    public DateTime? StartedAt { get; init; }
    public int? ExitCode { get; init; }
    public long MemoryWorkingSetBytes { get; init; }
    public long MemoryPrivateBytes { get; init; }
    public double CpuPercent { get; init; }
    public int RestartCount { get; init; }
}

public sealed record WindowsProcessStats
{
    public string ServerId { get; init; } = string.Empty;
    public int? ProcessId { get; init; }
    public double CpuPercent { get; init; }
    public long MemoryWorkingSetBytes { get; init; }
    public long MemoryPrivateBytes { get; init; }
    public int ThreadCount { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
