using GameServer.Docker.Models.V2;
using GameServer.Docker.Services.V2;

namespace GameServer.Docker.Tests.Services.V2;

public sealed class ServerVariableExpanderTests
{
    [Fact]
    public void Encode_WhenExpansionEnabled_ShouldPrefixValue()
    {
        Assert.Equal("@vars:{Name}-world", ServerVariableExpander.Encode(true, "{Name}-world"));
    }

    [Fact]
    public void Encode_WhenExpansionDisabled_ShouldEscapeReservedPrefixes()
    {
        Assert.Equal("@literal:@vars:raw", ServerVariableExpander.Encode(false, "@vars:raw"));
        Assert.Equal("plain", ServerVariableExpander.Encode(false, "plain"));
    }

    [Fact]
    public void Decode_ShouldRoundTripEncodedValues()
    {
        var (expandEnabled, rawEnabled) = ServerVariableExpander.Decode(ServerVariableExpander.Encode(true, "{Name}"));
        Assert.True(expandEnabled);
        Assert.Equal("{Name}", rawEnabled);

        var (expandLiteral, rawLiteral) = ServerVariableExpander.Decode(ServerVariableExpander.Encode(false, "@vars:x"));
        Assert.False(expandLiteral);
        Assert.Equal("@vars:x", rawLiteral);
    }

    [Fact]
    public void Resolve_WhenDisabled_ShouldReturnLiteralValue()
    {
        var tokens = ServerVariableExpander.BuildTokenValues(CreateServer(), CreateGameType(), CreateRevision());

        Assert.Equal("{Name} Server", ServerVariableExpander.Resolve("{Name} Server", tokens));
    }

    [Fact]
    public void Resolve_WhenEnabled_ShouldSubstituteKnownTokensAndKeepUnknownOnes()
    {
        var tokens = ServerVariableExpander.BuildTokenValues(CreateServer(), CreateGameType(), CreateRevision());

        var resolved = ServerVariableExpander.Resolve("@vars:{Name} [{GameTypeKey}/{RevisionVersionTag}] {Unknown}", tokens);

        Assert.Equal("My Server [minecraft/latest] {Unknown}", resolved);
    }

    private static Models.V2.GameServer CreateServer()
    {
        return new Models.V2.GameServer
        {
            ServerId = "abc123",
            Name = "My Server",
            ServiceName = "minecraft-abc123",
            Status = "Running"
        };
    }

    private static GameType CreateGameType()
    {
        return new GameType { Key = "minecraft" };
    }

    private static GameTypeRevision CreateRevision()
    {
        return new GameTypeRevision
        {
            VersionTag = "latest",
            ImageReference = "itzg/minecraft-server"
        };
    }
}
