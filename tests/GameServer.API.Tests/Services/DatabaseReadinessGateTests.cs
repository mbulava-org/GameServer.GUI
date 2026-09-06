using GameServer.API.Services;

namespace GameServer.API.Tests.Services
{
    public class DatabaseReadinessGateTests
    {
        [Fact]
        public void IsReady_InitialState_ShouldBeFalse()
        {
            var gate = new DatabaseReadinessGate();
            Assert.False(gate.IsReady);
        }

        [Fact]
        public void MarkReady_ShouldSetIsReadyToTrue()
        {
            var gate = new DatabaseReadinessGate();
            gate.MarkReady();
            Assert.True(gate.IsReady);
        }

        [Fact]
        public async Task WaitUntilReadyAsync_WhenAlreadyReady_ShouldReturnImmediately()
        {
            var gate = new DatabaseReadinessGate();
            gate.MarkReady();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await gate.WaitUntilReadyAsync(cts.Token);
            Assert.True(gate.IsReady);
        }

        [Fact]
        public async Task WaitUntilReadyAsync_WhenMarkedReadyAsynchronously_ShouldComplete()
        {
            var gate = new DatabaseReadinessGate();

            var waitTask = Task.Run(async () =>
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await gate.WaitUntilReadyAsync(cts.Token);
            });

            await Task.Delay(50);
            gate.MarkReady();

            await waitTask;
            Assert.True(gate.IsReady);
        }

        [Fact]
        public async Task WaitUntilReadyAsync_WhenCancelled_ShouldThrow()
        {
            var gate = new DatabaseReadinessGate();
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gate.WaitUntilReadyAsync(cts.Token));
        }
    }
}
