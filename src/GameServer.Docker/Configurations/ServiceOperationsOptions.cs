namespace GameServer.Docker.Configurations
{
    /// <summary>
    /// Configuration for service operations
    /// </summary>
    public class ServiceOperationsOptions
    {
        /// <summary>
        /// Method to use for service operations.
        /// Options: "Direct" (connect to Docker), "Agent" (delegate to manager agent)
        /// Default: "Direct" (for backward compatibility)
        /// </summary>
        public string Mode { get; set; } = "Direct";

        /// <summary>
        /// Whether to enable service operations at all.
        /// If false, service creation/update/delete will fail.
        /// Default: true
        /// </summary>
        public bool Enabled { get; set; } = true;
    }
}
