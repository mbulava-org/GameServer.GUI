namespace GameServer.Windows.Agent.Interfaces;

public class HostPortUsage
{
    public int Port { get; set; }
    public string Protocol { get; set; } = "tcp";
    public bool InUse { get; set; }
}

public interface IWindowsPortService
{
    bool IsPortAvailable(int port, string protocol = "tcp");
    IReadOnlyList<int> GetActiveTcpPorts();
    IReadOnlyList<int> GetActiveUdpPorts();
    IReadOnlyList<HostPortUsage> CheckPortsAvailability(IEnumerable<(int Port, string Protocol)> ports);
}
