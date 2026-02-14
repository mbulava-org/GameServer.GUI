namespace GameServer.Docker.Models
{
    /// <summary>
    /// Extended metadata for a GameType that provides additional configuration options
    /// beyond the basic GameTypeDefinition
    /// </summary>
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
    }
}
