using GameServer.API.Interfaces;
using GameServer.API.Models;
using GameServer.API.Services;
using GameServer.API.Services.V2;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace GameServer.API.Tests.Services.V2
{
    public class ServerLogAggregatorTests
    {
        private readonly Mock<ILogger<ServerLogAggregator>> _loggerMock;
        private readonly Mock<INodeAgentDiscovery> _discoveryMock;
        private readonly Mock<ILogger<NodeAgentClient>> _clientLoggerMock;
        private readonly IServiceProvider _serviceProvider;
        private readonly ServerLogAggregator _aggregator;

        public ServerLogAggregatorTests()
        {
            _loggerMock = new Mock<ILogger<ServerLogAggregator>>();
            _discoveryMock = new Mock<INodeAgentDiscovery>();
            _clientLoggerMock = new Mock<ILogger<NodeAgentClient>>();

            var services = new ServiceCollection();
            services.AddScoped(_ => _discoveryMock.Object);
            services.AddScoped(_ => new NodeAgentClient(_clientLoggerMock.Object));
            _serviceProvider = services.BuildServiceProvider();

            _aggregator = new ServerLogAggregator(_serviceProvider, _loggerMock.Object);
        }

        [Fact]
        public async Task StreamLogsAsync_WithEmptyServerId_ShouldThrowArgumentException()
        {
            await Assert.ThrowsAnyAsync<ArgumentException>(async () =>
            {
                await foreach (var _ in _aggregator.StreamLogsAsync(""))
                {
                }
            });
        }

        [Fact]
        public async Task DisposeAsync_ShouldCompleteCleanly()
        {
            await _aggregator.DisposeAsync();
        }
    }
}
