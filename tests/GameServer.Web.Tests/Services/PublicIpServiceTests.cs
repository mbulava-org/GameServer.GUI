using System.Net;
using GameServer.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

namespace GameServer.Web.Tests.Services;

public class PublicIpServiceTests
{
    [Fact]
    public async Task GetPublicIpAsync_WhenApiReturnsValidIp_ShouldReturnDiscoveredIp()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"ip\":\"198.51.100.42\",\"country\":\"United States\"}")
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var service = new PublicIpService(factoryMock.Object, NullLogger<PublicIpService>.Instance);

        // Act
        var ip = await service.GetPublicIpAsync();

        // Assert
        Assert.Equal("198.51.100.42", ip);
    }

    [Fact]
    public async Task GetPublicIpAsync_WhenAllEndpointsFail_ShouldFallbackToDefault()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("Network error"));

        var httpClient = new HttpClient(handlerMock.Object);
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var service = new PublicIpService(factoryMock.Object, NullLogger<PublicIpService>.Instance);

        // Act
        var ip = await service.GetPublicIpAsync();

        // Assert
        Assert.Equal("127.0.0.1", ip);
    }
}
