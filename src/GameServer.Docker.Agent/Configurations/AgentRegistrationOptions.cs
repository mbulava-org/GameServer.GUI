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
        /// Capabilities this agent supports.
        /// 
        /// Default: logs, exec, stats, attach, services
        /// 
        /// AUTOMATIC FILTERING:
        /// Manager-only capabilities ('services', 'tasks', 'nodes', 'swarm') are automatically 
        /// removed for worker nodes. Only manager nodes can perform Docker Swarm management operations.
        /// 
        /// Container Capabilities (all nodes):
        /// - logs: Stream container logs
        /// - exec: Execute commands in containers
        /// - stats: Get container resource stats
        /// - attach: Attach to container TTY
        /// 
        /// Manager-Only Capabilities (manager nodes only):
        /// - services: Create/update/delete/list Docker Swarm services
        /// - tasks: List and inspect Docker Swarm tasks
        /// - nodes: List and manage Docker Swarm nodes
        /// - swarm: Inspect and configure Docker Swarm cluster
        /// </summary>
        public List<string> Capabilities { get; set; } = new() { "logs", "exec", "stats", "attach", "services" };

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

        /// <summary>
        /// Maximum number of connection attempts at startup before giving up
        /// Default: 30 attempts (with exponential backoff, ~5-10 minutes total)
        /// Set to 0 or negative for unlimited retries
        /// </summary>
        public int MaxStartupRetries { get; set; } = 30;

        /// <summary>
        /// Base delay in seconds between startup connection retry attempts
        /// Uses exponential backoff: delay * 1.5^(attempt-1), capped at 60s
        /// Default: 5 seconds
        /// </summary>
        public int StartupRetryDelaySeconds { get; set; } = 5;
    }
}
