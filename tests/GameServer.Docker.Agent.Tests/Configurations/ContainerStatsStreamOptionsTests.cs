using GameServer.Docker.Agent.Configurations;

namespace GameServer.Docker.Agent.Tests.Configurations;

public class ContainerStatsStreamOptionsTests
{
    [Fact]
    public void ContainerStatsStreamOptions_ShouldHaveDefaultMaxStreamDuration()
    {
        // Act
        var options = new ContainerStatsStreamOptions();

        // Assert
        Assert.Equal(10, options.MaxStreamDurationSeconds);
    }

    [Fact]
    public void ContainerStatsStreamOptions_ShouldAllowCustomMaxStreamDuration()
    {
        // Arrange
        var options = new ContainerStatsStreamOptions
        {
            MaxStreamDurationSeconds = 60
        };

        // Act & Assert
        Assert.Equal(60, options.MaxStreamDurationSeconds);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(120)]
    public void ContainerStatsStreamOptions_ShouldAcceptVariousDurations(int duration)
    {
        // Arrange
        var options = new ContainerStatsStreamOptions
        {
            MaxStreamDurationSeconds = duration
        };

        // Act & Assert
        Assert.Equal(duration, options.MaxStreamDurationSeconds);
    }
}
