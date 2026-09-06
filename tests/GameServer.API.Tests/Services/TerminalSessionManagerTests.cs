using GameServer.API.Interfaces;
using GameServer.API.Models;
using GameServer.API.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace GameServer.API.Tests.Services
{
    public class TerminalSessionManagerTests
    {
        private readonly Mock<ILogger<TerminalSessionManager>> _loggerMock;
        private readonly Mock<ITerminalSessionNotifier> _notifierMock;
        private readonly Mock<INodeAgentDiscovery> _discoveryMock;
        private readonly Mock<IServerResourceMonitor> _monitorMock;
        private readonly IServiceProvider _serviceProvider;
        private readonly TerminalSessionManager _sessionManager;

        public TerminalSessionManagerTests()
        {
            _loggerMock = new Mock<ILogger<TerminalSessionManager>>();
            _notifierMock = new Mock<ITerminalSessionNotifier>();
            _discoveryMock = new Mock<INodeAgentDiscovery>();
            _monitorMock = new Mock<IServerResourceMonitor>();

            var services = new ServiceCollection();
            services.AddSingleton(_monitorMock.Object);
            _serviceProvider = services.BuildServiceProvider();

            _sessionManager = new TerminalSessionManager(
                _loggerMock.Object,
                _notifierMock.Object,
                _discoveryMock.Object,
                _serviceProvider);
        }

        [Fact]
        public async Task StartSessionAsync_WhenAgentNotFound_ShouldReturnFailure()
        {
            _discoveryMock
                .Setup(d => d.GetAgentForContainerAsync("unknown-container"))
                .ReturnsAsync((NodeAgentEndpoint?)null);

            var (success, error) = await _sessionManager.StartSessionAsync("conn-1", "unknown-container");

            Assert.False(success);
            Assert.Contains("not found", error ?? string.Empty);
        }

        [Fact]
        public async Task StartSessionAsync_WhenTargetIsServerId_ResolvesViaMonitorAndFailsIfNoAgent()
        {
            _discoveryMock
                .Setup(d => d.GetAgentForContainerAsync("srv-1"))
                .ReturnsAsync((NodeAgentEndpoint?)null);

            _monitorMock
                .Setup(m => m.GetSnapshotAsync("srv-1"))
                .ReturnsAsync(new ServerResourceUsage
                {
                    ServerId = "srv-1",
                    ContainerIds = new List<string> { "resolved-c-1" }
                });

            _discoveryMock
                .Setup(d => d.GetAgentForContainerAsync("resolved-c-1"))
                .ReturnsAsync((NodeAgentEndpoint?)null);

            var (success, error) = await _sessionManager.StartSessionAsync("conn-2", "srv-1");

            Assert.False(success);
            Assert.Contains("resolved-c-1", error ?? string.Empty);
        }

        [Fact]
        public async Task SendInputAsync_WhenSessionDoesNotExist_ShouldNotThrow()
        {
            await _sessionManager.SendInputAsync("nonexistent-conn", "ls -la\n");
        }

        [Fact]
        public async Task CloseSessionAsync_WhenSessionDoesNotExist_ShouldNotThrow()
        {
            await _sessionManager.CloseSessionAsync("nonexistent-conn");
        }

        [Fact]
        public void TerminalSession_ModelProperties_CanBeSetAndRetrieved()
        {
            var now = DateTime.UtcNow;
            var session = new TerminalSessionManager.TerminalSession
            {
                ConnectionId = "c1",
                ContainerId = "cont1",
                Shell = "/bin/bash",
                AgentUrl = "http://agent:8080",
                StartTime = now
            };

            Assert.Equal("c1", session.ConnectionId);
            Assert.Equal("cont1", session.ContainerId);
            Assert.Equal("/bin/bash", session.Shell);
            Assert.Equal("http://agent:8080", session.AgentUrl);
            Assert.Equal(now, session.StartTime);
        }

        [Fact]
        public async Task StartSessionAsync_WhenWebSocketFailsToConnect_ReturnsFailure()
        {
            _discoveryMock
                .Setup(d => d.GetAgentForContainerAsync("c-fail"))
                .ReturnsAsync(new NodeAgentEndpoint
                {
                    NodeId = "n-1",
                    NodeName = "node-1",
                    InternalUrl = "http://127.0.0.1:59999",
                    IsHealthy = true
                });

            var (success, error) = await _sessionManager.StartSessionAsync("conn-fail", "c-fail");
            Assert.False(success);
            Assert.NotNull(error);
        }

        [Fact]
        public async Task StartSessionAsync_WhenMonitorThrows_CatchesAndContinues()
        {
            _discoveryMock.Setup(d => d.GetAgentForContainerAsync("srv-err")).ReturnsAsync((NodeAgentEndpoint?)null);
            _monitorMock.Setup(m => m.GetSnapshotAsync("srv-err")).ThrowsAsync(new InvalidOperationException("DB fail"));

            var (success, error) = await _sessionManager.StartSessionAsync("conn-err", "srv-err");
            Assert.False(success);
        }
    }
}
