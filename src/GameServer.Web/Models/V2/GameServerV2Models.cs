namespace GameServer.Web.Models.V2;

public sealed record GameServerListItem
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

    public List<GameServerResolvedPort> ResolvedPorts { get; init; } = [];

    public List<GameServerPort> Ports { get; init; } = [];

    /// <summary>
    /// Running containers for this server (when available).
    /// </summary>
    public List<GameServerContainer> Containers { get; init; } = [];
}

public sealed record GameServerContainer
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;
}

public sealed record GameServerDetail
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

    public List<GameServerSetting> Settings { get; init; } = [];

    public List<GameServerPort> Ports { get; init; } = [];

    public List<GameServerResolvedPort> ResolvedPorts { get; init; } = [];

    public List<GameServerResolvedVolume> ResolvedVolumes { get; init; } = [];

    public List<GameServerResolvedWebHost> ResolvedWebHosts { get; init; } = [];

    public List<GameServerConfigurationOption> DockerVolumeOptions { get; init; } = [];

    public List<GameServerConfigurationOption> NetworkOptions { get; init; } = [];

    public List<GameServerValidationIssue> ConfigurationRules { get; init; } = [];

    /// <summary>
    /// Running containers for this server (when available).
    /// </summary>
    public List<GameServerContainer> Containers { get; init; } = [];
}

public sealed record GameServerSetting
{
    public int Id { get; init; }

    public string SettingKey { get; init; } = string.Empty;

    public string? Value { get; init; }
}

public sealed record GameServerResolvedPort
{
    public int ContainerPort { get; init; }

    public string Protocol { get; init; } = string.Empty;

    public int PublishedPort { get; init; }

    public bool AdvertisedPort { get; init; }

    public string? Description { get; init; }

    public int DisplayOrder { get; init; }
}

public sealed record GameServerResolvedVolume
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

    public string InitMode { get; init; } = "none";

    public string? SeedSourcePath { get; init; }

    public bool IsProvisioned { get; init; }

    public DateTime CreatedAt { get; init; }
}

public sealed record GameServerResolvedWebHost
{
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? PathSegment { get; init; }

    public int? ContainerPort { get; init; }

    public string? ContainerPortVariable { get; init; }

    public string? EnabledWhen { get; init; }

    public int DisplayOrder { get; init; }
}

public sealed record GameServerConfigurationOption
{
    public string Key { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool Required { get; init; }

    public string? Value { get; init; }
}

public sealed record GameServerValidationIssue
{
    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string Scope { get; init; } = string.Empty;

    public string Severity { get; init; } = string.Empty;

    public bool IsBlocking { get; init; }
}
