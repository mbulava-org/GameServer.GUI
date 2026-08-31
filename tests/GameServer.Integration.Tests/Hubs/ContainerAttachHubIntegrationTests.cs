using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using GameServer.API.Interfaces;
using GameServer.API.Hubs;
using System.Runtime.CompilerServices;

namespace GameServer.Integration.Tests.Hubs;

[Collection("Integration Tests")]
public class ContainerAttachHubIntegrationTests
{
    private readonly IntegrationTestFactory _factory;

    public ContainerAttachHubIntegrationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
    }

    [Fact(Skip = "Integration tests temporarily disabled - revisit later.")]
    public async Task SendInput_ShouldInvokeAggregatorSendInputWithConnectionId()
    {
        // Arrange
        var aggregatorMock = new Mock<IContainerAttachAggregator>();
        aggregatorMock
            .Setup(a => a.SendInputAsync(
                It.IsAny<string>(),
                "container-1",
                "help",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var client = CreateHubClient(services =>
        {
            services.AddSingleton(aggregatorMock.Object);
        });

        await client.StartAsync();

        // Act
        await client.InvokeAsync<bool>("SendInput", "container-1", "help");

        // Assert
        aggregatorMock.Verify(a => a.SendInputAsync(
            client.ConnectionId!,
            "container-1",
            "help",
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(Skip = "Integration tests temporarily disabled - revisit later.")]
    public async Task DisconnectFromContainer_ShouldInvokeAggregatorUnsubscribe()
    {
        // Arrange
        var aggregatorMock = new Mock<IContainerAttachAggregator>();
        aggregatorMock
            .Setup(a => a.UnsubscribeAsync(
                It.IsAny<string>(),
                "container-1"))
            .Returns(Task.CompletedTask);

        var client = CreateHubClient(services =>
        {
            services.AddSingleton(aggregatorMock.Object);
        });

        await client.StartAsync();

        // Act
        await client.InvokeAsync("DisconnectFromContainer", "container-1");

        // Assert
        aggregatorMock.Verify(a => a.UnsubscribeAsync(
            client.ConnectionId!,
            "container-1"),
            Times.Once);
    }

    [Fact(Skip = "Integration tests temporarily disabled - revisit later.")]
    public async Task SubscribeToContainer_ShouldStreamAggregatorFrames()
    {
        // Arrange
        var expected = new[]
        {
            new AttachStreamFrame { Kind = AttachFrameKind.Output, Payload = "hello" },
            new AttachStreamFrame { Kind = AttachFrameKind.InputControlledBy, Payload = "controller-1" }
        };

        var aggregatorMock = new Mock<IContainerAttachAggregator>();
        aggregatorMock
            .Setup(a => a.SubscribeAsync(
                It.IsAny<string>(),
                "container-1",
                It.IsAny<CancellationToken>()))
            .Returns(expected.ToAsyncEnumerable());

        var client = CreateHubClient(services =>
        {
            services.AddSingleton(aggregatorMock.Object);
        });

        await client.StartAsync();

        // Act
        var messages = new List<string>();
        await foreach (var message in client.StreamAsync<string>("SubscribeToContainer", "server-1", "container-1", false))
        {
            messages.Add(message);
            if (messages.Count == expected.Length)
                break;
        }

        // Assert
        Assert.Equal(expected.Length, messages.Count);
        Assert.Contains("hello", messages[0]);
        Assert.Contains("controller-1", messages[1]);
    }

    private HubConnection CreateHubClient(Action<IServiceCollection>? configureTestServices = null, string hubPath = "/hubs/attach")
    {
        if (configureTestServices is not null)
        {
            return CreateHubClientWithOverride(configureTestServices, hubPath);
        }

        var server = _factory.Server;
        var url = server.BaseAddress!.AbsoluteUri.TrimEnd('/') + hubPath;

        var client = new HubConnectionBuilder()
            .WithUrl(url, options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
            })
            .WithAutomaticReconnect()
            .Build();

        return client;
    }

    private HubConnection CreateHubClientWithOverride(Action<IServiceCollection> configureTestServices, string hubPath)
    {
        var testFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(configureTestServices);
        });

        var server = testFactory.Server;
        var url = server.BaseAddress!.AbsoluteUri.TrimEnd('/') + hubPath;

        var client = new HubConnectionBuilder()
            .WithUrl(url, options =>
            {
                options.HttpMessageHandlerFactory = _ => testFactory.Server.CreateHandler();
                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
            })
            .WithAutomaticReconnect()
            .Build();

        return client;
    }
}
