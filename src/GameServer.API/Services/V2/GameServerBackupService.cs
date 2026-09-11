using System.IO.Compression;
using System.Security.Claims;
using GameServer.API.Data.V2;
using GameServer.API.Dtos.V2;
using GameServer.API.Interfaces;
using GameServer.API.Models;
using GameServer.API.Models.V2;
using GameServer.API.Repositories.V2;
using Microsoft.EntityFrameworkCore;
using GameServerModel = GameServer.API.Models.V2.GameServer;

namespace GameServer.API.Services.V2;

public sealed class GameServerBackupService : IGameServerBackupService
{
    private readonly GameServerV2DbContext _dbContext;
    private readonly IGameServerRepository _gameServerRepository;
    private readonly IGameTypeRepository _gameTypeRepository;
    private readonly IMountTypeConfigRepository _mountTypeConfigRepository;
    private readonly INodeAgentDiscovery _nodeAgentDiscovery;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServerAuthorizationService _authService;
    private readonly IGroupRepository _groupRepository;
    private readonly IServerResourceMonitor? _serverResourceMonitor;
    private readonly ILogger<GameServerBackupService> _logger;

    private static readonly string BackupStorageDirectory = ResolveBackupDirectory();

    public GameServerBackupService(
        GameServerV2DbContext dbContext,
        IGameServerRepository gameServerRepository,
        IGameTypeRepository gameTypeRepository,
        IMountTypeConfigRepository mountTypeConfigRepository,
        INodeAgentDiscovery nodeAgentDiscovery,
        IHttpClientFactory httpClientFactory,
        IServerAuthorizationService authService,
        IGroupRepository groupRepository,
        ILogger<GameServerBackupService> logger,
        IServerResourceMonitor? serverResourceMonitor = null)
    {
        _dbContext = dbContext;
        _gameServerRepository = gameServerRepository;
        _gameTypeRepository = gameTypeRepository;
        _mountTypeConfigRepository = mountTypeConfigRepository;
        _nodeAgentDiscovery = nodeAgentDiscovery;
        _httpClientFactory = httpClientFactory;
        _authService = authService;
        _groupRepository = groupRepository;
        _logger = logger;
        _serverResourceMonitor = serverResourceMonitor;

        Directory.CreateDirectory(BackupStorageDirectory);
    }

    private static string ResolveBackupDirectory()
    {
        var configured = Environment.GetEnvironmentVariable("BACKUP_STORAGE_PATH");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var linuxData = "/data/backups";
        if (OperatingSystem.IsLinux() && Directory.Exists("/data"))
        {
            return linuxData;
        }

        return Path.Combine(AppContext.BaseDirectory, "data", "backups");
    }

    public async Task<GameServerBackupDto> CreateBackupAsync(
        string serverId,
        CreateBackupRequestDto request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!await _authService.CanViewServerAsync(user, serverId, cancellationToken))
        {
            throw new UnauthorizedAccessException("You are not authorized to create backups for this server.");
        }

        var server = await _gameServerRepository.GetByServerIdAsync(serverId)
            ?? throw new KeyNotFoundException($"Server '{serverId}' was not found.");

        var volume = server.Volumes.FirstOrDefault(v =>
            string.Equals(v.ContainerPath.TrimEnd('/'), request.VolumePath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Volume '{request.VolumePath}' not found on server '{serverId}'.");

        var userId = GetCurrentUserId(user) ?? server.CreatedByUserId ?? 0;
        var username = user.Identity?.Name ?? "system";

        var sanitizedServerName = SanitizeFileName(server.Name);
        var sourceLabel = string.IsNullOrWhiteSpace(request.SubPath)
            ? SanitizeFileName(request.VolumePath.Trim('/'))
            : SanitizeFileName(request.SubPath.Trim('/'));

        if (string.IsNullOrEmpty(sourceLabel))
        {
            sourceLabel = "volume";
        }

        var timestampStr = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var backupId = Guid.NewGuid().ToString("N");
        var fileName = $"{sanitizedServerName}_{sourceLabel}_{timestampStr}.zip";
        var targetZipPath = Path.Combine(BackupStorageDirectory, $"{backupId}_{fileName}");

        Directory.CreateDirectory(BackupStorageDirectory);

        var isDirectory = true;

        // Try local NFS filesystem path first if applicable or when container not running
        var localNfsPath = await TryResolveLocalNfsPathAsync(server, volume, request.SubPath, cancellationToken);

        if (!string.IsNullOrWhiteSpace(localNfsPath) && (File.Exists(localNfsPath) || Directory.Exists(localNfsPath)))
        {
            if (File.Exists(localNfsPath))
            {
                isDirectory = false;
                await CreateZipFromSingleFileAsync(localNfsPath, targetZipPath, cancellationToken);
            }
            else
            {
                isDirectory = true;
                await CreateZipFromDirectoryAsync(localNfsPath, targetZipPath, cancellationToken);
            }
        }
        else
        {
            // Try via running container
            var (agent, containerId) = await ResolveAgentAndContainerAsync(serverId, cancellationToken);
            if (agent != null && !string.IsNullOrWhiteSpace(containerId))
            {
                var containerTargetPath = GameServerFilesService.CombineContainerPath(request.VolumePath, request.SubPath);
                await CreateZipFromContainerAsync(agent, containerId, containerTargetPath, targetZipPath, cancellationToken);
            }
            else if (!string.IsNullOrWhiteSpace(localNfsPath))
            {
                // NFS directory does not exist yet; create empty zip or throw
                throw new DirectoryNotFoundException($"Source path '{localNfsPath}' does not exist on storage.");
            }
            else
            {
                throw new InvalidOperationException($"Cannot create backup: Server '{serverId}' is not running and has no accessible local NFS mount.");
            }
        }

        var fileInfo = new FileInfo(targetZipPath);
        var sizeBytes = fileInfo.Exists ? fileInfo.Length : 0;

        var entity = new GameServerBackupEntity
        {
            BackupId = backupId,
            ServerId = serverId,
            ServerName = server.Name,
            VolumePath = request.VolumePath,
            SourceSubPath = request.SubPath,
            IsDirectory = isDirectory,
            FileName = fileName,
            FilePath = targetZipPath,
            FileSizeBytes = sizeBytes,
            CreatedByUserId = userId,
            CreatedByUsername = username,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            ExtensionDaysAdded = 0,
            IsSharedWithGroups = request.IsSharedWithGroups,
            IsDeleted = false
        };

        _dbContext.Backups.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created backup {BackupId} ({FileName}, {Size} bytes) for server {ServerId} by {User}",
            backupId, fileName, sizeBytes, serverId, username);

        return MapToDto(entity, user);
    }

    private static async Task CreateZipFromSingleFileAsync(string sourceFile, string targetZipFile, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (File.Exists(targetZipFile)) File.Delete(targetZipFile);

        using var zipToOpen = new FileStream(targetZipFile, FileMode.CreateNew);
        using var archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create);
        var entryName = Path.GetFileName(sourceFile);
        archive.CreateEntryFromFile(sourceFile, entryName, CompressionLevel.Optimal);
        await Task.CompletedTask;
    }

    private static async Task CreateZipFromDirectoryAsync(string sourceDir, string targetZipFile, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (File.Exists(targetZipFile)) File.Delete(targetZipFile);

        using var zipToOpen = new FileStream(targetZipFile, FileMode.CreateNew);
        using var archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create);

        var rootUri = new Uri(sourceDir.TrimEnd('/', '\\') + "/");
        var files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var fileUri = new Uri(file);
            var relativePath = Uri.UnescapeDataString(rootUri.MakeRelativeUri(fileUri).ToString());
            archive.CreateEntryFromFile(file, relativePath, CompressionLevel.Optimal);
        }

        await Task.CompletedTask;
    }

    private async Task CreateZipFromContainerAsync(
        NodeAgentEndpoint agent,
        string containerId,
        string containerPath,
        string targetZipFile,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var client = _httpClientFactory.CreateClient();
        var downloadUrl = $"{agent.InternalUrl.TrimEnd('/')}/containers/{containerId}/files/download?path={Uri.EscapeDataString(containerPath)}";

        var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            // It was a single file
            using var remoteStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            if (File.Exists(targetZipFile)) File.Delete(targetZipFile);

            using var zipToOpen = new FileStream(targetZipFile, FileMode.CreateNew);
            using var archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create);
            var entryName = Path.GetFileName(containerPath);
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            await remoteStream.CopyToAsync(entryStream, ct).ConfigureAwait(false);
            return;
        }

        // Otherwise list files recursively and add them to zip
        if (File.Exists(targetZipFile)) File.Delete(targetZipFile);
        using var zipStream = new FileStream(targetZipFile, FileMode.CreateNew);
        using var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Create);

        await AddContainerFolderToZipAsync(agent, containerId, containerPath, containerPath, zipArchive, client, ct);
    }

    private async Task AddContainerFolderToZipAsync(
        NodeAgentEndpoint agent,
        string containerId,
        string currentPath,
        string rootPath,
        ZipArchive zipArchive,
        HttpClient client,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var listUrl = $"{agent.InternalUrl.TrimEnd('/')}/containers/{containerId}/files?path={Uri.EscapeDataString(currentPath)}";
        var response = await client.GetAsync(listUrl, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return;
        }

        var files = await response.Content.ReadFromJsonAsync<List<FileItemDto>>(cancellationToken: ct).ConfigureAwait(false);
        if (files is null) return;

        var normalizedRoot = rootPath.TrimEnd('/') + "/";

        foreach (var item in files)
        {
            ct.ThrowIfCancellationRequested();
            var itemFullPath = item.Path.StartsWith('/') ? item.Path : "/" + item.Path;

            if (item.IsDirectory)
            {
                await AddContainerFolderToZipAsync(agent, containerId, itemFullPath, rootPath, zipArchive, client, ct);
            }
            else
            {
                var relative = itemFullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
                    ? itemFullPath[normalizedRoot.Length..]
                    : itemFullPath.TrimStart('/');

                var downloadUrl = $"{agent.InternalUrl.TrimEnd('/')}/containers/{containerId}/files/download?path={Uri.EscapeDataString(itemFullPath)}";
                var fileResp = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                if (fileResp.IsSuccessStatusCode)
                {
                    using var stream = await fileResp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                    var entry = zipArchive.CreateEntry(relative, CompressionLevel.Optimal);
                    using var entryStream = entry.Open();
                    await stream.CopyToAsync(entryStream, ct).ConfigureAwait(false);
                }
            }
        }
    }

    public async Task<IReadOnlyList<GameServerBackupDto>> GetBackupsAsync(
        ClaimsPrincipal user,
        string? serverId = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var query = _dbContext.Backups.AsNoTracking().Where(b => !b.IsDeleted);

        if (!string.IsNullOrWhiteSpace(serverId))
        {
            query = query.Where(b => b.ServerId == serverId);
        }

        var allBackups = await query.OrderByDescending(b => b.CreatedAt).ToListAsync(cancellationToken);

        var isAdmin = user.IsInRole("Admin");
        var currentUserId = GetCurrentUserId(user);

        if (isAdmin)
        {
            return allBackups.Select(b => MapToDto(b, user)).ToList();
        }

        if (!currentUserId.HasValue)
        {
            return [];
        }

        // Get groups user belongs to
        var userGroupIds = await _groupRepository.GetUserGroupIdsAsync(currentUserId.Value, cancellationToken);
        var userGroupIdsSet = userGroupIds.ToHashSet();

        var accessibleBackups = new List<GameServerBackupDto>();

        foreach (var b in allBackups)
        {
            if (b.CreatedByUserId == currentUserId.Value)
            {
                accessibleBackups.Add(MapToDto(b, user));
            }
            else if (b.IsSharedWithGroups)
            {
                // Check if the backup creator shares any group with current user
                var creatorGroupIds = await _groupRepository.GetUserGroupIdsAsync(b.CreatedByUserId, cancellationToken);
                if (creatorGroupIds.Any(gid => userGroupIdsSet.Contains(gid)))
                {
                    accessibleBackups.Add(MapToDto(b, user));
                }
            }
        }

        return accessibleBackups;
    }

    public async Task<GameServerBackupDto?> GetBackupByIdAsync(
        int id,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var backup = await _dbContext.Backups.FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted, cancellationToken);
        if (backup == null)
        {
            return null;
        }

        if (!await CanAccessBackupAsync(backup, user, cancellationToken))
        {
            throw new UnauthorizedAccessException("You do not have access to this backup.");
        }

        return MapToDto(backup, user);
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> DownloadBackupAsync(
        int id,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var backup = await _dbContext.Backups.FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException($"Backup with ID {id} was not found.");

        if (backup.ExpiresAt <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("This backup has expired and is no longer available.");
        }

        if (!await CanAccessBackupAsync(backup, user, cancellationToken))
        {
            throw new UnauthorizedAccessException("You do not have access to download this backup.");
        }

        if (!File.Exists(backup.FilePath))
        {
            throw new FileNotFoundException($"Backup file '{backup.FileName}' was not found on persistent storage.");
        }

        var stream = new FileStream(backup.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return (stream, "application/zip", backup.FileName);
    }

    public async Task<GameServerBackupDto> ExtendBackupRetentionAsync(
        int id,
        ExtendBackupRequestDto request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var backup = await _dbContext.Backups.FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException($"Backup with ID {id} was not found.");

        var isAdmin = user.IsInRole("Admin");
        var currentUserId = GetCurrentUserId(user);

        if (!isAdmin && (!currentUserId.HasValue || backup.CreatedByUserId != currentUserId.Value))
        {
            throw new UnauthorizedAccessException("Only the creator or an administrator can extend backup retention.");
        }

        if (backup.ExpiresAt <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("Cannot extend an expired backup.");
        }

        var daysToAdd = Math.Clamp(request.AdditionalDays > 0 ? request.AdditionalDays : 30, 1, 30);
        var maxExpiresAt = backup.CreatedAt.AddDays(60);

        if (backup.ExtensionDaysAdded >= 30 || backup.ExpiresAt >= maxExpiresAt)
        {
            throw new InvalidOperationException("This backup has already reached the maximum 30-day retention extension limit (60 days total).");
        }

        var allowedExtension = Math.Min(daysToAdd, 30 - backup.ExtensionDaysAdded);
        backup.ExpiresAt = backup.ExpiresAt.AddDays(allowedExtension);
        if (backup.ExpiresAt > maxExpiresAt)
        {
            backup.ExpiresAt = maxExpiresAt;
        }

        backup.ExtensionDaysAdded += allowedExtension;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Extended retention for backup {BackupId} by {Days} days. New expiry: {Expiry}",
            backup.BackupId, allowedExtension, backup.ExpiresAt);

        return MapToDto(backup, user);
    }

    public async Task<GameServerBackupDto> UpdateSharingAsync(
        int id,
        UpdateBackupSharingRequestDto request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var backup = await _dbContext.Backups.FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException($"Backup with ID {id} was not found.");

        var isAdmin = user.IsInRole("Admin");
        var currentUserId = GetCurrentUserId(user);

        if (!isAdmin && (!currentUserId.HasValue || backup.CreatedByUserId != currentUserId.Value))
        {
            throw new UnauthorizedAccessException("Only the creator or an administrator can change backup sharing.");
        }

        backup.IsSharedWithGroups = request.IsSharedWithGroups;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated sharing for backup {BackupId} to {IsShared}", backup.BackupId, backup.IsSharedWithGroups);

        return MapToDto(backup, user);
    }

    public async Task DeleteBackupAsync(
        int id,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var backup = await _dbContext.Backups.FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException($"Backup with ID {id} was not found.");

        var isAdmin = user.IsInRole("Admin");
        var currentUserId = GetCurrentUserId(user);

        if (!isAdmin && (!currentUserId.HasValue || backup.CreatedByUserId != currentUserId.Value))
        {
            throw new UnauthorizedAccessException("Only the creator or an administrator can delete this backup.");
        }

        try
        {
            if (File.Exists(backup.FilePath))
            {
                File.Delete(backup.FilePath);
                _logger.LogInformation("Deleted backup file from disk: {FilePath}", backup.FilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete backup file from disk: {FilePath}", backup.FilePath);
        }

        backup.IsDeleted = true;
        backup.DeletedAt = DateTime.UtcNow;
        _dbContext.Backups.Remove(backup);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Permanently removed backup {BackupId}", backup.BackupId);
    }

    public async Task<int> CleanupExpiredBackupsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var now = DateTime.UtcNow;
        var expired = await _dbContext.Backups
            .Where(b => !b.IsDeleted && b.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        var cleanedCount = 0;
        foreach (var backup in expired)
        {
            try
            {
                if (File.Exists(backup.FilePath))
                {
                    File.Delete(backup.FilePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error deleting expired backup file {FilePath}", backup.FilePath);
            }

            backup.IsDeleted = true;
            backup.DeletedAt = now;
            _dbContext.Backups.Remove(backup);
            cleanedCount++;
        }

        if (cleanedCount > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Cleaned up {Count} expired backup(s)", cleanedCount);
        }

        return cleanedCount;
    }

    private async Task<bool> CanAccessBackupAsync(GameServerBackupEntity backup, ClaimsPrincipal user, CancellationToken ct)
    {
        if (user.IsInRole("Admin")) return true;
        var currentUserId = GetCurrentUserId(user);
        if (!currentUserId.HasValue) return false;

        if (backup.CreatedByUserId == currentUserId.Value) return true;

        if (backup.IsSharedWithGroups)
        {
            var userGroupIds = (await _groupRepository.GetUserGroupIdsAsync(currentUserId.Value, ct)).ToHashSet();
            var creatorGroupIds = await _groupRepository.GetUserGroupIdsAsync(backup.CreatedByUserId, ct);
            if (creatorGroupIds.Any(gid => userGroupIds.Contains(gid)))
            {
                return true;
            }
        }

        return false;
    }

    private static int? GetCurrentUserId(ClaimsPrincipal user)
    {
        var idClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value
            ?? user.FindFirst("userId")?.Value;

        return int.TryParse(idClaim, out var id) ? id : null;
    }

    private GameServerBackupDto MapToDto(GameServerBackupEntity entity, ClaimsPrincipal user)
    {
        var currentUserId = GetCurrentUserId(user);
        var isOwner = currentUserId.HasValue && entity.CreatedByUserId == currentUserId.Value;
        var isAdmin = user.IsInRole("Admin");
        var canExtend = !entity.IsDeleted
            && entity.ExtensionDaysAdded < 30
            && entity.ExpiresAt < entity.CreatedAt.AddDays(60)
            && entity.ExpiresAt > DateTime.UtcNow
            && (isOwner || isAdmin);

        return new GameServerBackupDto
        {
            Id = entity.Id,
            BackupId = entity.BackupId,
            ServerId = entity.ServerId,
            ServerName = entity.ServerName,
            VolumePath = entity.VolumePath,
            SourceSubPath = entity.SourceSubPath,
            IsDirectory = entity.IsDirectory,
            FileName = entity.FileName,
            FileSizeBytes = entity.FileSizeBytes,
            CreatedByUserId = entity.CreatedByUserId,
            CreatedByUsername = entity.CreatedByUsername,
            CreatedAt = entity.CreatedAt,
            ExpiresAt = entity.ExpiresAt,
            ExtensionDaysAdded = entity.ExtensionDaysAdded,
            CanExtend = canExtend,
            IsSharedWithGroups = entity.IsSharedWithGroups,
            IsOwner = isOwner
        };
    }

    private async Task<string?> TryResolveLocalNfsPathAsync(
        GameServerModel server,
        GameServer.API.Models.V2.GameServerVolume volume,
        string? subPath,
        CancellationToken ct)
    {
        try
        {
            var mountConfig = await _mountTypeConfigRepository.GetByKeyAsync(volume.MountType, ct);
            if (mountConfig == null) return null;

            var localRoot = mountConfig.GetOption("LocalPath")?.Replace('\\', '/').TrimEnd('/');
            if (string.IsNullOrWhiteSpace(localRoot)) return null;

            var sourceToken = (volume.ContainerPath ?? string.Empty).Replace('\\', '/').TrimStart('/').Replace('/', '-');
            var devicePathFormat = mountConfig.GetOption("DevicePathFormat") ?? sourceToken;

            var gameTypeKey = "gametype";
            var allTypes = await _gameTypeRepository.GetAllAsync();
            var match = allTypes.FirstOrDefault(gt => gt.Revisions.Any(r => r.Id == server.GameTypeRevisionId));
            if (match != null)
            {
                gameTypeKey = match.Key;
            }

            var devicePath = devicePathFormat
                .Replace("{gameTypeKey}", gameTypeKey, StringComparison.OrdinalIgnoreCase)
                .Replace("{serverId}", server.ServerId, StringComparison.OrdinalIgnoreCase)
                .Replace("{Source}", sourceToken, StringComparison.OrdinalIgnoreCase)
                .Trim('/');

            var fullPath = $"{localRoot}/{devicePath}";

            if (!string.IsNullOrWhiteSpace(subPath))
            {
                var cleanSub = subPath.Replace('\\', '/').Trim('/');
                fullPath = $"{fullPath}/{cleanSub}";
            }

            return fullPath;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not resolve local NFS path for volume {VolumePath}", volume.ContainerPath);
            return null;
        }
    }

    private async Task<(NodeAgentEndpoint? Agent, string? ContainerId)> ResolveAgentAndContainerAsync(
        string serverId,
        CancellationToken cancellationToken)
    {
        if (_serverResourceMonitor != null)
        {
            try
            {
                var snapshot = await _serverResourceMonitor.GetSnapshotAsync(serverId, cancellationToken).ConfigureAwait(false);
                var containerId = snapshot?.ContainerIds.FirstOrDefault() ?? snapshot?.RealTimeStats?.ContainerId;
                if (!string.IsNullOrWhiteSpace(containerId))
                {
                    var agent = await _nodeAgentDiscovery.GetAgentForContainerAsync(containerId).ConfigureAwait(false)
                        ?? await _nodeAgentDiscovery.GetAgentForServerAsync(serverId).ConfigureAwait(false);
                    if (agent != null)
                    {
                        return (agent, containerId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not resolve container from resource monitor for server {ServerId}", serverId);
            }
        }

        var fallbackAgent = await _nodeAgentDiscovery.GetAgentForServerAsync(serverId).ConfigureAwait(false);
        return (fallbackAgent, null);
    }

    private static string SanitizeFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "file";
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(input.Where(c => !invalid.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "file" : cleaned.Replace(' ', '_');
    }
}
