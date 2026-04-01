namespace GameServer.Docker.Models
{
    /// <summary>
    /// Defines a relationship between ports where one port's value determines another port's value.
    /// Used for games that require multiple related ports (e.g., game port, query port, RCON port).
    /// </summary>
    [Obsolete("Use GameServer.Docker.Models.V2.GameTypeSettingPortMapping for new persistence work. This legacy port relationship model will be removed with the old repository chain.")]
    public class PortRelationship
    {
        /// <summary>
        /// The type of relationship between ports
        /// </summary>
        public PortRelationshipType RelationType { get; set; } = PortRelationshipType.Offset;

        /// <summary>
        /// The container port that this relationship targets
        /// </summary>
        public uint TargetContainerPort { get; set; }

        /// <summary>
        /// The protocol of the target port (tcp/udp)
        /// </summary>
        public string TargetProtocol { get; set; } = "udp";

        /// <summary>
        /// For Offset type: The offset to add to the source port value.
        /// For example, if the game port is 27015 and offset is +1, the target will be 27016.
        /// </summary>
        public int Offset { get; set; } = 0;

        /// <summary>
        /// For Fixed type: The fixed value to assign regardless of source port.
        /// </summary>
        public uint? FixedValue { get; set; }

        /// <summary>
        /// Human-readable description of what this related port does.
        /// Example: "Query Port", "RCON Port", "Voice Chat Port"
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Whether this port relationship is required.
        /// If true, the target port must exist and cannot be removed.
        /// </summary>
        public bool IsRequired { get; set; } = true;
    }

    /// <summary>
    /// Types of port relationships
    /// </summary>
    public enum PortRelationshipType
    {
        /// <summary>
        /// The target port is calculated by adding an offset to the source port.
        /// Example: If game port is 27015 and offset is +1, query port becomes 27016.
        /// </summary>
        Offset = 0,

        /// <summary>
        /// The target port always has a fixed value regardless of the source port.
        /// Example: RCON port always stays at 27020.
        /// </summary>
        Fixed = 1,

        /// <summary>
        /// The target port is calculated using a multiplier.
        /// Example: If game port is 27015 and multiplier is 2, target becomes 54030.
        /// </summary>
        Multiplier = 2
    }

    /// <summary>
    /// Validation rule for port settings
    /// </summary>
    [Obsolete("Use backend V2 validation services instead of legacy persisted port validation metadata. This model will be removed with the old repository chain.")]
    public class PortValidationRule
    {
        /// <summary>
        /// Minimum allowed port number (inclusive)
        /// </summary>
        public uint MinPort { get; set; } = 1024;

        /// <summary>
        /// Maximum allowed port number (inclusive)
        /// </summary>
        public uint MaxPort { get; set; } = 65535;

        /// <summary>
        /// List of port numbers that are reserved and cannot be used
        /// </summary>
        public List<uint>? ReservedPorts { get; set; }

        /// <summary>
        /// Whether to check if the port is available on the host system before assignment
        /// </summary>
        public bool CheckAvailability { get; set; } = true;

        /// <summary>
        /// Custom validation error message
        /// </summary>
        public string? ValidationMessage { get; set; }

        /// <summary>
        /// Whether to allow the port to be changed by the user.
        /// If false, the port is managed automatically by the system.
        /// </summary>
        public bool IsUserEditable { get; set; } = true;

        /// <summary>
        /// Suggested/recommended port numbers for this setting
        /// </summary>
        public List<uint>? SuggestedPorts { get; set; }
    }

    /// <summary>
    /// Represents a web-accessible endpoint configuration for a port setting.
    /// Defines how a game server port should be exposed via load balancer (HTTP/HTTPS/TCP).
    /// </summary>
    [Obsolete("Use GameServer.Docker.Models.V2.GameTypeWebHost for new persistence work. This legacy web host model will be removed with the old repository chain.")]
    public class WebHost
    {
        /// <summary>
        /// Protocol for the web host (http, https, tcp, udp)
        /// </summary>
        public string Protocol { get; set; } = "https";

        /// <summary>
        /// Subdomain pattern for URL generation.
        /// Supports variables: {serverName}, {serverId}
        /// Example: "{serverName}" becomes "myserver.example.com"
        /// Example: "{serverName}-admin" becomes "myserver-admin.example.com"
        /// </summary>
        public string SubdomainPattern { get; set; } = "{serverName}";

        /// <summary>
        /// Source of the port value: "Setting" or "ContainerPort"
        /// </summary>
        public string PortSource { get; set; } = "Setting";

        /// <summary>
        /// When PortSource="Setting", the setting key to read the port from
        /// </summary>
        public string? PortSettingKey { get; set; }

        /// <summary>
        /// When PortSource="ContainerPort", the container port number
        /// </summary>
        public uint? PortContainerPort { get; set; }

        /// <summary>
        /// Priority/order for this web host (lower = higher priority)
        /// </summary>
        public int Priority { get; set; } = 1;

        /// <summary>
        /// Whether to enable load balancer routing for this endpoint
        /// </summary>
        public bool EnableLoadBalancer { get; set; } = true;
    }
}
