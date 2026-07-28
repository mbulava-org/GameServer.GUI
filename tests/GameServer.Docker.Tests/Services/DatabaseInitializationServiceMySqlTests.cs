using GameServer.Docker.Data.V2;
using GameServer.Docker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Testcontainers.MySql;
using V2Repositories = GameServer.Docker.Repositories.V2;

namespace GameServer.Docker.Tests.Services;

public class DatabaseInitializationServiceMySqlTests : IAsyncLifetime
{
    private MySqlContainer? _container;
    private GameServerV2DbContext? _context;
    private V2Repositories.GameTypeRepository? _v2Repository;

    public async Task InitializeAsync()
    {
        _container = new MySqlBuilder()
            .WithImage("mysql:8.4")
            .WithDatabase($"gameserver_init_{Guid.NewGuid():N}")
            .WithUsername("gameserver")
            .WithPassword("P@ssw0rd123!")
            .Build();

        await _container.StartAsync();

        var optionsBuilder = new DbContextOptionsBuilder<GameServerV2DbContext>();
        GameServerV2DbContextFactory.ConfigureProvider(optionsBuilder, "mysql", _container.GetConnectionString());

        _context = new GameServerV2DbContext(optionsBuilder.Options);
        _v2Repository = new V2Repositories.GameTypeRepository(_context, Mock.Of<ILogger<V2Repositories.GameTypeRepository>>());
    }

    public async Task DisposeAsync()
    {
        if (_context is not null)
        {
            await _context.DisposeAsync();
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenV2RepositoryUsesMySqlContainer_ShouldInitializeWithoutStoppingApplication()
    {
        ArgumentNullException.ThrowIfNull(_v2Repository);
        ArgumentNullException.ThrowIfNull(_context);

        var applicationLifetime = new Mock<IHostApplicationLifetime>();
        var serviceProvider = CreateServiceProvider(_v2Repository);
        var service = new TestDatabaseInitializationService(serviceProvider, applicationLifetime.Object, Mock.Of<ILogger<DatabaseInitializationService>>());

        var originalExitCode = Environment.ExitCode;

        try
        {
            await service.ExecuteForTestAsync(CancellationToken.None);

            applicationLifetime.Verify(x => x.StopApplication(), Times.Never);
            Assert.True(await _context!.Database.CanConnectAsync());
            Assert.Equal(0, await _context.GameTypes.CountAsync());
        }
        finally
        {
            Environment.ExitCode = originalExitCode;
        }
    }

    private static IServiceProvider CreateServiceProvider(V2Repositories.IGameTypeRepository v2Repository)
    {
        var scopeServiceProvider = new Mock<IServiceProvider>();
        scopeServiceProvider
            .Setup(x => x.GetService(typeof(V2Repositories.IGameTypeRepository)))
            .Returns(v2Repository);
        // GetRequiredService is an extension method and cannot be mocked directly.

        var scope = new Mock<IServiceScope>();
        scope.SetupGet(x => x.ServiceProvider).Returns(scopeServiceProvider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);

        var rootServiceProvider = new Mock<IServiceProvider>();
        rootServiceProvider
            .Setup(x => x.GetService(typeof(IServiceScopeFactory)))
            .Returns(scopeFactory.Object);

        return rootServiceProvider.Object;
    }

    private sealed class TestDatabaseInitializationService(
        IServiceProvider serviceProvider,
        IHostApplicationLifetime applicationLifetime,
        ILogger<DatabaseInitializationService> logger)
        : DatabaseInitializationService(serviceProvider, applicationLifetime, logger)
    {
        public Task ExecuteForTestAsync(CancellationToken cancellationToken)
        {
            return ExecuteAsync(cancellationToken);
        }
    }
}
