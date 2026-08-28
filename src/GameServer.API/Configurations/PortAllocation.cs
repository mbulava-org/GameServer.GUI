namespace GameServer.API.Configurations
{
    public class PortAllocation
    {
        public uint StartPort { get; set; } = 2000;
        public uint EndPort { get; set; } = 100000;



        /// <summary>
        /// List of reserved ports or port ranges. Each entry can be a single port (e.g., "8080") or a range using a hyphen (e.g., "8000-8010").
        /// </summary>
        public string[] ReservedPortRanges { get; set; } = Array.Empty<string>();
    }
}
