using GameServer.API.Interfaces;
using GameServer.API.Models.V2;
using GameServer.API.Repositories.V2;
using GameServer.API.Services.V2;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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

    [Fact]
    public async Task EnsureWatchingAsync_WhenReadyPatternFoundInServiceLogs_MarksReadyImmediatelyAndTransitionsToAvailable()
    {
        var server = new Models.V2.GameServer
        {
            ServerId = "srv-valheim-1",
            Name = "Valheim Survival",
            ServiceName = "games_valheim_1",
            Status = "Running",
            GameTypeRevisionId = 10
        };

        var gameType = new GameType
        {
            Key = "valheim",
            DisplayName = "Valheim",
            Revisions =
            [
                new GameTypeRevision
                {
                    Id = 10,
                    VersionTag = "0.217.46",
                    ReadyLogPattern = "Game server connected"
                }
            ]
        };

        var serverRepo = new Mock<IGameServerRepository>();
        serverRepo.Setup(r => r.GetByServerIdAsync("srv-valheim-1")).ReturnsAsync(server);

        var gameTypeRepo = new Mock<IGameTypeRepository>();
        gameTypeRepo.Setup(r => r.GetAllAsync(true)).ReturnsAsync([gameType]);

        var discovery = new Mock<INodeAgentDiscovery>();
        discovery.Setup(d => d.GetServiceLogsAsync("games_valheim_1", 0))
            .ReturnsAsync(new List<string>
            {
                "[01:00:00] Loading configuration...",
                "[01:00:05] World loaded.",
                "[01:00:10] Game server connected",
                "[01:00:12] Ready for connections."
            });

        var logAggregator = new Mock<IServerLogAggregator>();

        var scopeMock = new Mock<IServiceScope>();
        var scopeServiceProvider = new Mock<IServiceProvider>();
        scopeServiceProvider.Setup(sp => sp.GetService(typeof(IGameServerRepository))).Returns(serverRepo.Object);
        scopeServiceProvider.Setup(sp => sp.GetService(typeof(IGameTypeRepository))).Returns(gameTypeRepo.Object);
        scopeServiceProvider.Setup(sp => sp.GetService(typeof(INodeAgentDiscovery))).Returns(discovery.Object);
        scopeMock.Setup(s => s.ServiceProvider).Returns(scopeServiceProvider.Object);

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        var rootServiceProvider = new Mock<IServiceProvider>();
        rootServiceProvider.Setup(sp => sp.GetService(typeof(IServiceScopeFactory))).Returns(scopeFactoryMock.Object);

        var watcher = new GameServerReadinessWatcherService(
            rootServiceProvider.Object,
            logAggregator.Object,
            NullLogger<GameServerReadinessWatcherService>.Instance);

        await watcher.EnsureWatchingAsync("srv-valheim-1");

        Assert.True(watcher.IsServerReady("srv-valheim-1"));
        serverRepo.Verify(r => r.UpdateAsync(It.Is<Models.V2.GameServer>(s => s.Status == "Available")), Times.Once);
    }
}

