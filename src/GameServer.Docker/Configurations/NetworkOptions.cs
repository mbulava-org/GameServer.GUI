namespace GameServer.Docker.Configurations
{
    /// <summary>
    /// Network configuration options for Docker services.
    /// </summary>
    public class NetworkOptions
    {
        /// <summary>
        /// The network name used for agent-to-primary service communication.
        /// Services typically don't need to join this network as they expose their needed ports.
        /// May be used in the future for service-to-service communication.
        /// </summary>
        public string? NetworkName { get; set; }

        /// <summary>
        /// The network name where the load balancer (e.g., Traefik) is running.
        /// Services with web hosts will be attached to this network for reverse proxy discovery.
        /// Default: "traefik-public"
        /// </summary>
        public string? LoadBalancerNetwork { get; set; } = "traefik-public";

        /// <summary>
        /// The load balancer provider to use for generating discovery labels.
        /// Supported values: "traefik", "none"
        /// Default: "traefik"
        /// </summary>
        public string LoadBalancerProvider { get; set; } = "traefik";
    }
}
