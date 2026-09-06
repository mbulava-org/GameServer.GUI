using GameServer.API.Configurations;
using GameServer.API.Models;
using GameServer.API.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace GameServer.API.Tests.Services
{
    public class UdpAgentRegistryServiceTests
    {
        private readonly Mock<ILogger<UdpAgentRegistryService>> _loggerMock;
        private readonly UdpAgentDiscoveryOptions _options;
        private readonly UdpAgentRegistryService _service;

        public UdpAgentRegistryServiceTests()
        {
            _loggerMock = new Mock<ILogger<UdpAgentRegistryService>>();
            _options = new UdpAgentDiscoveryOptions
            {
                AnnouncementTtlSeconds = 10
            };
            _service = new UdpAgentRegistryService(_loggerMock.Object, _options);
        }

        [Fact]
        public void UpsertAnnouncement_WithValidAnnouncement_ShouldStoreAgentAndContainers()
        {
            var announcement = new UdpAgentAnnouncement
            {
                NodeId = "node-1",
                NodeName = "Node One",
                InternalUrl = "http://10.0.0.1:5000",
                IsManagerNode = true,
                ContainerIds = new List<string> { "container-a", "container-b" },
                Timestamp = DateTimeOffset.UtcNow
            };

            _service.UpsertAnnouncement(announcement);

            var agents = _service.GetAllAgents();
            Assert.Single(agents);
            Assert.Equal("node-1", agents.First().NodeId);
            Assert.Equal("Node One", agents.First().NodeName);
            Assert.Equal("http://10.0.0.1:5000", agents.First().InternalUrl);

            var agentForA = _service.GetAgentForContainer("container-a");
            Assert.NotNull(agentForA);
            Assert.Equal("node-1", agentForA.NodeId);

            var agentForB = _service.GetAgentForContainer("container-b");
            Assert.NotNull(agentForB);
            Assert.Equal("node-1", agentForB.NodeId);
        }

        [Fact]
        public void UpsertAnnouncement_WithMissingRequiredFields_ShouldBeIgnored()
        {
            var invalidAnnouncement = new UdpAgentAnnouncement
            {
                NodeId = "",
                NodeName = "Node One",
                InternalUrl = "http://10.0.0.1:5000"
            };

            _service.UpsertAnnouncement(invalidAnnouncement);

            Assert.Empty(_service.GetAllAgents());
        }

        [Fact]
        public void UpsertAnnouncement_WhenContainerMoves_ShouldUpdateMapping()
        {
            var node1 = new UdpAgentAnnouncement
            {
                NodeId = "node-1",
                NodeName = "Node One",
                InternalUrl = "http://10.0.0.1:5000",
                ContainerIds = new List<string> { "container-x" }
            };
            _service.UpsertAnnouncement(node1);

            var node2 = new UdpAgentAnnouncement
            {
                NodeId = "node-2",
                NodeName = "Node Two",
                InternalUrl = "http://10.0.0.2:5000",
                ContainerIds = new List<string> { "container-x" }
            };
            _service.UpsertAnnouncement(node2);

            var agent = _service.GetAgentForContainer("container-x");
            Assert.NotNull(agent);
            Assert.Equal("node-2", agent.NodeId);
        }

        [Fact]
        public void RemoveExpired_WhenExpired_ShouldRemoveAgentAndMappings()
        {
            var past = DateTimeOffset.UtcNow.AddMinutes(-5);
            var announcement = new UdpAgentAnnouncement
            {
                NodeId = "node-old",
                NodeName = "Old Node",
                InternalUrl = "http://10.0.0.99:5000",
                ContainerIds = new List<string> { "container-old" },
                Timestamp = past
            };

            _service.UpsertAnnouncement(announcement);

            _service.RemoveExpired(DateTimeOffset.UtcNow);

            Assert.Empty(_service.GetAllAgents());
            Assert.Null(_service.GetAgentForContainer("container-old"));
        }
    }
}
