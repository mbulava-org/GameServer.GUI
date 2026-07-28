using System;
using System.Threading;
using System.Threading.Tasks;
using GameServer.Docker.Client.Interfaces;
using GameServer.Docker.Client.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameServer.Docker.Client.Tests
{
    public class ContainerTerminalClientTests : IDisposable
    {
        private readonly TestServer _server;
        private readonly HubConnection _connection;
        private readonly ContainerTerminalClient _client;
        private readonly FakeTerminalHub _hub;

        public ContainerTerminalClientTests()
        {
            _hub = new FakeTerminalHub();

            var builder = new WebHostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSignalR();
                    services.AddSingleton(_hub);
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapHub<FakeTerminalHub>("/hubs/terminal");
                    });
                });

            _server = new TestServer(builder);

            _connection = new HubConnectionBuilder()
                .WithUrl("http://localhost/hubs/terminal", options =>
                {
                    options.HttpMessageHandlerFactory = _ => _server.CreateHandler();
                    options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
                })
                .Build();

            _client = new ContainerTerminalClient(_connection, NullLogger<ContainerTerminalClient>.Instance);
        }

        [Fact]
        public async Task ConnectAsync_ShouldMarkConnected()
        {
            Assert.False(_client.IsConnected);

            await _client.ConnectAsync(CancellationToken.None);

            Assert.True(_client.IsConnected);
        }

        [Fact]
        public async Task StartExecSessionAsync_ShouldReturnTrueAndSetContainerId()
        {
            await _client.ConnectAsync(CancellationToken.None);

            var result = await _client.StartExecSessionAsync("container-1", "/bin/bash");

            Assert.True(result);
            Assert.Equal("container-1", _client.ContainerId);
            Assert.Equal("container-1", _hub.LastContainerId);
            Assert.Equal("/bin/bash", _hub.LastShell);
        }

        [Fact]
        public async Task SendInputAsync_ShouldDeliverInputToHub()
        {
            await _client.ConnectAsync(CancellationToken.None);
            await _client.StartExecSessionAsync("container-1");

            await _client.SendInputAsync("ls -la");

            Assert.Equal("ls -la", _hub.LastInput);
        }

        [Fact]
        public async Task OutputReceived_ShouldFireOnHubOutput()
        {
            var tcs = new TaskCompletionSource<string>();
            _client.OutputReceived += (sender, output) => tcs.TrySetResult(output);

            await _client.ConnectAsync(CancellationToken.None);
            await _client.StartExecSessionAsync("container-1");

            await _hub.BroadcastOutput("hello");

            var output = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal("hello", output);
        }

        [Fact]
        public async Task ErrorReceived_ShouldFireOnHubError()
        {
            var tcs = new TaskCompletionSource<string>();
            _client.ErrorReceived += (sender, error) => tcs.TrySetResult(error);

            await _client.ConnectAsync(CancellationToken.None);
            await _client.StartExecSessionAsync("container-1");

            await _hub.BroadcastError("boom");

            var error = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal("boom", error);
        }

        [Fact]
        public async Task SessionStarted_ShouldFireOnHubSessionStarted()
        {
            var tcs = new TaskCompletionSource<string>();
            _client.SessionStarted += (sender, sid) => tcs.TrySetResult(sid);

            await _client.ConnectAsync(CancellationToken.None);
            await _client.StartExecSessionAsync("container-1");

            var sid = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.NotNull(sid);
        }

        [Fact]
        public async Task DisconnectAsync_ShouldResetContainerId()
        {
            await _client.ConnectAsync(CancellationToken.None);
            await _client.StartExecSessionAsync("container-1");

            await _client.DisconnectAsync();

            Assert.Null(_client.ContainerId);
            Assert.True(_hub.DisconnectCalled);
        }

        [Fact]
        public async Task SendInputAsync_WhenNotConnected_ShouldThrowInvalidOperationException()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _client.SendInputAsync("x"));
        }

        public void Dispose()
        {
            _connection.DisposeAsync().AsTask().Wait();
            _server.Dispose();
        }
    }

    public class FakeTerminalHub : Hub
    {
        public string? LastContainerId { get; private set; }
        public string? LastShell { get; private set; }
        public string? LastInput { get; private set; }
        public bool DisconnectCalled { get; private set; }

        public async Task<bool> StartExecSession(string containerId, string shell)
        {
            LastContainerId = containerId;
            LastShell = shell;
            await Clients.Caller.SendAsync("SessionStarted", Context.ConnectionId);
            await Clients.Caller.SendAsync("Connected", containerId);
            return true;
        }

        public async Task SendInput(string sessionId, string input)
        {
            LastInput = input;
            await Task.CompletedTask;
        }

        public async Task Disconnect()
        {
            DisconnectCalled = true;
            await Task.CompletedTask;
        }

        public async Task BroadcastOutput(string output)
        {
            await Clients.All.SendAsync("Output", output);
        }

        public async Task BroadcastError(string error)
        {
            await Clients.All.SendAsync("Error", error);
        }
    }
}
