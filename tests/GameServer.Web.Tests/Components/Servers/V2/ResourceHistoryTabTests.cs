using Bunit;
using GameServer.API.Client.Interfaces;
using GameServer.Web.Components.Server;
using GameServer.Web.Configurations;
using GameServer.Web.Models.V2;
using GameServer.Web.Services;
using GameServer.Web.Services.V2;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Radzen;
using Radzen.Blazor.Rendering;
using Xunit;

namespace GameServer.Web.Tests.Components.Servers.V2;

public class ResourceHistoryTabTests : BunitContext
{
    private readonly Mock<IGameServerV2ApiService> _apiMock = new();

    public ResourceHistoryTabTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<Rect>(inv => inv.Identifier.Contains("createChart"))
            .SetResult(new Rect { Width = 600, Height = 300 });

        Services.AddSingleton<NotificationService>();
        Services.AddSingleton<TooltipService>();
        Services.AddScoped<IUserTimeZoneService, UserTimeZoneService>();
        Services.AddSingleton<ILogger<ResourceHistoryTab>>(NullLogger<ResourceHistoryTab>.Instance);
        Services.AddSingleton(Options.Create(new GameServerDockerApi { BaseUri = "http://localhost:5164" }));
        _apiMock.Setup(a => a.GetResourceHistoryAsync(It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameServerCalculatedResourceHistory
            {
                ServerId = "srv-1",
                Points =
                [
                    new GameServerCalculatedResourceHistoryPoint
                    {
                        Timestamp = DateTime.UtcNow,
                        CpuUsagePercent = 25.5,
                        MemoryUsageBytes = 1024 * 1024 * 512,
                        MemoryLimitBytes = 1024 * 1024 * 1024,
                        MemoryUsagePercent = 50.0,
                        NetworkRxKBps = 50,
                        NetworkTxKBps = 25,
                        BlockReadKBps = 10,
                        BlockWriteKBps = 5,
                        NetworkRxTotalBytes = 1024 * 50,
                        NetworkTxTotalBytes = 1024 * 25,
                        BlockReadTotalBytes = 1024 * 10,
                        BlockWriteTotalBytes = 1024 * 5
                    }
                ]
            });
        Services.AddSingleton<IGameServerV2ApiService>(_apiMock.Object);
    }

    [Fact]
    public void ResourceHistoryTab_RendersHeaderAndTitle()
    {
        var cut = Render<ResourceHistoryTab>(parameters => parameters
            .Add(p => p.ServerId, "srv-1")
            .Add(p => p.AutoConnect, false));

        Assert.Contains("Resource Usage History", cut.Markup);
        Assert.Contains("Time Range:", cut.Markup);
        Assert.DoesNotContain("Live Resource Usage", cut.Markup);
    }

    [Fact]
    public void ResourceHistoryTab_RendersChartsAndHistoricalRecords()
    {
        var cut = Render<ResourceHistoryTab>(parameters => parameters
            .Add(p => p.ServerId, "srv-1")
            .Add(p => p.AutoConnect, false));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("CPU Usage", cut.Markup);
            Assert.Contains("Memory Usage", cut.Markup);
            Assert.Contains("Network Throughput", cut.Markup);
            Assert.Contains("Disk Throughput", cut.Markup);
            Assert.Contains("Network Aggregate", cut.Markup);
            Assert.Contains("Disk Aggregate", cut.Markup);
            Assert.Contains("512 MB", cut.Markup);
            Assert.Contains("1 points", cut.Markup);
        });
    }

    [Fact]
    public void ResourceHistoryTab_WhenLiveUpdateReceived_ShouldAppendToHistoryWithoutReloadingApi()
    {
        var mockClient = new Mock<IResourceMonitoringClient>();
        var cut = Render<ResourceHistoryTab>(parameters => parameters
            .Add(p => p.ServerId, "srv-1")
            .Add(p => p.AutoConnect, true)
            .Add(p => p.Client, mockClient.Object));

        var update = new ServerResourceUsage
        {
            ServerId = "srv-1",
            Timestamp = DateTime.UtcNow.AddSeconds(5),
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
            Assert.Contains("768 MB", cut.Markup);
            Assert.Contains("2 points", cut.Markup);
            Assert.Contains("Rx 50.0 / Tx 25.0 KB/s", cut.Markup);
        });
    }

    [Fact]
    public void ResourceHistoryTab_WhenCalculatedValuesAreMissing_ShouldPreserveUnavailableState()
    {
        _apiMock.Setup(a => a.GetResourceHistoryAsync("srv-missing", It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameServerCalculatedResourceHistory
            {
                ServerId = "srv-missing",
                Points =
                [
                    new GameServerCalculatedResourceHistoryPoint
                    {
                        Timestamp = DateTime.UtcNow,
                        CpuUsagePercent = null,
                        MemoryUsageBytes = null,
                        MemoryLimitBytes = null,
                        MemoryUsagePercent = null,
                        NetworkRxKBps = null,
                        NetworkTxKBps = null,
                        BlockReadKBps = null,
                        BlockWriteKBps = null,
                        NetworkRxTotalBytes = null,
                        NetworkTxTotalBytes = null,
                        BlockReadTotalBytes = null,
                        BlockWriteTotalBytes = null
                    }
                ]
            });

        var cut = Render<ResourceHistoryTab>(parameters => parameters
            .Add(p => p.ServerId, "srv-missing")
            .Add(p => p.AutoConnect, false));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("n/a", cut.Markup);
            Assert.DoesNotContain("0.0% CPU", cut.Markup);
            Assert.DoesNotContain("0.0 MB", cut.Markup);
        });
    }

    [Fact]
    public void ResourceHistoryTab_WhenCpuExceeds100Percent_ScalesAxisAccordingly()
    {
        _apiMock.Setup(a => a.GetResourceHistoryAsync("srv-multicore", It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameServerCalculatedResourceHistory
            {
                ServerId = "srv-multicore",
                Points =
                [
                    new GameServerCalculatedResourceHistoryPoint
                    {
                        Timestamp = DateTime.UtcNow,
                        CpuUsagePercent = 250.0,
                        MemoryUsageBytes = 1024 * 1024 * 512,
                        MemoryLimitBytes = 1024 * 1024 * 1024,
                        MemoryUsagePercent = 50.0
                    }
                ]
            });

        var cut = Render<ResourceHistoryTab>(parameters => parameters
            .Add(p => p.ServerId, "srv-multicore")
            .Add(p => p.AutoConnect, false));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("300%", cut.Markup);
            Assert.Contains("250.0%", cut.Markup);
        });
    }
}
