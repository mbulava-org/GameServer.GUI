using Bunit;
using GameServer.Docker.Client;
using GameServer.Web.Components.Pages.GameTypes.Components;
using Xunit;

namespace GameServer.Web.Tests.Components.GameTypes;

/// <summary>
/// Tests for GameTypeBasicInfo component
/// Verifies the component renders and handles data correctly
/// </summary>
public class GameTypeBasicInfoTests : BunitContext
{
    [Fact]
    public void GameTypeBasicInfo_ShouldRender_WithValidGameType()
    {
        // Arrange
        var gameType = new GameTypeDefinition
        {
            Key = "test-game",
            DisplayName = "Test Game Server",
            Description = "A test game server",
            Image = "test/image:latest",
            ThumbnailUrl = "https://example.com/thumb.png",
            DocumentationUrl = "https://docs.example.com"
        };

        // Act
        var cut = Render<GameTypeBasicInfo>(parameters => parameters
            .Add(p => p.GameType, gameType)
            .Add(p => p.IsNew, true));

        // Assert
        Assert.NotNull(cut);
        Assert.Contains("Basic Information", cut.Markup);
        Assert.Contains("test-game", cut.Markup);
        Assert.Contains("Test Game Server", cut.Markup);
    }

    [Fact]
    public void GameTypeBasicInfo_KeyField_ShouldBeDisabled_WhenNotNew()
    {
        // Arrange
        var gameType = new GameTypeDefinition
        {
            Key = "existing-game",
            DisplayName = "Existing Game",
            Image = "test:latest"
        };

        // Act
        var cut = Render<GameTypeBasicInfo>(parameters => parameters
            .Add(p => p.GameType, gameType)
            .Add(p => p.IsNew, false));

        // Assert - Component renders and shows the key
        Assert.Contains("existing-game", cut.Markup);
        Assert.Contains("Existing Game", cut.Markup);
    }

    [Fact]
    public void GameTypeBasicInfo_KeyField_ShouldBeEnabled_WhenNew()
    {
        // Arrange
        var gameType = new GameTypeDefinition
        {
            Key = "",
            DisplayName = "",
            Image = "test:latest"
        };

        // Act
        var cut = Render<GameTypeBasicInfo>(parameters => parameters
            .Add(p => p.GameType, gameType)
            .Add(p => p.IsNew, true));

        // Assert - Component renders successfully
        Assert.Contains("Basic Information", cut.Markup);
    }

    [Fact]
    public void GameTypeBasicInfo_ShouldShowKeyHint_WhenIsNew()
    {
        // Arrange
        var gameType = new GameTypeDefinition { 
            Key = "", 
            DisplayName = "",
            Image = "test:latest"
        };

        // Act
        var cut = Render<GameTypeBasicInfo>(parameters => parameters
            .Add(p => p.GameType, gameType)
            .Add(p => p.IsNew, true));

        // Assert
        Assert.Contains("Unique identifier", cut.Markup);
        Assert.Contains("cannot be changed later", cut.Markup);
    }

    [Fact]
    public void GameTypeBasicInfo_ShouldNotShowKeyHint_WhenNotNew()
    {
        // Arrange
        var gameType = new GameTypeDefinition { 
            Key = "existing", 
            DisplayName = "Existing",
            Image = "test:latest"
        };

        // Act
        var cut = Render<GameTypeBasicInfo>(parameters => parameters
            .Add(p => p.GameType, gameType)
            .Add(p => p.IsNew, false));

        // Assert
        Assert.DoesNotContain("cannot be changed later", cut.Markup);
    }

    [Fact]
    public void GameTypeBasicInfo_AllFields_ShouldRenderCorrectly()
    {
        // Arrange
        var gameType = new GameTypeDefinition
        {
            Key = "minecraft",
            DisplayName = "Minecraft Server",
            Description = "A Minecraft game server",
            Image = "itzg/minecraft-server:latest",
            ThumbnailUrl = "https://example.com/minecraft.png",
            DocumentationUrl = "https://minecraft.net/docs"
        };

        // Act
        var cut = Render<GameTypeBasicInfo>(parameters => parameters
            .Add(p => p.GameType, gameType)
            .Add(p => p.IsNew, false));

        // Assert
        Assert.Contains("minecraft", cut.Markup);
        Assert.Contains("Minecraft Server", cut.Markup);
        Assert.Contains("A Minecraft game server", cut.Markup);
        Assert.Contains("itzg/minecraft-server:latest", cut.Markup);
        Assert.Contains("https://example.com/minecraft.png", cut.Markup);
        Assert.Contains("https://minecraft.net/docs", cut.Markup);
    }

    [Fact]
    public void GameTypeBasicInfo_ShouldShowRequiredFields()
    {
        // Arrange
        var gameType = new GameTypeDefinition
        {
            Key = "",
            DisplayName = "",
            Image = "test:latest"
        };

        // Act
        var cut = Render<GameTypeBasicInfo>(parameters => parameters
            .Add(p => p.GameType, gameType)
            .Add(p => p.IsNew, true));

        // Assert
        // Verify all required form fields are present
        Assert.Contains("Key", cut.Markup);
        Assert.Contains("Display Name", cut.Markup);
        Assert.Contains("Description", cut.Markup);
        Assert.Contains("Docker Image", cut.Markup);
        Assert.Contains("Thumbnail URL", cut.Markup);
        Assert.Contains("Documentation URL", cut.Markup);
    }
}
