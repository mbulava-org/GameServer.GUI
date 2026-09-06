using GameServer.API.Hubs;
using GameServer.API.Interfaces;
using GameServer.API.Models;
using GameServer.API.Models.V2;
using GameServer.API.Repositories.V2;
using GameServer.API.Services;
using GameServer.API.Services.V2;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameServer.API.Tests.Hubs;

public class SignalRHubsTests
{
    private static void SetupHubMocks(Hub hub, string connectionId, Mock<ISingleClientProxy> clientProxyMock)
    {
        var contextMock = new Mock<HubCallerContext>();
        contextMock.Setup(c => c.ConnectionId).Returns(connectionId);
        contextMock.Setup(c => c.ConnectionAborted).Returns(CancellationToken.None);

        contextMock.Setup(c => c.Features).Returns(new Microsoft.AspNetCore.Http.Features.FeatureCollection());

        var clientsMock = new Mock<IHubCallerClients>();
        clientsMock.Setup(c => c.Caller).Returns(clientProxyMock.Object);
        clientsMock.Setup(c => c.Client(connectionId)).Returns(clientProxyMock.Object);

        hub.Context = contextMock.Object;
        hub.Clients = clientsMock.Object;
    }

    #region ContainerConsoleHub Tests

    [Fact]
    public async Task ContainerConsoleHub_StartExecSession_SuccessAndFailure()
    {
        var sessionManager = new Mock<TerminalSessionManager>(
            NullLogger<TerminalSessionManager>.Instance,
            Mock.Of<ITerminalSessionNotifier>(),
            Mock.Of<INodeAgentDiscovery>(),
            Mock.Of<IServiceProvider>());

        var hub = new ContainerConsoleHub(NullLogger<ContainerConsoleHub>.Instance, sessionManager.Object);
        var callerMock = new Mock<ISingleClientProxy>();
        SetupHubMocks(hub, "conn-1", callerMock);

        sessionManager.Setup(m => m.StartSessionAsync("conn-1", "c-1", "/bin/sh"))
            .ReturnsAsync((true, (string?)null));

        var success = await hub.StartExecSession("c-1", "/bin/sh");
        Assert.True(success);

        sessionManager.Setup(m => m.StartSessionAsync("conn-1", "c-2", "/bin/sh"))
            .ReturnsAsync((false, "Failed to start"));

        var failure = await hub.StartExecSession("c-2", "/bin/sh");
        Assert.False(failure);
    }

    [Fact]
    public async Task ContainerConsoleHub_SendInputAndDisconnect()
    {
        var sessionManager = new Mock<TerminalSessionManager>(
            NullLogger<TerminalSessionManager>.Instance,
            Mock.Of<ITerminalSessionNotifier>(),
            Mock.Of<INodeAgentDiscovery>(),
            Mock.Of<IServiceProvider>());

        var hub = new ContainerConsoleHub(NullLogger<ContainerConsoleHub>.Instance, sessionManager.Object);
        var callerMock = new Mock<ISingleClientProxy>();
        SetupHubMocks(hub, "conn-1", callerMock);

        await hub.SendInput("conn-1", "ls -la\n");
        sessionManager.Verify(m => m.SendInputAsync("conn-1", "ls -la\n"), Times.Once);

        await hub.Disconnect();
        sessionManager.Verify(m => m.CloseSessionAsync("conn-1"), Times.Once);

        await hub.OnDisconnectedAsync(null);
        sessionManager.Verify(m => m.CloseSessionAsync("conn-1"), Times.Exactly(2));
    }

    #endregion

    #region ContainerAttachHub Tests

    [Fact]
    public async Task ContainerAttachHub_SubscribeAndSendInput()
    {
        var attachAggregator = new Mock<IContainerAttachAggregator>();
        var resourceMonitor = new Mock<IServerResourceMonitor>();
        resourceMonitor.Setup(m => m.GetSnapshotAsync("srv-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServerResourceUsage { ServerId = "srv-1", ContainerIds = ["c-123"] });

        async IAsyncEnumerable<AttachStreamFrame> TestFrames()
        {
            yield return new AttachStreamFrame { Kind = AttachFrameKind.Output, Payload = "log 1" };
            yield return new AttachStreamFrame { Kind = AttachFrameKind.Output, Payload = "err 1" };
            await Task.Yield();
        }

        attachAggregator.Setup(a => a.SubscribeAsync("conn-1", "c-123", It.IsAny<CancellationToken>()))
            .Returns(TestFrames());
        attachAggregator.Setup(a => a.SendInputAsync("conn-1", "c-123", "command", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var hub = new ContainerAttachHub(NullLogger<ContainerAttachHub>.Instance, attachAggregator.Object, resourceMonitor.Object);
        var callerMock = new Mock<ISingleClientProxy>();
        SetupHubMocks(hub, "conn-1", callerMock);

        var frames = new List<string>();
        await foreach (var frame in hub.SubscribeToContainer("srv-1"))
        {
            frames.Add(frame);
        }

        Assert.Equal(2, frames.Count);

        var sent = await hub.SendInput("c-123", "command");
        Assert.True(sent);

        await hub.DisconnectFromContainer("c-123");
        attachAggregator.Verify(a => a.UnsubscribeAsync("conn-1", "c-123"), Times.Once);

        await hub.OnDisconnectedAsync(null);
    }

    [Fact]
    public async Task ContainerAttachHub_SubscribeToContainer_WhenNotFound_YieldsError()
    {
        var attachAggregator = new Mock<IContainerAttachAggregator>();
        var resourceMonitor = new Mock<IServerResourceMonitor>();
        resourceMonitor.Setup(m => m.GetSnapshotAsync("srv-missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServerResourceUsage?)null);

        var hub = new ContainerAttachHub(NullLogger<ContainerAttachHub>.Instance, attachAggregator.Object, resourceMonitor.Object);
        var callerMock = new Mock<ISingleClientProxy>();
        SetupHubMocks(hub, "conn-1", callerMock);

        var frames = new List<string>();
        await foreach (var frame in hub.SubscribeToContainer("srv-missing"))
        {
            frames.Add(frame);
        }

        Assert.Single(frames);
        Assert.Contains("Could not resolve container", frames[0]);
    }

    #endregion

    #region ServerLogsHub Tests

    [Fact]
    public async Task ServerLogsHub_StreamServerLogs_WhenServerFound()
    {
        var serverRepo = new Mock<IGameServerRepository>();
        serverRepo.Setup(r => r.GetByServerIdAsync("srv-1"))
            .ReturnsAsync(new Models.V2.GameServer { ServerId = "srv-1", Name = "Server 1", Status = "Running", ServiceName = "svc-1" });
        var gameTypeRepo = new Mock<IGameTypeRepository>();
        gameTypeRepo.Setup(r => r.GetAllAsync(true)).ReturnsAsync([]);
        var queryService = new GameServerQueryService(serverRepo.Object, gameTypeRepo.Object);

        var logAggregator = new Mock<IServerLogAggregator>();
        async IAsyncEnumerable<string> TestLogs()
        {
            yield return "line 1";
            yield return "line 2";
            await Task.Yield();
        }
        logAggregator.Setup(a => a.StreamLogsAsync("srv-1", true, 100, true, It.IsAny<CancellationToken>()))
            .Returns(TestLogs());

        var hub = new ServerLogsHub(NullLogger<ServerLogsHub>.Instance, queryService, logAggregator.Object);
        var callerMock = new Mock<ISingleClientProxy>();
        SetupHubMocks(hub, "conn-1", callerMock);

        var logs = new List<string>();
        await foreach (var line in hub.StreamServerLogs("srv-1", true, 100, true))
        {
            logs.Add(line);
        }

        Assert.Equal(2, logs.Count);
        Assert.Equal("line 1", logs[0]);
        Assert.Equal("line 2", logs[1]);

        await hub.OnDisconnectedAsync(null);
    }

    [Fact]
    public async Task ServerLogsHub_StreamServerLogs_WhenServerNotFound()
    {
        var serverRepo = new Mock<IGameServerRepository>();
        serverRepo.Setup(r => r.GetByServerIdAsync("missing")).ReturnsAsync((Models.V2.GameServer?)null);
        var gameTypeRepo = new Mock<IGameTypeRepository>();
        gameTypeRepo.Setup(r => r.GetAllAsync(true)).ReturnsAsync([]);
        var queryService = new GameServerQueryService(serverRepo.Object, gameTypeRepo.Object);
        var logAggregator = new Mock<IServerLogAggregator>();

        var hub = new ServerLogsHub(NullLogger<ServerLogsHub>.Instance, queryService, logAggregator.Object);
        var callerMock = new Mock<ISingleClientProxy>();
        SetupHubMocks(hub, "conn-1", callerMock);

        var logs = new List<string>();
        await foreach (var line in hub.StreamServerLogs("missing", true, 100, true))
        {
            logs.Add(line);
        }

        Assert.Single(logs);
        Assert.Equal("ERROR: Server not found", logs[0]);
    }

    [Fact]
    public async Task ServerLogsHub_StreamServerLogs_WhenServerExists_StreamsLogs()
    {
        var serverRepo = new Mock<IGameServerRepository>();
        serverRepo.Setup(r => r.GetByServerIdAsync("srv-1")).ReturnsAsync(new Models.V2.GameServer { ServerId = "srv-1", Name = "Server 1" });
        var gameTypeRepo = new Mock<IGameTypeRepository>();
        gameTypeRepo.Setup(r => r.GetAllAsync(true)).ReturnsAsync([]);
        var queryService = new GameServerQueryService(serverRepo.Object, gameTypeRepo.Object);
        var logAggregator = new Mock<IServerLogAggregator>();

        async IAsyncEnumerable<string> MockLogs()
        {
            yield return "line 1";
            yield return "line 2";
            await Task.Yield();
        }

        logAggregator.Setup(a => a.StreamLogsAsync("srv-1", true, 100, true, It.IsAny<CancellationToken>()))
            .Returns(MockLogs());

        var hub = new ServerLogsHub(NullLogger<ServerLogsHub>.Instance, queryService, logAggregator.Object);
        var callerMock = new Mock<ISingleClientProxy>();
        SetupHubMocks(hub, "conn-1", callerMock);

        var logs = new List<string>();
        await foreach (var line in hub.StreamServerLogs("srv-1", true, 100, true))
        {
            logs.Add(line);
        }

        Assert.Equal(2, logs.Count);
        Assert.Equal("line 1", logs[0]);
        Assert.Equal("line 2", logs[1]);
    }

    #endregion

    #region AgentRegistrationHub Tests

    [Fact]
    public async Task AgentRegistrationHub_RegisterHeartbeatAndDisconnect()
    {
        var agentRegistry = new Mock<IAgentRegistry>();
        var hub = new AgentRegistrationHub(agentRegistry.Object, NullLogger<AgentRegistrationHub>.Instance);
        var callerMock = new Mock<ISingleClientProxy>();
        SetupHubMocks(hub, "conn-1", callerMock);

        var regInfo = new AgentRegistrationInfo { NodeId = "node-1", NodeName = "node-1", InternalUrl = "http://node-1:8080" };
        await hub.RegisterAgent(regInfo);
        agentRegistry.Verify(r => r.RegisterAgent(regInfo, "conn-1"), Times.Once);

        var heartbeat = new AgentHeartbeatInfo { NodeId = "node-1", ContainerIds = ["c-1"], Health = "healthy" };
        await hub.SendHeartbeat(heartbeat);
        agentRegistry.Verify(r => r.UpdateAgentContainers("conn-1", heartbeat.ContainerIds), Times.Once);

        await hub.OnConnectedAsync();
        await hub.OnDisconnectedAsync(null);
        agentRegistry.Verify(r => r.MarkAgentDisconnected("conn-1"), Times.Once);
    }

    [Fact]
    public async Task AgentRegistrationHub_OnDisconnectedAsync_WithException_LogsWarning()
    {
        var agentRegistry = new Mock<IAgentRegistry>();
        var hub = new AgentRegistrationHub(agentRegistry.Object, NullLogger<AgentRegistrationHub>.Instance);
        var callerMock = new Mock<ISingleClientProxy>();
        SetupHubMocks(hub, "conn-1", callerMock);

        await hub.OnDisconnectedAsync(new Exception("Network dropped"));
        agentRegistry.Verify(r => r.MarkAgentDisconnected("conn-1"), Times.Once);
    }

    #endregion

    #region ResourceMonitoringHub Tests

    [Fact]
    public async Task ResourceMonitoringHub_Operations()
    {
        var resourceAggregator = new Mock<IServerResourceAggregator>();
        resourceAggregator.Setup(r => r.GetSnapshotAsync("srv-1"))
            .ReturnsAsync(new ServerResourceUsage
            {
                ServerId = "srv-1",
                RealTimeStats = new GameServer.API.Models.ContainerStats { CpuUsagePercent = 25.0 }
            });

        var hub = new ResourceMonitoringHub(NullLogger<ResourceMonitoringHub>.Instance, resourceAggregator.Object);
        var callerMock = new Mock<ISingleClientProxy>();
        SetupHubMocks(hub, "conn-1", callerMock);

        var snapshot = await hub.GetSnapshot("srv-1");
        Assert.NotNull(snapshot);
        Assert.Equal(25.0, snapshot.CpuUsagePercent);

        await hub.SubscribeToServer("srv-1", 5);
        await hub.SubscribeToMultipleServers(["srv-1", "srv-2"], 5);
        await hub.SubscribeToMultipleServers(Array.Empty<string>(), 5);

        await hub.UpdateInterval(10);
        await hub.UpdateInterval(0); // Should error

        await hub.Unsubscribe();
        await hub.OnDisconnectedAsync(null);
    }

    #endregion
}
