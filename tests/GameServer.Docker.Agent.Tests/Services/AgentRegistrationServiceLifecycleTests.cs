using Docker.DotNet;
using Docker.DotNet.Models;
using GameServer.Docker.Agent.Configurations;
using GameServer.Docker.Agent.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace GameServer.Docker.Agent.Tests.Services
{
    public class AgentRegistrationServiceLifecycleTests
    {
        private readonly Mock<IDockerClient> _mockDockerClient;
        private readonly Mock<ILogger<AgentRegistrationService>> _mockLogger;

        public AgentRegistrationServiceLifecycleTests()
        {
            _mockDockerClient = new Mock<IDockerClient>();
            _mockLogger = new Mock<ILogger<AgentRegistrationService>>();
        }

        [Fact]
        public async Task ExecuteAsync_WhenDisabled_ReturnsPromptly()
        {
            var options = Options.Create(new AgentRegistrationOptions
            {
                Enabled = false
            });

            var service = new AgentRegistrationService(_mockDockerClient.Object, _mockLogger.Object, options);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

            await service.StartAsync(cts.Token);
            await service.StopAsync(cts.Token);
        }

        [Fact]
        public async Task ExecuteAsync_WhenPrimaryUrlEmpty_LogsErrorAndReturns()
        {
            var options = Options.Create(new AgentRegistrationOptions
            {
                Enabled = true,
                PrimaryServiceUrl = ""
            });

            var service = new AgentRegistrationService(_mockDockerClient.Object, _mockLogger.Object, options);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

            await service.StartAsync(cts.Token);
            await service.StopAsync(cts.Token);
        }

        [Fact]
        public void IsUnexpectedDisconnect_IdentifiesSocketAndResetErrors()
        {
            var method = typeof(AgentRegistrationService).GetMethod(
                "IsUnexpectedDisconnect",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            Assert.NotNull(method);

            var nullEx = (bool)method!.Invoke(null, new object?[] { null })!;
            Assert.False(nullEx);

            var normalEx = (bool)method!.Invoke(null, new object?[] { new InvalidOperationException("Some logic error") })!;
            Assert.False(normalEx);

            var resetEx = (bool)method!.Invoke(null, new object?[] { new Exception("Connection reset by peer") })!;
            Assert.True(resetEx);

            var wsEx = (bool)method!.Invoke(null, new object?[] { new System.Net.WebSockets.WebSocketException("Closed") })!;
            Assert.True(wsEx);
        }

        [Fact]
        public async Task ResolveOverlayIpAsync_WhenOverlayNetworkPresent_ReturnsOverlayIp()
        {
            var options = Options.Create(new AgentRegistrationOptions
            {
                Enabled = true,
                PrimaryServiceUrl = "http://primary:5000"
            });

            var containerInspect = new ContainerInspectResponse
            {
                NetworkSettings = new NetworkSettings
                {
                    Networks = new Dictionary<string, EndpointSettings>
                    {
                        ["ingress"] = new EndpointSettings { IPAddress = "10.255.0.4" },
                        ["gameserver-net"] = new EndpointSettings { IPAddress = "10.0.1.42" }
                    }
                }
            };

            _mockDockerClient.Setup(d => d.Containers.InspectContainerAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(containerInspect);

            var service = new AgentRegistrationService(_mockDockerClient.Object, _mockLogger.Object, options);
            var method = typeof(AgentRegistrationService).GetMethod(
                "ResolveOverlayIpAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            Assert.NotNull(method);
            var task = (Task<string?>)method!.Invoke(service, new object[] { CancellationToken.None })!;
            var ip = await task;

            Assert.Equal("10.0.1.42", ip);
        }

        [Fact]
        public async Task ResolveOverlayIpAsync_WhenOnlyInfrastructureNetworks_ReturnsNull()
        {
            var options = Options.Create(new AgentRegistrationOptions
            {
                Enabled = true,
                PrimaryServiceUrl = "http://primary:5000"
            });

            var containerInspect = new ContainerInspectResponse
            {
                NetworkSettings = new NetworkSettings
                {
                    Networks = new Dictionary<string, EndpointSettings>
                    {
                        ["ingress"] = new EndpointSettings { IPAddress = "10.255.0.4" },
                        ["bridge"] = new EndpointSettings { IPAddress = "172.17.0.2" },
                        ["host"] = new EndpointSettings { IPAddress = "" }
                    }
                }
            };

            _mockDockerClient.Setup(d => d.Containers.InspectContainerAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(containerInspect);

            var service = new AgentRegistrationService(_mockDockerClient.Object, _mockLogger.Object, options);
            var method = typeof(AgentRegistrationService).GetMethod(
                "ResolveOverlayIpAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            Assert.NotNull(method);
            var task = (Task<string?>)method!.Invoke(service, new object[] { CancellationToken.None })!;
            var ip = await task;

            Assert.Null(ip);
        }

        [Fact]
        public async Task ResolveOverlayIpAsync_WhenInspectThrows_ReturnsNull()
        {
            var options = Options.Create(new AgentRegistrationOptions
            {
                Enabled = true,
                PrimaryServiceUrl = "http://primary:5000"
            });

            _mockDockerClient.Setup(d => d.Containers.InspectContainerAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new DockerApiException(System.Net.HttpStatusCode.NotFound, "not found"));

            var service = new AgentRegistrationService(_mockDockerClient.Object, _mockLogger.Object, options);
            var method = typeof(AgentRegistrationService).GetMethod(
                "ResolveOverlayIpAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            Assert.NotNull(method);
            var task = (Task<string?>)method!.Invoke(service, new object[] { CancellationToken.None })!;
            var ip = await task;

            Assert.Null(ip);
        }

        [Fact]
        public async Task InitializeAgentInfoAsync_WhenSwarmInfoPresent_InitializesNodeProperties()
        {
            var options = Options.Create(new AgentRegistrationOptions
            {
                Enabled = true,
                PrimaryServiceUrl = "http://primary:5000"
            });

            _mockDockerClient.Setup(d => d.System.GetSystemInfoAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SystemInfoResponse
                {
                    Name = "docker-node-1",
                    Swarm = new()
                    {
                        NodeID = "swarm-node-abc",
                        ControlAvailable = true
                    }
                });

            _mockDockerClient.Setup(d => d.Containers.InspectContainerAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ContainerInspectResponse
                {
                    NetworkSettings = new NetworkSettings
                    {
                        Networks = new Dictionary<string, EndpointSettings>
                        {
                            ["swarm-overlay"] = new EndpointSettings { IPAddress = "10.0.9.15" }
                        }
                    }
                });

            var service = new AgentRegistrationService(_mockDockerClient.Object, _mockLogger.Object, options);
            var method = typeof(AgentRegistrationService).GetMethod(
                "InitializeAgentInfoAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            Assert.NotNull(method);
            var task = (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;
            await task;

            var nodeIdField = typeof(AgentRegistrationService).GetField("_nodeId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var agentUrlField = typeof(AgentRegistrationService).GetField("_agentUrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var isManagerField = typeof(AgentRegistrationService).GetField("_isManagerNode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            Assert.Equal("swarm-node-abc", nodeIdField?.GetValue(service));
            Assert.Equal("http://10.0.9.15:8080", agentUrlField?.GetValue(service));
            Assert.True((bool)isManagerField?.GetValue(service)!);
        }
    }
}
