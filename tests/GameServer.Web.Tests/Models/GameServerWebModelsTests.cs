using GameServer.Web.Models.V2;

namespace GameServer.Web.Tests.Models;

public class GameServerWebModelsTests
{
    [Fact]
    public void GameServerDeploymentPreview_Properties_ShouldSetAndGet()
    {
        var preview = new GameServerDeploymentPreview
        {
            ServiceName = "svc-1",
            ServerId = "srv-1",
            GameTypeKey = "minecraft",
            ImageReference = "itzg/minecraft-server",
            VersionTag = "1.21.2",
            EnableTTY = true,
            VolumeBindingLayout = "standard",
            Labels = new Dictionary<string, string> { ["managed"] = "true" },
            Networks = [new GameServerPreviewNetwork { Name = "net1", Driver = "overlay", Description = "Desc" }],
            Containers = [new GameServerPreviewContainer { Id = "cnt1", Name = "container-1" }],
            EnvironmentVariables = [new GameServerPreviewEnvironmentVariable { Key = "E1", Value = "V1", Category = "settings" }],
            Ports = [new GameServerPreviewPort { PublishedPort = 25565, ContainerPort = 25565, Protocol = "tcp", Published = true }],
            Volumes = [new GameServerPreviewVolume { VolumeName = "vol1", ContainerPath = "/data", MountType = "nfs", ReadOnly = false, OwnerUid = 1000, OwnerGid = 1000, Permissions = "0755" }],
            Issues = [new GameServerValidationIssue { Code = "W1", Message = "Warning", Severity = "Warning", IsBlocking = false }],
            Notices = ["Notice 1"],
            RawServiceSpecJson = "{}"
        };

        Assert.Equal("svc-1", preview.ServiceName);
        Assert.Equal("srv-1", preview.ServerId);
        Assert.Equal("minecraft", preview.GameTypeKey);
        Assert.Single(preview.Networks);
        Assert.Single(preview.Containers);
        Assert.Single(preview.EnvironmentVariables);
        Assert.Single(preview.Ports);
        Assert.Single(preview.Volumes);
        Assert.Single(preview.Issues);
        Assert.Single(preview.Notices);
        Assert.Equal("{}", preview.RawServiceSpecJson);
    }

    [Fact]
    public void GameServerV2Models_Properties_ShouldSetAndGet()
    {
        var item = new GameServerListItem
        {
            ServerId = "srv-1",
            Name = "Server 1",
            Description = "Desc",
            Status = "Running",
            GameTypeKey = "factorio",
            GameTypeDisplayName = "Minecraft",
            GameTypeThumbnailUrl = "https://example.com/icon.png",
            RevisionVersionTag = "1.0",
            GameTypeRevisionId = 1,
            ResolvedPorts = [new GameServerResolvedPort { ContainerPort = 34197, Protocol = "udp", DisplayOrder = 0 }],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            LastDeployedAt = DateTime.UtcNow
        };

        Assert.Equal("srv-1", item.ServerId);
        Assert.Equal("Server 1", item.Name);
        Assert.Equal("Running", item.Status);
        Assert.Single(item.ResolvedPorts);

        var detail = new GameServerDetail
        {
            ServerId = "srv-1",
            Name = "Server 1",
            Description = "Desc",
            GameTypeRevisionId = 1,
            ServiceName = "factorio-srv-1",
            Status = "Running",
            GameTypeDisplayName = "Factorio",
            GameTypeThumbnailUrl = "https://example.com/icon.png",
            RevisionVersionTag = "1.0",
            RevisionImageReference = "factoriotools/factorio",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            LastDeployedAt = DateTime.UtcNow,
            Settings = [new GameServerSetting { SettingKey = "PORT", Value = "34197" }],
            Containers = [new GameServerContainer { Id = "c1", Name = "cnt1" }],
            ResolvedPorts = [new GameServerResolvedPort { ContainerPort = 34197, Protocol = "udp" }],
            ResolvedVolumes = [new GameServerResolvedVolume { VolumeName = "vol1", ContainerPath = "/factorio", MountType = "volume", ReadOnly = false, IsProvisioned = true, DriverOptionsJson = "{}" }],
            ResolvedWebHosts = [new GameServerResolvedWebHost { Name = "map", PathSegment = "map", ContainerPort = 8080 }],
            DockerVolumeOptions = [new GameServerConfigurationOption { Key = "opt1", DisplayName = "Option 1", Value = "val", Required = false }]
        };

        Assert.Equal("srv-1", detail.ServerId);
        Assert.Single(detail.Settings);
        Assert.Single(detail.Containers);
        Assert.Single(detail.ResolvedPorts);
        Assert.Single(detail.ResolvedVolumes);
        Assert.Single(detail.ResolvedWebHosts);
        Assert.Single(detail.DockerVolumeOptions);
    }

    [Fact]
    public void GameTypeV2Commands_PortablePackage_ShouldSetAndGet()
    {
        var package = new PortableGameTypePackage
        {
            FormatVersion = "1.0",
            GameType = new PortableGameType
            {
                Key = "valheim",
                DisplayName = "Valheim",
                Description = "Valheim Dedicated Server",
                ThumbnailUrl = "https://example.com/valheim.png",
                Revisions =
                [
                    new PortableGameTypeRevision
                    {
                        VersionTag = "0.217.46",
                        ImageReference = "lloesche/valheim-server",
                        EnableTTY = false,
                        Ports = [new PortableGameTypePort { ContainerPort = 2456, Protocol = "udp", AdvertisedPort = true }],
                        Volumes = [new PortableGameTypeVolume { Source = "/config", Usage = "config" }],
                        SettingDefinitions =
                        [
                            new PortableGameTypeSettingDefinition
                            {
                                SettingKey = "SERVER_NAME",
                                DefaultValue = "My Valheim Server",
                                Metadata = new PortableGameTypeSettingMetadata
                                {
                                    DataType = "string",
                                    IsRequired = true,
                                    PortMappings = [new PortableGameTypeSettingPortMapping { MappingRole = "Primary", RelationType = "Direct", TargetContainerPort = 2456, TargetProtocol = "udp" }]
                                }
                            }
                        ],
                        WebHosts = [new PortableGameTypeWebHost { Name = "admin", ContainerPort = 8080 }]
                    }
                ]
            }
        };

        Assert.Equal("1.0", package.FormatVersion);
        Assert.Equal("valheim", package.GameType.Key);
        var rev = Assert.Single(package.GameType.Revisions);
        Assert.Equal("0.217.46", rev.VersionTag);
        Assert.Single(rev.Ports);
        Assert.Single(rev.Volumes);
        Assert.Single(rev.SettingDefinitions);
        Assert.Single(rev.WebHosts);
    }

    [Fact]
    public void MountTypeConfig_Options_ShouldStoreAndRetrieveValues()
    {
        var config = new MountTypeConfig
        {
            Key = "nfs",
            DisplayName = "NFS Mount",
            Options = new Dictionary<string, string>
            {
                ["Driver"] = "local",
                ["DefaultPermissions"] = "0775"
            }
        };

        Assert.Equal("local", config.Options?["Driver"]);
        Assert.Equal("0775", config.Options?["DefaultPermissions"]);
    }

    [Fact]
    public void ServerVariableSetting_EncodeAndDecode_ShouldWorkCorrectly()
    {
        Assert.Equal("servervariable", ServerVariableSetting.DataType);
        Assert.Contains("ServerId", ServerVariableSetting.SupportedTokens);

        // Decode
        var (expand1, raw1) = ServerVariableSetting.Decode("@vars:Welcome {Name}");
        Assert.True(expand1);
        Assert.Equal("Welcome {Name}", raw1);

        var (expand2, raw2) = ServerVariableSetting.Decode("@literal:@vars:test");
        Assert.False(expand2);
        Assert.Equal("@vars:test", raw2);

        var (expand3, raw3) = ServerVariableSetting.Decode("plain text");
        Assert.False(expand3);
        Assert.Equal("plain text", raw3);

        var (expand4, raw4) = ServerVariableSetting.Decode(null);
        Assert.False(expand4);
        Assert.Null(raw4);

        // Encode
        Assert.Equal("@vars:Hello", ServerVariableSetting.Encode(true, "Hello"));
        Assert.Equal("@literal:@vars:Hello", ServerVariableSetting.Encode(false, "@vars:Hello"));
        Assert.Equal("Plain", ServerVariableSetting.Encode(false, "Plain"));
    }
}
