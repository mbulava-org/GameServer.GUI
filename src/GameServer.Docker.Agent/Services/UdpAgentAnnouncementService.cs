using Docker.DotNet;
using Docker.DotNet.Models;
using GameServer.Docker.Agent.Configurations;
using GameServer.Docker.Agent.Models;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace GameServer.Docker.Agent.Services
{
    /// <summary>
    /// Emits periodic UDP announcements so the primary service can discover agents without Docker polling.
    /// </summary>
    public sealed class UdpAgentAnnouncementService : BackgroundService
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

        private readonly IDockerClient _dockerClient;
        private readonly ILogger<UdpAgentAnnouncementService> _logger;
        private readonly UdpAgentAnnouncementOptions _options;

        private string _nodeId = string.Empty;
        private string _nodeName = string.Empty;
        private string _agentUrl = string.Empty;
        private bool _isManagerNode;

        public UdpAgentAnnouncementService(
            IDockerClient dockerClient,
            ILogger<UdpAgentAnnouncementService> logger,
            IOptions<UdpAgentAnnouncementOptions> options)
        {
            _dockerClient = dockerClient;
            _logger = logger;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("UDP agent announcements are disabled");
                return;
            }

            await InitializeAgentInfoAsync(stoppingToken);

            using var udpClient = CreateUdpClient();
            var multicastEndpoint = CreateMulticastEndpoint();

            _logger.LogInformation(
                "UDP agent announcements enabled for node {NodeName} ({NodeId}) to {Group}:{Port}",
                _nodeName,
                _nodeId,
                _options.MulticastGroup,
                _options.Port);

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, _options.AnnouncementIntervalSeconds)));

            try
            {
                await SendAnnouncementAsync(udpClient, multicastEndpoint, stoppingToken);

                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await SendAnnouncementAsync(udpClient, multicastEndpoint, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("UDP agent announcements are stopping");
            }
        }

        private UdpClient CreateUdpClient()
        {
            var udpClient = new UdpClient(AddressFamily.InterNetwork);
            udpClient.Ttl = (short)Math.Max(1, _options.TimeToLive);
            return udpClient;
        }

        private IPEndPoint CreateMulticastEndpoint()
        {
            if (!IPAddress.TryParse(_options.MulticastGroup, out var multicastAddress))
            {
                throw new InvalidOperationException($"UdpAgentAnnouncement:MulticastGroup '{_options.MulticastGroup}' is not a valid IPv4 address.");
            }

            return new IPEndPoint(multicastAddress, _options.Port);
        }

        private async Task InitializeAgentInfoAsync(CancellationToken cancellationToken)
        {
            var info = await _dockerClient.System.GetSystemInfoAsync(cancellationToken);

            _nodeId = info.Swarm?.NodeID ?? Guid.NewGuid().ToString();
            _nodeName = Environment.GetEnvironmentVariable("NODE_NAME") ?? info.Name ?? Environment.MachineName;
            _isManagerNode = info.Swarm?.ControlAvailable ?? false;

            var agentHost = Environment.GetEnvironmentVariable("AGENT_HOST") ?? Environment.MachineName;
            var agentPort = Environment.GetEnvironmentVariable("AGENT_PORT") ?? "8080";
            _agentUrl = $"http://{agentHost}:{agentPort}";
        }

        private async Task SendAnnouncementAsync(UdpClient udpClient, IPEndPoint multicastEndpoint, CancellationToken cancellationToken)
        {
            var containers = await _dockerClient.Containers.ListContainersAsync(
                new ContainersListParameters
                {
                    All = false
                },
                cancellationToken);

            var announcement = new UdpAgentAnnouncement
            {
                NodeId = _nodeId,
                NodeName = _nodeName,
                InternalUrl = _agentUrl,
                IsManagerNode = _isManagerNode,
                Timestamp = DateTimeOffset.UtcNow,
                ContainerIds = containers
                    .Select(container => container.ID)
                    .Where(containerId => !string.IsNullOrWhiteSpace(containerId))
                    .Distinct(StringComparer.Ordinal)
                    .ToList()
            };

            var payload = JsonSerializer.SerializeToUtf8Bytes(announcement, SerializerOptions);
            await udpClient.SendAsync(payload, multicastEndpoint, cancellationToken);

            _logger.LogTrace(
                "UDP agent announcement sent: Node={NodeName} ({NodeId}), Containers={ContainerCount}, Url={Url}",
                _nodeName,
                _nodeId,
                announcement.ContainerIds.Count,
                _agentUrl);
        }
    }
}
