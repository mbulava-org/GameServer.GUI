using System.Net;
using System.Text.Json;
using GameServer.API.Interfaces;
using GameServer.API.Services.V2;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;

namespace GameServer.API.Tests.Services.V2;

public class WindowsAgentOperationsTests
{
    [Fact]
    public async Task StartServerAsync_WhenAgentReturns200_ReturnsProcessInfo()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        var responseInfo = new WindowsProcessInfo
        {
            ServerId = "srv-1",
            Name = "Palworld",
            Status = "Running",
            ProcessId = 12345
        };

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString().Contains("api/servers/start")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(responseInfo), System.Text.Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var operations = new WindowsAgentOperations(mockFactory.Object, NullLogger<WindowsAgentOperations>.Instance);

        // Act
        var result = await operations.StartServerAsync("http://windows-node:5000", new WindowsStartServerRequest
        {
            ServerId = "srv-1",
            Name = "Palworld"
        });

        // Assert
        Assert.NotNull(result);
        Assert.Equal("srv-1", result.ServerId);
        Assert.Equal("Running", result.Status);
        Assert.Equal(12345, result.ProcessId);
    }

    [Fact]
    public async Task StopServerAsync_WhenAgentReturns200_ReturnsProcessInfo()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        var responseInfo = new WindowsProcessInfo
        {
            ServerId = "srv-1",
            Status = "Stopped"
        };

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString().Contains("api/servers/srv-1/stop")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(responseInfo), System.Text.Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var operations = new WindowsAgentOperations(mockFactory.Object, NullLogger<WindowsAgentOperations>.Instance);

        // Act
        var result = await operations.StopServerAsync("http://windows-node:5000", "srv-1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("srv-1", result.ServerId);
        Assert.Equal("Stopped", result.Status);
    }

    [Fact]
    public async Task GetServerStatsAsync_WhenAgentReturns200_ReturnsStats()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        var responseStats = new WindowsProcessStats
        {
            ServerId = "srv-1",
            CpuPercent = 14.5,
            MemoryWorkingSetBytes = 1024 * 1024 * 512,
            ThreadCount = 24
        };

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri!.ToString().Contains("api/servers/srv-1/stats")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(responseStats), System.Text.Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var operations = new WindowsAgentOperations(mockFactory.Object, NullLogger<WindowsAgentOperations>.Instance);

        // Act
        var result = await operations.GetServerStatsAsync("http://windows-node:5000", "srv-1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("srv-1", result.ServerId);
        Assert.Equal(14.5, result.CpuPercent);
        Assert.Equal(1024 * 1024 * 512, result.MemoryWorkingSetBytes);
    }
}
