namespace GameServer.Windows.Agent.Models;

public enum ServerProcessStatus
{
    Stopped,
    Starting,
    Running,
    Stopping,
    Crashed,
    Updating
}

public class GameServerInstance
{
    public string ServerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? GameTypeKey { get; set; }
    public uint? SteamAppId { get; set; }
    public string InstallDirectory { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string? WorkingDirectory { get; set; }
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
    public bool AutoRestart { get; set; } = true;
    public int? RconPort { get; set; }
    public string? RconPassword { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastStartedAt { get; set; }
}

public class GameServerProcessInfo
{
    public string ServerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ServerProcessStatus Status { get; set; } = ServerProcessStatus.Stopped;
    public int? ProcessId { get; set; }
    public DateTime? StartedAt { get; set; }
    public TimeSpan? Uptime => StartedAt.HasValue && Status == ServerProcessStatus.Running 
        ? DateTime.UtcNow - StartedAt.Value 
        : null;
    public int? ExitCode { get; set; }
    public long MemoryWorkingSetBytes { get; set; }
    public long MemoryPrivateBytes { get; set; }
    public double CpuPercent { get; set; }
    public int RestartCount { get; set; }
}

public class StartServerRequest
{
    public string ServerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? GameTypeKey { get; set; }
    public uint? SteamAppId { get; set; }
    public string? InstallDirectory { get; set; }
    public string ExecutablePath { get; set; } = string.Empty;
    public string? Arguments { get; set; }
    public string? WorkingDirectory { get; set; }
    public Dictionary<string, string>? EnvironmentVariables { get; set; }
    public bool AutoRestart { get; set; } = true;
    public int? RconPort { get; set; }
    public string? RconPassword { get; set; }
}

public class StopServerRequest
{
    public int? GracefulTimeoutSeconds { get; set; }
    public bool Force { get; set; }
}

public class SendCommandRequest
{
    public string Command { get; set; } = string.Empty;
    public bool UseRcon { get; set; }
}

public class SendCommandResponse
{
    public bool Success { get; set; }
    public string? Response { get; set; }
    public string? Error { get; set; }
}

public class ProcessStatsSnapshot
{
    public string ServerId { get; set; } = string.Empty;
    public int? ProcessId { get; set; }
    public double CpuPercent { get; set; }
    public long MemoryWorkingSetBytes { get; set; }
    public long MemoryPrivateBytes { get; set; }
    public int ThreadCount { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class ProcessLogsResponse
{
    public string ServerId { get; set; } = string.Empty;
    public List<string> Logs { get; set; } = [];
}
