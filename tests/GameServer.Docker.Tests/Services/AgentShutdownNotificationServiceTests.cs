using GameServer.Docker.Hubs;
using GameServer.Docker.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;

namespace GameServer.Docker.Tests.Services;

public class AgentShutdownNotificationServiceTests
{
    [Fact]
    public async Task StopAsync_ShouldNotifyAllAgents()
    {
        // Arrange
        var clientProxy = new Mock<IClientProxy>();
        var hubClients = new Mock<IHubClients>();
        hubClients.SetupGet(x => x.All).Returns(clientProxy.Object);

        var hubContext = new Mock<IHubContext<AgentRegistrationHub>>();
        hubContext.SetupGet(x => x.Clients).Returns(hubClients.Object);

        var service = new AgentShutdownNotificationService(hubContext.Object, Mock.Of<ILogger<AgentShutdownNotificationService>>());

        // Act
        await service.StopAsync(CancellationToken.None);

        // Assert
        clientProxy.Verify(
            x => x.SendCoreAsync(
                "PrimaryServiceShuttingDown",
                It.Is<object?[]>(args => args.Length == 1 && string.Equals(args[0] as string, "Primary Service is shutting down.", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
