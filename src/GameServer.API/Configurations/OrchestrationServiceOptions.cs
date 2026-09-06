namespace GameServer.API.Configurations;

/// <summary>
/// Configuration for the standalone Orchestration service (GameServer.Orchestration.Host).
/// Read from appsettings.json "OrchestrationService" section.
/// </summary>
public class OrchestrationServiceOptions
{
    public const string SectionName = "OrchestrationService";

    /// <summary>Base URL of the Orchestration Host REST API, e.g. http://gameserver-orchestration:8080</summary>
    public string BaseUrl { get; set; } = "http://gameserver-orchestration:8080";
}
