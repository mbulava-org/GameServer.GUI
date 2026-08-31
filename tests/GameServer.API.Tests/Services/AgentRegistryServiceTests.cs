using GameServer.API.Models;
using GameServer.API.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace GameServer.API.Tests.Services;

/// <summary>
/// Unit tests for AgentRegistryService - verifies agent discovery, registration,
/// heartbeat handling, container mapping, disconnection, and health checks.
/// Acceptance criteria for DEV-90: agent discovery and connection end-to-end.
/// </summary>
public class AgentRegistryServiceTests
{
    private readonly AgentRegistryService _service;
    private readonly Mock<ILogger<AgentRegistryService>> _mockLogger;

    public AgentRegistryServiceTests()
    {
        _mockLogger = new Mock<ILogger<AgentRegistryService>>();
        _service = new AgentRegistryService(_mockLogger.Object);
    }

    // -----------------------------------------------------------------------
    // RegisterAgent
    // -----------------------------------------------------------------------

    [Fact]
    public void RegisterAgent_NewAgent_IsReturnedInGetAllAgents()
    {
        var info = MakeRegistrationInfo("node-1", "worker-1", "http://10.0.1.1:8080");
        _service.RegisterAgent(info, "conn-1");

        var agents = _service.GetAllAgents();

        Assert.Single(agents);
        Assert.Equal("node-1", agents[0].NodeId);
        Assert.Equal("worker-1", agents[0].NodeName);
        Assert.Equal("http://10.0.1.1:8080", agents[0].InternalUrl);
        Assert.Equal("conn-1", agents[0].ConnectionId);
        Assert.True(agents[0].IsHealthy);
    }

    [Fact]
    public void RegisterAgent_ManagerNode_IsTrackedCorrectly()
    {
        var info = MakeRegistrationInfo("manager-1", "manager-node", "http://10.0.1.5:8080", isManager: true);
        _service.RegisterAgent(info, "conn-manager");

        var agent = _service.GetAgentByNodeId("manager-1");

        Assert.NotNull(agent);
        Assert.True(agent.IsManagerNode);
    }

    [Fact]
    public void RegisterAgent_WorkerNode_IsNotManagerNode()
    {
        var info = MakeRegistrationInfo("worker-1", "worker-node", "http://10.0.1.6:8080", isManager: false);
        _service.RegisterAgent(info, "conn-worker");

        var agent = _service.GetAgentByNodeId("worker-1");

        Assert.NotNull(agent);
        Assert.False(agent.IsManagerNode);
    }

    [Fact]
    public void RegisterAgent_SameNodeReregisters_UpdatesEntry()
    {
        var info1 = MakeRegistrationInfo("node-1", "worker-1", "http://10.0.1.1:8080");
        _service.RegisterAgent(info1, "conn-1");

        // Re-register same node with new connection (e.g. after restart)
        var info2 = MakeRegistrationInfo("node-1", "worker-1", "http://10.0.1.1:8080");
        _service.RegisterAgent(info2, "conn-2");

        // Both connections may coexist in the by-connection dict;
        // GetAgentByNodeId should return the latest
        var agent = _service.GetAgentByNodeId("node-1");
        Assert.NotNull(agent);
        Assert.Equal("conn-2", agent.ConnectionId);
    }

    [Fact]
    public void RegisterAgent_MultipleAgents_AllReturnedInGetAllAgents()
    {
        _service.RegisterAgent(MakeRegistrationInfo("node-1", "n1", "http://10.0.1.1:8080"), "conn-1");
        _service.RegisterAgent(MakeRegistrationInfo("node-2", "n2", "http://10.0.1.2:8080"), "conn-2");
        _service.RegisterAgent(MakeRegistrationInfo("node-3", "n3", "http://10.0.1.3:8080"), "conn-3");

        var agents = _service.GetAllAgents();

        Assert.Equal(3, agents.Count);
    }

    // -----------------------------------------------------------------------
    // UpdateAgentContainers (heartbeat)
    // -----------------------------------------------------------------------

    [Fact]
    public void UpdateAgentContainers_ValidConnection_MapsContainersToAgent()
    {
        _service.RegisterAgent(MakeRegistrationInfo("node-1", "n1", "http://10.0.1.1:8080"), "conn-1");

        _service.UpdateAgentContainers("conn-1", new List<string> { "container-aaa", "container-bbb" });

        var agentForA = _service.GetAgentForContainer("container-aaa");
        var agentForB = _service.GetAgentForContainer("container-bbb");

        Assert.NotNull(agentForA);
        Assert.NotNull(agentForB);
        Assert.Equal("node-1", agentForA.NodeId);
        Assert.Equal("node-1", agentForB.NodeId);
    }

    [Fact]
    public void UpdateAgentContainers_ContainersChange_OldMappingsRemoved()
    {
        _service.RegisterAgent(MakeRegistrationInfo("node-1", "n1", "http://10.0.1.1:8080"), "conn-1");
        _service.UpdateAgentContainers("conn-1", new List<string> { "old-container" });

        // Update heartbeat with different containers
        _service.UpdateAgentContainers("conn-1", new List<string> { "new-container" });

        Assert.Null(_service.GetAgentForContainer("old-container"));
        Assert.NotNull(_service.GetAgentForContainer("new-container"));
    }

    [Fact]
    public void UpdateAgentContainers_EmptyList_ClearsAllContainerMappings()
    {
        _service.RegisterAgent(MakeRegistrationInfo("node-1", "n1", "http://10.0.1.1:8080"), "conn-1");
        _service.UpdateAgentContainers("conn-1", new List<string> { "container-x" });

        // Agent reports no containers
        _service.UpdateAgentContainers("conn-1", new List<string>());

        Assert.Null(_service.GetAgentForContainer("container-x"));
    }

    [Fact]
    public void UpdateAgentContainers_UnknownConnection_DoesNotThrow()
    {
        // Should log warning but not throw
        var ex = Record.Exception(() =>
            _service.UpdateAgentContainers("unknown-conn", new List<string> { "container-x" }));

        Assert.Null(ex);
    }

    [Fact]
    public void UpdateAgentContainers_UpdatesLastHeartbeatTimestamp()
    {
        _service.RegisterAgent(MakeRegistrationInfo("node-1", "n1", "http://10.0.1.1:8080"), "conn-1");
        var before = DateTime.UtcNow;

        _service.UpdateAgentContainers("conn-1", new List<string>());

        var agent = _service.GetAgentByNodeId("node-1");
        Assert.NotNull(agent);
        Assert.True(agent.LastHeartbeat >= before);
    }

    // -----------------------------------------------------------------------
    // MarkAgentDisconnected
    // -----------------------------------------------------------------------

    [Fact]
    public void MarkAgentDisconnected_AgentRemovedFromRegistry()
    {
        _service.RegisterAgent(MakeRegistrationInfo("node-1", "n1", "http://10.0.1.1:8080"), "conn-1");
        _service.MarkAgentDisconnected("conn-1");

        Assert.Empty(_service.GetAllAgents());
        Assert.Null(_service.GetAgentByNodeId("node-1"));
    }

    [Fact]
    public void MarkAgentDisconnected_ContainerMappingsCleared()
    {
        _service.RegisterAgent(MakeRegistrationInfo("node-1", "n1", "http://10.0.1.1:8080"), "conn-1");
        _service.UpdateAgentContainers("conn-1", new List<string> { "container-x", "container-y" });

        _service.MarkAgentDisconnected("conn-1");

        Assert.Null(_service.GetAgentForContainer("container-x"));
        Assert.Null(_service.GetAgentForContainer("container-y"));
    }

    [Fact]
    public void MarkAgentDisconnected_UnknownConnection_DoesNotThrow()
    {
        var ex = Record.Exception(() => _service.MarkAgentDisconnected("nonexistent-conn"));
        Assert.Null(ex);
    }

    [Fact]
    public void MarkAgentDisconnected_OneOfManyAgents_OthersUnaffected()
    {
        _service.RegisterAgent(MakeRegistrationInfo("node-1", "n1", "http://10.0.1.1:8080"), "conn-1");
        _service.RegisterAgent(MakeRegistrationInfo("node-2", "n2", "http://10.0.1.2:8080"), "conn-2");
        _service.UpdateAgentContainers("conn-2", new List<string> { "container-on-node2" });

        _service.MarkAgentDisconnected("conn-1");

        var remaining = _service.GetAllAgents();
        Assert.Single(remaining);
        Assert.Equal("node-2", remaining[0].NodeId);
        Assert.NotNull(_service.GetAgentForContainer("container-on-node2"));
    }

    // -----------------------------------------------------------------------
    // GetAgentForContainer
    // -----------------------------------------------------------------------

    [Fact]
    public void GetAgentForContainer_NoAgents_ReturnsNull()
    {
        Assert.Null(_service.GetAgentForContainer("container-xyz"));
    }

    [Fact]
    public void GetAgentForContainer_ContainerOnSpecificNode_ReturnsCorrectAgent()
    {
        _service.RegisterAgent(MakeRegistrationInfo("node-1", "n1", "http://10.0.1.1:8080"), "conn-1");
        _service.RegisterAgent(MakeRegistrationInfo("node-2", "n2", "http://10.0.1.2:8080"), "conn-2");
        _service.UpdateAgentContainers("conn-1", new List<string> { "container-on-node1" });
        _service.UpdateAgentContainers("conn-2", new List<string> { "container-on-node2" });

        var agent1 = _service.GetAgentForContainer("container-on-node1");
        var agent2 = _service.GetAgentForContainer("container-on-node2");

        Assert.NotNull(agent1);
        Assert.NotNull(agent2);
        Assert.Equal("node-1", agent1.NodeId);
        Assert.Equal("node-2", agent2.NodeId);
    }

    // -----------------------------------------------------------------------
    // GetHealthyAgents
    // -----------------------------------------------------------------------

    [Fact]
    public void GetHealthyAgents_AfterRegistration_AllReturned()
    {
        _service.RegisterAgent(MakeRegistrationInfo("node-1", "n1", "http://10.0.1.1:8080"), "conn-1");
        _service.RegisterAgent(MakeRegistrationInfo("node-2", "n2", "http://10.0.1.2:8080"), "conn-2");

        var healthy = _service.GetHealthyAgents();

        Assert.Equal(2, healthy.Count);
    }

    // -----------------------------------------------------------------------
    // GetManagerAgents / GetHealthyManagerAgent
    // -----------------------------------------------------------------------

    [Fact]
    public void GetManagerAgents_ReturnsOnlyManagerNodes()
    {
        _service.RegisterAgent(MakeRegistrationInfo("manager-1", "mgr", "http://10.0.1.5:8080", isManager: true), "conn-mgr");
        _service.RegisterAgent(MakeRegistrationInfo("worker-1", "wkr", "http://10.0.1.6:8080", isManager: false), "conn-wkr");

        var managers = _service.GetManagerAgents();

        Assert.Single(managers);
        Assert.Equal("manager-1", managers[0].NodeId);
    }

    [Fact]
    public void GetHealthyManagerAgent_ReturnsManagerWhenPresent()
    {
        _service.RegisterAgent(MakeRegistrationInfo("manager-1", "mgr", "http://10.0.1.5:8080", isManager: true), "conn-mgr");

        var manager = _service.GetHealthyManagerAgent();

        Assert.NotNull(manager);
        Assert.Equal("manager-1", manager.NodeId);
    }

    [Fact]
    public void GetHealthyManagerAgent_NoManagers_ReturnsNull()
    {
        _service.RegisterAgent(MakeRegistrationInfo("worker-1", "wkr", "http://10.0.1.6:8080", isManager: false), "conn-wkr");

        var manager = _service.GetHealthyManagerAgent();

        Assert.Null(manager);
    }

    [Fact]
    public void GetHealthyManagerAgent_NoAgents_ReturnsNull()
    {
        Assert.Null(_service.GetHealthyManagerAgent());
    }

    // -----------------------------------------------------------------------
    // GetAgentByConnectionId
    // -----------------------------------------------------------------------

    [Fact]
    public void GetAgentByConnectionId_ValidConnection_ReturnsAgent()
    {
        _service.RegisterAgent(MakeRegistrationInfo("node-1", "n1", "http://10.0.1.1:8080"), "conn-1");

        var agent = _service.GetAgentByConnectionId("conn-1");

        Assert.NotNull(agent);
        Assert.Equal("node-1", agent.NodeId);
    }

    [Fact]
    public void GetAgentByConnectionId_UnknownConnection_ReturnsNull()
    {
        Assert.Null(_service.GetAgentByConnectionId("unknown-conn"));
    }

    // -----------------------------------------------------------------------
    // Connection retry / health: end-to-end flow simulation
    // -----------------------------------------------------------------------

    [Fact]
    public void EndToEnd_AgentRegistersAndSendsHeartbeat_ContainerReachable()
    {
        // Simulates: agent connects → registers → sends heartbeat with containers
        var nodeId = "swarm-node-42";
        var connectionId = "signalr-conn-abc";
        var containerId = "container-deadbeef";
        var agentUrl = "http://10.10.0.42:8080";

        // 1. Agent registers
        _service.RegisterAgent(new AgentRegistrationInfo
        {
            NodeId = nodeId,
            NodeName = "docker-node-42",
            InternalUrl = agentUrl,
            IsManagerNode = false,
            RegisteredAt = DateTime.UtcNow
        }, connectionId);

        // 2. Agent sends heartbeat
        _service.UpdateAgentContainers(connectionId, new List<string> { containerId });

        // 3. Primary Service resolves agent for container
        var resolved = _service.GetAgentForContainer(containerId);

        Assert.NotNull(resolved);
        Assert.Equal(nodeId, resolved.NodeId);
        Assert.Equal(agentUrl, resolved.InternalUrl);
        Assert.True(resolved.IsHealthy);
    }

    [Fact]
    public void EndToEnd_AgentDisconnectsAndReconnects_ContainerReachableAfterReconnect()
    {
        var nodeId = "swarm-node-1";
        var containerId = "container-abc123";

        // Initial connection
        _service.RegisterAgent(MakeRegistrationInfo(nodeId, "n1", "http://10.0.1.1:8080"), "conn-old");
        _service.UpdateAgentContainers("conn-old", new List<string> { containerId });

        // Agent disconnects (e.g. network blip)
        _service.MarkAgentDisconnected("conn-old");

        // Container should not be found during disconnect
        Assert.Null(_service.GetAgentForContainer(containerId));

        // Agent reconnects with new SignalR connection
        _service.RegisterAgent(MakeRegistrationInfo(nodeId, "n1", "http://10.0.1.1:8080"), "conn-new");
        _service.UpdateAgentContainers("conn-new", new List<string> { containerId });

        // Container should be reachable again
        var resolved = _service.GetAgentForContainer(containerId);
        Assert.NotNull(resolved);
        Assert.Equal(nodeId, resolved.NodeId);
    }

    [Fact]
    public void EndToEnd_MultipleAgents_EachContainerRoutedToCorrectAgent()
    {
        // Simulate a 3-node swarm: 1 manager + 2 workers
        _service.RegisterAgent(MakeRegistrationInfo("manager", "mgr", "http://10.0.0.1:8080", isManager: true), "conn-mgr");
        _service.RegisterAgent(MakeRegistrationInfo("worker-a", "w-a", "http://10.0.0.2:8080"), "conn-a");
        _service.RegisterAgent(MakeRegistrationInfo("worker-b", "w-b", "http://10.0.0.3:8080"), "conn-b");

        _service.UpdateAgentContainers("conn-mgr", new List<string> { "c-mgr-1" });
        _service.UpdateAgentContainers("conn-a", new List<string> { "c-a-1", "c-a-2" });
        _service.UpdateAgentContainers("conn-b", new List<string> { "c-b-1" });

        Assert.Equal("manager", _service.GetAgentForContainer("c-mgr-1")!.NodeId);
        Assert.Equal("worker-a", _service.GetAgentForContainer("c-a-1")!.NodeId);
        Assert.Equal("worker-a", _service.GetAgentForContainer("c-a-2")!.NodeId);
        Assert.Equal("worker-b", _service.GetAgentForContainer("c-b-1")!.NodeId);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static AgentRegistrationInfo MakeRegistrationInfo(
        string nodeId, string nodeName, string url, bool isManager = false)
    {
        return new AgentRegistrationInfo
        {
            NodeId = nodeId,
            NodeName = nodeName,
            InternalUrl = url,
            IsManagerNode = isManager,
            RegisteredAt = DateTime.UtcNow
        };
    }
}
