using GameServer.Windows.Agent.Interfaces;
using GameServer.Windows.Agent.Models;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Windows.Agent.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SteamCmdController : ControllerBase
{
    private readonly ISteamCmdService _steamCmdService;
    private readonly ILogger<SteamCmdController> _logger;

    public SteamCmdController(
        ISteamCmdService steamCmdService,
        ILogger<SteamCmdController> logger)
    {
        _steamCmdService = steamCmdService;
        _logger = logger;
    }

    /// <summary>
    /// Check whether SteamCMD is installed and accessible on this Windows host.
    /// </summary>
    [HttpGet("installed")]
    public ActionResult<bool> CheckInstalled()
    {
        return Ok(_steamCmdService.IsInstalled());
    }

    /// <summary>
    /// Trigger automated installation/download of SteamCMD if missing.
    /// </summary>
    [HttpPost("ensure-installed")]
    public async Task<IActionResult> EnsureInstalled(CancellationToken cancellationToken)
    {
        try
        {
            await _steamCmdService.EnsureSteamCmdInstalledAsync(cancellationToken).ConfigureAwait(false);
            return Ok(new { success = true, message = "SteamCMD is installed and ready." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure SteamCMD is installed");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Install or update a dedicated server game app via SteamCMD.
    /// </summary>
    [HttpPost("install")]
    public async Task<ActionResult<SteamCmdJobResult>> InstallOrUpdateApp(
        [FromBody] SteamAppInstallRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Received SteamCMD install request for App {AppId}", request.AppId);
            var result = await _steamCmdService.InstallOrUpdateAppAsync(request, null, cancellationToken).ConfigureAwait(false);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error installing/updating App {AppId}", request.AppId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Download a Steam Workshop item via SteamCMD.
    /// </summary>
    [HttpPost("workshop/download")]
    public async Task<ActionResult<SteamCmdJobResult>> DownloadWorkshopItem(
        [FromBody] SteamWorkshopDownloadRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Received SteamCMD Workshop download request for App {AppId}, Item {ItemId}",
                request.AppId, request.WorkshopItemId);
            var result = await _steamCmdService.DownloadWorkshopItemAsync(request, null, cancellationToken).ConfigureAwait(false);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading workshop item {ItemId}", request.WorkshopItemId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get the installation status and file inspection of a Steam app.
    /// </summary>
    [HttpGet("apps/{appId}/status")]
    public ActionResult<SteamAppStatusResponse> GetAppStatus(
        uint appId,
        [FromQuery] string installDirectory)
    {
        var status = _steamCmdService.GetAppStatus(appId, installDirectory);
        return Ok(status);
    }
}
