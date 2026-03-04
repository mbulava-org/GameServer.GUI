using GameServer.Docker.Configurations;
using GameServer.Docker.Models;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GameServer.Docker.Services
{
    /// <summary>
    /// Background service that broadcasts the Primary Service's presence via UDP subnet broadcast.
    /// Auto-detects the container's IP address and subnet broadcast address on the shared overlay network.
    /// Agents listen for these broadcasts to auto-discover and connect to the Primary.
    /// Implements automatic API key rotation for security.
    /// </summary>
    public class PrimaryServiceAnnouncementService : BackgroundService
    {
        private readonly ILogger<PrimaryServiceAnnouncementService> _logger;
        private readonly ServiceDiscoveryOptions _options;
        private readonly NetworkOptions _networkOptions;
        private readonly IConfiguration _configuration;
        private string _currentApiKey;
        private DateTime _lastKeyRotation;
        private readonly string _serviceId;
        private readonly string _version;
        private string? _detectedIpAddress;
        private IPAddress? _broadcastAddress;

        public PrimaryServiceAnnouncementService(
            ILogger<PrimaryServiceAnnouncementService> logger,
            IOptions<ServiceDiscoveryOptions> options,
            IOptions<NetworkOptions> networkOptions,
            IConfiguration configuration)
        {
            _logger = logger;
            _options = options.Value;
            _networkOptions = networkOptions.Value;
            _configuration = configuration;

            // Generate initial API key
            _currentApiKey = GenerateApiKey();
            _lastKeyRotation = DateTime.UtcNow;

            // Generate unique service ID
            _serviceId = $"gameserver-primary-{Environment.MachineName}-{DateTime.UtcNow.Ticks}";

            // Get version from assembly
            var assembly = Assembly.GetExecutingAssembly();
            _version = assembly.GetName().Version?.ToString() ?? "0.0.0.0";

            _logger.LogInformation("Primary Service Announcement initialized: ServiceId={ServiceId}, Version={Version}", 
                _serviceId, _version);
        }

        /// <summary>
        /// Gets the current API key. Should be used by SignalR hubs for validation.
        /// </summary>
        public string CurrentApiKey => _currentApiKey;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("Service discovery is disabled. Announcements will not be broadcast.");
                return;
            }

            // Small delay to ensure webhost is fully started
            await Task.Delay(500, stoppingToken);

            // Auto-detect IP address and subnet broadcast address
            var networkInfo = DetectNetworkConfiguration();

            if (networkInfo == null)
            {
                _logger.LogError("Failed to detect container network configuration on network '{NetworkName}'. Service discovery will not work.",
                    _networkOptions.NetworkName);
                return;
            }

            _detectedIpAddress = networkInfo.Value.IpAddress;
            _broadcastAddress = networkInfo.Value.BroadcastAddress;

            _logger.LogInformation(
                "Starting Primary Service announcements: IP={IpAddress}, Broadcast={BroadcastAddress}:{Port}, Interval={Interval}s",
                _detectedIpAddress,
                _broadcastAddress,
                _options.Port,
                _options.BroadcastIntervalSeconds);

            using var udpClient = new UdpClient();

            try
            {
                // Configure for broadcast
                udpClient.EnableBroadcast = true;

                var broadcastEndpoint = new IPEndPoint(_broadcastAddress, _options.Port);

                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.BroadcastIntervalSeconds));

                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    try
                    {
                        // Check if API key needs rotation
                        if (DateTime.UtcNow - _lastKeyRotation > TimeSpan.FromMinutes(_options.ApiKeyRotationMinutes))
                        {
                            RotateApiKey();
                        }

                        // Create announcement message
                        var message = CreateAnnouncementMessage();

                        // Serialize to JSON
                        var json = JsonSerializer.Serialize(message);
                        var bytes = Encoding.UTF8.GetBytes(json);

                        if (bytes.Length > _options.MaxMessageSize)
                        {
                            _logger.LogWarning("Announcement message too large: {Size} bytes (max: {Max})",
                                bytes.Length, _options.MaxMessageSize);
                            continue;
                        }

                        // Send broadcast
                        await udpClient.SendAsync(bytes, bytes.Length, broadcastEndpoint);

                        _logger.LogTrace("Broadcast sent: {Size} bytes to {Endpoint}",
                            bytes.Length, broadcastEndpoint);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send announcement broadcast");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Primary Service announcement service is stopping");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in announcement service");
                throw;
            }
            finally
            {
                udpClient.Close();
            }
        }

        /// <summary>
        /// Auto-detects the container's IP address and calculates the subnet broadcast address.
        /// Uses NetworkOptions.NetworkName to identify the correct network interface.
        /// </summary>
        private (string IpAddress, IPAddress BroadcastAddress)? DetectNetworkConfiguration()
        {
            try
            {
                _logger.LogDebug("Auto-detecting container network configuration on network '{NetworkName}'", _networkOptions.NetworkName);

                // Get all network interfaces
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();

                // In Docker, the overlay network interface is usually "eth0" or "eth1"
                // We look for an interface that:
                // 1. Is up and operational
                // 2. Has an IPv4 address with subnet mask
                // 3. Is not loopback
                // 4. Has a private IP range (10.x, 172.16-31.x, 192.168.x)

                foreach (var iface in interfaces)
                {
                    _logger.LogTrace("Checking interface: {Name}, Type={Type}, Status={Status}",
                        iface.Name, iface.NetworkInterfaceType, iface.OperationalStatus);

                    if (iface.OperationalStatus != OperationalStatus.Up)
                        continue;

                    if (iface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;

                    var ipProps = iface.GetIPProperties();

                    foreach (var addr in ipProps.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily != AddressFamily.InterNetwork)
                            continue;

                        var ipBytes = addr.Address.GetAddressBytes();

                        // Check if it's a private IP (Docker overlay networks use these)
                        bool isPrivate = 
                            ipBytes[0] == 10 || // 10.0.0.0/8
                            (ipBytes[0] == 172 && ipBytes[1] >= 16 && ipBytes[1] <= 31) || // 172.16.0.0/12
                            (ipBytes[0] == 192 && ipBytes[1] == 168); // 192.168.0.0/16

                        if (isPrivate)
                        {
                            var ip = addr.Address.ToString();

                            // Calculate broadcast address from IP and subnet mask
                            var broadcastAddr = CalculateBroadcastAddress(addr.Address, addr.IPv4Mask);

                            _logger.LogInformation(
                                "Detected network configuration: IP={IpAddress}, Mask={SubnetMask}, Broadcast={BroadcastAddress} on interface {InterfaceName}",
                                ip, addr.IPv4Mask, broadcastAddr, iface.Name);

                            return (ip, broadcastAddr);
                        }
                    }
                }

                _logger.LogWarning("No suitable network interface found with private IP address");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting container network configuration");
                return null;
            }
        }

        /// <summary>
        /// Calculates the broadcast address for a given IP and subnet mask.
        /// For example: IP=10.0.1.5, Mask=255.255.255.0 => Broadcast=10.0.1.255
        /// </summary>
        private static IPAddress CalculateBroadcastAddress(IPAddress address, IPAddress subnetMask)
        {
            var ipBytes = address.GetAddressBytes();
            var maskBytes = subnetMask.GetAddressBytes();
            var broadcastBytes = new byte[ipBytes.Length];

            for (int i = 0; i < ipBytes.Length; i++)
            {
                // Broadcast address = IP | ~Mask
                broadcastBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
            }

            return new IPAddress(broadcastBytes);
        }

        private ServiceAnnouncementMessage CreateAnnouncementMessage()
        {
            // Construct endpoint using detected IP address + known port
            var port = 8080; // Standard port for Primary Service
            var endpoint = $"http://{_detectedIpAddress}:{port}";

            return new ServiceAnnouncementMessage
            {
                ServiceId = _serviceId,
                Endpoint = endpoint,
                ApiKey = _currentApiKey,
                Timestamp = DateTime.UtcNow,
                Version = _version,
                Capabilities = new List<string>
                {
                    "service-management",
                    "agent-registration",
                    "container-operations",
                    "log-streaming",
                    "resource-monitoring"
                }
            };
        }

        private void RotateApiKey()
        {
            var oldKey = _currentApiKey;
            _currentApiKey = GenerateApiKey();
            _lastKeyRotation = DateTime.UtcNow;

            _logger.LogInformation("API key rotated. Agents will update on next broadcast.");
        }

        private static string GenerateApiKey()
        {
            // Generate cryptographically secure random API key
            var bytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }
    }
}
