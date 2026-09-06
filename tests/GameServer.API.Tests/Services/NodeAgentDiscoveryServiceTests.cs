using GameServer.API.Configurations;
using GameServer.API.Interfaces;
using GameServer.API.Models;
using GameServer.API.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace GameServer.API.Tests.Services
{
    public class NodeAgentDiscoveryServiceTests
    {
        private readonly Mock<ILogger<NodeAgentDiscoveryService>> _loggerMock;
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<IAgentRegistry> _agentRegistryMock;
        private readonly Mock<IUdpAgentRegistry> _udpAgentRegistryMock;
        private readonly IServiceProvider _serviceProvider;
        private readonly NodeAgentOptions _agentOptions;
        private readonly NodeAgentDiscoveryService _discoveryService;

        public NodeAgentDiscoveryServiceTests()
        {
            _loggerMock = new Mock<ILogger<NodeAgentDiscoveryService>>();
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _agentRegistryMock = new Mock<IAgentRegistry>();
            _udpAgentRegistryMock = new Mock<IUdpAgentRegistry>();

            var services = new ServiceCollection();
            _serviceProvider = services.BuildServiceProvider();

            _agentOptions = new NodeAgentOptions
            {
                EnableBackgroundDiscovery = false
            };

            _discoveryService = new NodeAgentDiscoveryService(
                _loggerMock.Object,
                _httpClientFactoryMock.Object,
                _serviceProvider,
                _agentOptions,
                _agentRegistryMock.Object,
                _udpAgentRegistryMock.Object);
        }

        [Fact]
        public async Task DiscoverAgentsAsync_MergesRegistryAndUdpAgents()
        {
            var registryAgent = new NodeAgentEndpoint
            {
                NodeId = "node-1",
                NodeName = "RegNode",
                InternalUrl = "http://10.0.0.1:5000",
                IsHealthy = true
            };

            var udpAgent = new NodeAgentEndpoint
            {
                NodeId = "node-2",
                NodeName = "UdpNode",
                InternalUrl = "http://10.0.0.2:5000",
                IsHealthy = true
            };

            _agentRegistryMock.Setup(r => r.GetAllAgents()).Returns(new List<NodeAgentEndpoint> { registryAgent });
            _udpAgentRegistryMock.Setup(u => u.GetAllAgents()).Returns(new List<NodeAgentEndpoint> { udpAgent });

            var agents = await _discoveryService.DiscoverAgentsAsync();

            Assert.Equal(2, agents.Count);
            Assert.Contains(agents, a => a.NodeId == "node-1");
            Assert.Contains(agents, a => a.NodeId == "node-2");
        }

        [Fact]
        public async Task GetAgentForContainerAsync_WhenInRegistry_ReturnsRegistryAgent()
        {
            var expectedAgent = new NodeAgentEndpoint
            {
                NodeId = "node-reg",
                NodeName = "RegistryNode",
                InternalUrl = "http://10.0.0.5:5000"
            };

            _agentRegistryMock.Setup(r => r.GetAgentForContainer("c-123")).Returns(expectedAgent);

            var agent = await _discoveryService.GetAgentForContainerAsync("c-123");

            Assert.NotNull(agent);
            Assert.Equal("node-reg", agent.NodeId);
        }

        [Fact]
        public async Task GetAgentForContainerAsync_WhenInUdpRegistry_ReturnsUdpAgent()
        {
            var expectedAgent = new NodeAgentEndpoint
            {
                NodeId = "node-udp",
                NodeName = "UdpNode",
                InternalUrl = "http://10.0.0.6:5000"
            };

            _agentRegistryMock.Setup(r => r.GetAgentForContainer("c-456")).Returns((NodeAgentEndpoint?)null);
            _udpAgentRegistryMock.Setup(u => u.GetAgentForContainer("c-456")).Returns(expectedAgent);

            var agent = await _discoveryService.GetAgentForContainerAsync("c-456");

            Assert.NotNull(agent);
            Assert.Equal("node-udp", agent.NodeId);
        }
    }
}
