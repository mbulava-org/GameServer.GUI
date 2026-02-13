namespace GameServer.Docker.Interfaces
{
    /// <summary>
    /// Interface for managing game server files through direct filesystem access
    /// </summary>
    public interface IGameServerFileManager
    {
        /// <summary>
        /// List files and directories in a server's volume at the specified path
        /// </summary>
        Task<List<Models.FileItem>> GetFilesAsync(string serverId, string targetVolume, string currentPath);
        
        /// <summary>
        /// Download a file from a server's volume
        /// </summary>
        Task<byte[]> DownloadFileAsync(string serverId, string targetVolume, string filePath);
        
        /// <summary>
        /// Upload a file to a server's volume
        /// </summary>
        Task UploadFileAsync(string serverId, string targetVolume, string filePath, byte[] content);
        
        /// <summary>
        /// Delete a file or directory from a server's volume
        /// </summary>
        Task DeleteFileAsync(string serverId, string targetVolume, string filePath, bool recursive = false);
        
        /// <summary>
        /// Create a directory in a server's volume
        /// </summary>
        Task CreateDirectoryAsync(string serverId, string targetVolume, string directoryPath);
    }
}
