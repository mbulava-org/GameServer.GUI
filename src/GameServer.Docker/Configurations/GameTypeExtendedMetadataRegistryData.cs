namespace GameServer.Docker.Configurations
{
    /// <summary>
    /// Configuration for GameType extended metadata storage.
    /// Each game type is stored in its own file: {DirectoryPath}/{GameTypeKey}.json
    /// </summary>
    public class GameTypeExtendedMetadataRegistryData
    {
        /// <summary>
        /// Directory path where extended metadata files are stored.
        /// Each game type will have its own file: {GameTypeKey}.json
        /// </summary>
        public string DirectoryPath { get; set; } = "/data/game-types-extended";
    }
}
