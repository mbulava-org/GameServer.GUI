using GameServer.API.Models.V2;
using GameServer.API.Services.V2;
using Xunit;

namespace GameServer.API.Tests.Services.V2;

public class GameServerReadinessWatcherServiceTests
{
    [Fact]
    public void ExpandPattern_ReplacesEnvironmentVariableSettingsAndTokens()
    {
        var server = new Models.V2.GameServer
        {
            ServerId = "srv-mc-1",
            Name = "Minecraft Alpha",
            ServiceName = "games_mc_1",
            Status = "Running",
            GameTypeRevisionId = 1,
            Settings =
            [
                new GameServerSetting { SettingKey = "SERVER_PORT", Value = "25565" },
                new GameServerSetting { SettingKey = "MOTD", Value = "Welcome to Server" }
            ],
            Ports =
            [
                new GameServerPort { ContainerPort = 25565, PublishedPort = 25565, Protocol = "tcp" }
            ]
        };

        var gameType = new GameType
        {
            Key = "minecraft",
            DisplayName = "Minecraft Java"
        };

        var revision = new GameTypeRevision
        {
            Id = 1,
            VersionTag = "1.21.2",
            ImageReference = "itzg/minecraft-server",
            ReadyLogPattern = "Done ({*})! For help, type \"help\" on port {SERVER_PORT}"
        };

        var expanded = GameServerReadinessWatcherService.ExpandPattern(
            "Server started on {SERVER_PORT} for {Name} (${MOTD})",
            server,
            gameType,
            revision);

        Assert.Equal("Server started on 25565 for Minecraft Alpha (Welcome to Server)", expanded);
    }

    [Theory]
    [InlineData("Done (3.456s)! For help, type \"help\"", "Done (*)! For help, type \"help\"", true)]
    [InlineData("[17:45:00 INFO]: Done (12.3s)! For help, type \"help\"", "Done (*)! For help*", true)]
    [InlineData("Server started on port 25565 successfully", "Server started on port 25565", true)]
    [InlineData("Downloading server jar...", "Done (*)! For help, type \"help\"", false)]
    [InlineData("Preparing spawn area: 54%", "Done (*)! For help, type \"help\"", false)]
    public void MatchesPattern_MatchesWildcardsAndSubstringsCorrectly(string logLine, string targetPattern, bool expectedMatch)
    {
        var result = GameServerReadinessWatcherService.MatchesPattern(logLine, targetPattern);
        Assert.Equal(expectedMatch, result);
    }
}
