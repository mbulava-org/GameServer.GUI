namespace GameServer.Docker.Models
{
    [Obsolete("Use GameServer.Docker.Models.V2.GameTypePort for new persistence work.")]
    public class PortDefinition
    {
        public uint Port { get; set; }
        public string Protocol { get; set; } = "tcp";

        /// <summary>
        /// Indicates this is the primary/default port that users should connect to.
        /// Used by UI to highlight the main connection port.
        /// </summary>
        public bool IsDefaultPort { get; set; } = false;

        public PortDefinition() { }

        public PortDefinition(uint port, string protocol)
        {
            Port = port;
            Protocol = protocol;
        }

        public PortDefinition(uint port, string protocol, bool isDefaultPort)
        {
            Port = port;
            Protocol = protocol;
            IsDefaultPort = isDefaultPort;
        }
    }

}
