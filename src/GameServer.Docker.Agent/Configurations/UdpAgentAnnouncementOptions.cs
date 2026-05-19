namespace GameServer.Docker.Agent.Configurations
{
    /// <summary>
    /// Configuration for periodic UDP announcements emitted by node agents.
    /// </summary>
    public sealed class UdpAgentAnnouncementOptions
    {
        public const string SectionName = "UdpAgentAnnouncement";

        public bool Enabled { get; set; } = true;

        public string MulticastGroup { get; set; } = "239.1.1.1";

        public int Port { get; set; } = 19090;

        public int AnnouncementIntervalSeconds { get; set; } = 15;

        public int TimeToLive { get; set; } = 1;
    }
}
