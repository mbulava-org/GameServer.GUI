using System.Text.Json;
using GameServer.API.Configurations;
using GameServer.API.Dtos.V2;
using GameServer.API.Models.V2;
using GameServer.API.Services.V2;

namespace GameServer.API.Tests.Services.V2;

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
        Assert.Equal(25565u, ports[0].GetProperty("TargetPort").GetUInt32());
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

    [Fact]
    public void Build_WhenPublishedPortDiffersFromContainerPort_ShouldSetTargetPortToContainerAndPublishedPortToPublished()
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
                Ports =
                [
                    new GameTypePort { ContainerPort = 25565, Protocol = "tcp", AdvertisedPort = true, DisplayOrder = 1 }
                ]
            },
            Result = new GameServerValidationResultDto
            {
                IsValid = true,
                ResolvedPorts =
                [
                    new GameServerResolvedPortDto { ContainerPort = 26000, Protocol = "tcp", AdvertisedPort = true, DisplayOrder = 1 }
                ]
            }
        };

        var preview = new GameServerSpecBuilder(NewNetworkOptions()).Build(request, resolution);

        var port = Assert.Single(preview.Ports);
        Assert.Equal(25565, port.ContainerPort);
        Assert.Equal(26000, port.PublishedPort);

        using var document = JsonDocument.Parse(preview.RawServiceSpecJson);
        var spec = document.RootElement.GetProperty("Service");
        var ports = spec.GetProperty("EndpointSpec").GetProperty("Ports");
        Assert.Equal(1, ports.GetArrayLength());
        Assert.Equal(25565u, ports[0].GetProperty("TargetPort").GetUInt32());
        Assert.Equal(26000u, ports[0].GetProperty("PublishedPort").GetUInt32());
    }

    [Fact]
    public void Build_WithMultiplePublishedPorts_PublishesAllToIngress()
    {
        var request = new SaveGameServerRequestDto
        {
            ServerId = "abc123",
            Name = "MultiPort Server",
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
                Ports =
                [
                    new GameTypePort { ContainerPort = 25565, Protocol = "tcp", AdvertisedPort = true, DisplayOrder = 1 },
                    new GameTypePort { ContainerPort = 25565, Protocol = "udp", AdvertisedPort = false, DisplayOrder = 2 },
                    new GameTypePort { ContainerPort = 25575, Protocol = "tcp", AdvertisedPort = false, DisplayOrder = 3 }
                ]
            },
            Result = new GameServerValidationResultDto
            {
                IsValid = true,
                ResolvedPorts =
                [
                    new GameServerResolvedPortDto { ContainerPort = 25565, PublishedPort = 25565, Protocol = "tcp", AdvertisedPort = true, DisplayOrder = 1 },
                    new GameServerResolvedPortDto { ContainerPort = 25565, PublishedPort = 25565, Protocol = "udp", AdvertisedPort = false, DisplayOrder = 2 },
                    new GameServerResolvedPortDto { ContainerPort = 25575, PublishedPort = 25575, Protocol = "tcp", AdvertisedPort = false, DisplayOrder = 3 }
                ]
            }
        };

        var preview = new GameServerSpecBuilder(NewNetworkOptions()).Build(request, resolution);

        Assert.Equal(3, preview.Ports.Count);
        Assert.All(preview.Ports, p => Assert.True(p.Published));

        using var document = JsonDocument.Parse(preview.RawServiceSpecJson);
        var ports = document.RootElement.GetProperty("Service").GetProperty("EndpointSpec").GetProperty("Ports");
        Assert.Equal(3, ports.GetArrayLength());
    }

    [Fact]
    public void Build_WhenWebHostsEnabled_GeneratesTraefikLabelsWithRewriteMiddleware()
    {
        var preview = BuildPreview(
            networkOptions: new NetworkOptions
            {
                NetworkName = "gameserver_overlay",
                LoadBalancerNetwork = "traefik-public",
                LoadBalancerProvider = "traefik",
                WebHostsAllowedEntryPoint = "websecure",
                CertificateResolverName = "myresolver",
                EnableResponseBodyRewrite = true,
                ResponseBodyRewritePluginName = "rewritebody"
            },
            resolvedWebHosts:
            [
                new GameServerResolvedWebHostDto
                {
                    Name = "Dynmap",
                    PathSegment = "map/{serverId}",
                    ContainerPort = 8123,
                    DisplayOrder = 1
                }
            ]);

        Assert.Contains(preview.Labels, kv => kv.Key == "traefik.enable" && kv.Value == "true");
        Assert.Contains(preview.Labels, kv => kv.Key == "traefik.docker.network" && kv.Value == "traefik-public");
        Assert.Contains(preview.Labels, kv => kv.Key == "traefik.http.routers.minecraft-abc123-dynmap.rule" && kv.Value == "PathRegexp(`^/map/abc123(/.*)?$`)");
        Assert.Contains(preview.Labels, kv => kv.Key == "traefik.http.routers.minecraft-abc123-dynmap.priority" && kv.Value == "10000");
        Assert.Contains(preview.Labels, kv => kv.Key == "traefik.http.routers.minecraft-abc123-dynmap.entrypoints" && kv.Value == "websecure");
        Assert.Contains(preview.Labels, kv => kv.Key == "traefik.http.routers.minecraft-abc123-dynmap.tls" && kv.Value == "true");
        Assert.Contains(preview.Labels, kv => kv.Key == "traefik.http.routers.minecraft-abc123-dynmap.tls.certresolver" && kv.Value == "myresolver");
        Assert.Contains(preview.Labels, kv => kv.Key == "traefik.http.routers.minecraft-abc123-dynmap.middlewares" && kv.Value == "minecraft-abc123-dynmap-rewrite,minecraft-abc123-dynmap-body-rewrite");
        Assert.Contains(preview.Labels, kv => kv.Key == "traefik.http.middlewares.minecraft-abc123-dynmap-rewrite.replacepathregex.regex" && kv.Value == "^/map/abc123/?(.*)");
        Assert.Contains(preview.Labels, kv => kv.Key == "traefik.http.middlewares.minecraft-abc123-dynmap-rewrite.replacepathregex.replacement" && kv.Value == "/$1");
        Assert.Contains(preview.Labels, kv => kv.Key == "traefik.http.middlewares.minecraft-abc123-dynmap-body-rewrite.plugin.rewritebody.lastModified" && kv.Value == "true");
        Assert.Contains(preview.Labels, kv => kv.Key == "traefik.http.middlewares.minecraft-abc123-dynmap-body-rewrite.plugin.rewritebody.rewrites[0].regex" && kv.Value.Contains("href|src|action"));
        Assert.Contains(preview.Labels, kv => kv.Key == "traefik.http.middlewares.minecraft-abc123-dynmap-body-rewrite.plugin.rewritebody.rewrites[0].replacement" && kv.Value == "$1/map/abc123/");
        Assert.Contains(preview.Labels, kv => kv.Key == "traefik.http.middlewares.minecraft-abc123-dynmap-body-rewrite.plugin.rewritebody.rewrites[1].regex" && kv.Value.Contains("url"));
        Assert.Contains(preview.Labels, kv => kv.Key == "traefik.http.middlewares.minecraft-abc123-dynmap-body-rewrite.plugin.rewritebody.rewrites[1].replacement" && kv.Value == "$1/map/abc123/");
        Assert.Contains(preview.Labels, kv => kv.Key == "traefik.http.services.minecraft-abc123-dynmap.loadbalancer.server.port" && kv.Value == "8123");
    }

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
                    new GameServerResolvedPortDto { ContainerPort = 25565, PublishedPort = 25565, Protocol = "tcp", AdvertisedPort = true, DisplayOrder = 1 },
                    new GameServerResolvedPortDto { ContainerPort = 25575, PublishedPort = 0, Protocol = "tcp", AdvertisedPort = false, DisplayOrder = 2 }
                ]
            }
        };

        return new GameServerSpecBuilder(networkOptions ?? NewNetworkOptions()).Build(request, resolution);
    }
}
