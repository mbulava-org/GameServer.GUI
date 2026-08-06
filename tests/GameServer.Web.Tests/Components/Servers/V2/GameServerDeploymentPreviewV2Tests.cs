using Bunit;
using GameServer.Web.Components.Pages.Servers;
using GameServer.Web.Models.V2;

namespace GameServer.Web.Tests.Components.Servers.V2;

public sealed class GameServerDeploymentPreviewV2Tests : BunitContext
{
    public GameServerDeploymentPreviewV2Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void WithoutPreview_ShouldShowGenerateAction()
    {
        var cut = Render<GameServerDeploymentPreviewV2>();

        Assert.Contains("Generate Preview", cut.Markup);
        Assert.DoesNotContain("Raw Service Spec", cut.Markup);
    }

    [Fact]
    public void WithPreview_ShouldRenderServiceEnvironmentAndPortDetails()
    {
        var cut = Render<GameServerDeploymentPreviewV2>(parameters => parameters
            .Add(p => p.Preview, CreatePreview()));

        Assert.Contains("minecraft-abc123", cut.Markup);
        Assert.Contains("itzg/minecraft-server:latest", cut.Markup);
        Assert.Contains("gameserver_overlay", cut.Markup);
        Assert.Contains("MOTD", cut.Markup);
        Assert.Contains("Welcome to minecraft", cut.Markup);
        Assert.Contains("25565", cut.Markup);
    }

    [Fact]
    public void WithPreview_ShouldRenderNoticesAndBlockingIssues()
    {
        var cut = Render<GameServerDeploymentPreviewV2>(parameters => parameters
            .Add(p => p.Preview, CreatePreview()));

        Assert.Contains("Volume resolution unavailable", cut.Markup);
        Assert.Contains("PortSettingInvalid", cut.Markup);
    }

    [Fact]
    public void RawServiceSpec_ShouldBeHiddenUntilToggled()
    {
        var cut = Render<GameServerDeploymentPreviewV2>(parameters => parameters
            .Add(p => p.Preview, CreatePreview()));

        Assert.DoesNotContain("\"ServiceSpec\"", cut.Markup);

        cut.FindAll("button").First(button => button.TextContent.Contains("Show")).Click();

        Assert.Contains("\"ServiceSpec\"", cut.Markup);
    }

    private static GameServerDeploymentPreview CreatePreview()
    {
        return new GameServerDeploymentPreview
        {
            ServiceName = "minecraft-abc123",
            ServerId = "abc123",
            GameTypeKey = "minecraft",
            ImageReference = "itzg/minecraft-server:latest",
            VersionTag = "latest",
            EnableTTY = true,
            VolumeBindingLayout = "standard",
            Labels = new Dictionary<string, string> { ["gameserver.docker.managed"] = "true" },
            Networks =
            [
                new GameServerPreviewNetwork { Name = "gameserver_overlay", Driver = "overlay" }
            ],
            EnvironmentVariables =
            [
                new GameServerPreviewEnvironmentVariable
                {
                    Key = "MOTD",
                    Value = "Welcome to minecraft",
                    RawValue = "@vars:Welcome to {GameTypeKey}",
                    DataType = "servervariable",
                    IsExpanded = true
                }
            ],
            Ports =
            [
                new GameServerPreviewPort
                {
                    ContainerPort = 25565,
                    PublishedPort = 25565,
                    Protocol = "tcp",
                    Published = true,
                    PublishMode = "ingress"
                }
            ],
            Issues =
            [
                new GameServerValidationIssue
                {
                    Code = "PortSettingInvalid",
                    Message = "Setting 'SERVER_PORT' must resolve to a valid port.",
                    Scope = "settings:SERVER_PORT",
                    Severity = "Error",
                    IsBlocking = true
                }
            ],
            Notices = ["Volume resolution unavailable while mount-type configuration is validated."],
            RawServiceSpecJson = "{ \"ServiceSpec\": { \"Name\": \"minecraft-abc123\" } }"
        };
    }
}
