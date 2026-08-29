using Bunit;
using GameServer.Web.Components.Server;
using GameServer.Web.Configurations;
using GameServer.Web.Models.V2;
using GameServer.Web.Services.V2;
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
    private readonly Mock<IGameServerV2ApiService> _apiMock = new();

    public ResourceMonitorTabTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddSingleton<NotificationService>();
        Services.AddSingleton<ILogger<ResourceMonitorTab>>(NullLogger<ResourceMonitorTab>.Instance);
        Services.AddSingleton(Options.Create(new GameServerDockerApi { BaseUri = "http://localhost:5164" }));
        _apiMock.Setup(a => a.GetResourceHistoryAsync(It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GameServerResourceHistoryItem>
            {
                new()
                {
                    Id = 1,
                    ServerId = "srv-1",
                    Timestamp = DateTime.UtcNow,
                    CpuUsagePercent = 25.5,
                    MemoryUsageBytes = 1024 * 1024 * 512,
                    MemoryLimitBytes = 1024 * 1024 * 1024,
                    MemoryUsagePercent = 50.0,
                    NetworkRxBytes = 1024 * 50,
                    NetworkTxBytes = 1024 * 25,
                    BlockReadBytes = 1024 * 10,
                    BlockWriteBytes = 1024 * 5,
                    RunningReplicas = 1,
                    DesiredReplicas = 1
                }
            });
        Services.AddSingleton<IGameServerV2ApiService>(_apiMock.Object);
    }

    [Fact]
    public void ResourceMonitorTab_RendersHeaderAndTitle()
    {
        var cut = Render<ResourceMonitorTab>(parameters => parameters
            .Add(p => p.ServerId, "srv-1")
            .Add(p => p.AutoConnect, false));

        Assert.Contains("Live Resource Usage", cut.Markup);
        Assert.Contains("Resource History", cut.Markup);
        Assert.DoesNotContain("Live Metrics Feed", cut.Markup);
        Assert.DoesNotContain("Container & Swarm Node", cut.Markup);
    }

    [Fact]
    public void ResourceMonitorTab_RendersHistoricalRecords()
    {
        var cut = Render<ResourceMonitorTab>(parameters => parameters
            .Add(p => p.ServerId, "srv-1")
            .Add(p => p.AutoConnect, false));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("25.5%", cut.Markup);
            Assert.Contains("512 MB", cut.Markup);
        });
    }
}
