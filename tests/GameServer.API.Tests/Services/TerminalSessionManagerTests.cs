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
        private readonly IServiceProvider _serviceProvider;
        private readonly TerminalSessionManager _sessionManager;

        public TerminalSessionManagerTests()
        {
            _loggerMock = new Mock<ILogger<TerminalSessionManager>>();
            _notifierMock = new Mock<ITerminalSessionNotifier>();
            _discoveryMock = new Mock<INodeAgentDiscovery>();

            var services = new ServiceCollection();
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
        public async Task SendInputAsync_WhenSessionDoesNotExist_ShouldNotThrow()
        {
            await _sessionManager.SendInputAsync("nonexistent-conn", "ls -la\n");
        }

        [Fact]
        public async Task CloseSessionAsync_WhenSessionDoesNotExist_ShouldNotThrow()
        {
            await _sessionManager.CloseSessionAsync("nonexistent-conn");
        }
    }
}
