namespace GameServer.Windows.Agent.Interfaces;

public class HostResourceSnapshot
{
    public string HostName { get; set; } = Environment.MachineName;
    public string OsVersion { get; set; } = Environment.OSVersion.ToString();
    public int ProcessorCount { get; set; } = Environment.ProcessorCount;
    public long TotalMemoryBytes { get; set; }
    public long FreeMemoryBytes { get; set; }
    public double MemoryUsagePercent => TotalMemoryBytes > 0 
        ? Math.Round(((double)(TotalMemoryBytes - FreeMemoryBytes) / TotalMemoryBytes) * 100, 1) 
        : 0;
    public double HostCpuPercent { get; set; }
    public List<DriveResourceSnapshot> Drives { get; set; } = [];
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class DriveResourceSnapshot
{
    public string Name { get; set; } = string.Empty;
    public string VolumeLabel { get; set; } = string.Empty;
    public long TotalBytes { get; set; }
    public long AvailableFreeBytes { get; set; }
    public double FreePercent => TotalBytes > 0 
        ? Math.Round(((double)AvailableFreeBytes / TotalBytes) * 100, 1) 
        : 0;
}

public interface IWindowsResourceMonitor
{
    HostResourceSnapshot GetHostSnapshot();
}
