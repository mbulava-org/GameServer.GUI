namespace GameServer.Docker.Models
{
    [Obsolete("Use GameServer.Docker.Models.V2.GameTypeVolume for new persistence work.")]
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
