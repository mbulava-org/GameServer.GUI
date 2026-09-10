using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using GameServer.Web.Services.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameServer.Web.Tests.Services.Extensions;

public class PalworldApiClientTests
{
    private static (PalworldApiClient client, RecordingHandler handler) Create()
    {
        var handler = new RecordingHandler();
        var factory = new SingleHandlerHttpClientFactory(handler);
        var proxy = new ThrowingProxy();
        var client = new PalworldApiClient(factory, proxy, NullLogger<PalworldApiClient>.Instance);
        return (client, handler);
    }

    private static PalworldRequestContext Ctx() => new()
    {
        ServiceName = "palworld-svc",
        Port = 8212,
        AdminPassword = "s3cret"
    };

    [Fact]
    public async Task GetInfoAsync_UsesServiceNameUrl_AndBasicAuthHeader()
    {
        var (client, handler) = Create();
        handler.RespondWith(new PalworldServerInfo { ServerName = "Test", Version = "1" });

        var info = await client.GetInfoAsync(Ctx());

        Assert.NotNull(handler.LastRequest);
        Assert.Equal("http://palworld-svc:8212/v1/api/info", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
        var auth = handler.LastRequest.Headers.Authorization;
        Assert.NotNull(auth);
        Assert.Equal("Basic", auth!.Scheme);
        var expected = Convert.ToBase64String(Encoding.ASCII.GetBytes("admin:s3cret"));
        Assert.Equal(expected, auth.Parameter);
        Assert.Equal("Test", info!.ServerName);
    }

    [Fact]
    public async Task AnnounceAsync_PostsJsonBody()
    {
        var (client, handler) = Create();
        handler.RespondWith(new { });

        await client.AnnounceAsync(Ctx(), "hello");

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("http://palworld-svc:8212/v1/api/announce", handler.LastRequest.RequestUri!.ToString());
        Assert.NotNull(handler.LastBody);
        var payload = JsonSerializer.Deserialize<JsonElement>(handler.LastBody!);
        Assert.Equal("hello", payload.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Send_TransportFailure_FallsBackToNodeAgent()
    {
        var handler = new FailingHandler();
        var factory = new SingleHandlerHttpClientFactory(handler);
        var proxy = new StubProxy(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        });
        var client = new PalworldApiClient(factory, proxy, NullLogger<PalworldApiClient>.Instance);

        await client.SaveWorldAsync(Ctx());

        Assert.True(proxy.WasCalled);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }
        private HttpResponseMessage _next = new(HttpStatusCode.OK) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };

        public void RespondWith(object obj)
        {
            _next = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(obj), Encoding.UTF8, "application/json")
            };
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
            {
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            return _next;
        }
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("transport down");
    }

    private sealed class SingleHandlerHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public SingleHandlerHttpClientFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private sealed class ThrowingProxy : INodeAgentExtensionProxy
    {
        public Task<HttpResponseMessage> SendAsync(string serviceName, HttpMethod method, string path, object? body, CancellationToken cancellationToken)
            => throw new NotImplementedException("proxy invoked unexpectedly");
    }

    private sealed class StubProxy : INodeAgentExtensionProxy
    {
        private readonly HttpResponseMessage _response;
        public bool WasCalled { get; private set; }
        public StubProxy(HttpResponseMessage response) => _response = response;
        public Task<HttpResponseMessage> SendAsync(string serviceName, HttpMethod method, string path, object? body, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(_response);
        }
    }
}
