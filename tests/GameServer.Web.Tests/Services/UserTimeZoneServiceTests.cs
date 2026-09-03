using System.Globalization;
using GameServer.Web.Services;
using Microsoft.JSInterop;
using Moq;

namespace GameServer.Web.Tests.Services;

public sealed class UserTimeZoneServiceTests
{
    private readonly Mock<IJSRuntime> jsRuntime = new();

    [Fact]
    public async Task InitializeAsync_WhenJsReturnsValidTimeZoneAndLocale_ShouldSetUserTimeZoneAndCulture()
    {
        // Arrange
        jsRuntime
            .Setup(j => j.InvokeAsync<string>("timeZoneHelper.getUserTimeZone", It.IsAny<CancellationToken>(), It.IsAny<object?[]>()))
            .ReturnsAsync("America/Chicago");
        jsRuntime
            .Setup(j => j.InvokeAsync<string>("timeZoneHelper.getUserLocale", It.IsAny<CancellationToken>(), It.IsAny<object?[]>()))
            .ReturnsAsync("en-US");

        var service = new UserTimeZoneService(jsRuntime.Object);

        // Act
        var result = await service.InitializeAsync();

        // Assert
        Assert.True(result);
        Assert.True(service.IsInitialized);
        Assert.NotNull(service.UserTimeZone);
        Assert.Equal("America/Chicago", service.UserTimeZone.Id);
        Assert.NotNull(service.UserCulture);
        Assert.Equal("en-US", service.UserCulture.Name);
    }

    [Fact]
    public async Task InitializeAsync_WhenCalledMultipleTimes_ShouldOnlyInitializeOnce()
    {
        // Arrange
        jsRuntime
            .Setup(j => j.InvokeAsync<string>("timeZoneHelper.getUserTimeZone", It.IsAny<CancellationToken>(), It.IsAny<object?[]>()))
            .ReturnsAsync("UTC");
        jsRuntime
            .Setup(j => j.InvokeAsync<string>("timeZoneHelper.getUserLocale", It.IsAny<CancellationToken>(), It.IsAny<object?[]>()))
            .ReturnsAsync("en-US");

        var service = new UserTimeZoneService(jsRuntime.Object);

        // Act
        var first = await service.InitializeAsync();
        var second = await service.InitializeAsync();

        // Assert
        Assert.True(first);
        Assert.False(second);
        jsRuntime.Verify(j => j.InvokeAsync<string>("timeZoneHelper.getUserTimeZone", It.IsAny<CancellationToken>(), It.IsAny<object?[]>()), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_WhenJsThrows_ShouldHandleGracefully()
    {
        // Arrange
        jsRuntime
            .Setup(j => j.InvokeAsync<string>("timeZoneHelper.getUserTimeZone", It.IsAny<CancellationToken>(), It.IsAny<object?[]>()))
            .ThrowsAsync(new JSException("JS interop not available"));

        var service = new UserTimeZoneService(jsRuntime.Object);

        // Act
        var result = await service.InitializeAsync();

        // Assert
        Assert.False(result);
        Assert.True(service.IsInitialized);
        Assert.Null(service.UserTimeZone);
    }

    [Fact]
    public async Task ConvertToUserLocalTime_WhenTimeZoneSet_ShouldConvertFromUtc()
    {
        // Arrange
        jsRuntime
            .Setup(j => j.InvokeAsync<string>("timeZoneHelper.getUserTimeZone", It.IsAny<CancellationToken>(), It.IsAny<object?[]>()))
            .ReturnsAsync("America/New_York");

        var service = new UserTimeZoneService(jsRuntime.Object);
        await service.InitializeAsync();

        // 2026-06-15 16:00:00 UTC -> EDT (UTC-4) -> 2026-06-15 12:00:00
        var utcDate = new DateTime(2026, 6, 15, 16, 0, 0, DateTimeKind.Utc);

        // Act
        var localDate = service.ConvertToUserLocalTime(utcDate);

        // Assert
        Assert.Equal(12, localDate.Hour);
        Assert.Equal(15, localDate.Day);
        Assert.Equal(6, localDate.Month);
    }

    [Fact]
    public async Task FormatLocalDateTime_WhenCultureSet_ShouldFormatAccordingToUserLocale()
    {
        // Arrange
        jsRuntime
            .Setup(j => j.InvokeAsync<string>("timeZoneHelper.getUserTimeZone", It.IsAny<CancellationToken>(), It.IsAny<object?[]>()))
            .ReturnsAsync("UTC");
        jsRuntime
            .Setup(j => j.InvokeAsync<string>("timeZoneHelper.getUserLocale", It.IsAny<CancellationToken>(), It.IsAny<object?[]>()))
            .ReturnsAsync("en-US");

        var service = new UserTimeZoneService(jsRuntime.Object);
        await service.InitializeAsync();

        var utcDate = new DateTime(2026, 9, 2, 14, 30, 0, DateTimeKind.Utc);

        // Act
        var formatted = service.FormatLocalDateTime(utcDate);

        // Assert
        Assert.Contains("9/2/2026", formatted);
        Assert.Contains("2:30", formatted);
        Assert.Contains("PM", formatted);
    }

    [Fact]
    public void FormatLocalDateTime_WhenDefaultDateTime_ShouldReturnEmptyString()
    {
        // Arrange
        var service = new UserTimeZoneService(jsRuntime.Object);

        // Act
        var formatted = service.FormatLocalDateTime(default);

        // Assert
        Assert.Equal(string.Empty, formatted);
    }
}
