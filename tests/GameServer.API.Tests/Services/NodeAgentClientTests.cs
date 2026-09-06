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

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task SendAttachInputAsync_WhenInputInvalid_ShouldThrowArgumentException(string? invalidInput)
        {
            using var ws = new ClientWebSocket();
            await Assert.ThrowsAnyAsync<ArgumentException>(() =>
                NodeAgentClient.SendAttachInputAsync(ws, invalidInput!));
        }

        [Fact]
        public async Task SendAttachInputAsync_WhenWebSocketNotOpen_ShouldThrowInvalidOperationException()
        {
            using var ws = new ClientWebSocket();
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                NodeAgentClient.SendAttachInputAsync(ws, "test input"));
        }

        [Theory]
        [InlineData("", "container-1")]
        [InlineData("   ", "container-1")]
        [InlineData(null, "container-1")]
        public async Task StreamContainerAttachAsync_WhenAgentUrlInvalid_ThrowsArgumentException(string? url, string containerId)
        {
            await Assert.ThrowsAnyAsync<ArgumentException>(async () =>
            {
                await foreach (var _ in _client.StreamContainerAttachAsync(url!, containerId))
                {
                }
            });
        }

        [Theory]
        [InlineData("http://agent:8080", "")]
        [InlineData("http://agent:8080", "   ")]
        [InlineData("http://agent:8080", null)]
        public async Task StreamContainerAttachAsync_WhenContainerIdInvalid_ThrowsArgumentException(string url, string? containerId)
        {
            await Assert.ThrowsAnyAsync<ArgumentException>(async () =>
            {
                await foreach (var _ in _client.StreamContainerAttachAsync(url, containerId!))
                {
                }
            });
        }
    }
}
