using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using GameServer.Windows.Agent.Configurations;
using GameServer.Windows.Agent.Interfaces;
using GameServer.Windows.Agent.Models;
using GameServer.Windows.Agent.Native;
using Microsoft.Extensions.Options;

namespace GameServer.Windows.Agent.Services;

public sealed class GameProcessManager : IGameProcessManager, IDisposable
{
    private readonly ILogger<GameProcessManager> _logger;
    private readonly WindowsAgentOptions _options;
    private readonly ConcurrentDictionary<string, ManagedServerContext> _servers = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public GameProcessManager(
        ILogger<GameProcessManager> logger,
        IOptions<WindowsAgentOptions> options)
    {
        _logger = logger;
        _options = options.Value;

        // Suppress crash popups on Windows so failed processes exit cleanly without modal dialogs
        WindowsProcessHelper.SuppressCrashDialogs();
    }

    public async Task<GameServerProcessInfo> StartServerAsync(StartServerRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ServerId);

        _logger.LogInformation("Starting game server '{ServerId}' ({Name})", request.ServerId, request.Name);

        var context = _servers.GetOrAdd(request.ServerId, id => new ManagedServerContext
        {
            ServerId = id,
            Instance = new GameServerInstance
            {
                ServerId = id,
                Name = request.Name,
                GameTypeKey = request.GameTypeKey,
                SteamAppId = request.SteamAppId,
                InstallDirectory = request.InstallDirectory ?? Path.Combine(_options.Storage.BaseInstancesDirectory, id),
                ExecutablePath = request.ExecutablePath,
                Arguments = request.Arguments ?? string.Empty,
                WorkingDirectory = request.WorkingDirectory,
                EnvironmentVariables = request.EnvironmentVariables ?? new(),
                AutoRestart = request.AutoRestart,
                RconPort = request.RconPort,
                RconPassword = request.RconPassword
            },
            LogBuffer = new ProcessLogRingBuffer(_options.ProcessSupervision.LogBufferSizeLines)
        });

        // Update instance details if provided
        context.Instance.Name = request.Name;
        context.Instance.ExecutablePath = request.ExecutablePath;
        context.Instance.Arguments = request.Arguments ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(request.InstallDirectory))
        {
            context.Instance.InstallDirectory = request.InstallDirectory;
        }
        if (!string.IsNullOrWhiteSpace(request.WorkingDirectory))
        {
            context.Instance.WorkingDirectory = request.WorkingDirectory;
        }
        if (request.EnvironmentVariables != null)
        {
            context.Instance.EnvironmentVariables = request.EnvironmentVariables;
        }
        context.Instance.AutoRestart = request.AutoRestart;
        context.Instance.RconPort = request.RconPort;
        context.Instance.RconPassword = request.RconPassword;

        await context.Lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (context.Status == ServerProcessStatus.Running && context.Process is { HasExited: false })
            {
                _logger.LogWarning("Server '{ServerId}' is already running with PID {ProcessId}", request.ServerId, context.Process.Id);
                return context.ToProcessInfo();
            }

            context.Status = ServerProcessStatus.Starting;
            context.Instance.LastStartedAt = DateTime.UtcNow;

            // Resolve paths
            var workingDir = !string.IsNullOrWhiteSpace(context.Instance.WorkingDirectory)
                ? context.Instance.WorkingDirectory
                : context.Instance.InstallDirectory;

            var exePath = Path.IsPathRooted(context.Instance.ExecutablePath)
                ? context.Instance.ExecutablePath
                : Path.Combine(workingDir, context.Instance.ExecutablePath);

            if (!File.Exists(exePath))
            {
                context.Status = ServerProcessStatus.Stopped;
                throw new FileNotFoundException($"Game server executable not found at: {exePath}");
            }

            // Create Job Object
            context.JobObject?.Dispose();
            context.JobObject = new JobObject($"GameServer_{request.ServerId}");

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = context.Instance.Arguments,
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            foreach (var (key, value) in context.Instance.EnvironmentVariables)
            {
                startInfo.Environment[key] = value;
            }

            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            context.Process = process;
            context.StartedAt = DateTime.UtcNow;
            context.ManualStopRequested = false;

            process.OutputDataReceived += (_, args) =>
            {
                if (args.Data != null)
                {
                    context.LogBuffer.Append($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {args.Data}");
                }
            };

            process.ErrorDataReceived += (_, args) =>
            {
                if (args.Data != null)
                {
                    context.LogBuffer.Append($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] [ERR] {args.Data}");
                }
            };

            process.Exited += async (_, _) =>
            {
                await HandleProcessExitedAsync(context).ConfigureAwait(false);
            };

            context.LogBuffer.Append($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] [SYSTEM] Launching {exePath} {startInfo.Arguments}");

            if (!process.Start())
            {
                context.Status = ServerProcessStatus.Stopped;
                throw new InvalidOperationException($"Failed to start process for server '{request.ServerId}'");
            }

            // Assign to Job Object
            try
            {
                context.JobObject.AssignProcess(process.Handle);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not assign process {ProcessId} to Job Object", process.Id);
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            context.Status = ServerProcessStatus.Running;
            _logger.LogInformation("Server '{ServerId}' started successfully with PID {ProcessId}", request.ServerId, process.Id);

            return context.ToProcessInfo();
        }
        finally
        {
            context.Lock.Release();
        }
    }

    public async Task<GameServerProcessInfo> StopServerAsync(string serverId, StopServerRequest? request = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);

        if (!_servers.TryGetValue(serverId, out var context))
        {
            throw new KeyNotFoundException($"Server '{serverId}' not found.");
        }

        await context.Lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (context.Process is null || context.Process.HasExited)
            {
                context.Status = ServerProcessStatus.Stopped;
                return context.ToProcessInfo();
            }

            context.ManualStopRequested = true;
            context.Status = ServerProcessStatus.Stopping;

            var process = context.Process;
            var timeoutSeconds = request?.GracefulTimeoutSeconds ?? _options.ProcessSupervision.GracefulStopTimeoutSeconds;
            var force = request?.Force ?? false;

            context.LogBuffer.Append($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] [SYSTEM] Stop requested (Force={force}, Timeout={timeoutSeconds}s)");

            if (!force)
            {
                // Attempt 1: Send standard quit command via stdin
                try
                {
                    if (process.StandardInput.BaseStream.CanWrite)
                    {
                        await process.StandardInput.WriteLineAsync("quit").ConfigureAwait(false);
                        await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
                catch
                {
                    // Stdin write may fail if process is closing
                }

                // Attempt 2: Send Ctrl+C
                try
                {
                    WindowsProcessHelper.SendCtrlC(process.Id);
                }
                catch
                {
                    // Best effort
                }

                // Wait for graceful exit
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                try
                {
                    await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Server '{ServerId}' did not exit gracefully within {Timeout}s. Forcing termination.", serverId, timeoutSeconds);
                }
            }

            // If still running, force kill process tree
            if (!process.HasExited)
            {
                context.LogBuffer.Append($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] [SYSTEM] Killing process tree (PID: {process.Id})");
                await WindowsProcessHelper.KillProcessTreeAsync(process.Id, CancellationToken.None).ConfigureAwait(false);
            }

            context.JobObject?.Dispose();
            context.JobObject = null;
            context.Status = ServerProcessStatus.Stopped;

            _logger.LogInformation("Server '{ServerId}' has been stopped", serverId);
            return context.ToProcessInfo();
        }
        finally
        {
            context.Lock.Release();
        }
    }

    public async Task<GameServerProcessInfo> RestartServerAsync(string serverId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);

        if (!_servers.TryGetValue(serverId, out var context))
        {
            throw new KeyNotFoundException($"Server '{serverId}' not found.");
        }

        await StopServerAsync(serverId, new StopServerRequest { Force = false }, cancellationToken).ConfigureAwait(false);

        return await StartServerAsync(new StartServerRequest
        {
            ServerId = context.Instance.ServerId,
            Name = context.Instance.Name,
            GameTypeKey = context.Instance.GameTypeKey,
            SteamAppId = context.Instance.SteamAppId,
            InstallDirectory = context.Instance.InstallDirectory,
            ExecutablePath = context.Instance.ExecutablePath,
            Arguments = context.Instance.Arguments,
            WorkingDirectory = context.Instance.WorkingDirectory,
            EnvironmentVariables = context.Instance.EnvironmentVariables,
            AutoRestart = context.Instance.AutoRestart,
            RconPort = context.Instance.RconPort,
            RconPassword = context.Instance.RconPassword
        }, cancellationToken).ConfigureAwait(false);
    }

    public GameServerProcessInfo? GetServerInfo(string serverId)
    {
        return _servers.TryGetValue(serverId, out var context) ? context.ToProcessInfo() : null;
    }

    public IReadOnlyList<GameServerProcessInfo> GetAllServers()
    {
        return _servers.Values.Select(c => c.ToProcessInfo()).ToList();
    }

    public ProcessLogsResponse GetLogs(string serverId, int tailLines = 100)
    {
        if (!_servers.TryGetValue(serverId, out var context))
        {
            throw new KeyNotFoundException($"Server '{serverId}' not found.");
        }

        return new ProcessLogsResponse
        {
            ServerId = serverId,
            Logs = context.LogBuffer.GetTail(tailLines)
        };
    }

    public async IAsyncEnumerable<string> StreamLogsAsync(
        string serverId,
        bool includeHistory = true,
        int historyTailLines = 100,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!_servers.TryGetValue(serverId, out var context))
        {
            throw new KeyNotFoundException($"Server '{serverId}' not found.");
        }

        await foreach (var line in context.LogBuffer.StreamLogsAsync(includeHistory, historyTailLines, cancellationToken))
        {
            yield return line;
        }
    }

    public async Task<SendCommandResponse> SendCommandAsync(string serverId, SendCommandRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_servers.TryGetValue(serverId, out var context))
        {
            return new SendCommandResponse { Success = false, Error = $"Server '{serverId}' not found." };
        }

        if (context.Status != ServerProcessStatus.Running || context.Process is not { HasExited: false })
        {
            return new SendCommandResponse { Success = false, Error = $"Server '{serverId}' is not running." };
        }

        try
        {
            context.LogBuffer.Append($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] [CMD INPUT] {request.Command}");

            await context.Process.StandardInput.WriteLineAsync(request.Command).ConfigureAwait(false);
            await context.Process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);

            return new SendCommandResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send command to server '{ServerId}'", serverId);
            return new SendCommandResponse { Success = false, Error = ex.Message };
        }
    }

    public ProcessStatsSnapshot? GetStats(string serverId)
    {
        if (!_servers.TryGetValue(serverId, out var context))
        {
            return null;
        }

        var info = context.ToProcessInfo();
        return new ProcessStatsSnapshot
        {
            ServerId = serverId,
            ProcessId = info.ProcessId,
            CpuPercent = info.CpuPercent,
            MemoryWorkingSetBytes = info.MemoryWorkingSetBytes,
            MemoryPrivateBytes = info.MemoryPrivateBytes,
            ThreadCount = context.Process is { HasExited: false } ? context.Process.Threads.Count : 0,
            Timestamp = DateTime.UtcNow
        };
    }

    public async IAsyncEnumerable<ProcessStatsSnapshot> StreamStatsAsync(
        string serverId,
        TimeSpan? interval = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var pollInterval = interval ?? TimeSpan.FromSeconds(2);
        using var timer = new PeriodicTimer(pollInterval);

        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            var stats = GetStats(serverId);
            if (stats != null)
            {
                yield return stats;
            }
        }
    }

    private async Task HandleProcessExitedAsync(ManagedServerContext context)
    {
        await context.Lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var exitCode = context.Process?.ExitCode ?? -1;
            var wasManual = context.ManualStopRequested;

            context.LogBuffer.Append($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] [SYSTEM] Process exited with code {exitCode} (ManualStop={wasManual})");

            context.JobObject?.Dispose();
            context.JobObject = null;

            if (wasManual)
            {
                context.Status = ServerProcessStatus.Stopped;
                context.RestartCount = 0;
                return;
            }

            // Unexpected exit (crash)
            context.Status = ServerProcessStatus.Crashed;
            _logger.LogWarning("Server '{ServerId}' crashed with exit code {ExitCode}", context.ServerId, exitCode);

            if (_options.ProcessSupervision.EnableCrashRestart && context.Instance.AutoRestart)
            {
                if (context.RestartCount < _options.ProcessSupervision.MaxRestartRetries)
                {
                    context.RestartCount++;
                    var backoff = _options.ProcessSupervision.RestartBackoffSeconds;
                    context.LogBuffer.Append($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] [SYSTEM] Auto-restarting server in {backoff}s (Attempt {context.RestartCount}/{_options.ProcessSupervision.MaxRestartRetries})...");

                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(backoff)).ConfigureAwait(false);
                        try
                        {
                            await StartServerAsync(new StartServerRequest
                            {
                                ServerId = context.Instance.ServerId,
                                Name = context.Instance.Name,
                                GameTypeKey = context.Instance.GameTypeKey,
                                SteamAppId = context.Instance.SteamAppId,
                                InstallDirectory = context.Instance.InstallDirectory,
                                ExecutablePath = context.Instance.ExecutablePath,
                                Arguments = context.Instance.Arguments,
                                WorkingDirectory = context.Instance.WorkingDirectory,
                                EnvironmentVariables = context.Instance.EnvironmentVariables,
                                AutoRestart = context.Instance.AutoRestart,
                                RconPort = context.Instance.RconPort,
                                RconPassword = context.Instance.RconPassword
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed auto-restart for server '{ServerId}'", context.ServerId);
                        }
                    });
                }
                else
                {
                    context.LogBuffer.Append($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] [SYSTEM] Max restart attempts ({_options.ProcessSupervision.MaxRestartRetries}) reached. Server remains crashed.");
                }
            }
        }
        finally
        {
            context.Lock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        foreach (var context in _servers.Values)
        {
            try
            {
                if (context.Process is { HasExited: false })
                {
                    context.Process.Kill(true);
                }
                context.JobObject?.Dispose();
            }
            catch
            {
                // Cleanup best effort
            }
        }

        _disposed = true;
    }

    private class ManagedServerContext
    {
        public string ServerId { get; set; } = string.Empty;
        public GameServerInstance Instance { get; set; } = new();
        public ProcessLogRingBuffer LogBuffer { get; set; } = new();
        public Process? Process { get; set; }
        public JobObject? JobObject { get; set; }
        public ServerProcessStatus Status { get; set; } = ServerProcessStatus.Stopped;
        public DateTime? StartedAt { get; set; }
        public bool ManualStopRequested { get; set; }
        public int RestartCount { get; set; }
        public SemaphoreSlim Lock { get; } = new(1, 1);

        // Previous sample for CPU calculation
        public DateTime? LastCpuSampleTime { get; set; }
        public TimeSpan LastTotalProcessorTime { get; set; }

        public GameServerProcessInfo ToProcessInfo()
        {
            var info = new GameServerProcessInfo
            {
                ServerId = ServerId,
                Name = Instance.Name,
                Status = Status,
                ProcessId = Process is { HasExited: false } ? Process.Id : null,
                StartedAt = StartedAt,
                ExitCode = Process is { HasExited: true } ? Process.ExitCode : null,
                RestartCount = RestartCount
            };

            if (Process is { HasExited: false })
            {
                try
                {
                    Process.Refresh();
                    info.MemoryWorkingSetBytes = Process.WorkingSet64;
                    info.MemoryPrivateBytes = Process.PrivateMemorySize64;

                    var now = DateTime.UtcNow;
                    var totalProcTime = Process.TotalProcessorTime;

                    if (LastCpuSampleTime.HasValue)
                    {
                        var timeDelta = (now - LastCpuSampleTime.Value).TotalMilliseconds;
                        var procDelta = (totalProcTime - LastTotalProcessorTime).TotalMilliseconds;
                        if (timeDelta > 0)
                        {
                            info.CpuPercent = Math.Round((procDelta / (timeDelta * Environment.ProcessorCount)) * 100, 1);
                        }
                    }

                    LastCpuSampleTime = now;
                    LastTotalProcessorTime = totalProcTime;
                }
                catch
                {
                    // Process may exit during sampling
                }
            }

            return info;
        }
    }
}
