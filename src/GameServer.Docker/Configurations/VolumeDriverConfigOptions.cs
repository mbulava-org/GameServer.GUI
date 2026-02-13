namespace GameServer.Docker.Configurations
{
    public class VolumeDriverConfigOptions
    {
        public string Name { get; set; } = "local";
        public VolumeDriverConfigOptionsOptions Options { get; set; } = new();


        public string RootStoragePath { get; set; } = "";
        public string SubPathFormat { get; set; } = "{gameTypeKey}_{serverId}/{Source}";

        /// <summary>
        /// If set to a non-empty value, this path should be mounted as the "RootStoragePath"'s value.
        /// The is the way you can ensure the folder structure for the remote storage can be accessed locally.
        /// </summary>
        public string LocalStoragePath { get; set; } = "/data";
    }

    public class VolumeDriverConfigOptionsOptions
    {
        public string type { get; set; } = "nfs";
        public string device { get; set; } = ":/exported/path";
        public string o { get; set; } = "addr=host.docker.internal,rw";

    }
}
