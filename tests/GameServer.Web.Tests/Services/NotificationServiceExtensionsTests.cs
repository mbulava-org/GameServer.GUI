using Radzen;

namespace GameServer.Web.Tests.Services;

public class NotificationServiceExtensionsTests
{
    [Fact]
    public void NotifyTimed_WithParameters_ShouldEnqueueMessageWithDuration()
    {
        // Arrange
        var service = new NotificationService();

        // Act
        service.NotifyTimed(NotificationSeverity.Success, "Title Test", "Detail Test", 3000);

        // Assert
        var captured = service.Messages.LastOrDefault();
        Assert.NotNull(captured);
        Assert.Equal(NotificationSeverity.Success, captured.Severity);
        Assert.Equal("Title Test", captured.Summary);
        Assert.Equal("Detail Test", captured.Detail);
        Assert.Equal(3000, captured.Duration);
        Assert.False(captured.CloseOnClick);
    }

    [Fact]
    public void NotifyTimed_WithDefaultDuration_ShouldApply5000Ms()
    {
        // Arrange
        var service = new NotificationService();

        // Act
        service.NotifyTimed(NotificationSeverity.Error, "Error Occurred");

        // Assert
        var captured = service.Messages.LastOrDefault();
        Assert.NotNull(captured);
        Assert.Equal(NotificationSeverity.Error, captured.Severity);
        Assert.Equal("Error Occurred", captured.Summary);
        Assert.Null(captured.Detail);
        Assert.Equal(5000, captured.Duration);
    }

    [Fact]
    public void NotifyTimed_WithMessageObject_ShouldPreserveOrApplyDefault()
    {
        // Arrange
        var service = new NotificationService();

        var messageWithoutDuration = new NotificationMessage
        {
            Severity = NotificationSeverity.Warning,
            Summary = "Warning Summary",
            Duration = null
        };

        // Act
        service.NotifyTimed(messageWithoutDuration);

        // Assert
        var captured = service.Messages.LastOrDefault();
        Assert.NotNull(captured);
        Assert.Equal(5000, captured.Duration);

        // With explicit duration override
        var messageWithDuration = new NotificationMessage
        {
            Severity = NotificationSeverity.Info,
            Summary = "Info Summary",
            Duration = 2000
        };

        service.NotifyTimed(messageWithDuration);
        var captured2 = service.Messages.LastOrDefault();
        Assert.NotNull(captured2);
        Assert.Equal(2000, captured2.Duration);
    }

    [Fact]
    public void NotifyTimed_WhenNullService_ShouldThrowArgumentNullException()
    {
        NotificationService nullService = null!;
        Assert.Throws<ArgumentNullException>(() => nullService.NotifyTimed(NotificationSeverity.Info, "Summary"));
        Assert.Throws<ArgumentNullException>(() => nullService.NotifyTimed(new NotificationMessage()));
    }

    [Fact]
    public void NotifyTimed_WhenNullMessage_ShouldThrowArgumentNullException()
    {
        var service = new NotificationService();
        Assert.Throws<ArgumentNullException>(() => service.NotifyTimed(null!));
    }
}
