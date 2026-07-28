using GameServer.Docker.Controllers.V2;
using GameServer.Docker.Dtos.V2;
using GameServer.Docker.Models.V2;
using GameServer.Docker.Repositories.V2;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace GameServer.Docker.Tests.Controllers.V2;

public class MountTypeConfigControllerTests
{
    [Fact]
    public async Task Get_WhenConfigurationExists_ShouldReturnDto()
    {
        var repository = new Mock<IMountTypeConfigRepository>();
        repository.Setup(x => x.GetByKeyAsync("volume", It.IsAny<CancellationToken>())).ReturnsAsync(CreateSampleConfig());
        var controller = new MountTypeConfigController(repository.Object, Mock.Of<ILogger<MountTypeConfigController>>());

        var result = await controller.Get("volume", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<MountTypeConfigDto>(ok.Value);
        Assert.Equal("volume", dto.Key);
        Assert.Equal("local", dto.Driver);
        Assert.Equal("{gameTypeKey}_{serverId}_{Source}", dto.SourcePathTemplate);
    }

    [Fact]
    public async Task Get_WhenConfigurationMissing_ShouldReturnNotFound()
    {
        var repository = new Mock<IMountTypeConfigRepository>();
        repository.Setup(x => x.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((MountTypeConfig?)null);
        var controller = new MountTypeConfigController(repository.Object, Mock.Of<ILogger<MountTypeConfigController>>());

        var result = await controller.Get("missing", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Save_WhenPayloadIsValid_ShouldCallRepositorySave()
    {
        var repository = new Mock<IMountTypeConfigRepository>();
        repository.Setup(x => x.SaveAsync(It.IsAny<MountTypeConfig>(), It.IsAny<CancellationToken>())).ReturnsAsync((MountTypeConfig c, CancellationToken _) => c);
        var controller = new MountTypeConfigController(repository.Object, Mock.Of<ILogger<MountTypeConfigController>>());
        var update = new MountTypeConfigDto
        {
            Key = "nfs",
            DisplayName = "NFS volume",
            Driver = "vieux/sshfs",
            DriverOptionsJson = "{\"type\":\"nfs\"}",
            SourcePathTemplate = "{gameTypeKey}_{serverId}_{Source}",
            ContainerPathTemplate = "{Source}"
        };

        var response = await controller.Save("nfs", update, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var dto = Assert.IsType<MountTypeConfigDto>(ok.Value);
        Assert.Equal("nfs", dto.Key);
        repository.Verify(x => x.SaveAsync(It.Is<MountTypeConfig>(c =>
            c.Key == "nfs" &&
            c.DisplayName == "NFS volume" &&
            c.Driver == "vieux/sshfs"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Save_WhenPayloadIsNull_ShouldReturnBadRequest()
    {
        var controller = new MountTypeConfigController(Mock.Of<IMountTypeConfigRepository>(), Mock.Of<ILogger<MountTypeConfigController>>());

        var response = await controller.Save("nfs", null!, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response.Result);
    }

    [Fact]
    public async Task Delete_WhenKeyIsValid_ShouldCallRepositoryDelete()
    {
        var repository = new Mock<IMountTypeConfigRepository>();
        repository.Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var controller = new MountTypeConfigController(repository.Object, Mock.Of<ILogger<MountTypeConfigController>>());

        var response = await controller.Delete("nfs", CancellationToken.None);

        Assert.IsType<NoContentResult>(response);
        repository.Verify(x => x.DeleteAsync("nfs", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static MountTypeConfig CreateSampleConfig()
    {
        return new MountTypeConfig
        {
            Key = "volume",
            DisplayName = "Docker volume",
            Driver = "local",
            SourcePathTemplate = "{gameTypeKey}_{serverId}_{Source}",
            ContainerPathTemplate = "{Source}"
        };
    }
}
