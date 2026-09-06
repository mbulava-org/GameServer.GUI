using System.Net.WebSockets;
using System.Text;
using GameServer.API.Dtos.V2;
using GameServer.API.Interfaces;
using GameServer.API.Models;
using GameServer.API.Services;
using GameServer.API.Services.V2;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GameServer.API.Tests.Services.V2;

public class MonitoringAndAggregatorsFunctionalTests
{
    [Fact]
    public async Task ContainerAttachAggregator_MultiSubscriberAndInputControl_FunctionalTest()
    {
        // Setup Kestrel WebSocket server simulating Node Agent /containers/{id}/attach
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        app.UseWebSockets();

        app.Map("/containers/{id}/attach/ws", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            using var ws = await context.WebSockets.AcceptWebSocketAsync();
            var sendBuffer = Encoding.UTF8.GetBytes("Server started and ready.\n");
            await ws.SendAsync(sendBuffer, WebSocketMessageType.Text, true, CancellationToken.None);

            var recvBuffer = new byte[1024];
            try
            {
                while (ws.State == WebSocketState.Open)
                {
                    var result = await ws.ReceiveAsync(recvBuffer, CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                        break;
                    }

                    var input = Encoding.UTF8.GetString(recvBuffer, 0, result.Count);
                    var echo = Encoding.UTF8.GetBytes($"Echo: {input}");
                    await ws.SendAsync(echo, WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
            catch (Exception)
            {
                // Graceful exit on disconnect
            }
        });

        await app.StartAsync();
        var serverUrl = app.Urls.First();

        try
        {
            var discoveryMock = new Mock<INodeAgentDiscovery>();
            discoveryMock
                .Setup(d => d.GetAgentForContainerAsync("c-functional"))
                .ReturnsAsync(new NodeAgentEndpoint { InternalUrl = serverUrl, NodeId = "node-1" });
            discoveryMock
                .Setup(d => d.GetContainerLogsAsync("c-functional", It.IsAny<int>()))
                .ReturnsAsync(new List<string> { "History log 1" });

            var services = new ServiceCollection();
            services.AddSingleton(discoveryMock.Object);
            var sp = services.BuildServiceProvider();

            var aggregator = new ContainerAttachAggregator(sp, NullLogger<ContainerAttachAggregator>.Instance);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            // Subscriber 1 connects
            var sub1Frames = new List<AttachStreamFrame>();
            var sub1Task = Task.Run(async () =>
            {
                await foreach (var frame in aggregator.SubscribeAsync("conn-user-1", "c-functional", cts.Token))
                {
                    sub1Frames.Add(frame);
                    if (sub1Frames.Any(f => f.Payload.Contains("Echo: test-input\n")))
                    {
                        break;
                    }
                }
            });

            // Wait for sub1 to receive the initial message ensuring backend WebSocket is connected
            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool inputSuccess = false;
            while (sw.ElapsedMilliseconds < 3000 && !inputSuccess)
            {
                if (sub1Frames.Any(f => f.Payload.Contains("Server started")))
                {
                    inputSuccess = await aggregator.SendInputAsync("conn-user-1", "c-functional", "test-input\n", cts.Token);
                    if (inputSuccess)
                    {
                        break;
                    }
                }
                await Task.Delay(50);
            }

            Assert.True(inputSuccess);

            // Subscriber 2 attempts input -> rejected since sub1 has control
            var sub2Input = await aggregator.SendInputAsync("conn-user-2", "c-functional", "unauthorized-input\n", cts.Token);
            Assert.False(sub2Input);

            await sub1Task;

            Assert.Contains(sub1Frames, f => f.Payload.Contains("History log 1") || f.Payload.Contains("Server started"));

            await aggregator.UnsubscribeAsync("conn-user-1", "c-functional");
            await aggregator.DisposeAsync();
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task ServerLogAggregator_MultipleSubscribers_StreamsLogs()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddSignalR();
        var app = builder.Build();
        app.MapGet("/containers", () => Results.Json(new[] { new { Id = "cnt-agg-1" } }));
        app.MapHub<MockNodeAgentHub>("/hubs/nodeagent");
        await app.StartAsync();
        var serverUrl = app.Urls.First();

        try
        {
            var discoveryMock = new Mock<INodeAgentDiscovery>();
            discoveryMock
                .Setup(d => d.GetAgentForServerAsync("srv-agg-1"))
                .ReturnsAsync(new NodeAgentEndpoint { InternalUrl = serverUrl, NodeId = "node-1" });

            var queryServiceMock = new Mock<IGameServerQueryService>();
            queryServiceMock
                .Setup(q => q.GetByServerIdAsync("srv-agg-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GameServerDetailDto
                {
                    ServerId = "srv-agg-1",
                    ServiceName = "gameserver-srv-agg-1"
                });

            var services = new ServiceCollection();
            services.AddSingleton(discoveryMock.Object);
            services.AddSingleton(queryServiceMock.Object);
            services.AddSingleton(new NodeAgentClient(NullLogger<NodeAgentClient>.Instance));
            var sp = services.BuildServiceProvider();

            var aggregator = new ServerLogAggregator(sp, NullLogger<ServerLogAggregator>.Instance);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var receivedLogs = new List<string>();

            await foreach (var log in aggregator.StreamLogsAsync("srv-agg-1", follow: true, tailLines: 10, cancellationToken: cts.Token))
            {
                receivedLogs.Add(log);
                if (receivedLogs.Count >= 2)
                {
                    break;
                }
            }

            Assert.Equal(2, receivedLogs.Count);
            Assert.Contains("log line 1 for cnt-agg-1", receivedLogs);

            await aggregator.DisposeAsync();
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task ServerResourceAggregator_MultipleSubscribers_StreamsAggregatedMetrics()
    {
        var services = new ServiceCollection();
        var monitorMock = new Mock<IServerResourceMonitor>();
        monitorMock
            .Setup(m => m.StreamResourceUsageAsync("srv-res-1", It.IsAny<CancellationToken>()))
            .Returns(GetUsageAsync());

        services.AddScoped(_ => monitorMock.Object);
        var sp = services.BuildServiceProvider();

        var aggregator = new ServerResourceAggregator(sp, NullLogger<ServerResourceAggregator>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var emittedUsage = new List<GameServer.API.Models.ServerResourceUsage>();

        await foreach (var usage in aggregator.StreamResourceUsageAsync("srv-res-1", intervalSeconds: 1, cancellationToken: cts.Token))
        {
            emittedUsage.Add(usage);
            if (emittedUsage.Count >= 2)
            {
                break;
            }
        }

        Assert.NotEmpty(emittedUsage);
        Assert.Equal(15.5, emittedUsage[0].CpuUsagePercent);
        Assert.Equal("Running", emittedUsage[0].ServiceStatus);

        await aggregator.DisposeAsync();

        static async IAsyncEnumerable<ServerResourceUsage> GetUsageAsync()
        {
            for (int i = 0; i < 5; i++)
            {
                yield return new ServerResourceUsage
                {
                    ServerId = "srv-res-1",
                    DesiredReplicas = 1,
                    RunningReplicas = 1,
                    RealTimeStats = new ContainerStats
                    {
                        CpuUsagePercent = 15.5,
                        MemoryUsageBytes = 1048576,
                        MemoryLimitBytes = 2097152,
                        MemoryUsagePercent = 50.0
                    }
                };
                await Task.Delay(50);
            }
        }
    }
}
