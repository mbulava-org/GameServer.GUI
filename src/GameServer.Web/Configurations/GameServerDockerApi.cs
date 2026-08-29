namespace GameServer.Web.Configurations
{
    public class GameServerDockerApi
    {
        public string BaseUri { get; set; } = "http://localhost:5164/";

        /// <summary>
        /// The public Internet IP address of the server / host cluster for connecting to game services.
        /// </summary>
        public string? PublicIp { get; set; }

        /// <summary>
        /// The public HostName / FQDN of the server / host cluster (e.g. play.example.com) for connecting to game services.
        /// </summary>
        public string? HostName { get; set; }
    }
}
