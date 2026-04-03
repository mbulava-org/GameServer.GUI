using Docker.DotNet;
using Docker.DotNet.Models;
using GameServer.Docker.Dtos.V2;
using GameServer.Docker.Models.V2;
using GameServer.Docker.Repositories.V2;
using GameServer.Docker.Services.V2.Detection;
using Microsoft.Extensions.Logging;
using Moq;

namespace GameServer.Docker.Tests.Services.V2.Detection;

public class GameTypeSetupDetectionServiceTests
{
    [Fact]
    public async Task DetectAsync_WhenDockerClientUnavailable_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var repository = new Mock<IGameTypeRepository>();
        repository
            .Setup(x => x.GetByKeyAsync("minecraft"))
            .ReturnsAsync(new GameType
            {
                Key = "minecraft",
                ImageReference = "itzg/minecraft-server"
            });

        var service = new GameTypeSetupDetectionService(repository.Object, dockerClient: null, Mock.Of<ILogger<GameTypeSetupDetectionService>>());

        // Act
        var action = () => service.DetectAsync("minecraft", new DetectGameTypeSetupRequestDto { VersionTag = "latest" });

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(action);
    }

    [Fact]
    public async Task DetectAsync_WhenGameTypeMissing_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        var repository = new Mock<IGameTypeRepository>();
        repository
            .Setup(x => x.GetByKeyAsync("missing"))
            .ReturnsAsync((GameType?)null);

        var dockerClient = new Mock<IDockerClient>();
        var service = new GameTypeSetupDetectionService(repository.Object, dockerClient.Object, Mock.Of<ILogger<GameTypeSetupDetectionService>>());

        // Act
        var action = () => service.DetectAsync("missing", new DetectGameTypeSetupRequestDto { VersionTag = "latest" });

        // Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(action);
    }

    [Fact]
    public async Task DetectAsync_WhenPortSettingMatchesDefinedPort_ShouldInferDirectMapping()
    {
        // Arrange
        var service = CreateService(
            environmentVariables: ["SERVER_PORT=25565"],
            exposedPorts: new Dictionary<string, EmptyStruct>
            {
                ["25565/tcp"] = default
            });

        // Act
        var result = await service.DetectAsync("minecraft", new DetectGameTypeSetupRequestDto { VersionTag = "latest" });

        // Assert
        var setting = Assert.Single(result.Settings);
        var mapping = Assert.Single(setting.PortMappings);
        Assert.Equal("25565", setting.DefaultValue);
        Assert.Equal(GameTypeSettingPortMappingRole.Primary.ToString(), mapping.MappingRole);
        Assert.Equal(GameTypeSettingPortRelationType.Direct.ToString(), mapping.RelationType);
        Assert.Equal(25565, mapping.TargetContainerPort);
        Assert.Equal("tcp", mapping.TargetProtocol);
    }

    [Fact]
    public async Task DetectAsync_WhenPrimaryPortSettingHasMultipleExposedPorts_ShouldInferRelatedMappings()
    {
        // Arrange
        var service = CreateService(
            environmentVariables: ["SERVER_PORT=25565"],
            exposedPorts: new Dictionary<string, EmptyStruct>
            {
                ["25565/tcp"] = default,
                ["25565/udp"] = default,
                ["25566/udp"] = default
            });

        // Act
        var result = await service.DetectAsync("minecraft", new DetectGameTypeSetupRequestDto { VersionTag = "latest" });

        // Assert
        var setting = Assert.Single(result.Settings);
        Assert.Equal(3, setting.PortMappings.Count);
        Assert.Contains(setting.PortMappings, mapping =>
            mapping.MappingRole == GameTypeSettingPortMappingRole.Primary.ToString()
            && mapping.RelationType == GameTypeSettingPortRelationType.Direct.ToString()
            && mapping.TargetContainerPort == 25565
            && mapping.TargetProtocol == "tcp");
        Assert.Contains(setting.PortMappings, mapping =>
            mapping.MappingRole == GameTypeSettingPortMappingRole.Related.ToString()
            && mapping.RelationType == GameTypeSettingPortRelationType.Direct.ToString()
            && mapping.TargetContainerPort == 25565
            && mapping.TargetProtocol == "udp");
        Assert.Contains(setting.PortMappings, mapping =>
            mapping.MappingRole == GameTypeSettingPortMappingRole.Related.ToString()
            && mapping.RelationType == GameTypeSettingPortRelationType.Offset.ToString()
            && mapping.TargetContainerPort == 25566
            && mapping.TargetProtocol == "udp"
            && mapping.CalculationValue == 1);
    }

    private static GameTypeSetupDetectionService CreateService(IList<string> environmentVariables, IDictionary<string, EmptyStruct> exposedPorts)
    {
        var repository = new Mock<IGameTypeRepository>();
        repository
            .Setup(x => x.GetByKeyAsync("minecraft"))
            .ReturnsAsync(new GameType
            {
                Key = "minecraft",
                ImageReference = "itzg/minecraft-server"
            });

        var imageOperations = new Mock<IImageOperations>();
        imageOperations
            .Setup(x => x.InspectImageAsync("itzg/minecraft-server:latest", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImageInspectResponse
            {
                RepoDigests = ["itzg/minecraft-server@sha256:test"],
                Config = new Config
                {
                    Env = environmentVariables,
                    ExposedPorts = exposedPorts
                }
            });

        var dockerClient = new Mock<IDockerClient>();
        dockerClient.SetupGet(x => x.Images).Returns(imageOperations.Object);

        return new GameTypeSetupDetectionService(repository.Object, dockerClient.Object, Mock.Of<ILogger<GameTypeSetupDetectionService>>());
    }
}
