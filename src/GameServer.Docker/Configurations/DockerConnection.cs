namespace GameServer.Docker.Configurations
{
    public class DockerConnection
    {
        public string Uri { get; set; } = "unix:///var/run/docker.sock";
    }
}
