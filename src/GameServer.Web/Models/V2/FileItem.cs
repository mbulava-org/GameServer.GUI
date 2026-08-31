namespace GameServer.Web.Models.V2;

public sealed record FileItem
{
    public string Name { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public long Size { get; set; }

    public bool IsDirectory { get; set; }

    public DateTime LastModified { get; set; }

    public string? Extension { get; set; }

    public string? Permissions { get; set; }
}

public sealed class UploadProgress
{
    public string FileName { get; set; } = string.Empty;

    public long TotalBytes { get; set; }

    public long UploadedBytes { get; set; }

    public bool IsComplete { get; set; }

    public string? Error { get; set; }

    public double Percentage => TotalBytes > 0 ? (double)UploadedBytes / TotalBytes * 100 : 0;
}
