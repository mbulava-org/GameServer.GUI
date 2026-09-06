using Docker.DotNet;
using Docker.DotNet.Models;
using GameServer.Docker.Agent.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace GameServer.Docker.Agent.Tests.Controllers
{
    public class TasksControllerTests
    {
        private readonly Mock<IDockerClient> _dockerClientMock;
        private readonly Mock<ITasksOperations> _tasksOperationsMock;
        private readonly Mock<ILogger<TasksController>> _loggerMock;
        private readonly TasksController _controller;

        public TasksControllerTests()
        {
            _dockerClientMock = new Mock<IDockerClient>();
            _tasksOperationsMock = new Mock<ITasksOperations>();
            _loggerMock = new Mock<ILogger<TasksController>>();

            _dockerClientMock.Setup(d => d.Tasks).Returns(_tasksOperationsMock.Object);

            _controller = new TasksController(
                _dockerClientMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task ListTasks_WhenSuccessful_ReturnsOkWithTasks()
        {
            var taskList = new List<TaskResponse>
            {
                new() { ID = "task-1", NodeID = "node-1" },
                new() { ID = "task-2", NodeID = "node-2" }
            };

            _tasksOperationsMock
                .Setup(t => t.ListAsync(It.IsAny<TasksListParameters>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(taskList);

            var result = await _controller.ListTasks();

            var okResult = Assert.IsType<OkObjectResult>(result);
            var json = JsonSerializer.Serialize(okResult.Value);
            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.GetProperty("Success").GetBoolean());
            Assert.Equal(2, doc.RootElement.GetProperty("Count").GetInt32());
        }

        [Fact]
        public async Task ListTasks_WithServiceFilter_PassesFilterParameters()
        {
            _tasksOperationsMock
                .Setup(t => t.ListAsync(It.Is<TasksListParameters>(p => p.Filters != null && p.Filters.ContainsKey("service")), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<TaskResponse>());

            var result = await _controller.ListTasks(serviceId: "svc-123");

            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task ListTasks_WhenExceptionThrown_Returns500()
        {
            _tasksOperationsMock
                .Setup(t => t.ListAsync(It.IsAny<TasksListParameters>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Swarm task query failed"));

            var result = await _controller.ListTasks();

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, objectResult.StatusCode);
        }
    }
}
