using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GameServer.Docker.Client.Interfaces;
using GameServer.Docker.Client.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameServer.Docker.Client.Tests
{
    public class ContainerConsoleClientTests : IDisposable
    {
        private readonly TestServer _server;
        private readonly HubConnection _connection;
        private readonly ContainerConsoleClient _client;
        private readonly FakeAttachHub _hub;

        public ContainerConsoleClientTests()
        {
            _hub = new FakeAttachHub();

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
                        endpoints.MapHub<FakeAttachHub>("/hubs/attach");
                    });
                });

            _server = new TestServer(builder);

            _connection = new HubConnectionBuilder()
                .WithUrl("http://localhost/hubs/attach", options =>
                {
                    options.HttpMessageHandlerFactory = _ => _server.CreateHandler();
                    options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
                })
                .Build();

            _client = new ContainerConsoleClient(_connection, NullLogger<ContainerConsoleClient>.Instance);
        }

        [Fact]
        public async Task ConnectAsync_ShouldMarkConnected()
        {
            await _client.ConnectAsync(CancellationToken.None);

            Assert.True(_client.IsConnected);
        }

        [Fact]
        public async Task AttachToContainerAsync_WithExplicitContainerId_ShouldSetContainerIdAndFireConnected()
        {
            var tcs = new TaskCompletionSource<string>();
            _client.Connected += (sender, containerId) => tcs.TrySetResult(containerId);

            await _client.ConnectAsync(CancellationToken.None);
            await _client.AttachToContainerAsync("server-1", "container-1", CancellationToken.None);

            await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(2)));

            Assert.Equal("container-1", _client.AttachedContainerId);
            Assert.Equal("container-1", _hub.LastContainerId);
        }

        [Fact]
        public async Task OutputReceived_ShouldFireOnStreamOutput()
        {
            var tcs = new TaskCompletionSource<string>();
            _client.OutputReceived += (sender, output) => tcs.TrySetResult(output);

            var frame = _hub.SerializeFrame(0, "hello world");
            _hub.EnqueueFrame(frame);

            await _client.ConnectAsync(CancellationToken.None);
            _ = _client.AttachToContainerAsync("server-1", "container-1");

            var output = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal("hello world", output);
        }

        [Fact]
        public async Task InputControlChanged_ShouldFireOnStreamControlFrame()
        {
            var tcs = new TaskCompletionSource<string>();
            _client.InputControlChanged += (sender, controllerId) => tcs.TrySetResult(controllerId);

            var frame = _hub.SerializeFrame(1, "conn-abc");
            _hub.EnqueueFrame(frame);

            await _client.ConnectAsync(CancellationToken.None);
            _ = _client.AttachToContainerAsync("server-1", "container-1");

            var controllerId = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal("conn-abc", controllerId);
        }

        [Fact]
        public async Task SendInputAsync_ShouldDeliverInputToHub()
        {
            await _client.ConnectAsync(CancellationToken.None);
            await _client.AttachToContainerAsync("server-1", "container-1");

            await Task.Delay(300);
            await _client.SendInputAsync("help");

            Assert.Equal("help", _hub.LastInput);
        }

        [Fact]
        public async Task DisconnectFromContainerAsync_ShouldResetContainerId()
        {
            await _client.ConnectAsync(CancellationToken.None);
            await _client.AttachToContainerAsync("server-1", "container-1");

            await _client.DisconnectFromContainerAsync();

            Assert.Null(_client.AttachedContainerId);
        }

        [Fact]
        public async Task SendInputAsync_WhenNotAttached_ShouldThrowInvalidOperationException()
        {
            await _client.ConnectAsync(CancellationToken.None);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _client.SendInputAsync("x"));
        }

        public void Dispose()
        {
            _connection.DisposeAsync().AsTask().Wait();
            _server.Dispose();
        }
    }

    public class FakeAttachHub : Hub
    {
        public string? LastContainerId { get; private set; }
        public string? LastInput { get; private set; }

        private readonly Queue<string> _frames = new();

        public void EnqueueFrame(string frame) => _frames.Enqueue(frame);

        public string SerializeFrame(int kind, string payload) =>
            JsonSerializer.Serialize(new { Kind = kind, Payload = payload });

        public async IAsyncEnumerable<string> SubscribeToContainer(
            string serverId,
            string? containerId,
            bool timestamps,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            LastContainerId = containerId ?? "resolved-container";

            while (!cancellationToken.IsCancellationRequested)
            {
                if (_frames.Count > 0)
                {
                    yield return _frames.Dequeue();
                }

                await Task.Delay(50, cancellationToken);
            }
        }

        public async Task<bool> SendInput(string containerId, string input)
        {
            LastContainerId = containerId;
            LastInput = input;
            await Task.CompletedTask;
            return true;
        }

        public async Task DisconnectFromContainer(string containerId)
        {
            await Task.CompletedTask;
        }


    }
}
