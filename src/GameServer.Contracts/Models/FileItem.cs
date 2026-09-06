namespace GameServer.API.Models
{
    /// <summary>
    /// Represents a file or directory in a Docker volume
    /// </summary>
    public class FileItem
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public long Size { get; set; }
        public string Permissions { get; set; } = string.Empty;
        public DateTime? LastModified { get; set; }
    }

    /// <summary>
    /// Request to upload/create a file
    /// </summary>
    public class FileUploadRequest
    {
        public string TargetPath { get; set; } = "";
        public byte[] Content { get; set; } = Array.Empty<byte>();
    }

    /// <summary>
    /// Request to create a directory
    /// </summary>
    public class DirectoryCreateRequest
    {
        public string Path { get; set; } = "";
    }

    /// <summary>
    /// Request to delete a file or directory
    /// </summary>
    public class FileDeleteRequest
    {
        public string Path { get; set; } = "";
        public bool Recursive { get; set; } = false;
    }
}
