using GameServer.Docker.Dtos.V2;
using GameServer.Docker.Services.V2;
using GameServer.Docker.Services.V2.Detection;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Docker.Controllers.V2;

[ApiController]
[Route("api/v2/gametypes")]
public sealed class GameTypesController(
    GameTypeQueryService queryService,
    GameTypeCommandService commandService,
    GameTypeSetupDetectionService detectionService,
    ILogger<GameTypesController> logger) : ControllerBase
{
    /// <summary>
    /// Gets the V2 GameType list.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(200, Type = typeof(IEnumerable<GameTypeListItemDto>))]
    public async Task<ActionResult<IReadOnlyList<GameTypeListItemDto>>> GetAll([FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting V2 game types (IncludeInactive={IncludeInactive})", includeInactive);
        var gameTypes = await queryService.GetListAsync(includeInactive, cancellationToken);
        return Ok(gameTypes);
    }

    /// <summary>
    /// Gets the V2 GameType editor payload for a specific key.
    /// </summary>
    [HttpGet("{key}")]
    [ProducesResponseType(200, Type = typeof(GameTypeDetailDto))]
    [ProducesResponseType(404)]
    public async Task<ActionResult<GameTypeDetailDto>> GetByKey(string key, CancellationToken cancellationToken = default)
    {
        var gameType = await queryService.GetByKeyAsync(key, cancellationToken);
        if (gameType is null)
        {
            logger.LogDebug("V2 game type '{GameTypeKey}' was not found", key);
            return NotFound();
        }

        return Ok(gameType);
    }

    /// <summary>
    /// Creates a V2 GameType.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(201, Type = typeof(GameTypeDetailDto))]
    [ProducesResponseType(400)]
    public async Task<ActionResult<GameTypeDetailDto>> Create([FromBody] SaveGameTypeRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var created = await commandService.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetByKey), new { key = created.Key }, created);
        }
        catch (ArgumentException ex)
        {
            logger.LogDebug(ex, "Invalid create request for V2 game type");
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Updates a V2 GameType.
    /// </summary>
    [HttpPut("{key}")]
    [ProducesResponseType(200, Type = typeof(GameTypeDetailDto))]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<GameTypeDetailDto>> Update(string key, [FromBody] SaveGameTypeRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var updated = await commandService.UpdateAsync(key, request, cancellationToken);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            logger.LogDebug(ex, "Invalid update request for V2 game type {GameTypeKey}", key);
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Adds a revision to a V2 GameType.
    /// </summary>
    [HttpPost("{key}/revisions")]
    [ProducesResponseType(201, Type = typeof(GameTypeRevisionDto))]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<GameTypeRevisionDto>> AddRevision(string key, [FromBody] SaveGameTypeRevisionRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var created = await commandService.AddRevisionAsync(key, request, cancellationToken);
            return CreatedAtAction(nameof(GetByKey), new { key }, created);
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
    /// Updates a V2 GameType revision.
    /// </summary>
    [HttpPut("{key}/revisions/{revisionId:int}")]
    [ProducesResponseType(200, Type = typeof(GameTypeRevisionDto))]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<GameTypeRevisionDto>> UpdateRevision(string key, int revisionId, [FromBody] SaveGameTypeRevisionRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var updated = await commandService.UpdateRevisionAsync(key, revisionId, request, cancellationToken);
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
    /// Publishes a V2 GameType revision.
    /// </summary>
    [HttpPost("{key}/revisions/{revisionId:int}/publish")]
    [ProducesResponseType(200, Type = typeof(GameTypeRevisionDto))]
    [ProducesResponseType(404)]
    public async Task<ActionResult<GameTypeRevisionDto>> PublishRevision(string key, int revisionId, [FromBody] PublishRevisionRequestDto? request, CancellationToken cancellationToken = default)
    {
        try
        {
            var published = await commandService.PublishRevisionAsync(key, revisionId, request?.SetAsCurrentRevision == true, cancellationToken);
            return Ok(published);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Sets the current revision for a V2 GameType.
    /// </summary>
    [HttpPost("{key}/revisions/{revisionId:int}/set-current")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> SetCurrentRevision(string key, int revisionId, CancellationToken cancellationToken = default)
    {
        try
        {
            await commandService.SetCurrentRevisionAsync(key, revisionId, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Detects Docker image setup data for a V2 GameType tag.
    /// </summary>
    [HttpPost("{key}/detection/scan-tag")]
    [ProducesResponseType(200, Type = typeof(GameTypeSetupDetectionResultDto))]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<GameTypeSetupDetectionResultDto>> ScanTag(string key, [FromBody] DetectGameTypeSetupRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var detected = await detectionService.DetectAsync(key, request, cancellationToken);
            return Ok(detected);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Compares detected Docker setup data to a selected V2 GameType revision.
    /// </summary>
    [HttpPost("{key}/detection/compare")]
    [ProducesResponseType(200, Type = typeof(GameTypeSetupComparisonResultDto))]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<GameTypeSetupComparisonResultDto>> CompareDetection(string key, [FromBody] CompareGameTypeSetupRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var comparison = await detectionService.CompareAsync(key, request, cancellationToken);
            return Ok(comparison);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
