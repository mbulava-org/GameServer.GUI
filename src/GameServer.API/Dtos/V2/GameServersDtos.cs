namespace GameServer.API.Dtos.V2;

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

    public int? CreatedByUserId { get; init; }

    public string? CreatedByUsername { get; init; }

    public string? GameTypeKey { get; init; }

    public string? GameTypeDisplayName { get; init; }

    public string? GameTypeThumbnailUrl { get; init; }

    public string? RevisionVersionTag { get; init; }

    public string? RevisionImageReference { get; init; }

    public List<GameServerResolvedPortDto> ResolvedPorts { get; init; } = [];

    public List<GameServerPortDto> Ports { get; init; } = [];

    /// <summary>
    /// Running containers for this server (when available).
    /// </summary>
    public List<GameServerContainerDto> Containers { get; init; } = [];
}

public sealed record GameServerContainerDto
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;
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

    public int? CreatedByUserId { get; init; }

    public string? CreatedByUsername { get; init; }

    public string? GameTypeKey { get; init; }

    public string? GameTypeDisplayName { get; init; }

    public string? GameTypeDescription { get; init; }

    public string? GameTypeThumbnailUrl { get; init; }

    public string? RevisionVersionTag { get; init; }

    public string? RevisionImageReference { get; init; }

    public List<GameServerSettingDto> Settings { get; init; } = [];

    public List<GameServerPortDto> Ports { get; init; } = [];

    public List<GameServerResolvedPortDto> ResolvedPorts { get; init; } = [];

    public List<GameServerResolvedVolumeDto> ResolvedVolumes { get; init; } = [];

    public List<GameServerResolvedWebHostDto> ResolvedWebHosts { get; init; } = [];

    public List<GameServerConfigurationOptionDto> DockerVolumeOptions { get; init; } = [];

    public List<GameServerConfigurationOptionDto> NetworkOptions { get; init; } = [];

    public List<GameServerValidationIssueDto> ConfigurationRules { get; init; } = [];

    /// <summary>
    /// Raw JSON descriptor list from the server's active revision declaring which
    /// GUI-side Blazor extension components should be attached as tabs.
    /// </summary>
    public string? UiExtensionsJson { get; init; }

    /// <summary>
    /// Typed view of <see cref="UiExtensionsJson"/> parsed server-side. The GUI
    /// still enforces its assembly whitelist before rendering.
    /// </summary>
    public List<GameTypeUiExtensionDescriptorDto> UiExtensions { get; init; } = [];
}

public sealed record GameServerSettingDto
{
    public int Id { get; init; }

    public string SettingKey { get; init; } = string.Empty;

    public string? Value { get; init; }

    public string? AccessPolicy { get; init; } = "Group"; // Group, Individual

    public List<int>? AllowedUserIds { get; init; } = [];

    public bool IsMasked { get; init; } = false;
}

public sealed record GameServerResolvedPortDto
{
    public int ContainerPort { get; init; }

    public string Protocol { get; init; } = string.Empty;

    public int PublishedPort { get; init; }

    public bool AdvertisedPort { get; init; }

    public string? Description { get; init; }

    public int DisplayOrder { get; init; }
}

public sealed record GameServerResolvedVolumeDto
{
    public string Usage { get; init; } = string.Empty;

    public string VolumeName { get; init; } = string.Empty;

    public string ContainerPath { get; init; } = string.Empty;

    public string MountType { get; init; } = "volume";

    public bool ReadOnly { get; init; }

    public string? DriverOptionsJson { get; init; }

    public int? OwnerUid { get; init; }

    public int? OwnerGid { get; init; }

    public string? Permissions { get; init; }

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

public sealed record GameServerInstanceInfoDto
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
