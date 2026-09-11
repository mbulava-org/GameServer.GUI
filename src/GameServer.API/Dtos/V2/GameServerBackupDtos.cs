namespace GameServer.API.Dtos.V2;

public sealed record CreateBackupRequestDto
{
    public string VolumePath { get; init; } = string.Empty;
    public string? SubPath { get; init; }
    public bool IsSharedWithGroups { get; init; } = false;
}

public sealed record GameServerBackupDto
{
    public int Id { get; init; }
    public string BackupId { get; init; } = string.Empty;
    public string ServerId { get; init; } = string.Empty;
    public string ServerName { get; init; } = string.Empty;
    public string VolumePath { get; init; } = string.Empty;
    public string? SourceSubPath { get; init; }
    public bool IsDirectory { get; init; }
    public string FileName { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public int CreatedByUserId { get; init; }
    public string CreatedByUsername { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
    public int ExtensionDaysAdded { get; init; }
    public bool CanExtend { get; init; }
    public bool IsSharedWithGroups { get; init; }
    public bool IsOwner { get; init; }
}

public sealed record ExtendBackupRequestDto
{
    public int AdditionalDays { get; init; } = 30;
}

public sealed record UpdateBackupSharingRequestDto
{
    public bool IsSharedWithGroups { get; init; }
}
