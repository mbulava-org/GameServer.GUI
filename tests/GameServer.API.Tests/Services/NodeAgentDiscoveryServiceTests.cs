using GameServer.API.Configurations;
using GameServer.API.Interfaces;
using GameServer.API.Models;
using GameServer.API.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;

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
                EnableBackgroundDiscovery = false,
                TimeoutSeconds = 5
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

        [Fact]
        public async Task GetAgentForContainerAsync_WhenNotInRegistries_ProbesHealthyAgents()
        {
            var agent1 = new NodeAgentEndpoint
            {
                NodeId = "node-1",
                InternalUrl = "http://agent1:5000",
                IsHealthy = true
            };

            _agentRegistryMock.Setup(r => r.GetAgentForContainer("c-probe")).Returns((NodeAgentEndpoint?)null);
            _udpAgentRegistryMock.Setup(u => u.GetAgentForContainer("c-probe")).Returns((NodeAgentEndpoint?)null);
            _agentRegistryMock.Setup(r => r.GetAllAgents()).Returns(new List<NodeAgentEndpoint> { agent1 });
            _udpAgentRegistryMock.Setup(u => u.GetAllAgents()).Returns(new List<NodeAgentEndpoint>());

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/containers/c-probe/stats")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            var httpClient = new HttpClient(handlerMock.Object);
            _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var result = await _discoveryService.GetAgentForContainerAsync("c-probe");

            Assert.NotNull(result);
            Assert.Equal("node-1", result.NodeId);
        }

        [Fact]
        public async Task GetAgentForContainerAsync_WhenProbeFails_ReturnsNull()
        {
            var agent1 = new NodeAgentEndpoint
            {
                NodeId = "node-1",
                InternalUrl = "http://agent1:5000",
                IsHealthy = true
            };

            _agentRegistryMock.Setup(r => r.GetAgentForContainer("c-fail")).Returns((NodeAgentEndpoint?)null);
            _udpAgentRegistryMock.Setup(u => u.GetAgentForContainer("c-fail")).Returns((NodeAgentEndpoint?)null);
            _agentRegistryMock.Setup(r => r.GetAllAgents()).Returns(new List<NodeAgentEndpoint> { agent1 });
            _udpAgentRegistryMock.Setup(u => u.GetAllAgents()).Returns(new List<NodeAgentEndpoint>());

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));

            var httpClient = new HttpClient(handlerMock.Object);
            _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var result = await _discoveryService.GetAgentForContainerAsync("c-fail");

            Assert.Null(result);
        }

        [Fact]
        public async Task GetAgentForServerAsync_WhenNoAgentsAvailable_ReturnsNull()
        {
            _agentRegistryMock.Setup(r => r.GetAllAgents()).Returns(new List<NodeAgentEndpoint>());
            _udpAgentRegistryMock.Setup(u => u.GetAllAgents()).Returns(new List<NodeAgentEndpoint>());

            var result = await _discoveryService.GetAgentForServerAsync("srv-missing");

            Assert.Null(result);
        }

        [Fact]
        public async Task GetAgentForServerAsync_WhenAgentHasContainer_ReturnsAgent()
        {
            var agent = new NodeAgentEndpoint
            {
                NodeId = "node-srv",
                InternalUrl = "http://agent-srv:5000",
                IsHealthy = true
            };

            _agentRegistryMock.Setup(r => r.GetAllAgents()).Returns(new List<NodeAgentEndpoint> { agent });
            _udpAgentRegistryMock.Setup(u => u.GetAllAgents()).Returns(new List<NodeAgentEndpoint>());

            var json = JsonSerializer.Serialize(new[] { new { id = "cont-123" } });
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/containers?label=")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json)
                });

            var httpClient = new HttpClient(handlerMock.Object);
            _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var result = await _discoveryService.GetAgentForServerAsync("srv-found");

            Assert.NotNull(result);
            Assert.Equal("node-srv", result.NodeId);
        }

        [Fact]
        public async Task GetContainerStatsAsync_WhenAgentFound_ParsesStatsPayload()
        {
            var agent = new NodeAgentEndpoint
            {
                NodeId = "node-stats",
                InternalUrl = "http://agent-stats:5000",
                IsHealthy = true
            };

            _agentRegistryMock.Setup(r => r.GetAgentForContainer("c-stat")).Returns(agent);

            var payload = new
            {
                timestamp = DateTime.UtcNow,
                cpu = new { usagePercent = 45.5, totalUsage = 1000UL, systemUsage = 5000UL, onlineCpus = 4U },
                memory = new { usageBytes = 1048576UL, limitBytes = 4194304UL, usagePercent = 25.0, maxUsageBytes = 2097152UL },
                network = new { rxBytes = 500L, txBytes = 600L },
                blockIo = new { readBytes = 100L, writeBytes = 200L },
                pids = 12UL
            };

            var json = JsonSerializer.Serialize(payload);
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/containers/c-stat/stats")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json)
                });

            var httpClient = new HttpClient(handlerMock.Object);
            _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var stats = await _discoveryService.GetContainerStatsAsync("c-stat");

            Assert.NotNull(stats);
            Assert.Equal(45.5, stats.CpuUsagePercent);
            Assert.Equal(1048576UL, stats.MemoryUsageBytes);
            Assert.Equal(500L, stats.NetworkRxBytes);
            Assert.Equal(100L, stats.BlockReadBytes);
            Assert.Equal(12UL, stats.Pids);
        }

        [Fact]
        public async Task GetContainerStatsAsync_WhenAgentNotFound_ReturnsNull()
        {
            _agentRegistryMock.Setup(r => r.GetAgentForContainer("c-none")).Returns((NodeAgentEndpoint?)null);
            _udpAgentRegistryMock.Setup(u => u.GetAgentForContainer("c-none")).Returns((NodeAgentEndpoint?)null);
            _agentRegistryMock.Setup(r => r.GetAllAgents()).Returns(new List<NodeAgentEndpoint>());
            _udpAgentRegistryMock.Setup(u => u.GetAllAgents()).Returns(new List<NodeAgentEndpoint>());

            var stats = await _discoveryService.GetContainerStatsAsync("c-none");

            Assert.Null(stats);
        }

        [Fact]
        public async Task GetContainerLogsAsync_WhenAgentFound_ReturnsLogsList()
        {
            var agent = new NodeAgentEndpoint
            {
                NodeId = "node-logs",
                InternalUrl = "http://agent-logs:5000",
                IsHealthy = true
            };

            _agentRegistryMock.Setup(r => r.GetAgentForContainer("c-logs")).Returns(agent);

            var payload = new { logs = new[] { "Line 1", "Line 2", "Line 3" } };
            var json = JsonSerializer.Serialize(payload);

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/containers/c-logs/logs")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json)
                });

            var httpClient = new HttpClient(handlerMock.Object);
            _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var logs = await _discoveryService.GetContainerLogsAsync("c-logs", 50);

            Assert.NotNull(logs);
            Assert.Equal(3, logs.Count);
            Assert.Equal("Line 1", logs[0]);
        }

        [Fact]
        public async Task GetServiceLogsAsync_WhenManagerAvailable_ReturnsServiceLogs()
        {
            var managerAgent = new NodeAgentEndpoint
            {
                NodeId = "mgr-node",
                InternalUrl = "http://mgr-agent:5000",
                IsManagerNode = true,
                IsHealthy = true
            };

            _agentRegistryMock.Setup(r => r.GetAllAgents()).Returns(new List<NodeAgentEndpoint> { managerAgent });
            _udpAgentRegistryMock.Setup(u => u.GetAllAgents()).Returns(new List<NodeAgentEndpoint>());

            var payload = new
            {
                success = true,
                data = new
                {
                    logs = new[] { "Swarm service log 1", "Swarm service log 2" }
                }
            };
            var json = JsonSerializer.Serialize(payload);

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/api/services/svc-1/logs")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json)
                });

            var httpClient = new HttpClient(handlerMock.Object);
            _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var logs = await _discoveryService.GetServiceLogsAsync("svc-1", 100);

            Assert.NotNull(logs);
            Assert.Equal(2, logs.Count);
            Assert.Equal("Swarm service log 1", logs[0]);
        }

        [Fact]
        public async Task GetServiceLogsAsync_WhenNoManagerAgent_ReturnsNull()
        {
            var workerAgent = new NodeAgentEndpoint
            {
                NodeId = "worker-node",
                InternalUrl = "http://worker:5000",
                IsManagerNode = false,
                IsHealthy = true
            };

            _agentRegistryMock.Setup(r => r.GetAllAgents()).Returns(new List<NodeAgentEndpoint> { workerAgent });
            _udpAgentRegistryMock.Setup(u => u.GetAllAgents()).Returns(new List<NodeAgentEndpoint>());

            var logs = await _discoveryService.GetServiceLogsAsync("svc-1");

            Assert.Null(logs);
        }
    }
}
