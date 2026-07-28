using Docker.DotNet;
using Docker.DotNet.Models;
using GameServer.Docker.Agent.Controllers;
using GameServer.Docker.Agent.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using DockerModels = Docker.DotNet.Models;

namespace GameServer.Docker.Agent.Tests.Controllers;

public class ImagesControllerTests
{
    [Fact]
    public async Task InspectImage_WhenImageReferenceMissing_ShouldReturnBadRequest()
    {
        // Arrange
        var controller = new ImagesController(Mock.Of<IDockerClient>(), Mock.Of<ILogger<ImagesController>>());

        // Act
        var result = await controller.InspectImage(new InspectImageRequest(), CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("Image reference is required.", error.Error);
    }

    [Fact]
    public async Task InspectImage_WhenImageExists_ShouldReturnImageDetails()
    {
        // Arrange
        var imageOperations = new Mock<IImageOperations>();
        imageOperations
            .Setup(x => x.InspectImageAsync("itzg/minecraft-server:latest", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DockerModels.ImageInspectResponse
            {
                RepoDigests = ["itzg/minecraft-server@sha256:test"],
                Config = new DockerModels.DockerOCIImageConfig
                {
                    Env = ["EULA=TRUE"],
                    ExposedPorts = new Dictionary<string, DockerModels.EmptyStruct>
                    {
                        ["25565/tcp"] = default
                    },
                    Volumes = new Dictionary<string, DockerModels.EmptyStruct>
                    {
                        ["/data"] = default
                    }
                }
            });

        var dockerClient = new Mock<IDockerClient>();
        dockerClient.SetupGet(x => x.Images).Returns(imageOperations.Object);

        var controller = new ImagesController(dockerClient.Object, Mock.Of<ILogger<ImagesController>>());

        // Act
        var result = await controller.InspectImage(
            new InspectImageRequest { ImageReference = "itzg/minecraft-server:latest" },
            CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<GameServer.Docker.Agent.Models.ImageInspectResponse>(okResult.Value);
        Assert.Equal("itzg/minecraft-server:latest", response.ImageReference);
        Assert.Equal("itzg/minecraft-server@sha256:test", Assert.Single(response.RepoDigests));
        Assert.Equal("EULA=TRUE", Assert.Single(response.EnvironmentVariables));
        Assert.Equal("25565/tcp", Assert.Single(response.ExposedPorts));
        Assert.Equal("/data", Assert.Single(response.VolumePaths));
    }

    [Fact]
    public async Task InspectImage_WhenImageMissingAndPullRequested_ShouldPullThenReturnImageDetails()
    {
        // Arrange
        var imageOperations = new Mock<IImageOperations>();
        imageOperations
            .SetupSequence(x => x.InspectImageAsync("itzg/minecraft-server:latest", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DockerApiException(HttpStatusCode.NotFound, "missing"))
            .ReturnsAsync(new DockerModels.ImageInspectResponse
            {
                RepoDigests = ["itzg/minecraft-server@sha256:test"],
                Config = new DockerModels.DockerOCIImageConfig
                {
                    Env = ["EULA=TRUE"],
                    ExposedPorts = new Dictionary<string, DockerModels.EmptyStruct>
                    {
                        ["25565/tcp"] = default
                    }
                }
            });

        imageOperations
            .Setup(x => x.CreateImageAsync(
                It.Is<DockerModels.ImagesCreateParameters>(p => p.FromImage == "itzg/minecraft-server" && p.Tag == "latest"),
                null,
                It.IsAny<IProgress<DockerModels.JSONMessage>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var dockerClient = new Mock<IDockerClient>();
        dockerClient.SetupGet(x => x.Images).Returns(imageOperations.Object);

        var controller = new ImagesController(dockerClient.Object, Mock.Of<ILogger<ImagesController>>());

        // Act
        var result = await controller.InspectImage(
            new InspectImageRequest { ImageReference = "itzg/minecraft-server:latest", PullIfMissing = true },
            CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        _ = Assert.IsType<GameServer.Docker.Agent.Models.ImageInspectResponse>(okResult.Value);
        imageOperations.Verify(
            x => x.CreateImageAsync(
                It.Is<DockerModels.ImagesCreateParameters>(p => p.FromImage == "itzg/minecraft-server" && p.Tag == "latest"),
                null,
                It.IsAny<IProgress<DockerModels.JSONMessage>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
