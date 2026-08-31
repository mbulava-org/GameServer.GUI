using GameServer.API.Dtos.V2;
using GameServer.API.Services.V2;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.API.Controllers.V2;

[ApiController]
[Route("api/v2/gameservers")]
public sealed class GameServersController(
    GameServerQueryService queryService,
    GameServerCommandService commandService,
    ILogger<GameServersController> logger,
    Repositories.V2.IGameServerResourceUtilizationRepository? resourceUtilizationRepository = null,
    IGameServerResourceCollector? resourceCollector = null,
    Interfaces.IServerResourceMonitor? resourceMonitor = null) : ControllerBase
{
    /// <summary>
    /// Gets the V2 GameServer list.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(200, Type = typeof(IEnumerable<GameServerListItemDto>))]
    public async Task<ActionResult<IReadOnlyList<GameServerListItemDto>>> GetAll([FromQuery] bool includeDeleted = false, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting V2 game servers (IncludeDeleted={IncludeDeleted})", includeDeleted);
        var servers = await queryService.GetListAsync(includeDeleted, cancellationToken);
        return Ok(servers);
    }

    /// <summary>
    /// Gets the V2 GameServer detail payload for a specific server id.
    /// </summary>
    [HttpGet("{serverId}")]
    [ProducesResponseType(200, Type = typeof(GameServerDetailDto))]
    [ProducesResponseType(404)]
    public async Task<ActionResult<GameServerDetailDto>> GetByServerId(string serverId, CancellationToken cancellationToken = default)
    {
        var server = await queryService.GetByServerIdAsync(serverId, cancellationToken);
        if (server is null)
        {
            logger.LogDebug("V2 game server '{ServerId}' was not found", serverId);
            return NotFound();
        }

        return Ok(server);
    }

    /// <summary>
    /// Validates a V2 GameServer request.
    /// </summary>
    [HttpPost("validate")]
    [ProducesResponseType(200, Type = typeof(GameServerValidationResultDto))]
    [ProducesResponseType(400)]
    public async Task<ActionResult<GameServerValidationResultDto>> Validate([FromBody] SaveGameServerRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var validation = await commandService.ValidateAsync(request, cancellationToken);
            return Ok(validation);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Produces a dry-run preview of the Swarm service that would be created for a request.
    /// </summary>
    [HttpPost("preview")]
    [ProducesResponseType(200, Type = typeof(GameServerDeploymentPreviewDto))]
    [ProducesResponseType(400)]
    public async Task<ActionResult<GameServerDeploymentPreviewDto>> Preview([FromBody] SaveGameServerRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var preview = await commandService.PreviewAsync(request, cancellationToken);
            return Ok(preview);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Checks whether the supplied published ports are available for the given server.
    /// </summary>
    [HttpPost("ports/availability")]
    [ProducesResponseType(200, Type = typeof(GameServerPortAvailabilityResultDto))]
    [ProducesResponseType(400)]
    public async Task<ActionResult<GameServerPortAvailabilityResultDto>> CheckPortAvailability([FromBody] GameServerPortAvailabilityRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var availability = await commandService.CheckPortAvailabilityAsync(request, cancellationToken);
            return Ok(availability);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Creates a V2 GameServer.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(201, Type = typeof(GameServerDetailDto))]
    [ProducesResponseType(400)]
    public async Task<ActionResult<GameServerDetailDto>> Create([FromBody] SaveGameServerRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var created = await commandService.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetByServerId), new { serverId = created.ServerId }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Updates a V2 GameServer.
    /// </summary>
    [HttpPut("{serverId}")]
    [ProducesResponseType(200, Type = typeof(GameServerDetailDto))]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<GameServerDetailDto>> Update(string serverId, [FromBody] SaveGameServerRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var updated = await commandService.UpdateAsync(serverId, request, cancellationToken);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Starts the Swarm service for a V2 GameServer.
    /// </summary>
    [HttpPost("{serverId}/start")]
    [ProducesResponseType(200, Type = typeof(GameServerDetailDto))]
    [ProducesResponseType(404)]
    public async Task<ActionResult<GameServerDetailDto>> Start(string serverId, CancellationToken cancellationToken = default)
    {
        try
        {
            var server = await commandService.StartAsync(serverId, cancellationToken);
            return Ok(server);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Stops the Swarm service for a V2 GameServer.
    /// </summary>
    [HttpPost("{serverId}/stop")]
    [ProducesResponseType(200, Type = typeof(GameServerDetailDto))]
    [ProducesResponseType(404)]
    public async Task<ActionResult<GameServerDetailDto>> Stop(string serverId, CancellationToken cancellationToken = default)
    {
        try
        {
            var server = await commandService.StopAsync(serverId, cancellationToken);
            return Ok(server);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Restarts the Swarm service for a V2 GameServer.
    /// </summary>
    [HttpPost("{serverId}/restart")]
    [ProducesResponseType(200, Type = typeof(GameServerDetailDto))]
    [ProducesResponseType(404)]
    public async Task<ActionResult<GameServerDetailDto>> Restart(string serverId, CancellationToken cancellationToken = default)
    {
        try
        {
            var server = await commandService.RestartAsync(serverId, cancellationToken);
            return Ok(server);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Redeploys and updates the Swarm service for a V2 GameServer.
    /// </summary>
    [HttpPost("{serverId}/redeploy")]
    [ProducesResponseType(200, Type = typeof(GameServerDetailDto))]
    [ProducesResponseType(404)]
    public async Task<ActionResult<GameServerDetailDto>> Redeploy(string serverId, CancellationToken cancellationToken = default)
    {
        try
        {
            var server = await commandService.RedeployAsync(serverId, cancellationToken);
            return Ok(server);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Deletes a V2 GameServer.
    /// </summary>
    [HttpDelete("{serverId}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(string serverId, [FromQuery] bool softDelete = true, CancellationToken cancellationToken = default)
    {
        try
        {
            await commandService.DeleteAsync(serverId, softDelete, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Gets the historical resource utilization records for a V2 GameServer.
    /// </summary>
    [HttpGet("{serverId}/resources/history")]
    [ProducesResponseType(200, Type = typeof(IEnumerable<GameServerResourceHistoryDto>))]
    public async Task<ActionResult<IReadOnlyList<GameServerResourceHistoryDto>>> GetResourceHistory(
        string serverId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int limit = 5000,
        CancellationToken cancellationToken = default)
    {
        if (resourceUtilizationRepository is null)
        {
            return Ok(Array.Empty<GameServerResourceHistoryDto>());
        }

        var records = await resourceUtilizationRepository.GetHistoryAsync(serverId, from, to, limit, cancellationToken);
        var dtos = records.Select(r => new GameServerResourceHistoryDto
        {
            Id = r.Id,
            ServerId = r.ServerId,
            Timestamp = r.Timestamp,
            CpuUsagePercent = r.CpuUsagePercent,
            MemoryUsageBytes = r.MemoryUsageBytes,
            MemoryLimitBytes = r.MemoryLimitBytes,
            MemoryUsagePercent = r.MemoryUsagePercent,
            NetworkRxBytes = r.NetworkRxBytes,
            NetworkTxBytes = r.NetworkTxBytes,
            BlockReadBytes = r.BlockReadBytes,
            BlockWriteBytes = r.BlockWriteBytes,
            DesiredReplicas = r.DesiredReplicas,
            RunningReplicas = r.RunningReplicas,
            ContainerId = r.ContainerId
        }).ToList();

        return Ok(dtos);
    }

    /// <summary>
    /// Gets the latest resource utilization snapshot for a V2 GameServer.
    /// </summary>
    [HttpGet("{serverId}/resources/latest")]
    [ProducesResponseType(200, Type = typeof(Models.ServerResourceUsage))]
    [ProducesResponseType(404)]
    public async Task<ActionResult<Models.ServerResourceUsage>> GetLatestResource(
        string serverId,
        CancellationToken cancellationToken = default)
    {
        // Try in-memory cached snapshot first
        var cached = resourceCollector?.GetCachedUsage(serverId);
        if (cached != null)
        {
            return Ok(cached);
        }

        // Fall back to on-demand snapshot
        if (resourceMonitor is not null)
        {
            var snapshot = await resourceMonitor.GetSnapshotAsync(serverId, cancellationToken);
            if (snapshot != null)
            {
                return Ok(snapshot);
            }
        }

        return NotFound();
    }
}
