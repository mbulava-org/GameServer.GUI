using System.Diagnostics;
using System.Runtime.InteropServices;
using GameServer.Windows.Agent.Interfaces;

namespace GameServer.Windows.Agent.Services;

public sealed class WindowsResourceMonitor : IWindowsResourceMonitor
{
    private readonly ILogger<WindowsResourceMonitor> _logger;
    private DateTime _lastCpuSample = DateTime.UtcNow;
    private TimeSpan _lastCpuTotal = TimeSpan.Zero;

    public WindowsResourceMonitor(ILogger<WindowsResourceMonitor> logger)
    {
        _logger = logger;
    }

    public HostResourceSnapshot GetHostSnapshot()
    {
        var snapshot = new HostResourceSnapshot();

        // Query memory
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var memStatus = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(memStatus))
            {
                snapshot.TotalMemoryBytes = (long)memStatus.ullTotalPhys;
                snapshot.FreeMemoryBytes = (long)memStatus.ullAvailPhys;
            }
        }
        else
        {
            // Fallback for non-Windows (GC heap info)
            var memInfo = GC.GetGCMemoryInfo();
            snapshot.TotalMemoryBytes = memInfo.TotalAvailableMemoryBytes;
            snapshot.FreeMemoryBytes = Math.Max(0, memInfo.TotalAvailableMemoryBytes - memInfo.MemoryLoadBytes);
        }

        // Query drives
        try
        {
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
            {
                snapshot.Drives.Add(new DriveResourceSnapshot
                {
                    Name = drive.Name,
                    VolumeLabel = drive.VolumeLabel,
                    TotalBytes = drive.TotalSize,
                    AvailableFreeBytes = drive.AvailableFreeSpace
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read drive information");
        }

        // Host CPU estimate
        try
        {
            var now = DateTime.UtcNow;
            var currentProcessTotal = Process.GetCurrentProcess().TotalProcessorTime;
            var timeDelta = (now - _lastCpuSample).TotalMilliseconds;
            var cpuDelta = (currentProcessTotal - _lastCpuTotal).TotalMilliseconds;

            if (timeDelta > 0)
            {
                snapshot.HostCpuPercent = Math.Round((cpuDelta / (timeDelta * Environment.ProcessorCount)) * 100, 1);
            }

            _lastCpuSample = now;
            _lastCpuTotal = currentProcessTotal;
        }
        catch
        {
            // Ignore CPU sampling errors
        }

        return snapshot;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;

        public MEMORYSTATUSEX()
        {
            dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);
}
