namespace GameServer.Docker.Configurations
{
    /// <summary>
    /// Configuration for NFS volume path preparation performed by the primary API service.
    /// The API container maps <see cref="BaseDataPath"/> to the folder above the full path of
    /// the NFS share so that resolved target folders can be created and prepared before the
    /// Swarm service is created or updated.
    /// </summary>
    public sealed class NfsPreparationOptions
    {
        public const string SectionName = "NfsPreparation";

        /// <summary>
        /// The container path that maps to the parent of the NFS export root. Resolved volume
        /// target folders are created underneath this path.
        /// </summary>
        public string BaseDataPath { get; set; } = "/data";
    }
}
