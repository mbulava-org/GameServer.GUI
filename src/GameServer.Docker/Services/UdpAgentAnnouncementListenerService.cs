using GameServer.Docker.Configurations;
using GameServer.Docker.Interfaces;
using GameServer.Docker.Models;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace GameServer.Docker.Services
{
    /// <summary>
    /// Listens for UDP agent announcements and updates the discovery registry.
    /// </summary>
    public sealed class UdpAgentAnnouncementListenerService : BackgroundService
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

        private readonly ILogger<UdpAgentAnnouncementListenerService> _logger;
        private readonly IUdpAgentRegistry _udpAgentRegistry;
        private readonly UdpAgentDiscoveryOptions _options;

        public UdpAgentAnnouncementListenerService(
            ILogger<UdpAgentAnnouncementListenerService> logger,
            IUdpAgentRegistry udpAgentRegistry,
            UdpAgentDiscoveryOptions options)
        {
            _logger = logger;
            _udpAgentRegistry = udpAgentRegistry;
            _options = options;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("UDP agent discovery is disabled");
                return;
            }

            using var udpClient = CreateUdpClient();

            _logger.LogInformation(
                "UDP agent listener started on {BindAddress}:{Port} (group: {Group})",
                _options.BindAddress,
                _options.Port,
                _options.MulticastGroup);

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    using var receiveTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(1, _options.CleanupIntervalSeconds)));
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, receiveTimeoutCts.Token);

                    try
                    {
                        var result = await udpClient.ReceiveAsync(linkedCts.Token);
                        ProcessAnnouncement(result.Buffer);
                        _udpAgentRegistry.RemoveExpired(DateTimeOffset.UtcNow);
                    }
                    catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested && receiveTimeoutCts.IsCancellationRequested)
                    {
                        _udpAgentRegistry.RemoveExpired(DateTimeOffset.UtcNow);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("UDP agent listener is stopping");
            }
        }

        private UdpClient CreateUdpClient()
        {
            var udpClient = new UdpClient(AddressFamily.InterNetwork);
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            var bindAddress = ParseBindAddress(_options.BindAddress);
            udpClient.Client.Bind(new IPEndPoint(bindAddress, _options.Port));

            if (!string.IsNullOrWhiteSpace(_options.MulticastGroup) &&
                IPAddress.TryParse(_options.MulticastGroup, out var multicastGroup))
            {
                udpClient.JoinMulticastGroup(multicastGroup);
            }

            return udpClient;
        }

        private void ProcessAnnouncement(byte[] buffer)
        {
            try
            {
                var json = Encoding.UTF8.GetString(buffer);
                var announcement = JsonSerializer.Deserialize<UdpAgentAnnouncement>(json, SerializerOptions);

                if (announcement == null)
                {
                    _logger.LogDebug("Ignored empty UDP agent announcement payload");
                    return;
                }

                _udpAgentRegistry.UpsertAnnouncement(announcement);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Ignoring invalid UDP agent announcement payload");
            }
        }

        private static IPAddress ParseBindAddress(string bindAddress)
        {
            if (string.IsNullOrWhiteSpace(bindAddress) || bindAddress == "0.0.0.0")
            {
                return IPAddress.Any;
            }

            if (IPAddress.TryParse(bindAddress, out var parsedAddress))
            {
                return parsedAddress;
            }

            throw new InvalidOperationException($"UdpAgentDiscovery:BindAddress '{bindAddress}' is not a valid IPv4 address.");
        }
    }
}
