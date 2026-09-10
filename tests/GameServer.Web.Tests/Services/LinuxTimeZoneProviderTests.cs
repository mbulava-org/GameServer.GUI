using GameServer.Web.Services;

namespace GameServer.Web.Tests.Services;

public sealed class LinuxTimeZoneProviderTests
{
    [Fact]
    public void AllTimeZones_ShouldContainStandardLinuxTimeZones()
    {
        var timeZones = LinuxTimeZoneProvider.AllTimeZones;

        Assert.NotNull(timeZones);
        Assert.NotEmpty(timeZones);
        Assert.Contains("UTC", timeZones);
        Assert.Contains("America/New_York", timeZones);
        Assert.Contains("America/Chicago", timeZones);
        Assert.Contains("America/Los_Angeles", timeZones);
        Assert.Contains("Europe/London", timeZones);
        Assert.Contains("Europe/Paris", timeZones);
        Assert.Contains("Asia/Tokyo", timeZones);
        Assert.Contains("Australia/Sydney", timeZones);
    }

    [Fact]
    public void AllTimeZones_ShouldBeSortedAlphabetically()
    {
        var timeZones = LinuxTimeZoneProvider.AllTimeZones;

        var sorted = timeZones.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        Assert.Equal(sorted, timeZones);
    }

    [Fact]
    public void AllTimeZones_ShouldHaveNoDuplicates()
    {
        var timeZones = LinuxTimeZoneProvider.AllTimeZones;

        var uniqueCount = timeZones.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        Assert.Equal(timeZones.Count, uniqueCount);
    }
}
