using GameServer.API.Interfaces;
using GameServer.API.Models;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Orchestration.Host.Controllers;

/// <summary>
/// REST API exposing INodeAgentDiscovery operations so GameServer.API
/// and GameServer.Monitoring.Host can query node discovery over HTTP.
/// Streaming stats (IAsyncEnumerable) are served via SSE on a dedicated endpoint.
/// </summary>
[ApiController]
[Route("api/discovery")]
public class NodeDiscoveryController(INodeAgentDiscovery discovery) : ControllerBase
{
    [HttpGet("agents")]
    public async Task<ActionResult<List<NodeAgentEndpoint>>> DiscoverAgents(CancellationToken ct)
        => Ok(await discovery.DiscoverAgentsAsync());

    [HttpGet("container/{containerId}")]
    public async Task<ActionResult<NodeAgentEndpoint?>> GetAgentForContainer(string containerId, CancellationToken ct)
    {
        var agent = await discovery.GetAgentForContainerAsync(containerId);
        return agent is null ? NotFound() : Ok(agent);
    }

    [HttpGet("server/{serverId}")]
    public async Task<ActionResult<NodeAgentEndpoint?>> GetAgentForServer(string serverId, CancellationToken ct)
    {
        var agent = await discovery.GetAgentForServerAsync(serverId);
        return agent is null ? NotFound() : Ok(agent);
    }

    [HttpGet("stats/{containerId}")]
    public async Task<ActionResult<ContainerStats?>> GetStats(string containerId, CancellationToken ct)
    {
        var stats = await discovery.GetContainerStatsAsync(containerId);
        return stats is null ? NotFound() : Ok(stats);
    }

    [HttpGet("logs/{containerId}")]
    public async Task<ActionResult<List<string>?>> GetLogs(
        string containerId,
        [FromQuery] int tailLines = 1000,
        CancellationToken ct = default)
    {
        var logs = await discovery.GetContainerLogsAsync(containerId, tailLines);
        return logs is null ? NotFound() : Ok(logs);
    }

    [HttpGet("servicelogs/{serviceId}")]
    public async Task<ActionResult<List<string>?>> GetServiceLogs(
        string serviceId,
        [FromQuery] int tailLines = 1000,
        CancellationToken ct = default)
    {
        var logs = await discovery.GetServiceLogsAsync(serviceId, tailLines);
        return logs is null ? NotFound() : Ok(logs);
    }
}
