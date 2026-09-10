using System.Diagnostics;
using System.IO.Compression;
using GameServer.Windows.Agent.Configurations;
using GameServer.Windows.Agent.Interfaces;
using GameServer.Windows.Agent.Models;
using Microsoft.Extensions.Options;

namespace GameServer.Windows.Agent.Services;

public sealed class SteamCmdService : ISteamCmdService
{
    private readonly ILogger<SteamCmdService> _logger;
    private readonly SteamCmdOptions _options;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _executionLock = new(1, 1);

    public SteamCmdService(
        ILogger<SteamCmdService> logger,
        IOptions<WindowsAgentOptions> options,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _options = options.Value.SteamCmd;
        _httpClient = httpClientFactory.CreateClient();
    }

    public string ExecutablePath => Path.Combine(_options.SteamCmdDirectory, _options.ExecutableName);

    public bool IsInstalled()
    {
        return File.Exists(ExecutablePath);
    }

    public async Task EnsureSteamCmdInstalledAsync(CancellationToken cancellationToken = default)
    {
        if (IsInstalled())
        {
            return;
        }

        if (!_options.AutoDownloadIfMissing)
        {
            throw new FileNotFoundException($"SteamCMD executable was not found at '{ExecutablePath}' and AutoDownloadIfMissing is false.");
        }

        _logger.LogInformation("SteamCMD not found at '{Path}'. Starting automated download from '{Url}'...", ExecutablePath, _options.DownloadUrl);

        Directory.CreateDirectory(_options.SteamCmdDirectory);
        var tempZipPath = Path.Combine(_options.SteamCmdDirectory, "steamcmd_temp.zip");

        try
        {
            using (var response = await _httpClient.GetAsync(_options.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using var fileStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await stream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
            }

            _logger.LogInformation("Extracting SteamCMD archive to '{Directory}'...", _options.SteamCmdDirectory);
            ZipFile.ExtractToDirectory(tempZipPath, _options.SteamCmdDirectory, overwriteFiles: true);

            _logger.LogInformation("SteamCMD successfully installed at '{Path}'", ExecutablePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download or extract SteamCMD from '{Url}'", _options.DownloadUrl);
            throw;
        }
        finally
        {
            if (File.Exists(tempZipPath))
            {
                try
                {
                    File.Delete(tempZipPath);
                }
                catch
                {
                    // Ignore temp file deletion failure
                }
            }
        }
    }

    public async Task<SteamCmdJobResult> InstallOrUpdateAppAsync(
        SteamAppInstallRequest request,
        IProgress<SteamCmdProgressEvent>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.AppId == 0) throw new ArgumentException("AppId must be greater than 0", nameof(request));
        ArgumentException.ThrowIfNullOrWhiteSpace(request.InstallDirectory);

        await EnsureSteamCmdInstalledAsync(cancellationToken).ConfigureAwait(false);

        var jobId = Guid.NewGuid().ToString("N");
        var targetDir = Path.GetFullPath(request.InstallDirectory);
        Directory.CreateDirectory(targetDir);

        _logger.LogInformation("Starting SteamCMD installation job {JobId} for App {AppId} at '{InstallDir}'",
            jobId, request.AppId, targetDir);

        // Build command arguments
        var args = new List<string>
        {
            $"+force_install_dir \"{targetDir}\""
        };

        if (request.AnonymousLogin)
        {
            args.Add("+login anonymous");
        }
        else
        {
            var loginCmd = $"+login {request.Username} {request.Password}";
            if (!string.IsNullOrWhiteSpace(request.SteamGuardCode))
            {
                loginCmd += $" {request.SteamGuardCode}";
            }
            args.Add(loginCmd);
        }

        var updateCmd = $"+app_update {request.AppId}";
        if (!string.IsNullOrWhiteSpace(request.Branch))
        {
            updateCmd += $" -beta {request.Branch}";
            if (!string.IsNullOrWhiteSpace(request.BetaPassword))
            {
                updateCmd += $" -betapassword {request.BetaPassword}";
            }
        }

        if (request.Validate)
        {
            updateCmd += " validate";
        }
        args.Add(updateCmd);
        args.Add("+quit");

        var argumentString = string.Join(" ", args);
        return await ExecuteSteamCmdJobAsync(jobId, request.AppId, argumentString, progress, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SteamCmdJobResult> DownloadWorkshopItemAsync(
        SteamWorkshopDownloadRequest request,
        IProgress<SteamCmdProgressEvent>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.AppId == 0) throw new ArgumentException("AppId must be greater than 0", nameof(request));
        if (request.WorkshopItemId == 0) throw new ArgumentException("WorkshopItemId must be greater than 0", nameof(request));

        await EnsureSteamCmdInstalledAsync(cancellationToken).ConfigureAwait(false);

        var jobId = Guid.NewGuid().ToString("N");
        var args = new List<string>();

        if (!string.IsNullOrWhiteSpace(request.InstallDirectory))
        {
            var targetDir = Path.GetFullPath(request.InstallDirectory);
            Directory.CreateDirectory(targetDir);
            args.Add($"+force_install_dir \"{targetDir}\"");
        }

        args.Add("+login anonymous");
        args.Add($"+workshop_download_item {request.AppId} {request.WorkshopItemId}");
        args.Add("+quit");

        var argumentString = string.Join(" ", args);
        return await ExecuteSteamCmdJobAsync(jobId, request.AppId, argumentString, progress, cancellationToken).ConfigureAwait(false);
    }

    public SteamAppStatusResponse GetAppStatus(uint appId, string installDirectory)
    {
        if (string.IsNullOrWhiteSpace(installDirectory) || !Directory.Exists(installDirectory))
        {
            return new SteamAppStatusResponse
            {
                AppId = appId,
                InstallDirectory = installDirectory,
                IsInstalled = false
            };
        }

        var dirInfo = new DirectoryInfo(installDirectory);
        var exes = dirInfo.GetFiles("*.exe", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(installDirectory, f.FullName))
            .ToList();

        var totalSize = 0L;
        try
        {
            totalSize = dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
        }
        catch
        {
            // Best effort
        }

        // An app is considered installed if executable files or appmanifest exists
        var manifestPath = Path.Combine(installDirectory, "steamapps", $"appmanifest_{appId}.acf");
        var isInstalled = File.Exists(manifestPath) || exes.Count > 0;

        return new SteamAppStatusResponse
        {
            AppId = appId,
            InstallDirectory = installDirectory,
            IsInstalled = isInstalled,
            TotalSizeBytes = totalSize,
            LastModified = dirInfo.LastWriteTimeUtc,
            Executables = exes
        };
    }

    private async Task<SteamCmdJobResult> ExecuteSteamCmdJobAsync(
        string jobId,
        uint appId,
        string arguments,
        IProgress<SteamCmdProgressEvent>? progress,
        CancellationToken cancellationToken)
    {
        var outputLines = new List<string>();
        var stopwatch = Stopwatch.StartNew();

        await _executionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(_options.DefaultTimeoutMinutes));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var startInfo = new ProcessStartInfo
            {
                FileName = ExecutablePath,
                Arguments = arguments,
                WorkingDirectory = _options.SteamCmdDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                lock (outputLines)
                {
                    outputLines.Add(e.Data);
                }

                var parsed = SteamCmdOutputParser.ParseLine(e.Data, jobId, appId);
                progress?.Report(parsed);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                lock (outputLines)
                {
                    outputLines.Add($"[STDERR] {e.Data}");
                }

                progress?.Report(new SteamCmdProgressEvent
                {
                    JobId = jobId,
                    AppId = appId,
                    State = "Error",
                    RawOutput = e.Data
                });
            };

            _logger.LogInformation("Executing SteamCMD: {Exe} {Args}", ExecutablePath, arguments);

            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to launch steamcmd.exe process");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
            stopwatch.Stop();

            var success = process.ExitCode == 0 || outputLines.Any(l => l.Contains("Success!", StringComparison.OrdinalIgnoreCase) || l.Contains("already up to date", StringComparison.OrdinalIgnoreCase));
            var message = success ? "SteamCMD operation completed successfully." : $"SteamCMD operation finished with exit code {process.ExitCode}.";

            _logger.LogInformation("SteamCMD job {JobId} finished in {Duration:g} with exit code {ExitCode} (Success={Success})",
                jobId, stopwatch.Elapsed, process.ExitCode, success);

            return new SteamCmdJobResult
            {
                JobId = jobId,
                AppId = appId,
                Success = success,
                ExitCode = process.ExitCode,
                Message = message,
                OutputLines = outputLines,
                Duration = stopwatch.Elapsed
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("SteamCMD job {JobId} cancelled by user", jobId);
            return new SteamCmdJobResult
            {
                JobId = jobId,
                AppId = appId,
                Success = false,
                ExitCode = -1,
                Message = "Operation cancelled by user",
                OutputLines = outputLines,
                Duration = stopwatch.Elapsed
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("SteamCMD job {JobId} timed out after {Minutes} minutes", jobId, _options.DefaultTimeoutMinutes);
            return new SteamCmdJobResult
            {
                JobId = jobId,
                AppId = appId,
                Success = false,
                ExitCode = -1,
                Message = $"Operation timed out after {_options.DefaultTimeoutMinutes} minutes",
                OutputLines = outputLines,
                Duration = stopwatch.Elapsed
            };
        }
        finally
        {
            _executionLock.Release();
        }
    }
}
