namespace GameServer.Web.Models.V2;

public sealed record CreateBackupRequest
{
    public string VolumePath { get; set; } = string.Empty;
    public string? SubPath { get; set; }
    public bool IsSharedWithGroups { get; set; } = false;
}

public sealed record GameServerBackup
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

public sealed record ExtendBackupRequest
{
    public int AdditionalDays { get; init; } = 30;
}

public sealed record UpdateBackupSharingRequest
{
    public bool IsSharedWithGroups { get; init; }
}

public sealed record GameServerInstanceInfo
{
    public string InstanceId { get; init; } = string.Empty;
    public string? TaskId { get; init; }
    public string? ContainerId { get; init; }
    public string? NodeId { get; init; }
    public string State { get; init; } = string.Empty;
    public string? DesiredState { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public bool IsCurrent { get; init; }
    public int? Slot { get; init; }
    public string? Error { get; init; }
}
