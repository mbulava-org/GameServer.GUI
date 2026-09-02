using GameServer.Windows.Agent.Controllers;
using GameServer.Windows.Agent.Interfaces;
using GameServer.Windows.Agent.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace GameServer.Windows.Agent.Tests.Controllers;

public class SteamCmdControllerTests
{
    private readonly Mock<ISteamCmdService> _steamCmdMock;
    private readonly Mock<ILogger<SteamCmdController>> _loggerMock;
    private readonly SteamCmdController _controller;

    public SteamCmdControllerTests()
    {
        _steamCmdMock = new Mock<ISteamCmdService>();
        _loggerMock = new Mock<ILogger<SteamCmdController>>();
        _controller = new SteamCmdController(_steamCmdMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void CheckInstalled_ReturnsValueFromService()
    {
        // Arrange
        _steamCmdMock.Setup(s => s.IsInstalled()).Returns(true);

        // Act
        var result = _controller.CheckInstalled();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(true, okResult.Value);
    }

    [Fact]
    public async Task EnsureInstalled_WhenSuccessful_ReturnsOk()
    {
        // Arrange
        _steamCmdMock.Setup(s => s.EnsureSteamCmdInstalledAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.EnsureInstalled(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        _steamCmdMock.Verify(s => s.EnsureSteamCmdInstalledAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnsureInstalled_WhenFails_Returns500()
    {
        // Arrange
        _steamCmdMock.Setup(s => s.EnsureSteamCmdInstalledAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Download failed"));

        // Act
        var result = await _controller.EnsureInstalled(CancellationToken.None);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    [Fact]
    public async Task InstallOrUpdateApp_WhenValid_ReturnsJobResult()
    {
        // Arrange
        var request = new SteamAppInstallRequest
        {
            AppId = 443030,
            InstallDirectory = @"C:\GameServers\instances\conan-01",
            Validate = true
        };
        var expectedJob = new SteamCmdJobResult
        {
            Success = true,
            AppId = 443030,
            ExitCode = 0
        };

        _steamCmdMock.Setup(s => s.InstallOrUpdateAppAsync(request, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedJob);

        // Act
        var result = await _controller.InstallOrUpdateApp(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var jobResult = Assert.IsType<SteamCmdJobResult>(okResult.Value);
        Assert.True(jobResult.Success);
        Assert.Equal(443030u, jobResult.AppId);
    }

    [Fact]
    public async Task InstallOrUpdateApp_WhenArgumentException_ReturnsBadRequest()
    {
        // Arrange
        var request = new SteamAppInstallRequest { AppId = 0 };
        _steamCmdMock.Setup(s => s.InstallOrUpdateAppAsync(request, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("AppId must be greater than 0."));

        // Act
        var result = await _controller.InstallOrUpdateApp(request, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task DownloadWorkshopItem_WhenValid_ReturnsJobResult()
    {
        // Arrange
        var request = new SteamWorkshopDownloadRequest
        {
            AppId = 443030,
            WorkshopItemId = 880454836
        };
        var expectedJob = new SteamCmdJobResult
        {
            Success = true,
            AppId = 443030,
            ExitCode = 0
        };

        _steamCmdMock.Setup(s => s.DownloadWorkshopItemAsync(request, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedJob);

        // Act
        var result = await _controller.DownloadWorkshopItem(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var jobResult = Assert.IsType<SteamCmdJobResult>(okResult.Value);
        Assert.True(jobResult.Success);
    }

    [Fact]
    public void GetAppStatus_ReturnsStatusFromService()
    {
        // Arrange
        var expected = new SteamAppStatusResponse
        {
            AppId = 443030,
            IsInstalled = true,
            InstallDirectory = @"C:\GameServers\instances\conan-01",
            TotalSizeBytes = 50L * 1024 * 1024 * 1024,
            Executables = ["ConanSandboxServer.exe"]
        };

        _steamCmdMock.Setup(s => s.GetAppStatus(443030, @"C:\GameServers\instances\conan-01"))
            .Returns(expected);

        // Act
        var result = _controller.GetAppStatus(443030, @"C:\GameServers\instances\conan-01");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SteamAppStatusResponse>(okResult.Value);
        Assert.True(response.IsInstalled);
        Assert.Contains("ConanSandboxServer.exe", response.Executables);
    }
}
