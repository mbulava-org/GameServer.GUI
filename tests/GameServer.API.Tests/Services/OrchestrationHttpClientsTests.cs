using GameServer.API.Configurations;
using GameServer.API.Interfaces;
using GameServer.API.Models;
using GameServer.API.Services;
using GameServer.Orchestration.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;

namespace GameServer.API.Tests.Services
{
    public class OrchestrationHttpClientsTests
    {
        private readonly Mock<ILogger<OrchestrationHttpAgentRegistry>> _registryLoggerMock;
        private readonly Mock<ILogger<OrchestrationHttpNodeAgentDiscovery>> _discoveryLoggerMock;

        public OrchestrationHttpClientsTests()
        {
            _registryLoggerMock = new Mock<ILogger<OrchestrationHttpAgentRegistry>>();
            _discoveryLoggerMock = new Mock<ILogger<OrchestrationHttpNodeAgentDiscovery>>();
        }

        [Fact]
        public void OrchestrationHttpAgentRegistry_GetAllAgents_ReturnsList()
        {
            var agents = new List<NodeAgentEndpoint>
            {
                new() { NodeId = "node-1", NodeName = "Worker1", InternalUrl = "http://w1:8080", IsHealthy = true }
            };

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("api/agents")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(agents))
                });

            var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://orch:8080") };
            var registry = new OrchestrationHttpAgentRegistry(httpClient, _registryLoggerMock.Object);

            var result = registry.GetAllAgents();

            Assert.Single(result);
            Assert.Equal("node-1", result[0].NodeId);
        }

        [Fact]
        public void OrchestrationHttpAgentRegistry_GetAgentByNodeId_WhenFound_ReturnsAgent()
        {
            var agent = new NodeAgentEndpoint { NodeId = "node-abc", InternalUrl = "http://abc:8080" };

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("api/agents/by-node/node-abc")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(agent))
                });

            var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://orch:8080") };
            var registry = new OrchestrationHttpAgentRegistry(httpClient, _registryLoggerMock.Object);

            var result = registry.GetAgentByNodeId("node-abc");

            Assert.NotNull(result);
            Assert.Equal("node-abc", result.NodeId);
        }

        [Fact]
        public void OrchestrationHttpAgentRegistry_WhenHttpThrows_ReturnsGracefully()
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network error"));

            var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://orch:8080") };
            var registry = new OrchestrationHttpAgentRegistry(httpClient, _registryLoggerMock.Object);

            Assert.Empty(registry.GetAllAgents());
            Assert.Empty(registry.GetHealthyAgents());
            Assert.Empty(registry.GetManagerAgents());
            Assert.Null(registry.GetHealthyManagerAgent());
            Assert.Null(registry.GetAgentForContainer("c-err"));
            Assert.Null(registry.GetAgentByNodeId("n-err"));
            Assert.Null(registry.GetAgentByConnectionId("conn-err"));

            // No-ops should not throw
            registry.RegisterAgent(new AgentRegistrationInfo(), "c1");
            registry.UpdateAgentContainers("c1", new List<string>());
            registry.MarkAgentDisconnected("c1");
        }

        [Fact]
        public async Task OrchestrationHttpNodeAgentDiscovery_DiscoverAgentsAsync_ReturnsList()
        {
            var agents = new List<NodeAgentEndpoint>
            {
                new() { NodeId = "node-1", InternalUrl = "http://w1:8080" }
            };

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("api/discovery/agents")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(agents))
                });

            var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://orch:8080") };
            var discovery = new OrchestrationHttpNodeAgentDiscovery(httpClient, _discoveryLoggerMock.Object);

            var result = await discovery.DiscoverAgentsAsync();

            Assert.Single(result);
            Assert.Equal("node-1", result[0].NodeId);
        }

        [Fact]
        public async Task OrchestrationHttpNodeAgentDiscovery_GetAgentForContainerAsync_ReturnsAgent()
        {
            var agent = new NodeAgentEndpoint { NodeId = "node-cont", InternalUrl = "http://cont:8080" };

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("api/discovery/container/c123")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(agent))
                });

            var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://orch:8080") };
            var discovery = new OrchestrationHttpNodeAgentDiscovery(httpClient, _discoveryLoggerMock.Object);

            var result = await discovery.GetAgentForContainerAsync("c123");

            Assert.NotNull(result);
            Assert.Equal("node-cont", result.NodeId);
        }

        [Fact]
        public async Task OrchestrationHttpNodeAgentDiscovery_WhenNotFoundOrError_HandlesGracefully()
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));

            var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://orch:8080") };
            var discovery = new OrchestrationHttpNodeAgentDiscovery(httpClient, _discoveryLoggerMock.Object);

            Assert.Empty(await discovery.DiscoverAgentsAsync());
            Assert.Null(await discovery.GetAgentForContainerAsync("c-missing"));
            Assert.Null(await discovery.GetAgentForServerAsync("s-missing"));
            Assert.Null(await discovery.GetContainerStatsAsync("c-missing"));
            Assert.Null(await discovery.GetContainerLogsAsync("c-missing"));
            Assert.Null(await discovery.GetServiceLogsAsync("s-missing"));
        }
    }
}
