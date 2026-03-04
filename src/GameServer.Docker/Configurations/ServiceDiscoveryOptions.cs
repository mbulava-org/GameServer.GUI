namespace GameServer.Docker.Configurations
{
    /// <summary>
    /// Configuration options for UDP-based service discovery.
    /// Used by both Primary Service (broadcaster) and Agents (listeners).
    /// Zero-configuration design: Primary auto-detects its IP and subnet broadcast address,
    /// Agents listen on the same subnet and auto-discover without needing configured endpoints.
    /// </summary>
    public class ServiceDiscoveryOptions
    {
        /// <summary>
        /// Enable or disable UDP-based service discovery.
        /// When disabled, falls back to configured endpoints.
        /// Default: true
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// UDP port for broadcasting/listening.
        /// Default: 5000
        /// Must be the same for Primary and Agents.
        /// </summary>
        public int Port { get; set; } = 5000;

        /// <summary>
        /// [Primary Only] How often to broadcast presence (in seconds).
        /// Default: 5 seconds
        /// Lower values = faster discovery, but more network traffic
        /// </summary>
        public int BroadcastIntervalSeconds { get; set; } = 5;

        /// <summary>
        /// [Primary Only] How often to rotate API keys (in minutes).
        /// Default: 5 minutes
        /// Shorter intervals = more secure, but more frequent key updates
        /// </summary>
        public int ApiKeyRotationMinutes { get; set; } = 5;

        /// <summary>
        /// [Agent Only] How long to wait before considering a Primary stale (in seconds).
        /// If no broadcast received for this duration, Primary is removed from registry.
        /// Default: 30 seconds (6x broadcast interval for reliability)
        /// </summary>
        public int PrimaryStaleTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// [Agent Only] Fallback endpoint if no Primary discovered via UDP.
        /// Used for backward compatibility and emergency failover.
        /// Example: "http://10.0.1.5:8080"
        /// If not set and no Primary discovered, agent will wait indefinitely.
        /// </summary>
        public string? FallbackEndpoint { get; set; }

        /// <summary>
        /// Maximum size of UDP message buffer (in bytes).
        /// Default: 8192 (8KB)
        /// Should be large enough for ServiceAnnouncementMessage JSON.
        /// </summary>
        public int MaxMessageSize { get; set; } = 8192;
    }
}
