using System.Text.RegularExpressions;
using GameServer.Windows.Agent.Models;

namespace GameServer.Windows.Agent.Services;

public static partial class SteamCmdOutputParser
{
    // Matches: Update state (0x5) downloading, progress: 45.20 (123456789 / 273123456)
    [GeneratedRegex(@"Update state \((0x[0-9a-fA-F]+)\)\s+([a-zA-Z\s]+),\s+progress:\s+([0-9.]+)\s+\(([0-9]+)\s+/\s+([0-9]+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex ProgressRegex();

    // Matches: Success! App '2394010' fully installed. OR Success! App '2394010' already up to date.
    [GeneratedRegex(@"Success!\s+App\s+'?([0-9]+)'?\s+(fully installed|already up to date)", RegexOptions.IgnoreCase)]
    private static partial Regex SuccessRegex();

    // Matches: ERROR! Failed to install app '2394010' (No subscription)
    [GeneratedRegex(@"ERROR!\s+Failed to (?:install|update) app\s+'?([0-9]+)'?\s*\((.+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex ErrorRegex();

    /// <summary>
    /// Parses a single line of SteamCMD output and returns a structured progress event if applicable.
    /// </summary>
    public static SteamCmdProgressEvent ParseLine(string line, string jobId, uint appId)
    {
        var progressEvent = new SteamCmdProgressEvent
        {
            JobId = jobId,
            AppId = appId,
            RawOutput = line,
            Timestamp = DateTime.UtcNow
        };

        if (string.IsNullOrWhiteSpace(line))
        {
            return progressEvent;
        }

        var progressMatch = ProgressRegex().Match(line);
        if (progressMatch.Success)
        {
            progressEvent.State = progressMatch.Groups[2].Value.Trim();
            if (double.TryParse(progressMatch.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture, out var pct))
            {
                progressEvent.ProgressPercent = pct;
            }
            if (long.TryParse(progressMatch.Groups[4].Value, out var downloaded))
            {
                progressEvent.BytesDownloaded = downloaded;
            }
            if (long.TryParse(progressMatch.Groups[5].Value, out var total))
            {
                progressEvent.TotalBytes = total;
            }
            return progressEvent;
        }

        var successMatch = SuccessRegex().Match(line);
        if (successMatch.Success)
        {
            progressEvent.State = "Completed";
            progressEvent.ProgressPercent = 100.0;
            return progressEvent;
        }

        var errorMatch = ErrorRegex().Match(line);
        if (errorMatch.Success)
        {
            progressEvent.State = $"Error: {errorMatch.Groups[2].Value.Trim()}";
            return progressEvent;
        }

        if (line.Contains("Logging in", StringComparison.OrdinalIgnoreCase))
        {
            progressEvent.State = "LoggingIn";
        }
        else if (line.Contains("Logged in OK", StringComparison.OrdinalIgnoreCase))
        {
            progressEvent.State = "LoggedIn";
        }
        else if (line.Contains("Waiting for user info", StringComparison.OrdinalIgnoreCase))
        {
            progressEvent.State = "WaitingForUserInfo";
        }
        else if (line.Contains("Checking for updates", StringComparison.OrdinalIgnoreCase))
        {
            progressEvent.State = "CheckingForUpdates";
        }

        return progressEvent;
    }
}
