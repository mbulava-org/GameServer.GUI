namespace GameServer.Windows.Agent.Models;

public class SteamAppInstallRequest
{
    public uint AppId { get; set; }
    public string InstallDirectory { get; set; } = string.Empty;
    public bool Validate { get; set; } = true;
    public string? Branch { get; set; }
    public string? BetaPassword { get; set; }
    public bool AnonymousLogin { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? SteamGuardCode { get; set; }
}

public class SteamCmdProgressEvent
{
    public string JobId { get; set; } = string.Empty;
    public uint AppId { get; set; }
    public string State { get; set; } = "Running";
    public double? ProgressPercent { get; set; }
    public long? BytesDownloaded { get; set; }
    public long? TotalBytes { get; set; }
    public string RawOutput { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class SteamCmdJobResult
{
    public string JobId { get; set; } = string.Empty;
    public uint AppId { get; set; }
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> OutputLines { get; set; } = [];
    public TimeSpan Duration { get; set; }
}

public class SteamWorkshopDownloadRequest
{
    public uint AppId { get; set; }
    public ulong WorkshopItemId { get; set; }
    public string? InstallDirectory { get; set; }
}

public class SteamAppStatusResponse
{
    public uint AppId { get; set; }
    public string InstallDirectory { get; set; } = string.Empty;
    public bool IsInstalled { get; set; }
    public long TotalSizeBytes { get; set; }
    public DateTime? LastModified { get; set; }
    public List<string> Executables { get; set; } = [];
}
