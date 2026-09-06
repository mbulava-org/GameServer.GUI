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

        [Fact]
        public void FilterCapabilitiesByNodeRole_WhenWorker_StripsManagerOnlyCapabilities()
        {
            var method = typeof(AgentRegistrationService).GetMethod(
                "FilterCapabilitiesByNodeRole",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            Assert.NotNull(method);

            var configured = new List<string> { "containers", "services", "tasks", "images", "nodes", "swarm" };
            var workerResult = (List<string>)method!.Invoke(null, new object[] { configured, false })!;
            Assert.Contains("containers", workerResult);
            Assert.Contains("images", workerResult);
            Assert.DoesNotContain("services", workerResult);
            Assert.DoesNotContain("tasks", workerResult);
            Assert.DoesNotContain("nodes", workerResult);
            Assert.DoesNotContain("swarm", workerResult);

            var managerResult = (List<string>)method!.Invoke(null, new object[] { configured, true })!;
            Assert.Equal(6, managerResult.Count);
        }

        [Fact]
        public async Task OnReconnecting_WhenInvoked_LogsAppropriately()
        {
            var options = Options.Create(new AgentRegistrationOptions
            {
                Enabled = true,
                PrimaryServiceUrl = "http://primary:5000"
            });

            var service = new AgentRegistrationService(_mockDockerClient.Object, _mockLogger.Object, options);
            var method = typeof(AgentRegistrationService).GetMethod(
                "OnReconnecting",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            Assert.NotNull(method);

            // 1. Regular close
            var task1 = (Task)method!.Invoke(service, new object?[] { null })!;
            await task1;

            // 2. Unexpected disconnect
            var task2 = (Task)method!.Invoke(service, new object?[] { new Exception("Connection reset by peer") })!;
            await task2;

            // 3. Primary shutdown in progress
            var shutdownField = typeof(AgentRegistrationService).GetField("_primaryServiceShutdownInProgress", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            shutdownField?.SetValue(service, true);
            var task3 = (Task)method!.Invoke(service, new object?[] { null })!;
            await task3;
        }

        [Fact]
        public async Task OnClosed_WhenInvoked_LogsAppropriately()
        {
            var options = Options.Create(new AgentRegistrationOptions
            {
                Enabled = true,
                PrimaryServiceUrl = "http://primary:5000"
            });

            var service = new AgentRegistrationService(_mockDockerClient.Object, _mockLogger.Object, options);
            var method = typeof(AgentRegistrationService).GetMethod(
                "OnClosed",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            Assert.NotNull(method);

            // Graceful close
            var task1 = (Task)method!.Invoke(service, new object?[] { null })!;
            await task1;

            // Unexpected close with exception
            var task2 = (Task)method!.Invoke(service, new object?[] { new Exception("Socket error") })!;
            await task2;

            // During coordinated shutdown
            var shutdownField = typeof(AgentRegistrationService).GetField("_primaryServiceShutdownInProgress", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            shutdownField?.SetValue(service, true);
            var task3 = (Task)method!.Invoke(service, new object?[] { null })!;
            await task3;
        }

        [Fact]
        public async Task HandlePrimaryServiceShutdownAsync_WhenCancellationRequested_ReturnsPromptly()
        {
            var options = Options.Create(new AgentRegistrationOptions
            {
                Enabled = true,
                PrimaryServiceUrl = "http://primary:5000"
            });

            var service = new AgentRegistrationService(_mockDockerClient.Object, _mockLogger.Object, options);
            var method = typeof(AgentRegistrationService).GetMethod(
                "HandlePrimaryServiceShutdownAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            Assert.NotNull(method);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var task = (Task)method!.Invoke(service, new object?[] { "restarting", cts.Token })!;
            await task;
        }

        [Fact]
        public async Task SendHeartbeatAsync_WhenNotConnected_ReturnsPromptly()
        {
            var options = Options.Create(new AgentRegistrationOptions
            {
                Enabled = true,
                PrimaryServiceUrl = "http://primary:5000"
            });

            var service = new AgentRegistrationService(_mockDockerClient.Object, _mockLogger.Object, options);
            var method = typeof(AgentRegistrationService).GetMethod(
                "SendHeartbeatAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            Assert.NotNull(method);

            var task = (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;
            await task;

            // When shutdown in progress
            var shutdownField = typeof(AgentRegistrationService).GetField("_primaryServiceShutdownInProgress", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            shutdownField?.SetValue(service, true);
            var task2 = (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;
            await task2;
        }

        [Fact]
        public async Task InitializeAgentInfoAsync_WhenSwarmNull_GeneratesGuidNodeId()
        {
            var options = Options.Create(new AgentRegistrationOptions
            {
                Enabled = true,
                PrimaryServiceUrl = "http://primary:5000"
            });

            _mockDockerClient.Setup(d => d.System.GetSystemInfoAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SystemInfoResponse
                {
                    Name = "standalone-docker",
                    Swarm = null
                });

            _mockDockerClient.Setup(d => d.Containers.InspectContainerAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ContainerInspectResponse
                {
                    NetworkSettings = new NetworkSettings
                    {
                        Networks = new Dictionary<string, EndpointSettings>
                        {
                            ["custom-net"] = new EndpointSettings { IPAddress = "192.168.1.100" }
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
            var isManagerField = typeof(AgentRegistrationService).GetField("_isManagerNode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var nodeId = (string?)nodeIdField?.GetValue(service);
            Assert.NotNull(nodeId);
            Assert.True(Guid.TryParse(nodeId, out _));
            Assert.False((bool)isManagerField?.GetValue(service)!);
        }

        [Fact]
        public async Task InitializeAgentInfoAsync_WhenSystemInfoThrows_Rethrows()
        {
            var options = Options.Create(new AgentRegistrationOptions
            {
                Enabled = true,
                PrimaryServiceUrl = "http://primary:5000"
            });

            _mockDockerClient.Setup(d => d.System.GetSystemInfoAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new DockerApiException(System.Net.HttpStatusCode.InternalServerError, "Docker daemon unavailable"));

            var service = new AgentRegistrationService(_mockDockerClient.Object, _mockLogger.Object, options);
            var method = typeof(AgentRegistrationService).GetMethod(
                "InitializeAgentInfoAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            Assert.NotNull(method);
            var ex = await Assert.ThrowsAsync<DockerApiException>(() => (Task)method!.Invoke(service, new object[] { CancellationToken.None })!);
            Assert.Equal(System.Net.HttpStatusCode.InternalServerError, ex.StatusCode);
        }

        [Fact]
        public async Task OnReconnected_WhenInvoked_ResetsShutdownFlag()
        {
            var options = Options.Create(new AgentRegistrationOptions
            {
                Enabled = true,
                PrimaryServiceUrl = "http://primary:5000"
            });

            var service = new AgentRegistrationService(_mockDockerClient.Object, _mockLogger.Object, options);
            var shutdownField = typeof(AgentRegistrationService).GetField("_primaryServiceShutdownInProgress", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            shutdownField?.SetValue(service, true);

            var method = typeof(AgentRegistrationService).GetMethod(
                "OnReconnected",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            Assert.NotNull(method);
            var task = (Task)method!.Invoke(service, new object?[] { "conn-123" })!;
            await task;

            Assert.False((bool)shutdownField?.GetValue(service)!);
        }
    }
}
