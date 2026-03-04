namespace GameServer.Docker.Models
{
    /// <summary>
    /// Message broadcast by Primary Service to announce its presence on the network.
    /// Agents listen for these broadcasts to auto-discover and connect to Primary services.
    /// </summary>
    public class ServiceAnnouncementMessage
    {
        /// <summary>
        /// Unique identifier for this Primary Service instance.
        /// Used to distinguish between multiple Primary instances.
        /// </summary>
        public string ServiceId { get; set; } = string.Empty;

        /// <summary>
        /// HTTP endpoint where this Primary Service can be reached.
        /// Example: "http://gameserver-docker_gameserver-docker:8080"
        /// </summary>
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>
        /// Current API key for authenticating with this Primary Service.
        /// Rotated periodically for security.
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// UTC timestamp when this message was generated.
        /// Used to detect stale broadcasts.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Version of the Primary Service (e.g., "0.0.4.220").
        /// Can be used for compatibility checks.
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Capabilities supported by this Primary Service.
        /// Example: ["service-management", "agent-registration", "monitoring"]
        /// </summary>
        public List<string> Capabilities { get; set; } = new();

        /// <summary>
        /// Optional: Signature of the message for verification.
        /// Prevents message spoofing attacks.
        /// </summary>
        public string? Signature { get; set; }
    }
}
