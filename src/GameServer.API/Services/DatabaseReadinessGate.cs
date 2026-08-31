namespace GameServer.API.Services
{
    /// <summary>
    /// Tracks whether background database initialization (schema migration + seeding) has completed.
    /// Used to gate API requests so they don't hit a database that is mid-migration, which can
    /// otherwise surface as transient "Unknown column" errors while columns are being renamed/added.
    /// </summary>
    public interface IDatabaseReadinessGate
    {
        bool IsReady { get; }

        void MarkReady();

        Task WaitUntilReadyAsync(CancellationToken cancellationToken = default);
    }

    public class DatabaseReadinessGate : IDatabaseReadinessGate
    {
        private readonly TaskCompletionSource<bool> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsReady => _tcs.Task.IsCompletedSuccessfully;

        public void MarkReady() => _tcs.TrySetResult(true);

        public async Task WaitUntilReadyAsync(CancellationToken cancellationToken = default)
        {
            if (IsReady)
            {
                return;
            }

            await _tcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}

