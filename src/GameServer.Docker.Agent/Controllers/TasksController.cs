using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Docker.Agent.Controllers
{
    /// <summary>
    /// Controller for Docker task operations.
    /// Provides access to Swarm task information.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class TasksController : ControllerBase
    {
        private readonly IDockerClient _dockerClient;
        private readonly ILogger<TasksController> _logger;

        public TasksController(
            IDockerClient dockerClient,
            ILogger<TasksController> logger)
        {
            _dockerClient = dockerClient;
            _logger = logger;
        }

        /// <summary>
        /// List Docker Swarm tasks with optional filters
        /// </summary>
        [HttpGet]
        public async Task<ActionResult> ListTasks([FromQuery] string? serviceId = null)
        {
            try
            {
                _logger.LogDebug("Listing tasks with serviceId filter: {ServiceId}", serviceId ?? "none");

                var parameters = new TasksListParameters();
                
                if (!string.IsNullOrEmpty(serviceId))
                {
                    parameters.Filters = new Dictionary<string, IDictionary<string, bool>>
                    {
                        ["service"] = new Dictionary<string, bool> { [serviceId] = true }
                    };
                }

                var tasks = await _dockerClient.Tasks.ListAsync(parameters);

                _logger.LogDebug("Found {Count} tasks", tasks.Count);

                return Ok(new
                {
                    Success = true,
                    Count = tasks.Count,
                    Tasks = tasks
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list tasks");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"Failed to list tasks: {ex.Message}"
                });
            }
        }
    }
}
