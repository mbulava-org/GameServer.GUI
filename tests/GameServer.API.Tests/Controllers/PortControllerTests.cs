using Docker.DotNet.Models;
using GameServer.API.Configurations;
using GameServer.API.Controllers;
using GameServer.API.Interfaces;
using GameServer.API.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace GameServer.API.Tests.Controllers
{
    public class PortControllerTests
    {
        private readonly Mock<IServiceOperations> _serviceOperationsMock;
        private readonly PortAllocation _portOptions;
        private readonly PortAllocator _portAllocator;
        private readonly PortController _controller;

        public PortControllerTests()
        {
            _serviceOperationsMock = new Mock<IServiceOperations>();
            _portOptions = new PortAllocation
            {
                StartPort = 20000,
                EndPort = 20100,
                ReservedPortRanges = Array.Empty<string>()
            };
            _portAllocator = new PortAllocator(_serviceOperationsMock.Object, _portOptions);
            _controller = new PortController(_portAllocator);
        }

        [Fact]
        public async Task Check_WhenPortIsAvailable_ShouldReturnOkTrue()
        {
            _serviceOperationsMock
                .Setup(s => s.ListServicesAsync(default))
                .ReturnsAsync(new List<SwarmService>());

            var result = await _controller.Check("tcp", 20050);

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(true, okResult.Value);
        }

        [Fact]
        public async Task Check_WhenPortIsOccupied_ShouldReturnOkFalse()
        {
            var occupiedService = new SwarmService
            {
                ID = "svc1",
                Endpoint = new Endpoint
                {
                    Ports = new List<PortConfig>
                    {
                        new() { PublishedPort = 20050, Protocol = "tcp" }
                    }
                }
            };

            _serviceOperationsMock
                .Setup(s => s.ListServicesAsync(default))
                .ReturnsAsync(new List<SwarmService> { occupiedService });

            var result = await _controller.Check("tcp", 20050);

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(false, okResult.Value);
        }

        [Fact]
        public async Task Check_WhenPortOutOfRange_ShouldReturnOkFalse()
        {
            var result = await _controller.Check("tcp", 80);

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(false, okResult.Value);
        }
    }
}
