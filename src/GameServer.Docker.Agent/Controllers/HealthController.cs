using Docker.DotNet;
using GameServer.Docker.Agent.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using System.Reflection;

namespace GameServer.Docker.Agent.Controllers
{
    /// <summary>
    /// Health check and node information controller
    /// </summary>
    [ApiController]
    [Route("")]
    public class HealthController : ControllerBase
    {
        private readonly ILogger<HealthController> _logger;
        private readonly Assembly _myAssembly;
        private readonly IDockerClient _dockerClient;
        public HealthController(ILogger<HealthController> logger, IDockerClient client)
        {
            _logger = logger;
            _myAssembly = Assembly.GetAssembly(typeof(HealthController));
        }

        /// <summary>
        /// Health check endpoint
        /// </summary>
        [HttpGet("health")]
        [ProducesResponseType(200, Type = typeof(Models.HealthResponse))]
        public IActionResult GetHealth()
        {
            _logger.LogDebug("Health check requested");
            
            var response = new Models.HealthResponse
            {
                Status = "healthy",
                Timestamp = DateTime.UtcNow,
                NodeName = Environment.GetEnvironmentVariable("NODE_NAME") ?? "unknown",
                Version = $"{_myAssembly.GetName().Version}"
            };

            return Ok(response);
        }
    }
}
