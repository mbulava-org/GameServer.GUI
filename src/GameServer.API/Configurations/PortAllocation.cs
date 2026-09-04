namespace GameServer.API.Configurations
{
    public class PortAllocation
    {
        public uint StartPort { get; set; } = 2000;
        public uint EndPort { get; set; } = 100000;

        /// <summary>
        /// List of reserved ports or port ranges. Each entry can be a single port (e.g., "8080"),
        /// a range using a hyphen (e.g., "8000-9002"), or a comma/semicolon/space-separated string of ports and ranges.
        /// </summary>
        public string[] ReservedPortRanges { get; set; } = Array.Empty<string>();

        public bool IsPortReserved(int port) => port >= 0 && IsPortReserved((uint)port);

        public bool IsPortReserved(uint port)
        {
            if (ReservedPortRanges == null || ReservedPortRanges.Length == 0)
            {
                return false;
            }

            foreach (var item in ReservedPortRanges)
            {
                if (string.IsNullOrWhiteSpace(item))
                {
                    continue;
                }

                var tokens = item.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var token in tokens)
                {
                    var trimmed = token.Trim();
                    if (trimmed.Contains('-'))
                    {
                        var parts = trimmed.Split('-');
                        if (parts.Length == 2
                            && uint.TryParse(parts[0].Trim(), out var start)
                            && uint.TryParse(parts[1].Trim(), out var end))
                        {
                            if (start <= port && port <= end)
                            {
                                return true;
                            }
                        }
                    }
                    else if (uint.TryParse(trimmed, out var single))
                    {
                        if (single == port)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
