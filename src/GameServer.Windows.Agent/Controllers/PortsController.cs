using GameServer.Windows.Agent.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Windows.Agent.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PortsController : ControllerBase
{
    private readonly IWindowsPortService _portService;

    public PortsController(IWindowsPortService portService)
    {
        _portService = portService;
    }

    /// <summary>
    /// Check whether a specific port and protocol is currently free on this host.
    /// </summary>
    [HttpGet("check")]
    public ActionResult<bool> CheckPort(
        [FromQuery] int port,
        [FromQuery] string protocol = "tcp")
    {
        return Ok(_portService.IsPortAvailable(port, protocol));
    }

    /// <summary>
    /// Check multiple ports and protocols for availability in a single batch.
    /// </summary>
    [HttpPost("check-batch")]
    public ActionResult<IReadOnlyList<HostPortUsage>> CheckBatch(
        [FromBody] List<PortCheckRequest> requests)
    {
        var tuples = requests.Select(r => (r.Port, r.Protocol ?? "tcp"));
        return Ok(_portService.CheckPortsAvailability(tuples));
    }

    public class PortCheckRequest
    {
        public int Port { get; set; }
        public string? Protocol { get; set; } = "tcp";
    }
}
