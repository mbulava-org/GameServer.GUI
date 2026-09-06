using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameServer.API.Client.Interfaces;
using GameServer.API.Client.Models;
using GameServer.API.Client.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameServer.API.Client.Tests;

public class ResourceMonitoringClientTests : IDisposable
{
    private readonly TestServer _server;
    private readonly HubConnection _connection;
    private readonly ResourceMonitoringClient _client;
    private readonly FakeResourceHub _hub;

    public ResourceMonitoringClientTests()
    {
        _hub = new FakeResourceHub();

        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSignalR();
                services.AddSingleton(_hub);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapHub<FakeResourceHub>("/hubs/resources");
                });
            });

        _server = new TestServer(builder);

        _connection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/resources", options =>
            {
                options.HttpMessageHandlerFactory = _ => _server.CreateHandler();
                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
            })
            .Build();

        _client = new ResourceMonitoringClient(_connection, NullLogger<ResourceMonitoringClient>.Instance);
    }

    [Fact]
    public void HubResourceUsage_FlatProperties_ShouldMapToInterfaceModel()
    {
        var hubUsage = new HubResourceUsage
        {
            ServerId = "srv-123",
            Timestamp = DateTime.UtcNow,
            DesiredReplicas = 1,
            RunningReplicas = 1,
            CpuUsagePercent = 33.3,
            MemoryUsageBytes = 1024 * 1024 * 300,
            MemoryLimitBytes = 1024 * 1024 * 1024,
            MemoryUsagePercent = 30.0,
            NetworkRxBytes = 500,
            NetworkTxBytes = 1500,
            BlockReadBytes = 2500,
            BlockWriteBytes = 3500,
            ContainerIds = new List<string> { "c-123" }
        };

        var client = new ResourceMonitoringClient("http://localhost:5164/hubs/resources");

        Assert.Equal(33.3, hubUsage.CpuUsagePercent);
        Assert.Equal(1024 * 1024 * 300, hubUsage.MemoryUsageBytes);
        Assert.Equal(30.0, hubUsage.MemoryUsagePercent);
        Assert.Equal("Running", hubUsage.ServiceStatus);
        Assert.False(client.IsConnected);
        Assert.Null(client.MonitoredServerId);
        Assert.Null(client.MonitoredServerIds);
        Assert.Null(client.CurrentIntervalSeconds);
    }

    [Fact]
    public void ResourceMonitoringClient_Constructor_ValidatesArguments()
    {
        Assert.Throws<ArgumentException>(() => new ResourceMonitoringClient(""));
        Assert.Throws<ArgumentException>(() => new ResourceMonitoringClient("   "));
        Assert.Throws<ArgumentNullException>(() => new ResourceMonitoringClient((HubConnection)null!));
    }

    [Fact]
    public async Task ResourceMonitoringClient_Methods_ValidateArguments()
    {
        var client = new ResourceMonitoringClient("http://localhost:5164/hubs/resources");

        await Assert.ThrowsAsync<ArgumentException>(() => client.SubscribeToServerAsync(""));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.SubscribeToServerAsync("srv-1", 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.SubscribeToServerAsync("srv-1", 61));

        await Assert.ThrowsAsync<ArgumentException>(() => client.SubscribeToMultipleServersAsync(Array.Empty<string>()));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.SubscribeToMultipleServersAsync(new[] { "srv-1" }, 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.SubscribeToMultipleServersAsync(new[] { "srv-1" }, 61));

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetSnapshotAsync(""));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.UpdateIntervalAsync(0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.UpdateIntervalAsync(61));

        // When not connected:
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SubscribeToServerAsync("srv-1", 5));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SubscribeToMultipleServersAsync(new[] { "srv-1" }, 5));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetSnapshotAsync("srv-1"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.UpdateIntervalAsync(5));

        // Dispose when disconnected shouldn't throw
        await client.DisposeAsync();
    }

    [Fact]
    public async Task ConnectAsync_ShouldConnectAndDoubleConnectShouldBeNoOp()
    {
        await _client.ConnectAsync(CancellationToken.None);
        Assert.True(_client.IsConnected);

        // Connecting again when already connected
        await _client.ConnectAsync(CancellationToken.None);
        Assert.True(_client.IsConnected);
    }

    [Fact]
    public async Task SubscribeToServerAsync_ShouldTriggerSubscribedAndResourceUpdateEvents()
    {
        var subscribedTcs = new TaskCompletionSource<(string ServerId, int Interval)>();
        var updateTcs = new TaskCompletionSource<Interfaces.ServerResourceUsage>();

        _client.Subscribed += (s, e) => subscribedTcs.TrySetResult(e);
        _client.ResourceUpdateReceived += (s, e) => updateTcs.TrySetResult(e);

        await _client.ConnectAsync(CancellationToken.None);
        await _client.SubscribeToServerAsync("server-alpha", 10, CancellationToken.None);

        var subResult = await subscribedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("server-alpha", subResult.ServerId);
        Assert.Equal(10, subResult.Interval);
        Assert.Equal("server-alpha", _client.MonitoredServerId);
        Assert.Null(_client.MonitoredServerIds);
        Assert.Equal(10, _client.CurrentIntervalSeconds);

        // Hub sends single update
        await _hub.SendSingleUpdateAsync("server-alpha", 45.5, 1024 * 1024 * 512);

        var updateResult = await updateTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("server-alpha", updateResult.ServerId);
        Assert.Equal(45.5, updateResult.CpuUsagePercent);
        Assert.Equal(1024 * 1024 * 512, updateResult.MemoryUsageBytes);
    }

    [Fact]
    public async Task SubscribeToMultipleServersAsync_ShouldTriggerEvents()
    {
        var subscribedTcs = new TaskCompletionSource<(string[] ServerIds, int Interval)>();
        var batchTcs = new TaskCompletionSource<List<Interfaces.ServerResourceUsage>>();

        _client.SubscribedMultiple += (s, e) => subscribedTcs.TrySetResult((e.ServerIds, e.IntervalSeconds));
        _client.ResourceUpdateBatchReceived += (s, e) => batchTcs.TrySetResult(e.ToList());

        await _client.ConnectAsync(CancellationToken.None);
        await _client.SubscribeToMultipleServersAsync(new[] { "srv-1", "srv-2" }, 15, CancellationToken.None);

        var subResult = await subscribedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, subResult.ServerIds.Length);
        Assert.Equal(15, subResult.Interval);
        Assert.Null(_client.MonitoredServerId);
        Assert.Equal(2, _client.MonitoredServerIds?.Count);
        Assert.Equal(15, _client.CurrentIntervalSeconds);

        // Hub sends batch update
        await _hub.SendBatchUpdateAsync(new[] { "srv-1", "srv-2" });

        var batchResult = await batchTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, batchResult.Count);
    }

    [Fact]
    public async Task GetSnapshotAsync_ShouldReturnSnapshot()
    {
        await _client.ConnectAsync(CancellationToken.None);

        var snapshot = await _client.GetSnapshotAsync("server-alpha", CancellationToken.None);

        Assert.NotNull(snapshot);
        Assert.Equal("server-alpha", snapshot.ServerId);
        Assert.True(snapshot.IsRunning);
    }

    [Fact]
    public async Task UpdateIntervalAsync_AndUnsubscribeAsync_ShouldUpdateState()
    {
        var intervalTcs = new TaskCompletionSource<int>();
        var unsubTcs = new TaskCompletionSource<bool>();

        _client.IntervalUpdated += (s, e) => intervalTcs.TrySetResult(e);
        _client.Unsubscribed += (s, e) => unsubTcs.TrySetResult(true);

        await _client.ConnectAsync(CancellationToken.None);
        await _client.SubscribeToServerAsync("server-alpha", 5, CancellationToken.None);

        await _client.UpdateIntervalAsync(20, CancellationToken.None);
        var newInterval = await intervalTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(20, newInterval);
        Assert.Equal(20, _client.CurrentIntervalSeconds);

        await _client.UnsubscribeAsync(CancellationToken.None);
        var unsubscribed = await unsubTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(unsubscribed);
        Assert.Null(_client.MonitoredServerId);
        Assert.Null(_client.CurrentIntervalSeconds);
    }

    [Fact]
    public async Task ErrorReceived_ShouldFireOnError()
    {
        var errorTcs = new TaskCompletionSource<string>();
        _client.ErrorReceived += (s, e) => errorTcs.TrySetResult(e);

        await _client.ConnectAsync(CancellationToken.None);
        await _hub.SendErrorAsync("Rate limit exceeded");

        var err = await errorTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("Rate limit exceeded", err);
    }

    public void Dispose()
    {
        _connection.DisposeAsync().AsTask().Wait();
        _server.Dispose();
    }
}

public class FakeResourceHub : Hub
{
    public async Task SubscribeToServer(string serverId, int intervalSeconds)
    {
        await Clients.Caller.SendAsync("Subscribed", serverId, intervalSeconds);
    }

    public async Task SubscribeToMultipleServers(string[] serverIds, int intervalSeconds)
    {
        await Clients.Caller.SendAsync("SubscribedMultiple", serverIds, intervalSeconds);
    }

    public async Task<HubResourceUsage?> GetSnapshot(string serverId)
    {
        await Task.CompletedTask;
        return new HubResourceUsage
        {
            ServerId = serverId,
            DesiredReplicas = 1,
            RunningReplicas = 1,
            Timestamp = DateTime.UtcNow,
            CpuUsagePercent = 25.0,
            MemoryUsageBytes = 1024 * 1024 * 200,
            MemoryLimitBytes = 1024 * 1024 * 1024,
            MemoryUsagePercent = 20.0,
            RealTimeStats = new GameServer.API.Client.Models.ContainerStats
            {
                CpuUsagePercent = 25.0,
                MemoryUsageBytes = 1024 * 1024 * 200,
                MemoryLimitBytes = 1024 * 1024 * 1024,
                MemoryUsagePercent = 20.0
            }
        };
    }

    public async Task UpdateInterval(int intervalSeconds)
    {
        await Clients.Caller.SendAsync("IntervalUpdated", intervalSeconds);
    }

    public async Task Unsubscribe()
    {
        await Clients.Caller.SendAsync("Unsubscribed");
    }

    public async Task SendSingleUpdateAsync(string serverId, double cpu, long memory)
    {
        await Clients.All.SendAsync("ResourceUpdate", new HubResourceUsage
        {
            ServerId = serverId,
            DesiredReplicas = 1,
            RunningReplicas = 1,
            Timestamp = DateTime.UtcNow,
            CpuUsagePercent = cpu,
            MemoryUsageBytes = memory,
            MemoryLimitBytes = 1024 * 1024 * 1024,
            MemoryUsagePercent = 50.0
        });
    }

    public async Task SendBatchUpdateAsync(string[] serverIds)
    {
        var list = serverIds.Select(id => new HubResourceUsage
        {
            ServerId = id,
            DesiredReplicas = 1,
            RunningReplicas = 1,
            Timestamp = DateTime.UtcNow,
            CpuUsagePercent = 10.0,
            MemoryUsageBytes = 1024 * 1024 * 100,
            MemoryLimitBytes = 1024 * 1024 * 1024,
            MemoryUsagePercent = 10.0
        }).ToList();

        await Clients.All.SendAsync("ResourceUpdateBatch", list);
    }

    public async Task SendErrorAsync(string error)
    {
        await Clients.All.SendAsync("Error", error);
    }
}
