using Docker.DotNet;
using GameServer.Docker.Interfaces;
using Microsoft.Extensions.Options;
using System.Text;

namespace GameServer.Docker.Services
{
    /// <summary>
    /// Service for managing game server files through direct filesystem access
    /// Uses VolumeDriverConfigOptions to resolve volume paths on the host filesystem
    /// </summary>
    public class GameServerFileManagerService : IGameServerFileManager
    {
        private readonly ILogger<GameServerFileManagerService> _logger;
        private readonly DockerServiceHelper _dockerServiceHelper;
        private readonly Configurations.VolumeDriverConfigOptions _volumeConfig;

        public GameServerFileManagerService(
            ILogger<GameServerFileManagerService> logger,
            DockerServiceHelper dockerServiceHelper,
            IOptions<Configurations.VolumeDriverConfigOptions> volumeConfig)
        {
            _logger = logger;
            _dockerServiceHelper = dockerServiceHelper;
            _volumeConfig = volumeConfig.Value;
        }

        public async Task<List<Models.FileItem>> GetFilesAsync(string serverId, string targetVolume, string currentPath)
        {
            _logger.LogInformation($"Listing files for server {serverId} in volume {targetVolume} at path {currentPath}");
            
            var gameTypeKey = await GetGameTypeKeyAsync(serverId);
            if (gameTypeKey == null)
                throw new InvalidOperationException($"Unable to resolve game type for server {serverId}");

            var localVolumePath = GetLocalVolumePath(serverId, gameTypeKey, targetVolume);
            var fullPath = Path.Combine(localVolumePath, currentPath.TrimStart('/'));

            _logger.LogDebug($"Listing directory: {fullPath}");

            if (!Directory.Exists(fullPath))
            {
                _logger.LogWarning($"Directory does not exist: {fullPath}");
                return new List<Models.FileItem>();
            }

            var items = new List<Models.FileItem>();

            try
            {
                // Get directories
                var directories = Directory.GetDirectories(fullPath);
                foreach (var dir in directories)
                {
                    var dirInfo = new DirectoryInfo(dir);
                    items.Add(new Models.FileItem
                    {
                        Name = dirInfo.Name,
                        Path = Path.Combine(currentPath, dirInfo.Name).Replace('\\', '/'),
                        IsDirectory = true,
                        Size = 0,
                        Permissions = GetPermissionsString(dirInfo),
                        LastModified = dirInfo.LastWriteTimeUtc
                    });
                }

                // Get files
                var files = Directory.GetFiles(fullPath);
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    items.Add(new Models.FileItem
                    {
                        Name = fileInfo.Name,
                        Path = Path.Combine(currentPath, fileInfo.Name).Replace('\\', '/'),
                        IsDirectory = false,
                        Size = fileInfo.Length,
                        Permissions = GetPermissionsString(fileInfo),
                        LastModified = fileInfo.LastWriteTimeUtc
                    });
                }

                _logger.LogDebug($"Found {items.Count} items in {fullPath}");
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, $"Access denied to directory: {fullPath}");
                throw new InvalidOperationException($"Access denied to directory: {currentPath}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error listing directory: {fullPath}");
                throw;
            }

            return items.OrderBy(i => !i.IsDirectory).ThenBy(i => i.Name).ToList();
        }

        public async Task<byte[]> DownloadFileAsync(string serverId, string targetVolume, string filePath)
        {
            _logger.LogInformation($"Downloading file {filePath} from server {serverId}");
            
            var gameTypeKey = await GetGameTypeKeyAsync(serverId);
            if (gameTypeKey == null)
                throw new InvalidOperationException($"Unable to resolve game type for server {serverId}");

            var localVolumePath = GetLocalVolumePath(serverId, gameTypeKey, targetVolume);
            var fullPath = Path.Combine(localVolumePath, filePath.TrimStart('/'));

            _logger.LogDebug($"Downloading file: {fullPath}");

            if (!File.Exists(fullPath))
            {
                _logger.LogWarning($"File does not exist: {fullPath}");
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            try
            {
                return await File.ReadAllBytesAsync(fullPath);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, $"Access denied to file: {fullPath}");
                throw new InvalidOperationException($"Access denied to file: {filePath}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error reading file: {fullPath}");
                throw;
            }
        }

        public async Task UploadFileAsync(string serverId, string targetVolume, string filePath, byte[] content)
        {
            _logger.LogInformation($"Uploading file {filePath} to server {serverId} ({content.Length} bytes)");
            
            var gameTypeKey = await GetGameTypeKeyAsync(serverId);
            if (gameTypeKey == null)
                throw new InvalidOperationException($"Unable to resolve game type for server {serverId}");

            var localVolumePath = GetLocalVolumePath(serverId, gameTypeKey, targetVolume);
            var fullPath = Path.Combine(localVolumePath, filePath.TrimStart('/'));

            _logger.LogDebug($"Uploading file: {fullPath}");

            try
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    _logger.LogDebug($"Creating directory: {directory}");
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllBytesAsync(fullPath, content);
                _logger.LogInformation($"Successfully uploaded file: {filePath}");
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, $"Access denied to file: {fullPath}");
                throw new InvalidOperationException($"Access denied to file: {filePath}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error writing file: {fullPath}");
                throw;
            }
        }

        public async Task DeleteFileAsync(string serverId, string targetVolume, string filePath, bool recursive = false)
        {
            _logger.LogInformation($"Deleting {(recursive ? "recursively " : "")}file/directory {filePath} from server {serverId}");
            
            var gameTypeKey = await GetGameTypeKeyAsync(serverId);
            if (gameTypeKey == null)
                throw new InvalidOperationException($"Unable to resolve game type for server {serverId}");

            var localVolumePath = GetLocalVolumePath(serverId, gameTypeKey, targetVolume);
            var fullPath = Path.Combine(localVolumePath, filePath.TrimStart('/'));

            _logger.LogDebug($"Deleting: {fullPath}");

            try
            {
                if (Directory.Exists(fullPath))
                {
                    Directory.Delete(fullPath, recursive);
                    _logger.LogInformation($"Successfully deleted directory: {filePath}");
                }
                else if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    _logger.LogInformation($"Successfully deleted file: {filePath}");
                }
                else
                {
                    _logger.LogWarning($"Path does not exist: {fullPath}");
                    throw new FileNotFoundException($"File or directory not found: {filePath}");
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, $"Access denied: {fullPath}");
                throw new InvalidOperationException($"Access denied: {filePath}", ex);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, $"IO error deleting: {fullPath}");
                throw new InvalidOperationException($"Cannot delete {filePath}: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting: {fullPath}");
                throw;
            }
        }

        public async Task CreateDirectoryAsync(string serverId, string targetVolume, string directoryPath)
        {
            _logger.LogInformation($"Creating directory {directoryPath} in server {serverId}");
            
            var gameTypeKey = await GetGameTypeKeyAsync(serverId);
            if (gameTypeKey == null)
                throw new InvalidOperationException($"Unable to resolve game type for server {serverId}");

            var localVolumePath = GetLocalVolumePath(serverId, gameTypeKey, targetVolume);
            var fullPath = Path.Combine(localVolumePath, directoryPath.TrimStart('/'));

            _logger.LogDebug($"Creating directory: {fullPath}");

            try
            {
                if (Directory.Exists(fullPath))
                {
                    _logger.LogDebug($"Directory already exists: {fullPath}");
                    return;
                }

                Directory.CreateDirectory(fullPath);
                _logger.LogInformation($"Successfully created directory: {directoryPath}");
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, $"Access denied: {fullPath}");
                throw new InvalidOperationException($"Access denied: {directoryPath}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating directory: {fullPath}");
                throw;
            }
        }

        #region Private Helper Methods

        /// <summary>
        /// Get the local filesystem path for a volume
        /// </summary>
        private string GetLocalVolumePath(string serverId, string gameTypeKey, string targetVolume)
        {
            // Format: {LocalStoragePath}/{SubPathFormat}
            // SubPathFormat example: "{gameTypeKey}_{serverId}/{Source}"
            // {Source} is replaced with targetVolume with "/" removed (e.g., "/data" -> "data")
            
            var sanitizedTarget = targetVolume.Replace("/", "");
            
            var subPath = _volumeConfig.SubPathFormat
                .Replace("{gameTypeKey}", gameTypeKey)
                .Replace("{serverId}", serverId)
                .Replace("{Source}", sanitizedTarget);

            var fullPath = Path.Combine(_volumeConfig.LocalStoragePath, subPath);
            
            _logger.LogDebug($"Resolved volume path: LocalStoragePath={_volumeConfig.LocalStoragePath}, SubPath={subPath}, TargetVolume={targetVolume}, Sanitized={sanitizedTarget}, FullPath={fullPath}");
            
            return fullPath;
        }

        /// <summary>
        /// Get the game type key from server configuration
        /// </summary>
        private async Task<string?> GetGameTypeKeyAsync(string serverId)
        {
            var server = await _dockerServiceHelper.GetGameServerById(serverId);
            return server?.GameType;
        }

        /// <summary>
        /// Get a Unix-style permissions string for a file or directory
        /// </summary>
        private string GetPermissionsString(FileSystemInfo info)
        {
            var permissions = new StringBuilder();
            
            // Type indicator
            permissions.Append(info is DirectoryInfo ? 'd' : '-');
            
            // Owner permissions
            permissions.Append(HasAttribute(info, FileAttributes.ReadOnly) ? 'r' : 'r');
            permissions.Append('w');
            permissions.Append('x');
            
            // Group permissions (simplified for Windows)
            permissions.Append('r');
            permissions.Append('w');
            permissions.Append('x');
            
            // Other permissions (simplified for Windows)
            permissions.Append('r');
            permissions.Append('-');
            permissions.Append('-');
            
            return permissions.ToString();
        }

        private bool HasAttribute(FileSystemInfo info, FileAttributes attribute)
        {
            return (info.Attributes & attribute) == attribute;
        }

        #endregion
    }
}
