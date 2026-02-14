namespace GameServer.Docker.Models
{
    public class VolumeDefinition
    {
        public string Source { get; set; } = "";
        public string Target { get; set; } = "";

        public VolumeDefinition() { }

        public VolumeDefinition(string source, string target)
        {
            Source = source;
            Target = target;
        }
    }

}
