using GameServer.Docker.Interfaces;
using GameServer.Docker.Repositories;
using GameServer.Docker.Services;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Docker.Controllers
{
    [ApiController]
    [Route("api/servers")]
    public class GameServerController : ControllerBase
    {
        private readonly IGameServerManager _manager;
        private readonly IGameTypeRepository _repository;
        private readonly IGameServerFileManager _fileManager;
        private readonly ILogger<GameServerController> _logger;

        public GameServerController(
            IGameServerManager orchestrator,
            IGameServerFileManager fileManager,
            IGameTypeRepository repository,
            ILogger<GameServerController> logger)
        {
            _manager = orchestrator;
            _repository = repository;
            _fileManager = fileManager;
            _logger = logger;
        }

        [HttpPost("deploy")]
        public async Task<IActionResult> Deploy([FromBody] Models.GameServer server)
        {
            var def = await _repository.GetByKeyAsync(server.GameType);
            if (def == null)
                return BadRequest($"Unknown game type: {server.GameType}");

            await _manager.CreateOrUpdateAsync(server, def);

            return Ok(new { message = "Server deployed", server.ServerId });
        }

        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Models.GameServer))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get(string id)
        {
            var server = await _manager.GetServerById(id);
            if (server == null)
                return NotFound();

            return Ok(server);
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<Models.GameServer>))]
        public async Task<IActionResult> List()
        {
            var servers = await _manager.ListServersAsync();
            return Ok(servers);
        }

        // File Management Endpoints
        [HttpGet("{id}/files")]
        [ProducesResponseType(200, Type = typeof(List<Models.FileItem>))]
        public async Task<IActionResult> GetFiles(string id, [FromQuery] string volumeTarget, [FromQuery] string path = "/")
        {
            try
            {
                var files = await _fileManager.GetFilesAsync(id, volumeTarget, path);
                return Ok(files);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("{id}/files/download")]
        public async Task<IActionResult> DownloadFile(string id, [FromQuery] string volumeTarget, [FromQuery] string filePath)
        {
            try
            {
                var content = await _fileManager.DownloadFileAsync(id, volumeTarget, filePath);
                var fileName = Path.GetFileName(filePath);
                return File(content, "application/octet-stream", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("{id}/files/upload")]
        [ProducesResponseType(200)]
        public async Task<IActionResult> UploadFile(string id, [FromQuery] string volumeTarget, [FromQuery] string filePath, IFormFile file)
        {
            try
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                var content = ms.ToArray();

                await _fileManager.UploadFileAsync(id, volumeTarget, filePath, content);
                return Ok(new { message = "File uploaded successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpDelete("{id}/files")]
        [ProducesResponseType(200)]
        public async Task<IActionResult> DeleteFile(string id, [FromQuery] string volumeTarget, [FromQuery] string filePath, [FromQuery] bool recursive = false)
        {
            try
            {
                await _fileManager.DeleteFileAsync(id, volumeTarget, filePath, recursive);
                return Ok(new { message = "File deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("{id}/files/directory")]
        public async Task<IActionResult> CreateDirectory(string id, [FromQuery] string volumeTarget, [FromQuery] string directoryPath)
        {
            try
            {
                await _fileManager.CreateDirectoryAsync(id, volumeTarget, directoryPath);
                return Ok(new { message = "Directory created successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

  

        /// <summary>
        /// Get current resource usage for a server
        /// </summary>
        [HttpGet("{id}/resources")]
        [ProducesResponseType(200, Type = typeof(Models.ServerResourceUsage))]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetResourceUsage(string id, [FromServices] IGameServerResourceMonitor resourceMonitor)
        {
            try
            {
                var usage = await resourceMonitor.GetResourceUsageAsync(id);
                if (usage == null)
                    return NotFound($"No resource data available for server {id}");

                return Ok(usage);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get Docker Swarm service logs (aggregated from all replicas/tasks)
        /// Note: For real-time container logs from a specific container, use /api/servers/{id}/container/logs
        /// </summary>
        [HttpGet("{id}/logs")]
        [ProducesResponseType(200, Type = typeof(List<string>))]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetServiceLogs(string id, [FromQuery] int tail = 1000)
        {
            try
            {
                var server = await _manager.GetServerById(id);
                if (server == null)
                    return NotFound($"Server {id} not found");

                var logs = await _manager.GetServiceLogsAsync(id, tail);
                return Ok(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching service logs for server {id}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get real-time container logs from the running container via Node Agent
        /// Note: For Docker Swarm service logs (aggregated), use /api/servers/{id}/logs
        /// </summary>
        [HttpGet("{id}/container/logs")]
        [ProducesResponseType(200, Type = typeof(List<string>))]
        [ProducesResponseType(404)]
        [ProducesResponseType(503, Type = typeof(ProblemDetails))]
        public async Task<IActionResult> GetContainerLogs(
            string id,
            [FromServices] INodeAgentDiscovery agentDiscovery,
            [FromQuery] int tail = 100)
        {
            try
            {
                var server = await _manager.GetServerById(id);
                if (server == null)
                    return NotFound($"Server {id} not found");

                _logger.LogDebug("Fetching container logs for server {ServerId} via Node Agent", id);
                
                var containerId = await _manager.GetRunningContainerIdAsync(id);
                if (string.IsNullOrEmpty(containerId))
                {
                    return StatusCode(503, new ProblemDetails
                    {
                        Title = "No Running Container",
                        Detail = $"Server {id} has no running container. The server may be stopped or starting.",
                        Status = 503
                    });
                }

                var logs = await agentDiscovery.GetContainerLogsAsync(containerId, tail);
                if (logs == null)
                {
                    return StatusCode(503, new ProblemDetails
                    {
                        Title = "Agent Unavailable",
                        Detail = $"Could not fetch container logs for server {id}. The node agent may be unavailable or the container may not be accessible.",
                        Status = 503
                    });
                }

                return Ok(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching container logs for server {ServerId}", id);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get available node agents in the swarm
        /// </summary>
        [HttpGet("agents")]
        [ProducesResponseType(200, Type = typeof(Models.AgentDiscoveryResponse))]
        public async Task<IActionResult> GetNodeAgents([FromServices] INodeAgentDiscovery agentDiscovery)
        {
            try
            {
                var agents = await agentDiscovery.DiscoverAgentsAsync();
                
                var response = new Models.AgentDiscoveryResponse
                {
                    Timestamp = DateTime.UtcNow,
                    AgentCount = agents.Count,
                    Agents = agents.Select(a => new Models.AgentInfo
                    {
                        NodeId = a.NodeId,
                        NodeName = a.NodeName,
                        TaskId = a.TaskId,
                        InternalUrl = a.InternalUrl,
                        IsHealthy = a.IsHealthy,
                        DiscoveredAt = a.DiscoveredAt
                    }).ToList()
                };
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error discovering node agents");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get real-time container statistics via node agent
        /// </summary>
        [HttpGet("{id}/stats")]
        [ProducesResponseType(200, Type = typeof(Models.ContainerStats))]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetContainerStats(string id, [FromServices] INodeAgentDiscovery agentDiscovery)
        {
            try
            {
                var server = await _manager.GetServerById(id);
                if (server == null)
                    return NotFound($"Server {id} not found");

                var agent = await agentDiscovery.GetAgentForServerAsync(id);
                if (agent == null)
                    return StatusCode(503, new { error = "No agent available for this server" });

                var containerId = await _manager.GetRunningContainerIdAsync(id);
                if (string.IsNullOrEmpty(containerId))
                    return StatusCode(503, new { error = "No running container found for this server" });

                var stats = await agentDiscovery.GetContainerStatsAsync(containerId);
                if (stats == null)
                    return StatusCode(503, new { error = "Could not retrieve container stats" });

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching container stats for server {id}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("{id}/start")]
        [ProducesResponseType(200)]
        public async Task<IActionResult> StartServer(string id)
        {
            try
            {
                await _manager.StartServer(id);
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("{id}/stop")]
        [ProducesResponseType(200)]
        public async Task<IActionResult> StopServer(string id)
        {
            try
            {
                await _manager.StopServer(id);
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        //[HttpPost("{id}/restart")]
        //[ProducesResponseType(200)]
        //public async Task<IActionResult> RestartServer(string id)
        //{
        //    try
        //    {
        //        await _manager.StopServer(id);
        //        await Task.Delay(TimeSpan.FromSeconds(5)); // brief pause
        //        await _manager.StartServer(id);
        //        return Ok();
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { error = ex.Message });
        //    }
        //}

        [HttpDelete("{Id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> DeleteServer(string Id, [FromQuery] bool deleteData = false)
        {
            try
            {
                await _manager.DeleteServer(Id, deleteData);
                return Ok(new { message = "Server deleted successfully", deletedData = deleteData });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
