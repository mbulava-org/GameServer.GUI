namespace GameServer.Docker.Agent.Configurations
{
    /// <summary>
    /// Configuration options for agent registration with Primary Service
    /// </summary>
    public class AgentRegistrationOptions
    {
        /// <summary>
        /// URL of the Primary Service to register with
        /// Example: http://gameserver-docker:8080
        /// </summary>
        public string PrimaryServiceUrl { get; set; } = string.Empty;

        /// <summary>
        /// Interval in seconds between heartbeat messages
        /// Default: 30 seconds
        /// </summary>
        public int HeartbeatIntervalSeconds { get; set; } = 30;

        /// <summary>
        /// Whether agent registration is enabled
        /// Set to false to disable the new registration system
        /// Default: true
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Capabilities this agent supports
        /// Default: logs, exec, stats, attach
        /// </summary>
        public List<string> Capabilities { get; set; } = new() { "logs", "exec", "stats", "attach" };

        /// <summary>
        /// Timeout in seconds for initial connection to Primary Service
        /// Default: 30 seconds
        /// </summary>
        public int ConnectionTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Automatic reconnect intervals for SignalR connection
        /// Default: 0, 2, 10, 30 seconds
        /// </summary>
        public List<int> ReconnectDelaySeconds { get; set; } = new() { 0, 2, 10, 30 };
    }
}
