namespace GameServer.Docker.Constants;

/// <summary>
/// Docker Swarm service label keys used to identify and manage GameServer services.
/// These labels are applied to services to mark them as managed GameServers.
/// </summary>
public static class ServiceLabels
{
    /// <summary>
    /// Label key to identify a service as a managed GameServer.
    /// Value should be "true" for managed services.
    /// </summary>
    public const string Managed = "gameserver.docker.managed";
    
    /// <summary>
    /// Label key for the unique GameServer ID.
    /// </summary>
    public const string ServerId = "gameserver.docker.Id";
    
    /// <summary>
    /// Label key for the GameServer display name.
    /// </summary>
    public const string Name = "gameserver.docker.name";
    
    /// <summary>
    /// Label key for the GameServer description.
    /// </summary>
    public const string Description = "gameserver.docker.description";
    
    /// <summary>
    /// Label key for the GameServer game type (e.g., "minecraft", "valheim").
    /// </summary>
    public const string GameType = "gameserver.docker.gametype";
    
    /// <summary>
    /// Expected value for the Managed label to indicate a managed service.
    /// </summary>
    public const string ManagedValue = "true";
}
