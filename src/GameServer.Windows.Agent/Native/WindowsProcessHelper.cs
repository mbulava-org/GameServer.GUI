using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GameServer.Windows.Agent.Native;

public static class WindowsProcessHelper
{
    private const uint SEM_FAILCRITICALERRORS = 0x0001;
    private const uint SEM_NOGPFAULTERRORBOX = 0x0002;
    private const uint CTRL_C_EVENT = 0;
    private const uint CTRL_BREAK_EVENT = 1;

    [DllImport("kernel32.dll")]
    private static extern uint SetErrorMode(uint uMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    private static extern bool FreeConsole();

    /// <summary>
    /// Suppresses Windows Error Reporting crash dialogs so a crashed game server process
    /// terminates immediately instead of hanging waiting for user interaction.
    /// </summary>
    public static void SuppressCrashDialogs()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX);
            }
            catch
            {
                // Best effort
            }
        }
    }

    /// <summary>
    /// Sends a console Ctrl+C event to a target process group to request a graceful shutdown.
    /// </summary>
    public static bool SendCtrlC(int processId)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        try
        {
            if (AttachConsole((uint)processId))
            {
                try
                {
                    return GenerateConsoleCtrlEvent(CTRL_C_EVENT, 0);
                }
                finally
                {
                    FreeConsole();
                }
            }
        }
        catch
        {
            // Fall back
        }

        return false;
    }

    /// <summary>
    /// Forcibly terminates a process and all of its child processes using taskkill or process tree kill.
    /// </summary>
    public static async Task KillProcessTreeAsync(int processId, CancellationToken cancellationToken = default)
    {
        if (processId <= 0)
        {
            return;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }
        catch (ArgumentException)
        {
            // Process already exited
            return;
        }
        catch (InvalidOperationException)
        {
            // Process already exited
            return;
        }
        catch
        {
            // Fall back to taskkill on Windows
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                using var taskkill = Process.Start(new ProcessStartInfo
                {
                    FileName = "taskkill.exe",
                    Arguments = $"/F /T /PID {processId}",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });

                if (taskkill != null)
                {
                    await taskkill.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch
            {
                // Best effort
            }
        }
    }
}
