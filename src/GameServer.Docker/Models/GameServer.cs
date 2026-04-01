namespace GameServer.Docker.Models
{
    [Obsolete("Use GameServer.Docker.Models.V2.GameServer for new persistence work. This legacy server model will be removed with the old repository chain.")]
    public class GameServer
    {
        public string ServerId { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string GameType { get; set; } = ""; // e.g., "minecraft", "valheim"
        
        // Arbitrary settings (seed, world name, password, etc.)
        public Dictionary<string, string> Settings { get; set; } = new();

        //Only supplied when the server is actually deployed
        public string ServiceName { get; set; } = string.Empty;
        
        /// <summary>
        /// The actual Docker container ID (only available when container is running)
        /// </summary>
        public string? ContainerId { get; set; }

        public List<VolumeDefinition> Volumes { get; set; } = new();

        public List<PortMapping> Ports { get; set; } = new();

        public bool IsRunning { get; set; } = false;

        public string Status { get; set; } = string.Empty;
    }

    [Obsolete("Use GameServer.Docker.Models.V2.GameTypePort or derived V2 deployment data instead. This legacy port mapping model will be removed with the old repository chain.")]
    public  class PortMapping
    {
        public uint PublishedPort { get; set; }
        public uint ContainerPort { get; set; }
        public string Protocol { get; set; } = "tcp";
    }


}
