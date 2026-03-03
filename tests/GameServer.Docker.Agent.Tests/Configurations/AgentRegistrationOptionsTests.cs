using GameServer.Docker.Agent.Configurations;

namespace GameServer.Docker.Agent.Tests.Configurations;

public class AgentRegistrationOptionsTests
{
    [Fact]
    public void AgentRegistrationOptions_ShouldHaveDefaultValues()
    {
        // Act
        var options = new AgentRegistrationOptions();

        // Assert
        Assert.Equal(string.Empty, options.PrimaryServiceUrl);
        Assert.Equal(30, options.HeartbeatIntervalSeconds);
        Assert.True(options.Enabled);
        Assert.Equal(30, options.ConnectionTimeoutSeconds);
    }

    [Fact]
    public void AgentRegistrationOptions_ShouldHaveDefaultCapabilities()
    {
        // Act
        var options = new AgentRegistrationOptions();

        // Assert
        Assert.NotNull(options.Capabilities);
        Assert.Contains("logs", options.Capabilities);
        Assert.Contains("exec", options.Capabilities);
        Assert.Contains("stats", options.Capabilities);
        Assert.Contains("attach", options.Capabilities);
        Assert.Contains("services", options.Capabilities);
    }

    [Fact]
    public void AgentRegistrationOptions_ShouldHaveDefaultReconnectDelays()
    {
        // Act
        var options = new AgentRegistrationOptions();

        // Assert
        Assert.NotNull(options.ReconnectDelaySeconds);
        Assert.Equal(4, options.ReconnectDelaySeconds.Count);
        Assert.Equal(0, options.ReconnectDelaySeconds[0]);
        Assert.Equal(2, options.ReconnectDelaySeconds[1]);
        Assert.Equal(10, options.ReconnectDelaySeconds[2]);
        Assert.Equal(30, options.ReconnectDelaySeconds[3]);
    }

    [Fact]
    public void AgentRegistrationOptions_ShouldAllowCustomPrimaryServiceUrl()
    {
        // Arrange
        var options = new AgentRegistrationOptions
        {
            PrimaryServiceUrl = "http://custom-service:9000"
        };

        // Act & Assert
        Assert.Equal("http://custom-service:9000", options.PrimaryServiceUrl);
    }

    [Fact]
    public void AgentRegistrationOptions_ShouldAllowCustomHeartbeatInterval()
    {
        // Arrange
        var options = new AgentRegistrationOptions
        {
            HeartbeatIntervalSeconds = 60
        };

        // Act & Assert
        Assert.Equal(60, options.HeartbeatIntervalSeconds);
    }

    [Fact]
    public void AgentRegistrationOptions_ShouldAllowDisabling()
    {
        // Arrange
        var options = new AgentRegistrationOptions
        {
            Enabled = false
        };

        // Act & Assert
        Assert.False(options.Enabled);
    }

    [Fact]
    public void AgentRegistrationOptions_ShouldAllowCustomCapabilities()
    {
        // Arrange
        var customCapabilities = new List<string> { "custom1", "custom2" };
        var options = new AgentRegistrationOptions
        {
            Capabilities = customCapabilities
        };

        // Act & Assert
        Assert.Equal(customCapabilities, options.Capabilities);
        Assert.Equal(2, options.Capabilities.Count);
    }

    [Fact]
    public void AgentRegistrationOptions_ShouldAllowCustomConnectionTimeout()
    {
        // Arrange
        var options = new AgentRegistrationOptions
        {
            ConnectionTimeoutSeconds = 60
        };

        // Act & Assert
        Assert.Equal(60, options.ConnectionTimeoutSeconds);
    }

    [Fact]
    public void AgentRegistrationOptions_ShouldAllowCustomReconnectDelays()
    {
        // Arrange
        var customDelays = new List<int> { 1, 5, 15, 45 };
        var options = new AgentRegistrationOptions
        {
            ReconnectDelaySeconds = customDelays
        };

        // Act & Assert
        Assert.Equal(customDelays, options.ReconnectDelaySeconds);
        Assert.Equal(4, options.ReconnectDelaySeconds.Count);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(120)]
    public void AgentRegistrationOptions_ShouldAcceptVariousHeartbeatIntervals(int interval)
    {
        // Arrange
        var options = new AgentRegistrationOptions
        {
            HeartbeatIntervalSeconds = interval
        };

        // Act & Assert
        Assert.Equal(interval, options.HeartbeatIntervalSeconds);
    }
}
