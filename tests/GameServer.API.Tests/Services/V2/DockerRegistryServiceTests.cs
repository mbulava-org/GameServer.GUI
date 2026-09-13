using System.Net;
using GameServer.API.Services.V2;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;

namespace GameServer.API.Tests.Services.V2;

public sealed class DockerRegistryServiceTests
{
    [Theory]
    [InlineData("vinanrra/7dtd-server", null, "registry-1.docker.io", "vinanrra/7dtd-server", "latest")]
    [InlineData("vinanrra/7dtd-server:tag1", null, "registry-1.docker.io", "vinanrra/7dtd-server", "tag1")]
    [InlineData("vinanrra/7dtd-server", "v2.0", "registry-1.docker.io", "vinanrra/7dtd-server", "v2.0")]
    [InlineData("ubuntu", "22.04", "registry-1.docker.io", "library/ubuntu", "22.04")]
    [InlineData("ghcr.io/linuxserver/heimdall", "latest", "ghcr.io", "linuxserver/heimdall", "latest")]
    [InlineData("quay.io/prometheus/prometheus:v2.45.0", null, "quay.io", "prometheus/prometheus", "v2.45.0")]
    [InlineData("myregistry.local:5000/app/service", "prod", "myregistry.local:5000", "app/service", "prod")]
    public void ParseImageReference_ShouldCorrectlyParseComponents(
        string imageReference,
        string? explicitTag,
        string expectedHost,
        string expectedRepo,
        string expectedTag)
    {
        var (host, repo, tag) = DockerRegistryService.ParseImageReference(imageReference, explicitTag);

        Assert.Equal(expectedHost, host);
        Assert.Equal(expectedRepo, repo);
        Assert.Equal(expectedTag, tag);
    }

    [Fact]
    public async Task GetRemoteDigestAsync_WhenRegistryReturnsDockerContentDigest_ShouldReturnHeaderValue()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/manifests/latest")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}")
                };
                response.Headers.Add("Docker-Content-Digest", "sha256:4d47f9bc8e5610b4da481f4a9b6c00d413693e506abcc7c9dfbf42d05718df88");
                return response;
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var service = new DockerRegistryService(factoryMock.Object, NullLogger<DockerRegistryService>.Instance);

        // Act
        var digest = await service.GetRemoteDigestAsync("vinanrra/7dtd-server", "latest");

        // Assert
        Assert.Equal("sha256:4d47f9bc8e5610b4da481f4a9b6c00d413693e506abcc7c9dfbf42d05718df88", digest);
    }
}
