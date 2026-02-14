namespace GameServer.Docker.Models
{
    public class GameTypeDefinition
    {
        public string Key { get; set; } = "";            // "minecraft", "valheim"
        public string DisplayName { get; set; } = "";    // "Minecraft", "Valheim"
        public string Description { get; set; } = "";

        public string Image { get; set; } = "";          // Docker image name
        
        /// <summary>
        /// URL to a thumbnail image for this game type (optional)
        /// </summary>
        public string? ThumbnailUrl { get; set; }
        
        /// <summary>
        /// URL to the Docker container documentation (optional)
        /// </summary>
        public string? DocumentationUrl { get; set; }
        
        /// <summary>
        /// The Ports required to be exposed
        /// </summary>
        public List<PortDefinition> Ports { get; set; } = new();

        /// <summary>
        /// Volumes Perisistent Storage 
        /// </summary>
        public List<VolumeDefinition> Volumes { get; set; } = new();

        /// <summary>
        /// Gets or sets the collection of default Environment Variables the container uses 
        /// </summary>
        /// <remarks>The dictionary contains setting names as keys and their corresponding default values
        /// as strings. Modifying this property affects the default behavior of components that rely on these
        /// settings.</remarks>
        public Dictionary<string, string> DefaultSettings { get; set; } = new();
    }
}
