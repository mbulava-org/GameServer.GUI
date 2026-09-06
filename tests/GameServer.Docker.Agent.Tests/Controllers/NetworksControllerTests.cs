using Docker.DotNet;
using Docker.DotNet.Models;
using GameServer.Docker.Agent.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace GameServer.Docker.Agent.Tests.Controllers
{
    public class NetworksControllerTests
    {
        private readonly Mock<IDockerClient> _dockerClientMock;
        private readonly Mock<INetworkOperations> _networkOperationsMock;
        private readonly Mock<ILogger<NetworksController>> _loggerMock;
        private readonly NetworksController _controller;

        public NetworksControllerTests()
        {
            _dockerClientMock = new Mock<IDockerClient>();
            _networkOperationsMock = new Mock<INetworkOperations>();
            _loggerMock = new Mock<ILogger<NetworksController>>();

            _dockerClientMock.Setup(d => d.Networks).Returns(_networkOperationsMock.Object);

            _controller = new NetworksController(
                _dockerClientMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task ListNetworks_WhenSuccessful_ReturnsOkWithNetworks()
        {
            var networks = new List<NetworkResponse>
            {
                new() { ID = "net-1", Name = "bridge" },
                new() { ID = "net-2", Name = "custom-overlay" }
            };

            _networkOperationsMock
                .Setup(n => n.ListNetworksAsync(It.IsAny<NetworksListParameters>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(networks);

            var result = await _controller.ListNetworks();

            var okResult = Assert.IsType<OkObjectResult>(result);
            var json = JsonSerializer.Serialize(okResult.Value);
            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.GetProperty("Success").GetBoolean());
            Assert.Equal(2, doc.RootElement.GetProperty("Count").GetInt32());
        }

        [Fact]
        public async Task ListNetworks_WithNameFilter_PassesFilterParameters()
        {
            _networkOperationsMock
                .Setup(n => n.ListNetworksAsync(It.Is<NetworksListParameters>(p => p.Filters != null && p.Filters.ContainsKey("name")), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<NetworkResponse>());

            var result = await _controller.ListNetworks(nameFilter: "my-net");

            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task ListNetworks_WhenExceptionThrown_Returns500()
        {
            _networkOperationsMock
                .Setup(n => n.ListNetworksAsync(It.IsAny<NetworksListParameters>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Docker daemon unavailable"));

            var result = await _controller.ListNetworks();

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, objectResult.StatusCode);
        }

        [Fact]
        public async Task InspectNetwork_WhenSuccessful_ReturnsOk()
        {
            var network = new NetworkResponse { ID = "net-123", Name = "test-net" };

            _networkOperationsMock
                .Setup(n => n.InspectNetworkAsync("net-123", It.IsAny<CancellationToken>()))
                .ReturnsAsync(network);

            var result = await _controller.InspectNetwork("net-123");

            var okResult = Assert.IsType<OkObjectResult>(result);
            var json = JsonSerializer.Serialize(okResult.Value);
            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.GetProperty("Success").GetBoolean());
        }

        [Fact]
        public async Task InspectNetwork_WhenExceptionThrown_Returns500()
        {
            _networkOperationsMock
                .Setup(n => n.InspectNetworkAsync("net-err", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Network not found"));

            var result = await _controller.InspectNetwork("net-err");

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, objectResult.StatusCode);
        }
    }
}
