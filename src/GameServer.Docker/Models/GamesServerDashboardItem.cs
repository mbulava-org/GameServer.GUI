namespace GameServer.Docker.Models
{
    public class GameServerDashboardItem
    {
        public string ServerId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";

        public string GameType { get; set; } = "";
        public string Ports { get; set; } = "";

        public bool IsRunning { get; set; }

        public string Status { get; set; } = "";

        //Only supplied when the server is actually deployed
        public string ServiceName { get; set; } = string.Empty;
    }
}
