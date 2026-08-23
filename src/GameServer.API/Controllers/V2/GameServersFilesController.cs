using GameServer.API.Dtos.V2;
using GameServer.API.Services.V2;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.API.Controllers.V2;

public sealed record SaveFileContentRequestDto
{
    public string Content { get; init; } = string.Empty;
}

[ApiController]
[Route("api/v2/gameservers/{serverId}/files")]
public sealed class GameServersFilesController(IGameServerFilesService filesService, ILogger<GameServersFilesController> logger)
    : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<FileItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(
        string serverId,
        [FromQuery] string volumePath,
        [FromQuery] string? subPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(volumePath))
        {
            return BadRequest("volumePath query parameter is required.");
        }

        try
        {
            var files = await filesService.ListFilesAsync(serverId, volumePath, subPath, cancellationToken);
            return Ok(files);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (DirectoryNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (NotSupportedException ex)
        {
            return StatusCode(StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing files for server {ServerId}, volume {VolumePath}", serverId, volumePath);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    [HttpGet("content")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContent(
        string serverId,
        [FromQuery] string volumePath,
        [FromQuery] string subPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(volumePath) || string.IsNullOrWhiteSpace(subPath))
        {
            return BadRequest("volumePath and subPath query parameters are required.");
        }

        try
        {
            var content = await filesService.GetFileContentTextAsync(serverId, volumePath, subPath, cancellationToken);
            return Ok(content);
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(ex.Message);
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
            logger.LogError(ex, "Error reading file content for server {ServerId}, path {SubPath}", serverId, subPath);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    [HttpGet("download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(
        string serverId,
        [FromQuery] string volumePath,
        [FromQuery] string subPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(volumePath) || string.IsNullOrWhiteSpace(subPath))
        {
            return BadRequest("volumePath and subPath query parameters are required.");
        }

        try
        {
            var (stream, contentType, fileName) = await filesService.GetFileStreamAsync(serverId, volumePath, subPath, cancellationToken);
            return File(stream, contentType, fileName);
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(ex.Message);
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
            logger.LogError(ex, "Error downloading file for server {ServerId}, path {SubPath}", serverId, subPath);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    [HttpPut("content")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SaveContent(
        string serverId,
        [FromQuery] string volumePath,
        [FromQuery] string subPath,
        [FromBody] SaveFileContentRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(volumePath) || string.IsNullOrWhiteSpace(subPath))
        {
            return BadRequest("volumePath and subPath query parameters are required.");
        }

        try
        {
            await filesService.SaveFileContentTextAsync(serverId, volumePath, subPath, request?.Content ?? string.Empty, cancellationToken);
            return Ok();
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
            logger.LogError(ex, "Error saving file content for server {ServerId}, path {SubPath}", serverId, subPath);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    [HttpPost("upload")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(100_000_000)] // 100 MB
    public async Task<IActionResult> Upload(
        string serverId,
        [FromQuery] string volumePath,
        [FromQuery] string? subPath = null,
        IFormFile? file = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(volumePath))
        {
            return BadRequest("volumePath query parameter is required.");
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest("No file was uploaded.");
        }

        try
        {
            using var stream = file.OpenReadStream();
            await filesService.UploadFileAsync(serverId, volumePath, subPath, stream, file.FileName, cancellationToken);
            return Ok(new { FileName = file.FileName, Size = file.Length });
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
            logger.LogError(ex, "Error uploading file for server {ServerId}, volume {VolumePath}", serverId, volumePath);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    [HttpPost("directory")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateDirectory(
        string serverId,
        [FromQuery] string volumePath,
        [FromQuery] string subPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(volumePath) || string.IsNullOrWhiteSpace(subPath))
        {
            return BadRequest("volumePath and subPath query parameters are required.");
        }

        try
        {
            await filesService.CreateDirectoryAsync(serverId, volumePath, subPath, cancellationToken);
            return Ok();
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
            logger.LogError(ex, "Error creating directory for server {ServerId}, path {SubPath}", serverId, subPath);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        string serverId,
        [FromQuery] string volumePath,
        [FromQuery] string subPath,
        [FromQuery] bool recursive = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(volumePath) || string.IsNullOrWhiteSpace(subPath))
        {
            return BadRequest("volumePath and subPath query parameters are required.");
        }

        try
        {
            await filesService.DeleteFileOrDirectoryAsync(serverId, volumePath, subPath, recursive, cancellationToken);
            return Ok();
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(ex.Message);
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
            logger.LogError(ex, "Error deleting file for server {ServerId}, path {SubPath}", serverId, subPath);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }
}
