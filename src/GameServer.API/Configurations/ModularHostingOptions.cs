namespace GameServer.API.Configurations;

/// <summary>
/// Controls how the GameServer.API host cooperates with the standalone
/// GameServer.Orchestration.Host and GameServer.Monitoring.Host services.
/// Read from appsettings.json "ModularHosting" section.
/// </summary>
/// <remarks>
/// <para>
/// The default values keep GameServer.API self-contained (in-process orchestration,
/// all hubs mapped locally) so single-host deployments (see
/// <c>docs/samples/docker-stack.yml</c>) continue to work without configuration.
/// </para>
/// <para>
/// The modular sample stack (<c>docs/samples/docker-stack.modular.yml</c>) sets
/// <see cref="HostHubsInApi"/> to <c>false</c> and <see cref="UseHttpAgentRegistry"/>
/// to <c>true</c> so agent registration + streaming hubs live only on the
/// standalone hosts and the API becomes a pure REST + OpenAPI surface.
/// </para>
/// </remarks>
public class ModularHostingOptions
{
    public const string SectionName = "ModularHosting";

    /// <summary>
    /// When <c>true</c> (default) the API maps the shared streaming hubs
    /// (<c>/hubs/attach</c>, <c>/hubs/terminal</c>, <c>/hubs/serverlogs</c>,
    /// <c>/hubs/resources</c>) and the <c>/hubs/agentregistration</c> hub
    /// in-process. Set to <c>false</c> when deploying
    /// <see href="../../docs/samples/docker-stack.modular.yml">docker-stack.modular.yml</see>
    /// so the standalone hosts own those endpoints.
    /// </summary>
    public bool HostHubsInApi { get; set; } = true;

    /// <summary>
    /// When <c>true</c>, the API replaces the in-process
    /// <see cref="Interfaces.IAgentRegistry"/> and
    /// <see cref="Interfaces.INodeAgentDiscovery"/> registrations with
    /// HTTP client implementations that call
    /// <c>GameServer.Orchestration.Host</c>. Defaults to <c>false</c>
    /// so existing single-host deployments keep the in-process registry.
    /// </summary>
    public bool UseHttpAgentRegistry { get; set; } = false;
}
