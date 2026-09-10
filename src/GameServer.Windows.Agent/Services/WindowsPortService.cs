using System.Net.NetworkInformation;
using GameServer.Windows.Agent.Interfaces;

namespace GameServer.Windows.Agent.Services;

public sealed class WindowsPortService : IWindowsPortService
{
    private readonly ILogger<WindowsPortService> _logger;

    public WindowsPortService(ILogger<WindowsPortService> logger)
    {
        _logger = logger;
    }

    public bool IsPortAvailable(int port, string protocol = "tcp")
    {
        if (port <= 0 || port > 65535)
        {
            return false;
        }

        var isTcp = string.Equals(protocol, "tcp", StringComparison.OrdinalIgnoreCase);

        try
        {
            var ipProperties = IPGlobalProperties.GetIPGlobalProperties();

            if (isTcp)
            {
                var tcpListeners = ipProperties.GetActiveTcpListeners();
                return tcpListeners.All(endpoint => endpoint.Port != port);
            }
            else
            {
                var udpListeners = ipProperties.GetActiveUdpListeners();
                return udpListeners.All(endpoint => endpoint.Port != port);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking port availability for {Protocol} port {Port}", protocol, port);
            return false;
        }
    }

    public IReadOnlyList<int> GetActiveTcpPorts()
    {
        try
        {
            var ipProperties = IPGlobalProperties.GetIPGlobalProperties();
            return ipProperties.GetActiveTcpListeners().Select(ep => ep.Port).Distinct().OrderBy(p => p).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve active TCP listeners");
            return [];
        }
    }

    public IReadOnlyList<int> GetActiveUdpPorts()
    {
        try
        {
            var ipProperties = IPGlobalProperties.GetIPGlobalProperties();
            return ipProperties.GetActiveUdpListeners().Select(ep => ep.Port).Distinct().OrderBy(p => p).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve active UDP listeners");
            return [];
        }
    }

    public IReadOnlyList<HostPortUsage> CheckPortsAvailability(IEnumerable<(int Port, string Protocol)> ports)
    {
        var activeTcp = GetActiveTcpPorts().ToHashSet();
        var activeUdp = GetActiveUdpPorts().ToHashSet();

        var result = new List<HostPortUsage>();

        foreach (var (port, protocol) in ports)
        {
            var isTcp = string.Equals(protocol, "tcp", StringComparison.OrdinalIgnoreCase);
            var inUse = isTcp ? activeTcp.Contains(port) : activeUdp.Contains(port);

            result.Add(new HostPortUsage
            {
                Port = port,
                Protocol = isTcp ? "tcp" : "udp",
                InUse = inUse
            });
        }

        return result;
    }
}
