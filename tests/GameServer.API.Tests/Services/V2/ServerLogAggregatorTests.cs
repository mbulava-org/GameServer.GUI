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
        private readonly Mock<IGameServerQueryService> _queryServiceMock;
        private readonly IServiceProvider _serviceProvider;
        private readonly ServerLogAggregator _aggregator;

        public ServerLogAggregatorTests()
        {
            _loggerMock = new Mock<ILogger<ServerLogAggregator>>();
            _discoveryMock = new Mock<INodeAgentDiscovery>();
            _queryServiceMock = new Mock<IGameServerQueryService>();

            var services = new ServiceCollection();
            services.AddSingleton(_discoveryMock.Object);
            services.AddSingleton(_queryServiceMock.Object);
            services.AddSingleton(new NodeAgentClient(Mock.Of<ILogger<NodeAgentClient>>()));
            _serviceProvider = services.BuildServiceProvider();

            _aggregator = new ServerLogAggregator(_serviceProvider, _loggerMock.Object);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task StreamLogsAsync_WhenServerIdNullOrEmpty_ThrowsArgumentException(string? serverId)
        {
            await Assert.ThrowsAnyAsync<ArgumentException>(async () =>
            {
                await foreach (var line in _aggregator.StreamLogsAsync(serverId!))
                {
                }
            });
        }

        [Fact]
        public async Task StreamLogsAsync_WhenServerNotFound_CompletesEmpty()
        {
            _queryServiceMock
                .Setup(q => q.GetByServerIdAsync("nonexistent", It.IsAny<CancellationToken>()))
                .ReturnsAsync((GameServer.API.Dtos.V2.GameServerDetailDto?)null);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var lines = new List<string>();

            await foreach (var line in _aggregator.StreamLogsAsync("nonexistent", cancellationToken: cts.Token))
            {
                lines.Add(line);
            }

            Assert.Empty(lines);
        }

        [Fact]
        public async Task StreamLogsAsync_WhenAgentNotFound_CompletesEmpty()
        {
            var serverDetail = new GameServer.API.Dtos.V2.GameServerDetailDto
            {
                ServerId = "srv-logs-1"
            };
            _queryServiceMock
                .Setup(q => q.GetByServerIdAsync("srv-logs-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(serverDetail);

            _discoveryMock
                .Setup(d => d.GetAgentForServerAsync("srv-logs-1"))
                .ReturnsAsync((NodeAgentEndpoint?)null);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var lines = new List<string>();

            await foreach (var line in _aggregator.StreamLogsAsync("srv-logs-1", cancellationToken: cts.Token))
            {
                lines.Add(line);
            }

            Assert.Empty(lines);
        }

        [Fact]
        public async Task DisposeAsync_CleansUpSourcesGracefully()
        {
            await _aggregator.DisposeAsync();
        }
    }
}
