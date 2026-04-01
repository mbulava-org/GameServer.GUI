namespace GameServer.Docker.Models
{
    /// <summary>
    /// Extended metadata for a GameType that provides additional configuration options
    /// beyond the basic GameTypeDefinition
    /// </summary>
    [Obsolete("Use GameServer.Docker.Models.V2.GameTypeRevision and related V2 metadata models for new persistence work.")]
    public class GameTypeExtendedMetadata
    {
        /// <summary>
        /// The GameType key this metadata applies to (must match GameTypeDefinition.Key)
        /// </summary>
        public string GameTypeKey { get; set; } = "";

        /// <summary>
        /// Whether to enable TTY (pseudo-terminal) attachment for this service.
        /// Useful for interactive console-based game servers.
        /// </summary>
        public bool EnableTTY { get; set; } = false;

        /// <summary>
        /// Metadata for individual settings/environment variables
        /// Key: Setting name (e.g., "EULA", "VERSION")
        /// Value: SettingMetadata object with validation rules and display info
        /// </summary>
        public Dictionary<string, SettingMetadata> SettingsMetadata { get; set; } = new();

        /// <summary>
        /// Additional custom metadata for extensibility
        /// </summary>
        public Dictionary<string, string> CustomProperties { get; set; } = new();

        ///<summary>
        ///Port the container exposes as a Management GUI, if any.
        ///</summary>
        public uint? ManagementUIPort { get; set; }

        /// <summary>
        /// Web hosts exposed by this container for reverse proxy configuration.
        /// Each host will automatically get a unique URL path when service is created.
        /// Routes are only generated if the host's EnabledWhen condition is satisfied.
        /// </summary>
        public List<WebHostDefinition> WebHosts { get; set; } = new();
    }

    /// <summary>
    /// Defines a web interface exposed by a game server container that should be
    /// accessible through a reverse proxy/load balancer.
    /// </summary>
    [Obsolete("Use GameServer.Docker.Models.V2.GameTypeWebHost for new persistence work. This legacy web host definition will be removed with the old repository chain.")]
    public class WebHostDefinition
    {
        /// <summary>
        /// Display name for this web interface (e.g., "Dynmap", "Admin Panel", "Metrics")
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Container port this web interface listens on (used when ContainerPortVariable is null)
        /// </summary>
        public int ContainerPort { get; set; }

        /// <summary>
        /// Brief description of what this interface provides
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Optional path segment (defaults to lowercase name)
        /// E.g., "dynmap" -> /game-{serverId}/dynmap/
        /// </summary>
        public string? PathSegment { get; set; }

        /// <summary>
        /// Whether this host requires authentication (future use)
        /// </summary>
        public bool RequiresAuth { get; set; } = false;

        /// <summary>
        /// Optional condition that must be true for this host to be exposed.
        /// Format: "VARIABLE_NAME=value" or "VARIABLE_NAME!=value"
        /// Example: "DYNMAP_ENABLED=true" or "WEB_MODE!=disabled"
        /// If null or empty, host is always enabled.
        /// </summary>
        public string? EnabledWhen { get; set; }

        /// <summary>
        /// Optional environment variable name to read the actual container port from.
        /// If set, ContainerPort is ignored and the port is read from the server's settings.
        /// Example: "WEBUI_PORT" - will use the value of WEBUI_PORT setting as the container port
        /// </summary>
        public string? ContainerPortVariable { get; set; }
    }
}
