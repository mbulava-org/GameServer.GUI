namespace GameServer.Docker.Models
{
    /// <summary>
    /// Metadata for a game type setting/environment variable
    /// </summary>
    [Obsolete("Use GameServer.Docker.Models.V2.GameTypeSettingDefinition and related V2 metadata models for new persistence work. This legacy metadata model will be removed with the old repository chain.")]
    public class SettingMetadata
    {
        /// <summary>
        /// The setting/environment variable name (e.g., "EULA", "VERSION")
        /// </summary>
        public string Key { get; set; } = "";

        /// <summary>
        /// Human-readable description displayed in the editor
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Whether this setting is required (must be provided by user)
        /// </summary>
        public bool IsRequired { get; set; } = false;

        /// <summary>
        /// Whether this setting cannot be empty/blank
        /// </summary>
        public bool CannotBeEmpty { get; set; } = false;

        /// <summary>
        /// The data type for this setting. Overrides automatic type detection.
        /// Supported values: "string", "number", "boolean", "list", "enum", "port", "timezone"
        /// </summary>
        public string? DataType { get; set; }

        /// <summary>
        /// When true, this setting's value controls a port in the GameTypeDefinition.Ports list.
        /// The LinkedContainerPort specifies which port to update.
        /// For example, if SERVER_PORT setting is "25566", the port mapping with 
        /// LinkedContainerPort=25565 will be updated to expose 25566 instead.
        /// </summary>
        public bool MapsToContainerPort { get; set; } = false;

        /// <summary>
        /// The original container port number that this setting controls.
        /// When MapsToContainerPort is true, this identifies which port in 
        /// GameTypeDefinition.Ports to update with the setting's value.
        /// </summary>
        public uint? LinkedContainerPort { get; set; }

        /// <summary>
        /// The protocol for the linked port (default: "tcp")
        /// </summary>
        public string PortProtocol { get; set; } = "tcp";

        /// <summary>
        /// For "list" data types, the delimiter used to split values (default: ",")
        /// </summary>
        public string ListDelimiter { get; set; } = ",";

        /// <summary>
        /// For "enum" and "timezone" data types, the list of allowed values.
        /// Displayed as a dropdown in the UI.
        /// Example for enum: ["peaceful", "easy", "normal", "hard"]
        /// For timezone: This is automatically populated with available time zones.
        /// </summary>
        public List<string>? AllowedValues { get; set; }

        /// <summary>
        /// For settings with semantic value meanings, maps the value to a description.
        /// Example: { "0": "Disabled", "1": "Normal", "2": "Aggressive" }
        /// </summary>
        public Dictionary<string, string>? ValueMappings { get; set; }

        /// <summary>
        /// Display order hint for UI (lower numbers appear first)
        /// </summary>
        public int DisplayOrder { get; set; } = 0;

        /// <summary>
        /// Category or group name for organizing settings in the UI
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Placeholder text to display in the editor
        /// </summary>
        public string? Placeholder { get; set; }

        /// <summary>
        /// Validation regex pattern (optional)
        /// </summary>
        public string? ValidationPattern { get; set; }

        /// <summary>
        /// Error message to display when validation fails
        /// </summary>
        public string? ValidationMessage { get; set; }

        /// <summary>
        /// For port-type settings (MapsToContainerPort = true), defines relationships
        /// with other ports that should be automatically updated when this port changes.
        /// Example: When game port changes from 27015 to 28015, query port (game+1) 
        /// should automatically change from 27016 to 28016.
        /// </summary>
        public List<PortRelationship>? PortRelationships { get; set; }

        /// <summary>
        /// For port-type settings, defines validation rules for the port value.
        /// Includes min/max ranges, reserved ports, and availability checking.
        /// </summary>
        public PortValidationRule? PortValidation { get; set; }

        /// <summary>
        /// For port-type settings, whether this port must remain synchronized with
        /// another port setting. Used for dependent ports that cannot be changed independently.
        /// </summary>
        public string? SynchronizedWithSetting { get; set; }

        /// <summary>
        /// For port-type settings, whether changes to this port should automatically
        /// allocate/deallocate ports from the port management system.
        /// </summary>
        public bool AutoAllocatePort { get; set; } = false;

        /// <summary>
        /// For port-type settings, whether to validate that all related ports 
        /// (defined in PortRelationships) are available before allowing the change.
        /// </summary>
        public bool ValidateRelatedPortsAvailability { get; set; } = true;

        /// <summary>
        /// For port-type settings, defines web-accessible endpoints (HTTP/HTTPS) that use this port.
        /// Includes protocol, subdomain pattern, and load balancer configuration.
        /// Example: HTTP/HTTPS endpoints, TCP stream endpoints, etc.
        /// </summary>
        public List<WebHost>? WebHosts { get; set; }
    }
}
