using GameServer.Windows.Agent.Services;

namespace GameServer.Windows.Agent.Tests;

public class SteamCmdOutputParserTests
{
    [Fact]
    public void ParseLine_DownloadingProgress_ParsesPercentageAndBytes()
    {
        // Arrange
        var line = "Update state (0x5) downloading, progress: 45.20 (123456789 / 273123456)";
        var jobId = "test-job-1";
        var appId = 2394010u;

        // Act
        var result = SteamCmdOutputParser.ParseLine(line, jobId, appId);

        // Assert
        Assert.Equal("downloading", result.State, ignoreCase: true);
        Assert.Equal(45.20, result.ProgressPercent);
        Assert.Equal(123456789L, result.BytesDownloaded);
        Assert.Equal(273123456L, result.TotalBytes);
        Assert.Equal(jobId, result.JobId);
        Assert.Equal(appId, result.AppId);
    }

    [Fact]
    public void ParseLine_ValidatingProgress_ParsesCorrectly()
    {
        // Arrange
        var line = "Update state (0x7) validating, progress: 80.50 (805000 / 1000000)";

        // Act
        var result = SteamCmdOutputParser.ParseLine(line, "job-1", 896660u);

        // Assert
        Assert.Equal("validating", result.State, ignoreCase: true);
        Assert.Equal(80.50, result.ProgressPercent);
        Assert.Equal(805000L, result.BytesDownloaded);
        Assert.Equal(1000000L, result.TotalBytes);
    }

    [Fact]
    public void ParseLine_SuccessMessage_SetsCompletedState()
    {
        // Arrange
        var line = "Success! App '2394010' fully installed.";

        // Act
        var result = SteamCmdOutputParser.ParseLine(line, "job-1", 2394010u);

        // Assert
        Assert.Equal("Completed", result.State);
        Assert.Equal(100.0, result.ProgressPercent);
    }

    [Fact]
    public void ParseLine_AlreadyUpToDate_SetsCompletedState()
    {
        // Arrange
        var line = "Success! App '896660' already up to date.";

        // Act
        var result = SteamCmdOutputParser.ParseLine(line, "job-1", 896660u);

        // Assert
        Assert.Equal("Completed", result.State);
        Assert.Equal(100.0, result.ProgressPercent);
    }

    [Fact]
    public void ParseLine_ErrorMessage_ParsesReason()
    {
        // Arrange
        var line = "ERROR! Failed to install app '2394010' (No subscription)";

        // Act
        var result = SteamCmdOutputParser.ParseLine(line, "job-1", 2394010u);

        // Assert
        Assert.Contains("No subscription", result.State);
    }

    [Fact]
    public void ParseLine_LoggingIn_SetsState()
    {
        // Arrange
        var line = "Connecting anonymously to Steam Public... Logged in OK";

        // Act
        var result = SteamCmdOutputParser.ParseLine(line, "job-1", 2394010u);

        // Assert
        Assert.Equal("LoggedIn", result.State);
    }
}
