namespace GameServer.Docker.Configurations
{
    /// <summary>
    /// Configuration options for Node Agent discovery and communication
    /// </summary>
    public class NodeAgentOptions
    {
        /// <summary>
        /// Enable background discovery of agents via Docker Swarm API polling
        /// DEPRECATED: This legacy discovery method will be removed in a future version.
        /// Use agent registration (AgentRegistry) instead for better performance and reliability.
        /// Default: true (for backward compatibility)
        /// </summary>
        [Obsolete("Background discovery via Docker Swarm polling is deprecated. Use agent registration instead. This will be removed in a future version.")]
        public bool EnableBackgroundDiscovery { get; set; } = true;

        /// <summary>
        /// The name of the Node Agent service in Docker Swarm
        /// Default: "gameserver-agent"
        /// </summary>
        public string ServiceName { get; set; } = "gameserver-agent";

        /// <summary>
        /// The overlay network name where Node Agents are deployed
        /// Default: "gameserver-network"
        /// </summary>
        public string NetworkName { get; set; } = "gameserver-network";

        /// <summary>
        /// The port the Node Agent listens on
        /// Default: 8080
        /// </summary>
        public int Port { get; set; } = 8080;

        /// <summary>
        /// HTTP timeout for agent requests in seconds
        /// Default: 5
        /// </summary>
        public int TimeoutSeconds { get; set; } = 5;

        /// <summary>
        /// How long to cache discovered agents in seconds (deprecated - use BackgroundRefreshIntervalSeconds)
        /// Default: 30
        /// </summary>
        [Obsolete("Use BackgroundRefreshIntervalSeconds instead. This property is maintained for backward compatibility.")]
        public int CacheTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// How often the background discovery task refreshes the agent list in seconds
        /// Default: 15 seconds
        /// </summary>
        public int BackgroundRefreshIntervalSeconds { get; set; } = 15;

        /// <summary>
        /// Task states to consider as "active" when discovering agents
        /// Default: ["running", "starting", "ready"]
        /// </summary>
        public List<string> ActiveTaskStates { get; set; } = new() { "running", "starting", "ready" };
    }
}
