using GameServer.API.Configurations;
using GameServer.API.Interfaces;
using GameServer.API.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace GameServer.API.Tests.Services
{
    public class UdpAgentAnnouncementListenerServiceTests
    {
        [Fact]
        public async Task ExecuteAsync_WhenDisabled_ShouldReturnPromptlyWithoutException()
        {
            var loggerMock = new Mock<ILogger<UdpAgentAnnouncementListenerService>>();
            var registryMock = new Mock<IUdpAgentRegistry>();
            var options = new UdpAgentDiscoveryOptions
            {
                Enabled = false
            };

            var service = new UdpAgentAnnouncementListenerService(loggerMock.Object, registryMock.Object, options);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

            await service.StartAsync(cts.Token);
            await service.StopAsync(cts.Token);
        }
    }
}
