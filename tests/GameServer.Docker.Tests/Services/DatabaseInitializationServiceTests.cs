using GameServer.Docker.Repositories;
using GameServer.Docker.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using V2Repositories = GameServer.Docker.Repositories.V2;

namespace GameServer.Docker.Tests.Services;

public class DatabaseInitializationServiceTests
{
    [Fact]
    public async Task ExecuteAsync_WhenInitializationSucceeds_ShouldInitializeBothRepositories()
    {
        // Arrange
        var legacyRepository = new Mock<IGameTypeRepository>();
        var v2Repository = new Mock<V2Repositories.IGameTypeRepository>();
        var applicationLifetime = new Mock<IHostApplicationLifetime>();
        var serviceProvider = CreateServiceProvider(legacyRepository.Object, v2Repository.Object);
        var service = new TestDatabaseInitializationService(serviceProvider, applicationLifetime.Object, Mock.Of<ILogger<DatabaseInitializationService>>());

        // Act
        await service.ExecuteForTestAsync(CancellationToken.None);

        // Assert
        legacyRepository.Verify(x => x.InitializeDatabaseAsync(), Times.Once);
        v2Repository.Verify(x => x.InitializeDatabaseAsync(), Times.Once);
        applicationLifetime.Verify(x => x.StopApplication(), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenV2InitializationFails_ShouldStopApplication()
    {
        // Arrange
        var legacyRepository = new Mock<IGameTypeRepository>();
        var v2Repository = new Mock<V2Repositories.IGameTypeRepository>();
        v2Repository
            .Setup(x => x.InitializeDatabaseAsync())
            .ThrowsAsync(new InvalidOperationException("V2 init failed"));

        var applicationLifetime = new Mock<IHostApplicationLifetime>();
        var serviceProvider = CreateServiceProvider(legacyRepository.Object, v2Repository.Object);
        var service = new TestDatabaseInitializationService(serviceProvider, applicationLifetime.Object, Mock.Of<ILogger<DatabaseInitializationService>>());

        var originalExitCode = Environment.ExitCode;

        try
        {
            // Act
            await service.ExecuteForTestAsync(CancellationToken.None);

            // Assert
            applicationLifetime.Verify(x => x.StopApplication(), Times.Once);
            Assert.Equal(1, Environment.ExitCode);
        }
        finally
        {
            Environment.ExitCode = originalExitCode;
        }
    }

    private static IServiceProvider CreateServiceProvider(IGameTypeRepository legacyRepository, V2Repositories.IGameTypeRepository v2Repository)
    {
        var scopeServiceProvider = new Mock<IServiceProvider>();
        scopeServiceProvider
            .Setup(x => x.GetService(typeof(IGameTypeRepository)))
            .Returns(legacyRepository);
        scopeServiceProvider
            .Setup(x => x.GetService(typeof(V2Repositories.IGameTypeRepository)))
            .Returns(v2Repository);

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
