using Bunit;
using GameServer.API.Client.Interfaces;
using GameServer.Web.Components.Server;
using GameServer.Web.Configurations;
using GameServer.Web.Models.V2;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Radzen;
using Xunit;

namespace GameServer.Web.Tests.Components.Servers.V2;

public class LiveResourceMonitorTabTests : BunitContext
{
    public LiveResourceMonitorTabTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<NotificationService>();
        Services.AddSingleton<TooltipService>();
        Services.AddSingleton<ILogger<LiveResourceMonitorTab>>(NullLogger<LiveResourceMonitorTab>.Instance);
        Services.AddSingleton(Options.Create(new GameServerDockerApi { BaseUri = "http://localhost:5164" }));
    }

    [Fact]
    public void LiveResourceMonitorTab_RendersHeaderAndTitle()
    {
        var cut = Render<LiveResourceMonitorTab>(parameters => parameters
            .Add(p => p.ServerId, "srv-1")
            .Add(p => p.AutoConnect, false));

        Assert.Contains("Live Resource Usage", cut.Markup);
        Assert.Contains("Disconnected", cut.Markup);
        Assert.DoesNotContain("Resource Usage History", cut.Markup);
    }

    [Fact]
    public void LiveResourceMonitorTab_WhenLiveUpdateReceived_RendersMetrics()
    {
        var mockClient = new Mock<IResourceMonitoringClient>();
        var cut = Render<LiveResourceMonitorTab>(parameters => parameters
            .Add(p => p.ServerId, "srv-1")
            .Add(p => p.AutoConnect, true)
            .Add(p => p.Client, mockClient.Object));

        var update = new ServerResourceUsage
        {
            ServerId = "srv-1",
            Timestamp = DateTime.UtcNow,
            CpuUsagePercent = 42.5,
            MemoryUsageBytes = 1024 * 1024 * 512,
            MemoryLimitBytes = 1024 * 1024 * 1024,
            MemoryUsagePercent = 50.0,
            NetworkRxBytes = 1024 * 100,
            NetworkTxBytes = 1024 * 50,
            BlockReadBytes = 1024 * 20,
            BlockWriteBytes = 1024 * 10,
            Replicas = 1,
            HealthyReplicas = 1
        };

        mockClient.Raise(m => m.ResourceUpdateReceived += null, mockClient.Object, update);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("42.5", cut.Markup);
            Assert.Contains("512 MB", cut.Markup);
            Assert.Contains("1 GB", cut.Markup);
            Assert.Contains("100 KB", cut.Markup);
            Assert.Contains("50 KB", cut.Markup);
        });
    }
}
