using System.Net;
using GameServer.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace GameServer.Web.Tests.Services;

public sealed class ThumbnailCacheServiceTests
{
    [Fact]
    public async Task GetCachedThumbnailUrlAsync_WhenRemoteThumbnailExists_ShouldDownloadOnceAndReturnCachedPath()
    {
        var requestCount = 0;
        var service = CreateService((request, cancellationToken) =>
        {
            requestCount++;
            Assert.Equal("https://example.com/thumb.png", request.RequestUri?.ToString());

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47])
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            return Task.FromResult(response);
        }, out var options);

        try
        {
            var first = await service.GetCachedThumbnailUrlAsync("https://example.com/thumb.png");
            var second = await service.GetCachedThumbnailUrlAsync("https://example.com/thumb.png");

            Assert.NotNull(first);
            Assert.Equal(first, second);
            Assert.StartsWith(options.RequestPath + "/", first, StringComparison.Ordinal);
            Assert.Equal(1, requestCount);

            var relativeFileName = first![options.RequestPath.Length..].TrimStart('/');
            var filePath = Path.Combine(options.CacheDirectory, Uri.UnescapeDataString(relativeFileName));
            Assert.True(File.Exists(filePath));
        }
        finally
        {
            if (Directory.Exists(options.CacheDirectory))
            {
                Directory.Delete(options.CacheDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GetCachedThumbnailUrlAsync_WhenSourceIsNotHttp_ShouldReturnOriginalUrl()
    {
        var service = CreateService((request, cancellationToken) =>
        {
            throw new InvalidOperationException("HTTP should not be called for non-http source URLs.");
        }, out var options);

        try
        {
            var sourceUrl = "/images/local-thumbnail.png";
            var result = await service.GetCachedThumbnailUrlAsync(sourceUrl, TestContext.Current.CancellationToken);

            Assert.Equal(sourceUrl, result);
        }
        finally
        {
            if (Directory.Exists(options.CacheDirectory))
            {
                Directory.Delete(options.CacheDirectory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetCachedThumbnailUrlAsync_WhenNullOrWhitespace_ShouldReturnNull(string? sourceUrl)
    {
        var service = CreateService((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)), out var options);

        try
        {
            var result = await service.GetCachedThumbnailUrlAsync(sourceUrl, TestContext.Current.CancellationToken);
            Assert.Null(result);
        }
        finally
        {
            if (Directory.Exists(options.CacheDirectory))
            {
                Directory.Delete(options.CacheDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GetCachedThumbnailUrlAsync_WhenHttpFails_ShouldFallbackToOriginalUrl()
    {
        var service = CreateService((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)), out var options);

        try
        {
            var sourceUrl = "https://example.com/missing.png";
            var result = await service.GetCachedThumbnailUrlAsync(sourceUrl, TestContext.Current.CancellationToken);
            Assert.Equal(sourceUrl, result);
        }
        finally
        {
            if (Directory.Exists(options.CacheDirectory))
            {
                Directory.Delete(options.CacheDirectory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("image/jpeg", ".jpg")]
    [InlineData("image/gif", ".gif")]
    [InlineData("image/webp", ".webp")]
    [InlineData("image/svg+xml", ".svg")]
    [InlineData("image/avif", ".avif")]
    [InlineData("application/octet-stream", ".png")] // uri has .png
    [InlineData("unknown/type", ".jpg")] // fallback
    public async Task GetCachedThumbnailUrlAsync_ShouldResolveCorrectFileExtension(string mediaType, string expectedExtension)
    {
        var url = mediaType == "application/octet-stream"
            ? "https://example.com/custom.png"
            : "https://example.com/image-no-ext";

        var service = CreateService((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([0x01, 0x02])
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType);
            return Task.FromResult(response);
        }, out var options);

        try
        {
            var result = await service.GetCachedThumbnailUrlAsync(url, TestContext.Current.CancellationToken);
            Assert.NotNull(result);
            Assert.EndsWith(expectedExtension, result, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(options.CacheDirectory))
            {
                Directory.Delete(options.CacheDirectory, recursive: true);
            }
        }
    }

    private static ThumbnailCacheService CreateService(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder,
        out ThumbnailCacheOptions options)
    {
        options = new ThumbnailCacheOptions
        {
            CacheDirectory = Path.Combine(Path.GetTempPath(), "GameServer.Web.Tests", Guid.NewGuid().ToString("N")),
            RequestPath = "/thumbnail-cache"
        };

        var handler = new StubHttpMessageHandler(responder);
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(handler));

        return new ThumbnailCacheService(
            httpClientFactory.Object,
            Options.Create(options),
            NullLogger<ThumbnailCacheService>.Instance);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return responder(request, cancellationToken);
        }
    }
}
