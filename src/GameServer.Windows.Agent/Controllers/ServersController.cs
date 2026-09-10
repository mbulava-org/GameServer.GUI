using GameServer.Windows.Agent.Interfaces;
using GameServer.Windows.Agent.Models;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Windows.Agent.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServersController : ControllerBase
{
    private readonly IGameProcessManager _processManager;
    private readonly ILogger<ServersController> _logger;

    public ServersController(
        IGameProcessManager processManager,
        ILogger<ServersController> logger)
    {
        _processManager = processManager;
        _logger = logger;
    }

    /// <summary>
    /// List all game server processes currently registered on this Windows host.
    /// </summary>
    [HttpGet]
    public ActionResult<IReadOnlyList<GameServerProcessInfo>> GetAllServers()
    {
        return Ok(_processManager.GetAllServers());
    }

    /// <summary>
    /// Get process status and details for a specific game server.
    /// </summary>
    [HttpGet("{serverId}")]
    public ActionResult<GameServerProcessInfo> GetServer(string serverId)
    {
        var info = _processManager.GetServerInfo(serverId);
        if (info == null)
        {
            return NotFound(new { error = $"Server '{serverId}' not found." });
        }
        return Ok(info);
    }

    /// <summary>
    /// Start a game server process.
    /// </summary>
    [HttpPost("start")]
    public async Task<ActionResult<GameServerProcessInfo>> StartServer(
        [FromBody] StartServerRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var info = await _processManager.StartServerAsync(request, cancellationToken).ConfigureAwait(false);
            return Ok(info);
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start server '{ServerId}'", request.ServerId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Stop a running game server process.
    /// </summary>
    [HttpPost("{serverId}/stop")]
    public async Task<ActionResult<GameServerProcessInfo>> StopServer(
        string serverId,
        [FromBody] StopServerRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var info = await _processManager.StopServerAsync(serverId, request, cancellationToken).ConfigureAwait(false);
            return Ok(info);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Server '{serverId}' not found." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop server '{ServerId}'", serverId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Restart a game server process.
    /// </summary>
    [HttpPost("{serverId}/restart")]
    public async Task<ActionResult<GameServerProcessInfo>> RestartServer(
        string serverId,
        CancellationToken cancellationToken)
    {
        try
        {
            var info = await _processManager.RestartServerAsync(serverId, cancellationToken).ConfigureAwait(false);
            return Ok(info);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Server '{serverId}' not found." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart server '{ServerId}'", serverId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get recent log lines from the server's circular buffer.
    /// </summary>
    [HttpGet("{serverId}/logs")]
    public ActionResult<ProcessLogsResponse> GetLogs(
        string serverId,
        [FromQuery] int tail = 100)
    {
        try
        {
            var logs = _processManager.GetLogs(serverId, tail);
            return Ok(logs);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Server '{serverId}' not found." });
        }
    }

    /// <summary>
    /// Get point-in-time process CPU and memory statistics.
    /// </summary>
    [HttpGet("{serverId}/stats")]
    public ActionResult<ProcessStatsSnapshot> GetStats(string serverId)
    {
        var stats = _processManager.GetStats(serverId);
        if (stats == null)
        {
            return NotFound(new { error = $"Server '{serverId}' not found." });
        }
        return Ok(stats);
    }

    /// <summary>
    /// Send an interactive stdin or RCON command to a running game server.
    /// </summary>
    [HttpPost("{serverId}/command")]
    public async Task<ActionResult<SendCommandResponse>> SendCommand(
        string serverId,
        [FromBody] SendCommandRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _processManager.SendCommandAsync(serverId, request, cancellationToken).ConfigureAwait(false);
        if (!response.Success)
        {
            return BadRequest(response);
        }
        return Ok(response);
    }
}
