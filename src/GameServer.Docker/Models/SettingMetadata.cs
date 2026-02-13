namespace GameServer.Docker.Models
{
    /// <summary>
    /// Metadata for a game type setting/environment variable
    /// </summary>
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
        /// Supported values: "string", "number", "boolean", "list", "enum", "port"
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
        /// For "enum" data types, the list of allowed values.
        /// Displayed as a dropdown in the UI.
        /// Example: ["peaceful", "easy", "normal", "hard"]
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
    }
}
