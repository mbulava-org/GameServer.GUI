using GameServer.Orchestration.Hubs;
using GameServer.Orchestration.Services;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Xunit;

namespace GameServer.API.Tests.Services;

public class SignalRTerminalSessionNotifierTests
{
    [Fact]
    public async Task SignalRTerminalSessionNotifier_SendsMessagesToClient()
    {
        var hubContextMock = new Mock<IHubContext<ContainerConsoleHub>>();
        var clientsMock = new Mock<IHubClients>();
        var clientProxyMock = new Mock<ISingleClientProxy>();

        hubContextMock.Setup(h => h.Clients).Returns(clientsMock.Object);
        clientsMock.Setup(c => c.Client("conn-1")).Returns(clientProxyMock.Object);

        var notifier = new SignalRTerminalSessionNotifier(hubContextMock.Object);

        await notifier.SendOutputAsync("conn-1", "test output");
        clientProxyMock.Verify(c => c.SendCoreAsync("Output", It.Is<object[]>(o => (string)o[0] == "test output"), It.IsAny<CancellationToken>()), Times.Once);

        await notifier.SendDisconnectedAsync("conn-1");
        clientProxyMock.Verify(c => c.SendCoreAsync("Disconnected", It.Is<object[]>(o => (string)o[0] == "Shell exited"), It.IsAny<CancellationToken>()), Times.Once);

        await notifier.SendErrorAsync("conn-1", "some error");
        clientProxyMock.Verify(c => c.SendCoreAsync("Error", It.Is<object[]>(o => (string)o[0] == "some error"), It.IsAny<CancellationToken>()), Times.Once);
    }
}
