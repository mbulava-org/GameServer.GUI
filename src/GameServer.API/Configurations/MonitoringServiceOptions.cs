namespace GameServer.API.Configurations;

/// <summary>
/// Configuration for the standalone Monitoring service (GameServer.Monitoring.Host).
/// Read from appsettings.json "MonitoringService" section.
/// </summary>
public class MonitoringServiceOptions
{
    public const string SectionName = "MonitoringService";

    /// <summary>Base URL of the Monitoring host (e.g. http://localhost:5166 or http://gameserver-monitoring:8080).</summary>
    public string BaseUrl { get; set; } = "http://localhost:5166";
}
