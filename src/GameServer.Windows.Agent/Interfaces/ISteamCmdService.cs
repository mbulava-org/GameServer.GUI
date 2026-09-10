using GameServer.Windows.Agent.Models;

namespace GameServer.Windows.Agent.Interfaces;

public interface ISteamCmdService
{
    Task EnsureSteamCmdInstalledAsync(CancellationToken cancellationToken = default);
    Task<SteamCmdJobResult> InstallOrUpdateAppAsync(SteamAppInstallRequest request, IProgress<SteamCmdProgressEvent>? progress = null, CancellationToken cancellationToken = default);
    Task<SteamCmdJobResult> DownloadWorkshopItemAsync(SteamWorkshopDownloadRequest request, IProgress<SteamCmdProgressEvent>? progress = null, CancellationToken cancellationToken = default);
    SteamAppStatusResponse GetAppStatus(uint appId, string installDirectory);
    bool IsInstalled();
}
