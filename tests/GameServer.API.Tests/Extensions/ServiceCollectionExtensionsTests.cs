using GameServer.Catalog;
using GameServer.Deployment;
using GameServer.Monitoring;
using GameServer.Orchestration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Moq;

namespace GameServer.API.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMonitoringModule_RegistersAllExpectedServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMonitoringModule();

        var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetService<GameServer.API.Interfaces.IServerResourceAggregator>());
        Assert.NotNull(sp.GetService<GameServer.API.Interfaces.IServerLogAggregator>());
        Assert.NotNull(sp.GetService<GameServer.API.Interfaces.IContainerAttachAggregator>());
        Assert.NotNull(sp.GetService<GameServer.API.Interfaces.IGameServerReadinessWatcherService>());
    }

    [Fact]
    public void AddOrchestrationModule_RegistersAllExpectedServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["NodeAgentOptions:DiscoveryIntervalSeconds"] = "15",
                ["UdpAgentDiscoveryOptions:DiscoveryPort"] = "9999"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient();
        services.AddSingleton(Mock.Of<GameServer.API.Interfaces.ITerminalSessionNotifier>());
        services.AddOrchestrationModule(configuration);

        var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetService<GameServer.API.Interfaces.IAgentRegistry>());
        Assert.NotNull(sp.GetService<GameServer.API.Interfaces.IUdpAgentRegistry>());
        Assert.NotNull(sp.GetService<GameServer.API.Services.NodeAgentClient>());
        Assert.NotNull(sp.GetService<GameServer.API.Services.TerminalSessionManager>());
    }

    [Fact]
    public void AddDeploymentModule_WithReservedPorts_ConfiguresAndRegistersServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PortAllocation:StartingPort"] = "25565",
                ["PortAllocation:ReservedPortRanges"] = "25560-25564, 8080"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient();
        services.AddSingleton(Mock.Of<GameServer.API.Interfaces.IAgentRegistry>());
        services.AddSingleton(Mock.Of<GameServer.API.Interfaces.IUdpAgentRegistry>());
        services.AddDeploymentModule(configuration);

        var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetService<GameServer.API.Services.PortAllocator>());
        Assert.NotNull(sp.GetService<GameServer.API.Interfaces.IServiceOperations>());
    }

    [Theory]
    [InlineData("sqlite", "Data Source=:memory:")]
    [InlineData("postgres", "Host=localhost;Database=test;Username=postgres;Password=postgres;")]
    public void AddCatalogModule_WithDifferentProviders_RegistersDbContextAndServices(string provider, string connStr)
    {
        var configDict = new Dictionary<string, string?>
        {
            ["V2Database:Provider"] = provider,
            ["V2Database:ConnectionStringName"] = "GameServerV2Db",
            ["ConnectionStrings:GameServerV2Db"] = connStr,
            ["SKIP_DB_INIT"] = "true"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.SetupGet(e => e.EnvironmentName).Returns("Development");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient();
        services.AddSingleton(Mock.Of<GameServer.API.Interfaces.IAgentRegistry>());
        services.AddCatalogModule(configuration, mockEnv.Object, skipDbInit: true);

        var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetService<GameServer.API.Services.IDatabaseReadinessGate>());
        Assert.NotNull(sp.GetService<GameServer.API.Repositories.V2.IGameServerRepository>());
        Assert.NotNull(sp.GetService<GameServer.API.Repositories.V2.IGameTypeRepository>());
        Assert.NotNull(sp.GetService<GameServer.API.Repositories.V2.IMountTypeConfigRepository>());
        Assert.NotNull(sp.GetService<GameServer.API.Services.V2.Detection.GameTypeSetupDetectionService>());
    }
}
