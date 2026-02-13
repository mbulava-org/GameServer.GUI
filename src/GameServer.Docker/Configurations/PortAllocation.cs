namespace GameServer.Docker.Configurations
{
    public class PortAllocation
    {
        public uint StartPort { get; set; } = 2000;
        public uint EndPort { get; set; } = 100000;
    }
}
