using System.Runtime.CompilerServices;
using GameServer.API.Interfaces;
using GameServer.API.Models;
using GameServer.API.Services.V2;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace GameServer.API.Tests.Services.V2
{
    public class ServerResourceAggregatorTests
    {
        private class TestResourceMonitor : IServerResourceMonitor
        {
            public async IAsyncEnumerable<ServerResourceUsage> StreamResourceUsageAsync(string serverId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                for (int i = 0; i < 10 && !cancellationToken.IsCancellationRequested; i++)
                {
                    yield return new ServerResourceUsage { ServerId = serverId, DesiredReplicas = 1, RunningReplicas = 1 };
                    await Task.Delay(20, cancellationToken);
                }
            }

            public Task<ServerResourceUsage?> GetSnapshotAsync(string serverId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<ServerResourceUsage?>(new ServerResourceUsage { ServerId = serverId, DesiredReplicas = 1, RunningReplicas = 1 });
            }
        }

        private readonly Mock<ILogger<ServerResourceAggregator>> _loggerMock;
        private readonly IServiceProvider _serviceProvider;
        private readonly ServerResourceAggregator _aggregator;

        public ServerResourceAggregatorTests()
        {
            _loggerMock = new Mock<ILogger<ServerResourceAggregator>>();

            var services = new ServiceCollection();
            services.AddScoped<IServerResourceMonitor, TestResourceMonitor>();
            _serviceProvider = services.BuildServiceProvider();

            _aggregator = new ServerResourceAggregator(_serviceProvider, _loggerMock.Object);
        }

        [Fact]
        public async Task GetSnapshotAsync_WithEmptyServerId_ShouldThrowArgumentException()
        {
            await Assert.ThrowsAnyAsync<ArgumentException>(() => _aggregator.GetSnapshotAsync(""));
        }

        [Fact]
        public async Task GetSnapshotAsync_WithValidServerId_ShouldDelegateToMonitor()
        {
            var result = await _aggregator.GetSnapshotAsync("server-1");

            Assert.NotNull(result);
            Assert.Equal("server-1", result.ServerId);
            Assert.Equal(1, result.RunningReplicas);
        }

        [Fact]
        public async Task StreamResourceUsageAsync_ShouldYieldStreamedData()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var results = new List<ServerResourceUsage>();

            await foreach (var item in _aggregator.StreamResourceUsageAsync("server-1", 1, cts.Token))
            {
                results.Add(item);
                if (results.Count >= 1) break;
            }

            Assert.NotEmpty(results);
            Assert.Equal("server-1", results[0].ServerId);
        }

        [Fact]
        public async Task DisposeAsync_ShouldCleanUpWithoutError()
        {
            await _aggregator.DisposeAsync();
        }
    }
}
