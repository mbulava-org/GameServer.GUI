namespace GameServer.Docker.Services
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
    }

    public class DatabaseReadinessGate : IDatabaseReadinessGate
    {
        private volatile bool _isReady;

        public bool IsReady => _isReady;

        public void MarkReady() => _isReady = true;
    }
}
