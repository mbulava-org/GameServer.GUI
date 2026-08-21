namespace GameServer.API.Models
{
    /// <summary>
    /// UDP payload emitted by a node agent for lightweight discovery.
    /// </summary>
    public sealed class UdpAgentAnnouncement
    {
        public string NodeId { get; set; } = string.Empty;

        public string NodeName { get; set; } = string.Empty;

        public string InternalUrl { get; set; } = string.Empty;

        public bool IsManagerNode { get; set; }

        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        public List<string> ContainerIds { get; set; } = new();
    }
}
