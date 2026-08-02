namespace GameServer.Docker.Dtos.V2;

public sealed record GameServerListItemDto
{
    public int Id { get; init; }

    public string ServerId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public int GameTypeRevisionId { get; init; }

    public string ServiceName { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }

    public DateTime? LastDeployedAt { get; init; }

    public DateTime? LastSeenAt { get; init; }

    public bool IsDeleted { get; init; }

    public string? GameTypeKey { get; init; }

    public string? GameTypeDisplayName { get; init; }

    public string? GameTypeThumbnailUrl { get; init; }

    public string? RevisionVersionTag { get; init; }

    public string? RevisionImageReference { get; init; }

    public List<GameServerResolvedPortDto> ResolvedPorts { get; init; } = [];
}

public sealed record GameServerDetailDto
{
    public int Id { get; init; }

    public string ServerId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public int GameTypeRevisionId { get; init; }

    public string ServiceName { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }

    public DateTime? LastDeployedAt { get; init; }

    public DateTime? LastSeenAt { get; init; }

    public bool IsDeleted { get; init; }

    public string? GameTypeKey { get; init; }

    public string? GameTypeDisplayName { get; init; }

    public string? GameTypeDescription { get; init; }

    public string? GameTypeThumbnailUrl { get; init; }

    public string? RevisionVersionTag { get; init; }

    public string? RevisionImageReference { get; init; }

    public List<GameServerSettingDto> Settings { get; init; } = [];

    public List<GameServerResolvedPortDto> ResolvedPorts { get; init; } = [];

    public List<GameServerResolvedVolumeDto> ResolvedVolumes { get; init; } = [];

    public List<GameServerResolvedWebHostDto> ResolvedWebHosts { get; init; } = [];

    public List<GameServerConfigurationOptionDto> DockerVolumeOptions { get; init; } = [];

    public List<GameServerConfigurationOptionDto> NetworkOptions { get; init; } = [];

    public List<GameServerValidationIssueDto> ConfigurationRules { get; init; } = [];
}

public sealed record GameServerSettingDto
{
    public int Id { get; init; }

    public string SettingKey { get; init; } = string.Empty;

    public string? Value { get; init; }
}

public sealed record GameServerResolvedPortDto
{
    public int ContainerPort { get; init; }

    public string Protocol { get; init; } = string.Empty;

    public bool AdvertisedPort { get; init; }

    public string? Description { get; init; }

    public int DisplayOrder { get; init; }
}

public sealed record GameServerResolvedVolumeDto
{
    public string Usage { get; init; } = string.Empty;

    public string VolumeName { get; init; } = string.Empty;

    public string ContainerPath { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public string MountType { get; init; } = "volume";

    public bool ReadOnly { get; init; }

    public string Driver { get; init; } = "local";

    public string? DriverOptionsJson { get; init; }

    public int? OwnerUid { get; init; }

    public int? OwnerGid { get; init; }

    public string? Permissions { get; init; }

    public bool EnsureNfsPathExists { get; init; }

    public bool IsProvisioned { get; init; }

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

public sealed record GameServerResolvedWebHostDto
{
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? PathSegment { get; init; }

    public int? ContainerPort { get; init; }

    public string? ContainerPortVariable { get; init; }

    public string? EnabledWhen { get; init; }

    public int DisplayOrder { get; init; }
}

public sealed record GameServerConfigurationOptionDto
{
    public string Key { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool Required { get; init; }

    public string? Value { get; init; }
}

public sealed record GameServerValidationIssueDto
{
    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string Scope { get; init; } = string.Empty;

    public string Severity { get; init; } = string.Empty;

    public bool IsBlocking { get; init; }
}
