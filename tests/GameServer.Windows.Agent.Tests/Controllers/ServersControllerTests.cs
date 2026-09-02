using GameServer.Windows.Agent.Controllers;
using GameServer.Windows.Agent.Interfaces;
using GameServer.Windows.Agent.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace GameServer.Windows.Agent.Tests.Controllers;

public class ServersControllerTests
{
    private readonly Mock<IGameProcessManager> _processManagerMock;
    private readonly Mock<ILogger<ServersController>> _loggerMock;
    private readonly ServersController _controller;

    public ServersControllerTests()
    {
        _processManagerMock = new Mock<IGameProcessManager>();
        _loggerMock = new Mock<ILogger<ServersController>>();
        _controller = new ServersController(_processManagerMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void GetAllServers_ReturnsAllServersList()
    {
        // Arrange
        var servers = new List<GameServerProcessInfo>
        {
            new() { ServerId = "conan-01", Name = "Conan Exiles Server", Status = ServerProcessStatus.Running, ProcessId = 1234 },
            new() { ServerId = "palworld-01", Name = "Palworld Server", Status = ServerProcessStatus.Stopped }
        };
        _processManagerMock.Setup(m => m.GetAllServers()).Returns(servers);

        // Act
        var result = _controller.GetAllServers();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<GameServerProcessInfo>>(okResult.Value);
        Assert.Equal(2, returned.Count);
    }

    [Fact]
    public void GetServer_WhenExists_ReturnsOkWithServerInfo()
    {
        // Arrange
        var info = new GameServerProcessInfo
        {
            ServerId = "conan-01",
            Name = "Conan Server",
            Status = ServerProcessStatus.Running,
            ProcessId = 5678
        };
        _processManagerMock.Setup(m => m.GetServerInfo("conan-01")).Returns(info);

        // Act
        var result = _controller.GetServer("conan-01");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var server = Assert.IsType<GameServerProcessInfo>(okResult.Value);
        Assert.Equal("conan-01", server.ServerId);
    }

    [Fact]
    public void GetServer_WhenDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        _processManagerMock.Setup(m => m.GetServerInfo("missing")).Returns((GameServerProcessInfo?)null);

        // Act
        var result = _controller.GetServer("missing");

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task StartServer_WhenValid_ReturnsOk()
    {
        // Arrange
        var request = new StartServerRequest
        {
            ServerId = "conan-01",
            Name = "Conan Server",
            ExecutablePath = "ConanSandboxServer.exe",
            Arguments = "-log -Port=7777 -QueryPort=27015"
        };
        var expected = new GameServerProcessInfo
        {
            ServerId = "conan-01",
            Name = "Conan Server",
            Status = ServerProcessStatus.Running,
            ProcessId = 9999
        };
        _processManagerMock.Setup(m => m.StartServerAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _controller.StartServer(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var info = Assert.IsType<GameServerProcessInfo>(okResult.Value);
        Assert.Equal(ServerProcessStatus.Running, info.Status);
    }

    [Fact]
    public async Task StartServer_WhenExecutableNotFound_ReturnsNotFound()
    {
        // Arrange
        var request = new StartServerRequest
        {
            ServerId = "conan-01",
            ExecutablePath = "missing.exe"
        };
        _processManagerMock.Setup(m => m.StartServerAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException("Executable not found"));

        // Act
        var result = await _controller.StartServer(request, CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task StopServer_WhenExists_ReturnsOk()
    {
        // Arrange
        var expected = new GameServerProcessInfo
        {
            ServerId = "conan-01",
            Status = ServerProcessStatus.Stopped
        };
        _processManagerMock.Setup(m => m.StopServerAsync("conan-01", It.IsAny<StopServerRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _controller.StopServer("conan-01", new StopServerRequest { GracefulTimeoutSeconds = 30 }, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var info = Assert.IsType<GameServerProcessInfo>(okResult.Value);
        Assert.Equal(ServerProcessStatus.Stopped, info.Status);
    }

    [Fact]
    public async Task StopServer_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        _processManagerMock.Setup(m => m.StopServerAsync("missing", It.IsAny<StopServerRequest?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());

        // Act
        var result = await _controller.StopServer("missing", null, CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task RestartServer_WhenExists_ReturnsOk()
    {
        // Arrange
        var expected = new GameServerProcessInfo
        {
            ServerId = "conan-01",
            Status = ServerProcessStatus.Running
        };
        _processManagerMock.Setup(m => m.RestartServerAsync("conan-01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _controller.RestartServer("conan-01", CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var info = Assert.IsType<GameServerProcessInfo>(okResult.Value);
        Assert.Equal(ServerProcessStatus.Running, info.Status);
    }

    [Fact]
    public void GetLogs_WhenExists_ReturnsLogs()
    {
        // Arrange
        var expectedLogs = new ProcessLogsResponse
        {
            ServerId = "conan-01",
            Logs = ["[2026.09.02-12.00.00] LogConan: Server initialized."]
        };
        _processManagerMock.Setup(m => m.GetLogs("conan-01", 50)).Returns(expectedLogs);

        // Act
        var result = _controller.GetLogs("conan-01", 50);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var logs = Assert.IsType<ProcessLogsResponse>(okResult.Value);
        Assert.Single(logs.Logs);
    }

    [Fact]
    public void GetStats_WhenRunning_ReturnsStats()
    {
        // Arrange
        var expectedStats = new ProcessStatsSnapshot
        {
            ServerId = "conan-01",
            CpuPercent = 4.2,
            MemoryWorkingSetBytes = 2L * 1024 * 1024 * 1024,
            ThreadCount = 32
        };
        _processManagerMock.Setup(m => m.GetStats("conan-01")).Returns(expectedStats);

        // Act
        var result = _controller.GetStats("conan-01");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var stats = Assert.IsType<ProcessStatsSnapshot>(okResult.Value);
        Assert.Equal(4.2, stats.CpuPercent);
    }

    [Fact]
    public async Task SendCommand_WhenSuccess_ReturnsOk()
    {
        // Arrange
        var request = new SendCommandRequest { Command = "Broadcast Server restarting in 5 minutes" };
        var expected = new SendCommandResponse { Success = true, Response = "Broadcast sent." };

        _processManagerMock.Setup(m => m.SendCommandAsync("conan-01", request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _controller.SendCommand("conan-01", request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SendCommandResponse>(okResult.Value);
        Assert.True(response.Success);
    }
}
