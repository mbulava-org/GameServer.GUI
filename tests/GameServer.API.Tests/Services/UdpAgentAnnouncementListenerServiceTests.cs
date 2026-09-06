using GameServer.API.Configurations;
using GameServer.API.Interfaces;
using GameServer.API.Models;
using GameServer.API.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

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

        [Fact]
        public async Task ExecuteAsync_WhenInvalidBindAddress_ThrowsInvalidOperationException()
        {
            var loggerMock = new Mock<ILogger<UdpAgentAnnouncementListenerService>>();
            var registryMock = new Mock<IUdpAgentRegistry>();
            var options = new UdpAgentDiscoveryOptions
            {
                Enabled = true,
                BindAddress = "invalid-ip-address",
                Port = 19199
            };

            var service = new UdpAgentAnnouncementListenerService(loggerMock.Object, registryMock.Object, options);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

            await service.StartAsync(cts.Token);
            if (service.ExecuteTask != null)
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteTask);
            }
        }

        [Fact]
        public async Task ExecuteAsync_WhenValidAnnouncementSent_UpsertsToRegistry()
        {
            var loggerMock = new Mock<ILogger<UdpAgentAnnouncementListenerService>>();
            var registryMock = new Mock<IUdpAgentRegistry>();

            int freePort = 19280 + Random.Shared.Next(100, 500);
            var options = new UdpAgentDiscoveryOptions
            {
                Enabled = true,
                BindAddress = "127.0.0.1",
                Port = freePort,
                CleanupIntervalSeconds = 1
            };

            var service = new UdpAgentAnnouncementListenerService(loggerMock.Object, registryMock.Object, options);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

            await service.StartAsync(cts.Token);

            var announcement = new UdpAgentAnnouncement
            {
                NodeId = "node-test-1",
                InternalUrl = "http://127.0.0.1:5000",
                Timestamp = DateTimeOffset.UtcNow
            };

            using var senderClient = new UdpClient();
            var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(announcement));
            await senderClient.SendAsync(payload, payload.Length, new IPEndPoint(IPAddress.Loopback, freePort));

            // Wait brief moment for packet processing
            await Task.Delay(200);

            // Send invalid payload to test catch block
            var invalidPayload = Encoding.UTF8.GetBytes("not-valid-json");
            await senderClient.SendAsync(invalidPayload, invalidPayload.Length, new IPEndPoint(IPAddress.Loopback, freePort));

            await Task.Delay(200);

            await service.StopAsync(cts.Token);

            registryMock.Verify(r => r.UpsertAnnouncement(It.Is<UdpAgentAnnouncement>(a => a.NodeId == "node-test-1")), Times.AtLeastOnce());
        }

        [Fact]
        public void UdpAgentDiscoveryOptions_Properties_CanBeSet()
        {
            var options = new UdpAgentDiscoveryOptions
            {
                Enabled = true,
                BindAddress = "127.0.0.1",
                MulticastGroup = "239.1.1.1",
                Port = 19090,
                AnnouncementTtlSeconds = 60,
                CleanupIntervalSeconds = 15
            };

            Assert.True(options.Enabled);
            Assert.Equal("127.0.0.1", options.BindAddress);
            Assert.Equal("239.1.1.1", options.MulticastGroup);
            Assert.Equal(19090, options.Port);
            Assert.Equal(60, options.AnnouncementTtlSeconds);
            Assert.Equal(15, options.CleanupIntervalSeconds);
        }
    }
}
