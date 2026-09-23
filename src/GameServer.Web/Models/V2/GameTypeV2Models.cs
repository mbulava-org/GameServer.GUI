namespace GameServer.Web.Models.V2;

public sealed record GameTypeListItem
{
    public int Id { get; init; }

    public string Key { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string Type { get; init; } = "docker";

    public string? CurrentImageReference { get; init; }

    public string? ThumbnailUrl { get; init; }

    public bool IsActive { get; init; }

    public int? CurrentRevisionId { get; init; }

    public string? CurrentVersionTag { get; init; }

    public int RevisionCount { get; init; }

    public int PublishedRevisionCount { get; init; }

    public DateTime UpdatedAt { get; init; }
}

public sealed record GameTypeDetail
{
    public int Id { get; init; }

    public string Key { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string Type { get; init; } = "docker";

    public string? ThumbnailUrl { get; init; }

    public string? DocumentationUrl { get; init; }

    public bool IsActive { get; init; }

    public int? CurrentRevisionId { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }

    public List<GameTypeRevision> Revisions { get; init; } = [];
}

public sealed record GameTypeRevision
{
    public int Id { get; init; }

    public string VersionTag { get; init; } = string.Empty;

    public string ImageReference { get; init; } = string.Empty;

    public string? ImageDigest { get; init; }

    public bool EnableTTY { get; init; }

    public string? ReadyLogPattern { get; init; }

    public string? Notes { get; init; }

    public bool IsPublished { get; init; }

    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Raw JSON descriptor list declaring which GUI-side Blazor extension
    /// components should be attached as extra tabs for servers running this revision.
    /// </summary>
    public string? UiExtensionsJson { get; init; }

    /// <summary>
    /// Typed view of <see cref="UiExtensionsJson"/> as parsed by the API.
    /// </summary>
    public List<GameTypeUiExtensionDescriptor> UiExtensions { get; init; } = [];

    /// <summary>
    /// Raw JSON representation of CPU, memory reservations/limits, pids limits,
    /// and placement constraints attached to this revision.
    /// </summary>
    public string? ResourcesJson { get; init; }

    /// <summary>
    /// Typed view of <see cref="ResourcesJson"/> as parsed by the API.
    /// </summary>
    public GameTypeRevisionResources? Resources { get; init; }

    /// <summary>
    /// Raw JSON representation of container healthcheck configuration (start_period, interval, timeout, retries, test command).
    /// </summary>
    public string? HealthcheckJson { get; init; }

    /// <summary>
    /// Typed view of <see cref="HealthcheckJson"/> as parsed by the API.
    /// </summary>
    public GameTypeRevisionHealthcheck? Healthcheck { get; init; }

    public List<GameTypePort> Ports { get; init; } = [];

    public List<GameTypeVolume> Volumes { get; init; } = [];

    public List<GameTypeSettingDefinition> SettingDefinitions { get; init; } = [];

    public List<GameTypeWebHost> WebHosts { get; init; } = [];
}

public sealed record GameTypePort
{
    public int Id { get; init; }

    public int ContainerPort { get; init; }

    public string Protocol { get; init; } = string.Empty;

    public bool AdvertisedPort { get; init; }

    public string? Description { get; init; }

    public int DisplayOrder { get; init; }
}

public sealed record GameTypeVolume
{
    public int Id { get; init; }

    public string Source { get; init; } = string.Empty;

    public string? Description { get; init; }

    public int DisplayOrder { get; init; }

    public string Usage { get; init; } = string.Empty;

    public string MountType { get; init; } = "volume";

    public bool ReadOnly { get; init; }

    public int? OwnerUid { get; init; }

    public int? OwnerGid { get; init; }

    public string? OwnerUidVariable { get; init; }

    public string? OwnerGidVariable { get; init; }

    public string? Permissions { get; init; }

    public bool EnsureNfsPathExists { get; init; }

    public bool Required { get; init; } = true;
}

public sealed record GameTypeSettingDefinition
{
    public int Id { get; init; }

    public string SettingKey { get; init; } = string.Empty;

    public string? DefaultValue { get; init; }

    public string? Description { get; init; }

    public int DisplayOrder { get; init; }

    public GameTypeSettingMetadata? Metadata { get; init; }
}

public sealed record GameTypeSettingMetadata
{
    public int Id { get; init; }

    public string? DataType { get; init; }

    public string? Category { get; init; }

    public bool IsRequired { get; init; }

    public bool CannotBeEmpty { get; init; }

    public string? Placeholder { get; init; }

    public string? ValidationPattern { get; init; }

    public string? ValidationMessage { get; init; }

    public bool AutoAllocatePort { get; init; }

    public bool ValidateRelatedPortsAvailability { get; init; }

    public string? AllowedValuesJson { get; init; }

    public string? ValueMappingsJson { get; init; }

    public List<GameTypeSettingPortMapping> PortMappings { get; init; } = [];
}

public sealed record GameTypeSettingPortMapping
{
    public int Id { get; init; }

    public string MappingRole { get; init; } = string.Empty;

    public string RelationType { get; init; } = string.Empty;

    public int TargetContainerPort { get; init; }

    public string TargetProtocol { get; init; } = string.Empty;

    public int? CalculationValue { get; init; }

    public bool IsRequired { get; init; }

    public int DisplayOrder { get; init; }
}

public enum GameTypeSettingPortMappingRole
{
    Primary = 0,
    Related = 1
}

public enum GameTypeSettingPortRelationType
{
    Direct = 0,
    Offset = 1,
    Fixed = 2,
    Multiplier = 3
}

public sealed record GameTypeWebHost
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? PathSegment { get; init; }

    public int? ContainerPort { get; init; }

    public string? ContainerPortVariable { get; init; }

    public string? EnabledWhen { get; init; }

    public int DisplayOrder { get; init; }
}

public sealed record SaveGameTypeRequest
{
    public string Key { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string Type { get; init; } = "docker";

    public string? ThumbnailUrl { get; init; }

    public string? DocumentationUrl { get; init; }

    public bool IsActive { get; init; } = true;
}

public sealed record GameTypeRevisionResources
{
    public decimal? CpuReservationCores { get; init; }

    public decimal? CpuLimitCores { get; init; }

    public long? MemoryReservationBytes { get; init; }

    public long? MemoryLimitBytes { get; init; }

    public string? MemoryReservationVariable { get; init; }

    public string? MemoryLimitVariable { get; init; }

    public long? PidsLimit { get; init; }

    public ulong? MaxReplicasPerNode { get; init; }

    public List<PlacementConstraint> Constraints { get; init; } = [];

    public List<PlacementPreference> Preferences { get; init; } = [];
}

public sealed record PlacementConstraint
{
    public string Target { get; init; } = string.Empty;

    public string Operator { get; init; } = "==";

    public string Value { get; init; } = string.Empty;
}

public sealed record PlacementPreference
{
    public string Strategy { get; init; } = "spread";

    public string Descriptor { get; init; } = string.Empty;
}

public sealed record GameTypeRevisionHealthcheck
{
    public bool Disable { get; init; }

    public string? TestType { get; init; }

    public string? TestCommand { get; init; }

    public int? IntervalSeconds { get; init; }

    public int? TimeoutSeconds { get; init; }

    public int? StartPeriodSeconds { get; init; }

    public int? StartIntervalSeconds { get; init; }

    public long? Retries { get; init; }
}
