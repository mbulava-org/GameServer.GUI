namespace GameServer.Docker.Configurations
{
    /// <summary>
    /// Configuration for selecting the backing datastore provider.
    /// </summary>
    public sealed class DataStoreOptions
    {
        public const string SectionName = "DataStore";

        public string Provider { get; set; } = DataStoreProviders.Sqlite;
    }

    /// <summary>
    /// Supported datastore provider names.
    /// </summary>
    public static class DataStoreProviders
    {
        public const string Sqlite = "Sqlite";
        public const string MySql = "MySql";

        public static bool IsMySql(string? provider)
        {
            return string.Equals(provider, MySql, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSqlite(string? provider)
        {
            return string.IsNullOrWhiteSpace(provider) ||
                   string.Equals(provider, Sqlite, StringComparison.OrdinalIgnoreCase);
        }
    }
}
