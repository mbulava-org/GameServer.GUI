using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Docker.Agent.Controllers
{
    /// <summary>
    /// Controller for Docker network operations.
    /// Provides access to Docker network information.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class NetworksController : ControllerBase
    {
        private readonly IDockerClient _dockerClient;
        private readonly ILogger<NetworksController> _logger;

        public NetworksController(
            IDockerClient dockerClient,
            ILogger<NetworksController> logger)
        {
            _dockerClient = dockerClient;
            _logger = logger;
        }

        /// <summary>
        /// List Docker networks
        /// </summary>
        [HttpGet]
        public async Task<ActionResult> ListNetworks([FromQuery] string? nameFilter = null)
        {
            try
            {
                _logger.LogDebug("Listing networks with name filter: {NameFilter}", nameFilter ?? "none");

                var parameters = new NetworksListParameters();
                
                if (!string.IsNullOrEmpty(nameFilter))
                {
                    parameters.Filters = new Dictionary<string, IDictionary<string, bool>>
                    {
                        ["name"] = new Dictionary<string, bool> { [nameFilter] = true }
                    };
                }

                var networks = await _dockerClient.Networks.ListNetworksAsync(parameters);

                _logger.LogDebug("Found {Count} networks", networks.Count);

                return Ok(new
                {
                    Success = true,
                    Count = networks.Count,
                    Networks = networks
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list networks");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"Failed to list networks: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Inspect a specific network
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult> InspectNetwork(string id)
        {
            try
            {
                _logger.LogDebug("Inspecting network: {NetworkId}", id);

                var network = await _dockerClient.Networks.InspectNetworkAsync(id);

                return Ok(new
                {
                    Success = true,
                    Network = network
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to inspect network: {NetworkId}", id);
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"Failed to inspect network: {ex.Message}"
                });
            }
        }
    }
}
