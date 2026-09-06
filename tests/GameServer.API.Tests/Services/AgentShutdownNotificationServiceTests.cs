using GameServer.API.Hubs;
using GameServer.API.Interfaces;
using GameServer.API.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;

namespace GameServer.API.Tests.Services;

public class AgentShutdownNotificationServiceTests
{
    [Fact]
    public async Task StopAsync_ShouldNotifyAllAgents()
    {
        // Arrange
        var notifierMock = new Mock<IAgentShutdownNotifier>();
        var service = new AgentShutdownNotificationService(notifierMock.Object, Mock.Of<ILogger<AgentShutdownNotificationService>>());

        // Act
        await service.StopAsync(CancellationToken.None);

        // Assert
        notifierMock.Verify(x => x.NotifyPrimaryServiceShuttingDownAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SignalRAgentShutdownNotifier_ShouldBroadcastToAllClients()
    {
        // Arrange
        var clientProxy = new Mock<IClientProxy>();
        var hubClients = new Mock<IHubClients>();
        hubClients.SetupGet(x => x.All).Returns(clientProxy.Object);

        var hubContext = new Mock<IHubContext<AgentRegistrationHub>>();
        hubContext.SetupGet(x => x.Clients).Returns(hubClients.Object);

        var notifier = new SignalRAgentShutdownNotifier(hubContext.Object);

        // Act
        await notifier.NotifyPrimaryServiceShuttingDownAsync(CancellationToken.None);

        // Assert
        clientProxy.Verify(
            x => x.SendCoreAsync(
                "PrimaryServiceShuttingDown",
                It.Is<object?[]>(args => args.Length == 1 && string.Equals(args[0] as string, "Primary Service is shutting down.", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AgentShutdownNotificationService_StartAsync_And_ExceptionHandling_ExecutesSafely()
    {
        var notifierMock = new Mock<IAgentShutdownNotifier>();
        var service = new AgentShutdownNotificationService(notifierMock.Object, Mock.Of<ILogger<AgentShutdownNotificationService>>());

        await service.StartAsync(CancellationToken.None);

        notifierMock.Setup(x => x.NotifyPrimaryServiceShuttingDownAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Notifier failed"));

        // Should catch and log warning without throwing
        await service.StopAsync(CancellationToken.None);
    }
}
