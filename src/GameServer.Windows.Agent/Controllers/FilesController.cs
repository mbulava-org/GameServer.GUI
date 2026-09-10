using GameServer.Windows.Agent.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Windows.Agent.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FilesController : ControllerBase
{
    private readonly IFileManagerService _fileManager;
    private readonly ILogger<FilesController> _logger;

    public FilesController(
        IFileManagerService fileManager,
        ILogger<FilesController> logger)
    {
        _fileManager = fileManager;
        _logger = logger;
    }

    /// <summary>
    /// List files and directories within a target root folder.
    /// </summary>
    [HttpGet]
    public ActionResult<IReadOnlyList<FileNode>> ListFiles(
        [FromQuery] string directoryPath,
        [FromQuery] string? subPath = null)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return BadRequest(new { error = "directoryPath is required." });
        }

        var files = _fileManager.ListFiles(directoryPath, subPath);
        return Ok(files);
    }

    /// <summary>
    /// Read the text content of a configuration or log file.
    /// </summary>
    [HttpGet("content")]
    public async Task<ActionResult<string>> ReadFile(
        [FromQuery] string filePath,
        CancellationToken cancellationToken)
    {
        try
        {
            var content = await _fileManager.ReadTextFileAsync(filePath, cancellationToken).ConfigureAwait(false);
            return Ok(content);
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read file '{Path}'", filePath);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Save or overwrite the text content of a configuration file.
    /// </summary>
    [HttpPost("content")]
    public async Task<IActionResult> WriteFile(
        [FromQuery] string filePath,
        [FromBody] FileContentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _fileManager.WriteTextFileAsync(filePath, request.Content ?? string.Empty, cancellationToken).ConfigureAwait(false);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write file '{Path}'", filePath);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Create a compressed zip backup archive of a server's data directory.
    /// </summary>
    [HttpPost("backups/{serverId}")]
    public async Task<ActionResult<BackupArchiveInfo>> CreateBackup(
        string serverId,
        [FromQuery] string sourceDirectory,
        [FromQuery] string? subDirectory,
        CancellationToken cancellationToken)
    {
        try
        {
            var backup = await _fileManager.CreateBackupAsync(serverId, sourceDirectory, subDirectory, cancellationToken).ConfigureAwait(false);
            return Ok(backup);
        }
        catch (DirectoryNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create backup for server '{ServerId}'", serverId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// List all backup archives for a specific game server.
    /// </summary>
    [HttpGet("backups/{serverId}")]
    public ActionResult<IReadOnlyList<BackupArchiveInfo>> ListBackups(string serverId)
    {
        var backups = _fileManager.ListBackups(serverId);
        return Ok(backups);
    }

    /// <summary>
    /// Restore a backup archive into a target directory.
    /// </summary>
    [HttpPost("backups/{serverId}/restore/{backupId}")]
    public async Task<IActionResult> RestoreBackup(
        string serverId,
        string backupId,
        [FromQuery] string targetDirectory,
        CancellationToken cancellationToken)
    {
        try
        {
            await _fileManager.RestoreBackupAsync(serverId, backupId, targetDirectory, cancellationToken).ConfigureAwait(false);
            return Ok(new { success = true, message = $"Backup '{backupId}' restored to '{targetDirectory}'." });
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore backup '{BackupId}' for server '{ServerId}'", backupId, serverId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    public class FileContentRequest
    {
        public string Content { get; set; } = string.Empty;
    }
}
