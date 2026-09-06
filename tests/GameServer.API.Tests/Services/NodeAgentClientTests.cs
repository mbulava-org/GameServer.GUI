using System.Net.WebSockets;
using GameServer.API.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace GameServer.API.Tests.Services
{
    public class NodeAgentClientTests
    {
        private readonly Mock<ILogger<NodeAgentClient>> _loggerMock;
        private readonly NodeAgentClient _client;

        public NodeAgentClientTests()
        {
            _loggerMock = new Mock<ILogger<NodeAgentClient>>();
            _client = new NodeAgentClient(_loggerMock.Object);
        }

        [Fact]
        public async Task DisposeAsync_WhenNoConnections_ShouldCompleteCleanly()
        {
            await _client.DisposeAsync();
        }

        [Fact]
        public async Task SendAttachInputAsync_WhenWebSocketNull_ShouldThrowArgumentNullException()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                NodeAgentClient.SendAttachInputAsync(null!, "test"));
        }

        [Fact]
        public async Task SendAttachInputAsync_WhenInputEmpty_ShouldThrowArgumentException()
        {
            using var ws = new ClientWebSocket();
            await Assert.ThrowsAnyAsync<ArgumentException>(() =>
                NodeAgentClient.SendAttachInputAsync(ws, ""));
        }
    }
}
