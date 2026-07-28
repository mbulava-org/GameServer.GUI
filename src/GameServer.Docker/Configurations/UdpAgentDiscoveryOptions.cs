namespace GameServer.Docker.Configurations
{
    /// <summary>
    /// Configuration for UDP-based agent discovery announcements.
    /// </summary>
    public sealed class UdpAgentDiscoveryOptions
    {
        public const string SectionName = "UdpAgentDiscovery";

        public bool Enabled { get; set; } = true;

        public string BindAddress { get; set; } = "0.0.0.0";

        public string MulticastGroup { get; set; } = "239.1.1.1";

        public int Port { get; set; } = 19090;

        public int AnnouncementTtlSeconds { get; set; } = 90;

        public int CleanupIntervalSeconds { get; set; } = 30;
    }
}
