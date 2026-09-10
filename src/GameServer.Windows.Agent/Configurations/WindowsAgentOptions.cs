namespace GameServer.Windows.Agent.Configurations;

public class WindowsAgentOptions
{
    public const string SectionName = "WindowsAgent";

    public SteamCmdOptions SteamCmd { get; set; } = new();
    public StorageOptions Storage { get; set; } = new();
    public ProcessSupervisionOptions ProcessSupervision { get; set; } = new();
    public AgentRegistrationOptions AgentRegistration { get; set; } = new();
    public string AgentPort { get; set; } = "5180";
}

public class SteamCmdOptions
{
    public string SteamCmdDirectory { get; set; } = "C:\\GameServers\\_steamcmd";
    public string ExecutableName { get; set; } = "steamcmd.exe";
    public bool AutoDownloadIfMissing { get; set; } = true;
    public string DownloadUrl { get; set; } = "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip";
    public int DefaultTimeoutMinutes { get; set; } = 30;
}

public class StorageOptions
{
    public string BaseInstancesDirectory { get; set; } = "C:\\GameServers\\instances";
    public string BackupsDirectory { get; set; } = "C:\\GameServers\\backups";
}

public class ProcessSupervisionOptions
{
    public int GracefulStopTimeoutSeconds { get; set; } = 30;
    public int LogBufferSizeLines { get; set; } = 2000;
    public bool EnableCrashRestart { get; set; } = true;
    public int MaxRestartRetries { get; set; } = 5;
    public int RestartBackoffSeconds { get; set; } = 10;
}

public class AgentRegistrationOptions
{
    public bool Enabled { get; set; } = true;
    public string PrimaryServiceUrl { get; set; } = "http://localhost:5164";
    public int HeartbeatIntervalSeconds { get; set; } = 30;
    public int ConnectionTimeoutSeconds { get; set; } = 15;
    public int MaxStartupRetries { get; set; } = 30;
    public int StartupRetryDelaySeconds { get; set; } = 5;
    public int[] ReconnectDelaySeconds { get; set; } = [2, 5, 10, 30, 60];
    public string? NodeId { get; set; }
    public string? NodeName { get; set; }
    public List<string> Capabilities { get; set; } =
    [
        "steamcmd",
        "windows-process",
        "process-exec",
        "stats",
        "logs",
        "files",
        "ports"
    ];
}
