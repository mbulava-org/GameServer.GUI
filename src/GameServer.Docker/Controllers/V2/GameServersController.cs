using GameServer.Docker.Dtos.V2;
using GameServer.Docker.Services.V2;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Docker.Controllers.V2;

[ApiController]
[Route("api/v2/gameservers")]
public sealed class GameServersController(GameServerQueryService queryService, GameServerCommandService commandService, ILogger<GameServersController> logger) : ControllerBase
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
}
