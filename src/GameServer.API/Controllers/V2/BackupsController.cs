using GameServer.API.Dtos.V2;
using GameServer.API.Services.V2;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.API.Controllers.V2;

[ApiController]
[Authorize]
public sealed class BackupsController : ControllerBase
{
    private readonly IGameServerBackupService _backupService;
    private readonly ILogger<BackupsController> _logger;

    public BackupsController(
        IGameServerBackupService backupService,
        ILogger<BackupsController> logger)
    {
        _backupService = backupService;
        _logger = logger;
    }

    [HttpPost("api/v2/gameservers/{serverId}/backups")]
    [ProducesResponseType(typeof(GameServerBackupDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(
        string serverId,
        [FromBody] CreateBackupRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(serverId))
        {
            return BadRequest("Server ID is required.");
        }

        if (string.IsNullOrWhiteSpace(request?.VolumePath))
        {
            return BadRequest("VolumePath is required.");
        }

        try
        {
            var backup = await _backupService.CreateBackupAsync(serverId, request, User, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, backup);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (DirectoryNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating backup for server {ServerId}", serverId);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    [HttpGet("api/v2/gameservers/{serverId}/backups")]
    [ProducesResponseType(typeof(IReadOnlyList<GameServerBackupDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetForServer(
        string serverId,
        CancellationToken cancellationToken = default)
    {
        var backups = await _backupService.GetBackupsAsync(User, serverId, cancellationToken);
        return Ok(backups);
    }

    [HttpGet("api/v2/backups")]
    [ProducesResponseType(typeof(IReadOnlyList<GameServerBackupDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? serverId = null,
        CancellationToken cancellationToken = default)
    {
        var backups = await _backupService.GetBackupsAsync(User, serverId, cancellationToken);
        return Ok(backups);
    }

    [HttpGet("api/v2/backups/{id:int}")]
    [ProducesResponseType(typeof(GameServerBackupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(
        int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var backup = await _backupService.GetBackupByIdAsync(id, User, cancellationToken);
            if (backup == null)
            {
                return NotFound();
            }

            return Ok(backup);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    [HttpGet("api/v2/backups/{id:int}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(
        int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (stream, contentType, fileName) = await _backupService.DownloadBackupAsync(id, User, cancellationToken);
            return File(stream, contentType, fileName);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading backup {BackupId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    [HttpPost("api/v2/backups/{id:int}/extend")]
    [ProducesResponseType(typeof(GameServerBackupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Extend(
        int id,
        [FromBody] ExtendBackupRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var updated = await _backupService.ExtendBackupRetentionAsync(id, request ?? new ExtendBackupRequestDto(), User, cancellationToken);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extending backup retention {BackupId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    [HttpPut("api/v2/backups/{id:int}/share")]
    [ProducesResponseType(typeof(GameServerBackupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSharing(
        int id,
        [FromBody] UpdateBackupSharingRequestDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var updated = await _backupService.UpdateSharingAsync(id, request, User, cancellationToken);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating backup sharing {BackupId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    [HttpDelete("api/v2/backups/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _backupService.DeleteBackupAsync(id, User, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting backup {BackupId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }
}
