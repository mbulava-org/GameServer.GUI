using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using GameServer.API.Interfaces;
using GameServer.API.Models;
using GameServer.API.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GameServer.API.Tests.Services;

public class MockNodeAgentHub : Hub
{
    public async IAsyncEnumerable<string> StreamContainerLogs(
        string containerId,
        bool follow,
        int tailLines,
        bool timestamps,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return $"log line 1 for {containerId}";
        yield return $"log line 2 for {containerId}";
    }

    public async IAsyncEnumerable<object> StreamContainerStats(
        string containerId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new { ContainerId = containerId, CpuPercent = 12.5 };
    }

    public Task<object?> GetContainerStatsSnapshot(string containerId)
    {
        return Task.FromResult<object?>(new { ContainerId = containerId, MemoryUsage = 4096 });
    }

    public Task<object?> GetContainerLogs(string containerId, int tailLines)
    {
        return Task.FromResult<object?>(new { ContainerId = containerId, Logs = "sample logs" });
    }
}

public class NodeAgentClientFunctionalTests
{
    [Fact]
    public async Task NodeAgentClient_FullLifecycle_StreamsLogsStatsAndInvokesMethods()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddSignalR();

        var app = builder.Build();
        app.UseWebSockets();
        app.MapHub<MockNodeAgentHub>("/hubs/nodeagent");

        app.Map("/containers/{id}/attach/ws", async (HttpContext context, string id) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            try
            {
                using var ws = await context.WebSockets.AcceptWebSocketAsync();
                var msg = Encoding.UTF8.GetBytes($"attach frame for {id}");
                await ws.SendAsync(new ArraySegment<byte>(msg), WebSocketMessageType.Text, true, CancellationToken.None);

                var buf = new byte[1024];
                while (ws.State == WebSocketState.Open)
                {
                    var res = await ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None);
                    if (res.MessageType == WebSocketMessageType.Close)
                    {
                        if (ws.State == WebSocketState.Open || ws.State == WebSocketState.CloseReceived)
                        {
                            await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                        }
                        break;
                    }

                    if (res.MessageType == WebSocketMessageType.Text)
                    {
                        var received = Encoding.UTF8.GetString(buf, 0, res.Count);
                        var echo = Encoding.UTF8.GetBytes($"echo: {received}");
                        await ws.SendAsync(new ArraySegment<byte>(echo), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }
            }
            catch (Exception)
            {
                // Normal on client abrupt disconnect
            }
        });

        await app.StartAsync();
        var address = app.Urls.First();

        var client = new NodeAgentClient(NullLogger<NodeAgentClient>.Instance);

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            // 1. Stream Logs
            var logs = new List<string>();
            await foreach (var log in client.StreamContainerLogsAsync(address, "test-container-1", cancellationToken: cts.Token))
            {
                logs.Add(log);
            }
            Assert.Equal(2, logs.Count);
            Assert.Contains("test-container-1", logs[0]);

            // 2. Stream Stats
            var statsList = new List<object>();
            await foreach (var stat in client.StreamContainerStatsAsync(address, "test-container-2", cancellationToken: cts.Token))
            {
                statsList.Add(stat);
            }
            Assert.Single(statsList);

            // 3. Stats Snapshot
            var snapshot = await client.GetContainerStatsSnapshotAsync(address, "test-container-3", cts.Token);
            Assert.NotNull(snapshot);

            // 4. Container Logs Snapshot
            var logsSnapshot = await client.GetContainerLogsAsync(address, "test-container-4", 50, cts.Token);
            Assert.NotNull(logsSnapshot);

            // 5. Stream Attach WebSocket
            var attachFrames = new List<string>();
            await foreach (var frame in client.StreamContainerAttachAsync(address, "attach-c1", cts.Token))
            {
                attachFrames.Add(frame);
                break; // read first frame then break cleanly
            }
            Assert.NotEmpty(attachFrames);
            Assert.Contains("attach-c1", attachFrames[0]);

            // 6. Direct WebSocket Send Attach Input
            using var directWs = new ClientWebSocket();
            var wsUrl = address.Replace("http://", "ws://") + "/containers/c-send/attach/ws";
            await directWs.ConnectAsync(new Uri(wsUrl), cts.Token);
            await NodeAgentClient.SendAttachInputAsync(directWs, "hello agent", cts.Token);
            await directWs.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
        }
        finally
        {
            await client.DisposeAsync();
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task TerminalSessionManager_FullLifecycle_ConnectsAndForwardsIO()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");

        var app = builder.Build();
        app.UseWebSockets();

        app.Map("/containers/{id}/exec/ws", async (HttpContext context, string id) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            try
            {
                using var ws = await context.WebSockets.AcceptWebSocketAsync();
                var initialOutput = Encoding.UTF8.GetBytes($"sh-5.2# ");
                await ws.SendAsync(new ArraySegment<byte>(initialOutput), WebSocketMessageType.Text, true, CancellationToken.None);

                var buf = new byte[1024];
                while (ws.State == WebSocketState.Open)
                {
                    var res = await ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None);
                    if (res.MessageType == WebSocketMessageType.Close)
                    {
                        if (ws.State == WebSocketState.Open || ws.State == WebSocketState.CloseReceived)
                        {
                            await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Session ended", CancellationToken.None);
                        }
                        break;
                    }
                    if (res.MessageType == WebSocketMessageType.Text)
                    {
                        var input = Encoding.UTF8.GetString(buf, 0, res.Count);
                        var echo = Encoding.UTF8.GetBytes($"output of: {input}");
                        await ws.SendAsync(new ArraySegment<byte>(echo), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }
            }
            catch (Exception)
            {
                // Normal when client disconnects
            }
        });

        await app.StartAsync();
        var address = app.Urls.First();

        var notifierMock = new Mock<ITerminalSessionNotifier>();
        var discoveryMock = new Mock<INodeAgentDiscovery>();
        discoveryMock.Setup(d => d.GetAgentForContainerAsync("terminal-c1"))
            .ReturnsAsync(new NodeAgentEndpoint
            {
                NodeId = "n-1",
                NodeName = "local-node",
                InternalUrl = address,
                IsHealthy = true
            });

        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();

        var manager = new TerminalSessionManager(
            NullLogger<TerminalSessionManager>.Instance,
            notifierMock.Object,
            discoveryMock.Object,
            sp);

        try
        {
            var (success, error) = await manager.StartSessionAsync("conn-term-1", "terminal-c1", "/bin/sh");
            Assert.True(success, error);

            // Wait a bit for initial output forwarding
            await Task.Delay(200);
            notifierMock.Verify(n => n.SendOutputAsync("conn-term-1", It.Is<string>(s => s.Contains("sh-5.2#"))), Times.AtLeastOnce);

            // Send command
            await manager.SendInputAsync("conn-term-1", "whoami\n");

            await Task.Delay(200);
            notifierMock.Verify(n => n.SendOutputAsync("conn-term-1", It.Is<string>(s => s.Contains("whoami"))), Times.AtLeastOnce);

            await manager.CloseSessionAsync("conn-term-1");
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }
}
