namespace GameServer.Docker.Configurations;

/// <summary>
/// Configures the V2 persistence provider and connection settings.
/// </summary>
public sealed class V2DatabaseOptions
{
    public const string SectionName = "V2Database";

    public string Provider { get; set; } = "Sqlite";

    public string? ConnectionStringName { get; set; } = "GameServerV2Db";
}
