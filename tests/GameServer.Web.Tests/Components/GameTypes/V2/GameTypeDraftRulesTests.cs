using GameServer.Web.Components.Pages.GameTypes.Components.V2;

namespace GameServer.Web.Tests.Components.GameTypes.V2;

public sealed class GameTypeDraftRulesTests
{
    [Fact]
    public void ValidateBasicInfo_WhenValid_ShouldReturnNoIssues()
    {
        var issues = GameTypeDraftRules.ValidateBasicInfo(
            key: "valheim",
            displayName: "Valheim Dedicated Server",
            type: "docker",
            thumbnailUrl: "https://example.com/thumb.png",
            documentationUrl: "https://example.com/docs");

        Assert.Empty(issues);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateBasicInfo_WhenKeyIsMissing_ShouldReturnRequiredIssue(string? key)
    {
        var issues = GameTypeDraftRules.ValidateBasicInfo(
            key: key,
            displayName: "Minecraft",
            type: "docker",
            thumbnailUrl: null,
            documentationUrl: null);

        Assert.Contains(issues, i => i.Contains("Key is required"));
    }

    [Fact]
    public void ValidateBasicInfo_WhenKeyExceeds100Characters_ShouldReturnLengthIssue()
    {
        var longKey = new string('k', 101);
        var issues = GameTypeDraftRules.ValidateBasicInfo(
            key: longKey,
            displayName: "Minecraft",
            type: "docker",
            thumbnailUrl: null,
            documentationUrl: null);

        Assert.Contains(issues, i => i.Contains("Key cannot exceed 100 characters"));
    }

    [Fact]
    public void ValidateBasicInfo_WhenDisplayNameExceeds200Characters_ShouldReturnLengthIssue()
    {
        var longName = new string('n', 201);
        var issues = GameTypeDraftRules.ValidateBasicInfo(
            key: "minecraft",
            displayName: longName,
            type: "docker",
            thumbnailUrl: null,
            documentationUrl: null);

        Assert.Contains(issues, i => i.Contains("Display Name cannot exceed 200 characters"));
    }

    [Fact]
    public void ValidateBasicInfo_WhenTypeExceeds50Characters_ShouldReturnLengthIssue()
    {
        var longType = new string('t', 51);
        var issues = GameTypeDraftRules.ValidateBasicInfo(
            key: "minecraft",
            displayName: "Minecraft",
            type: longType,
            thumbnailUrl: null,
            documentationUrl: null);

        Assert.Contains(issues, i => i.Contains("Type cannot exceed 50 characters"));
    }

    [Fact]
    public void ValidateBasicInfo_WhenThumbnailUrlExceeds500Characters_ShouldReturnLengthIssue()
    {
        var longUrl = "https://example.com/" + new string('a', 500);
        var issues = GameTypeDraftRules.ValidateBasicInfo(
            key: "minecraft",
            displayName: "Minecraft",
            type: "docker",
            thumbnailUrl: longUrl,
            documentationUrl: null);

        Assert.Contains(issues, i => i.Contains("Thumbnail URL cannot exceed 500 characters"));
    }

    [Fact]
    public void ValidateBasicInfo_WhenDocumentationUrlExceeds500Characters_ShouldReturnLengthIssue()
    {
        var longUrl = "https://example.com/docs/" + new string('d', 500);
        var issues = GameTypeDraftRules.ValidateBasicInfo(
            key: "minecraft",
            displayName: "Minecraft",
            type: "docker",
            thumbnailUrl: null,
            documentationUrl: longUrl);

        Assert.Contains(issues, i => i.Contains("Documentation URL cannot exceed 500 characters"));
    }
}
