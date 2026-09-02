using GameServer.Windows.Agent.Configurations;

namespace GameServer.Windows.Agent.Tests.Configurations;

public class WindowsAgentOptionsTests
{
    [Fact]
    public void WindowsAgentOptions_HasSensibleDefaults()
    {
        // Act
        var options = new WindowsAgentOptions();

        // Assert
        Assert.Equal("5180", options.AgentPort);
        Assert.NotNull(options.SteamCmd);
        Assert.Equal("steamcmd.exe", options.SteamCmd.ExecutableName);
        Assert.True(options.SteamCmd.AutoDownloadIfMissing);
        Assert.Equal(30, options.SteamCmd.DefaultTimeoutMinutes);

        Assert.NotNull(options.Storage);
        Assert.NotEmpty(options.Storage.BaseInstancesDirectory);
        Assert.NotEmpty(options.Storage.BackupsDirectory);

        Assert.NotNull(options.ProcessSupervision);
        Assert.Equal(30, options.ProcessSupervision.GracefulStopTimeoutSeconds);
        Assert.Equal(2000, options.ProcessSupervision.LogBufferSizeLines);
        Assert.True(options.ProcessSupervision.EnableCrashRestart);

        Assert.NotNull(options.AgentRegistration);
        Assert.True(options.AgentRegistration.Enabled);
        Assert.Equal(30, options.AgentRegistration.HeartbeatIntervalSeconds);
    }
}
