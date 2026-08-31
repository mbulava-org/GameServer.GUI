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

public class ResourceMonitorTabTests : BunitContext
{
    public ResourceMonitorTabTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<NotificationService>();
        Services.AddSingleton<TooltipService>();
        Services.AddSingleton<ILogger<LiveResourceMonitorTab>>(NullLogger<LiveResourceMonitorTab>.Instance);
        Services.AddSingleton(Options.Create(new GameServerDockerApi { BaseUri = "http://localhost:5164" }));
    }

    [Fact]
    public void ResourceMonitorTab_RendersHeaderAndTitle()
    {
        var cut = Render<ResourceMonitorTab>(parameters => parameters
            .Add(p => p.ServerId, "srv-1")
            .Add(p => p.AutoConnect, false));

        Assert.Contains("Live Resource Usage", cut.Markup);
        Assert.Contains("Disconnected", cut.Markup);
    }

    [Fact]
    public void ResourceMonitorTab_WhenLiveUpdateReceived_RendersMetrics()
    {
        var mockClient = new Mock<IResourceMonitoringClient>();
        var cut = Render<ResourceMonitorTab>(parameters => parameters
            .Add(p => p.ServerId, "srv-1")
            .Add(p => p.AutoConnect, true)
            .Add(p => p.Client, mockClient.Object));

        var update = new ServerResourceUsage
        {
            ServerId = "srv-1",
            Timestamp = DateTime.UtcNow,
            CpuUsagePercent = 88.0,
            MemoryUsageBytes = 1024 * 1024 * 768,
            MemoryLimitBytes = 1024 * 1024 * 1024,
            MemoryUsagePercent = 75.0,
            NetworkRxBytes = 1024 * 100,
            NetworkTxBytes = 1024 * 80,
            BlockReadBytes = 1024 * 40,
            BlockWriteBytes = 1024 * 20,
            Replicas = 1,
            HealthyReplicas = 1
        };

        mockClient.Raise(m => m.ResourceUpdateReceived += null, mockClient.Object, update);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("88.0", cut.Markup);
            Assert.Contains("768 MB", cut.Markup);
        });
    }
}
