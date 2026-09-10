using System.Reflection;
using GameServer.Windows.Agent.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Windows.Agent.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IWindowsResourceMonitor _resourceMonitor;

    public HealthController(IWindowsResourceMonitor resourceMonitor)
    {
        _resourceMonitor = resourceMonitor;
    }

    [HttpGet]
    public IActionResult GetHealth()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "0.0.1";
        var host = _resourceMonitor.GetHostSnapshot();

        return Ok(new
        {
            status = "Healthy",
            agent = "GameServer.Windows.Agent",
            version,
            platform = "Windows",
            timestamp = DateTime.UtcNow,
            host
        });
    }
}
