using System.Text.Json;
using GameServer.Docker.Configurations;
using GameServer.Docker.Dtos.V2;
using GameServer.Docker.Models.V2;
using GameServer.Docker.Services.V2;

namespace GameServer.Docker.Tests.Services.V2;

public sealed class GameServerSpecBuilderTests
{
    [Fact]
    public void Build_WhenRevisionMissing_ShouldReturnNoticeAndNoSpec()
    {
        var preview = new GameServerSpecBuilder(NewNetworkOptions()).Build(
            new SaveGameServerRequestDto { Name = "Test" },
            new GameServerResolutionContext());

        Assert.Empty(preview.EnvironmentVariables);
        Assert.Empty(preview.Ports);
        Assert.Contains(preview.Notices, notice => notice.Contains("could not be resolved", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_ShouldEmitEveryRevisionSettingAsEnvironmentVariable()
    {
        var preview = BuildPreview();

        Assert.Equal(["EULA", "MOTD", "SERVER_PORT"], preview.EnvironmentVariables.Select(entry => entry.Key));
        Assert.Equal("true", preview.EnvironmentVariables.Single(entry => entry.Key == "EULA").Value);
    }

    [Fact]
    public void Build_ShouldExpandServerVariableTokensInEnvironmentValues()
    {
        var preview = BuildPreview();

        var motd = preview.EnvironmentVariables.Single(entry => entry.Key == "MOTD");

        Assert.True(motd.IsExpanded);
        Assert.Equal("Welcome to minecraft", motd.Value);
        Assert.Equal("@vars:Welcome to {GameTypeKey}", motd.RawValue);
    }

    [Fact]
    public void Build_ShouldMapAdvertisedPortsToPublishedPortsOnly()
    {
        var preview = BuildPreview();

        var advertised = preview.Ports.Single(port => port.ContainerPort == 25565);
        var internalPort = preview.Ports.Single(port => port.ContainerPort == 25575);

        Assert.True(advertised.Published);
        Assert.Equal(25565, advertised.PublishedPort);
        Assert.Equal("ingress", advertised.PublishMode);
        Assert.False(internalPort.Published);
    }

    [Fact]
    public void Build_WhenNoVolumesResolved_ShouldAddUnavailableNotice()
    {
        var preview = BuildPreview();

        Assert.Empty(preview.Volumes);
        Assert.Contains(preview.Notices, notice => notice.Contains("Volume/mount resolution is currently unavailable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_ShouldProduceParsableRawServiceSpecJsonWithEnvAndPorts()
    {
        var preview = BuildPreview();

        using var document = JsonDocument.Parse(preview.RawServiceSpecJson);
        var spec = document.RootElement.GetProperty("Service");

        Assert.Equal("minecraft-abc123", spec.GetProperty("Name").GetString());

        var env = spec.GetProperty("TaskTemplate").GetProperty("ContainerSpec").GetProperty("Env")
            .EnumerateArray().Select(item => item.GetString()).ToList();
        Assert.Contains("EULA=true", env);
        Assert.Contains("MOTD=Welcome to minecraft", env);

        var ports = spec.GetProperty("EndpointSpec").GetProperty("Ports");
        Assert.Equal(1, ports.GetArrayLength());
        Assert.Equal(25565u, ports[0].GetProperty("PublishedPort").GetUInt32());
    }

    [Fact]
    public void Build_ShouldAttachTheConfiguredGameServerNetwork()
    {
        var preview = BuildPreview();

        Assert.Equal("gameserver_overlay", Assert.Single(preview.Networks).Name);
    }

    [Fact]
    public void Build_WhenNetworkNameNull_ShouldNotAttachGameServerNetwork()
    {
        var preview = BuildPreview(networkOptions: new NetworkOptions { NetworkName = null, LoadBalancerNetwork = "traefik-public" });

        Assert.Empty(preview.Networks);
    }

    [Fact]
    public void Build_WhenWebHostsEnabled_ShouldAttachLoadBalancerNetwork()
    {
        var preview = BuildPreview(
            networkOptions: new NetworkOptions { NetworkName = "gameserver_overlay", LoadBalancerNetwork = "traefik-public" },
            resolvedWebHosts: [new GameServerResolvedWebHostDto { Name = "web", ContainerPort = 8080 }]);

        Assert.Contains(preview.Networks, network => network.Name == "gameserver_overlay");
        Assert.Contains(preview.Networks, network => network.Name == "traefik-public");
    }

    [Fact]
    public void Build_WhenNoWebHostsEnabled_ShouldNotAttachLoadBalancerNetwork()
    {
        var preview = BuildPreview(
            networkOptions: new NetworkOptions { NetworkName = "gameserver_overlay", LoadBalancerNetwork = "traefik-public" });

        Assert.DoesNotContain(preview.Networks, network => network.Name == "traefik-public");
    }

    private static NetworkOptions NewNetworkOptions() =>
        new() { NetworkName = "gameserver_overlay", LoadBalancerNetwork = "traefik-public" };

    private static GameServerDeploymentPreviewDto BuildPreview(
        NetworkOptions? networkOptions = null,
        IReadOnlyList<GameServerResolvedWebHostDto>? resolvedWebHosts = null)
    {
        var request = new SaveGameServerRequestDto
        {
            ServerId = "abc123",
            Name = "My Server",
            ServiceName = "minecraft-abc123",
            GameTypeRevisionId = 7
        };

        var resolution = new GameServerResolutionContext
        {
            GameType = new GameType { Key = "minecraft" },
            Revision = new GameTypeRevision
            {
                Id = 7,
                VersionTag = "latest",
                ImageReference = "itzg/minecraft-server:latest",
                EnableTTY = true,
                SettingDefinitions =
                [
                    new GameTypeSettingDefinition
                    {
                        SettingKey = "EULA",
                        DefaultValue = "true",
                        DisplayOrder = 1,
                        Metadata = new GameTypeSettingMetadata { DataType = "boolean" }
                    },
                    new GameTypeSettingDefinition
                    {
                        SettingKey = "MOTD",
                        DisplayOrder = 2,
                        Metadata = new GameTypeSettingMetadata { DataType = ServerVariableExpander.ServerVariableDataType }
                    },
                    new GameTypeSettingDefinition
                    {
                        SettingKey = "SERVER_PORT",
                        DefaultValue = "25565",
                        DisplayOrder = 3,
                        Metadata = new GameTypeSettingMetadata { DataType = "port" }
                    }
                ]
            },
            EffectiveSettings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["EULA"] = "true",
                ["MOTD"] = "@vars:Welcome to {GameTypeKey}",
                ["SERVER_PORT"] = "25565"
            },
            Result = new GameServerValidationResultDto
            {
                IsValid = true,
                ResolvedWebHosts = resolvedWebHosts?.ToList() ?? [],
                ResolvedPorts =
                [
                    new GameServerResolvedPortDto { ContainerPort = 25565, Protocol = "tcp", AdvertisedPort = true, DisplayOrder = 1 },
                    new GameServerResolvedPortDto { ContainerPort = 25575, Protocol = "tcp", AdvertisedPort = false, DisplayOrder = 2 }
                ]
            }
        };

        return new GameServerSpecBuilder(networkOptions ?? NewNetworkOptions()).Build(request, resolution);
    }
}
