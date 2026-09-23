using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using GameServer.Web.Services;
using Xunit;

namespace GameServer.Web.Tests.Services;

public class HttpResponseMessageExtensionsTests
{
    [Fact]
    public async Task EnsureSuccessStatusCodeWithDetailsAsync_WhenSuccessStatus_DoesNotThrow()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"status\":\"ok\"}", Encoding.UTF8, "application/json")
        };

        await response.EnsureSuccessStatusCodeWithDetailsAsync();
    }

    [Fact]
    public async Task EnsureSuccessStatusCodeWithDetailsAsync_WhenRfc7807ProblemDetails_ExtractsDetailInException()
    {
        var problem = new
        {
            type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            title = "Internal Server Error",
            status = 500,
            detail = "Duplicate entry '1-EULA' for key 'IX_GameTypeSettingDefinitions_GameTypeRevisionId_SettingKey'"
        };

        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent(JsonSerializer.Serialize(problem), Encoding.UTF8, "application/problem+json")
        };

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => response.EnsureSuccessStatusCodeWithDetailsAsync());

        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
        Assert.Contains("Duplicate entry '1-EULA'", ex.Message);
        Assert.Contains("HTTP 500", ex.Message);
    }

    [Fact]
    public async Task EnsureSuccessStatusCodeWithDetailsAsync_WhenValidationErrors_ExtractsConcatenatedErrors()
    {
        var validationProblem = new
        {
            title = "One or more validation errors occurred.",
            status = 400,
            errors = new Dictionary<string, string[]>
            {
                ["ThumbnailUrl"] = new[] { "The field ThumbnailUrl must be a string or array type with a maximum length of '500'." },
                ["Key"] = new[] { "The field Key is required." }
            }
        };

        var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(JsonSerializer.Serialize(validationProblem), Encoding.UTF8, "application/json")
        };

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => response.EnsureSuccessStatusCodeWithDetailsAsync());

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Contains("ThumbnailUrl must be a string", ex.Message);
        Assert.Contains("The field Key is required.", ex.Message);
    }

    [Fact]
    public async Task EnsureSuccessStatusCodeWithDetailsAsync_WhenPlainTextError_UsesRawText()
    {
        var response = new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("Custom upstream gateway timeout error message", Encoding.UTF8, "text/plain")
        };

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => response.EnsureSuccessStatusCodeWithDetailsAsync());

        Assert.Equal(HttpStatusCode.BadGateway, ex.StatusCode);
        Assert.Contains("Custom upstream gateway timeout error message", ex.Message);
    }

    [Fact]
    public async Task EnsureSuccessStatusCodeWithDetailsAsync_WhenEmptyBody_FallsBackToReasonPhrase()
    {
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            ReasonPhrase = "Internal Server Error"
        };

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => response.EnsureSuccessStatusCodeWithDetailsAsync());

        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
        Assert.Contains("500", ex.Message);
    }

    [Fact]
    public void ExtractDetailFromResponseContent_WhenMessagePropertyPresent_ExtractsMessage()
    {
        var json = "{\"message\":\"An unexpected database error occurred.\"}";
        var result = HttpResponseMessageExtensions.ExtractDetailFromResponseContent(json);
        Assert.Equal("An unexpected database error occurred.", result);
    }
}
