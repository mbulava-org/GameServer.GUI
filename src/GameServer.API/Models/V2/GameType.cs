namespace GameServer.API.Models.V2;

public sealed record GameType
{
    public int Id { get; init; }

    public string Key { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string Type { get; init; } = "docker";

    public string? ThumbnailUrl { get; init; }

    public string? DocumentationUrl { get; init; }

    public bool IsActive { get; init; } = true;

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

    public GameType? GameType { get; set; }

    public List<GameTypePort> Ports { get; init; } = [];

    public List<GameTypeVolume> Volumes { get; init; } = [];

    public List<GameTypeSettingDefinition> SettingDefinitions { get; init; } = [];

    public List<GameTypeWebHost> WebHosts { get; init; } = [];
}

public sealed record GameTypePort
{
    public int Id { get; init; }

    public int ContainerPort { get; init; }

    public string Protocol { get; init; } = "tcp";

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

    /// <summary>
    /// Mount-type code matching <see cref="MountTypeConfig.Key"/>.
    /// </summary>
    public string MountType { get; init; } = "volume";

    public bool ReadOnly { get; init; }

    public int? OwnerUid { get; init; }

    public int? OwnerGid { get; init; }

    /// <summary>
    /// Optional revision setting key whose numeric value resolves the owner UID at deploy time.
    /// </summary>
    public string? OwnerUidVariable { get; init; }

    /// <summary>
    /// Optional revision setting key whose numeric value resolves the owner GID at deploy time.
    /// </summary>
    public string? OwnerGidVariable { get; init; }

    public string? Permissions { get; init; }

    /// <summary>
    /// When true, the NFS target directory is ensured to exist (and default permissions
    /// pre-applied where provided) before the container starts. Only meaningful for nfs mounts.
    /// </summary>
    public bool EnsureNfsPathExists { get; init; }

    public bool Required { get; init; } = true;
}

public sealed record GameServerVolume
{
    public int Id { get; init; }

    public int GameServerId { get; init; }

    public string Usage { get; init; } = string.Empty;

    public string ContainerPath { get; init; } = string.Empty;

    /// <summary>
    /// Calculated docker volume name (from the mount type's SourcePathTemplate). Also the
    /// {Source} value for the Docker mount and the folder name for provisioned backends.
    /// </summary>
    public string VolumeName { get; init; } = string.Empty;

    /// <summary>
    /// Concrete mount-type code used when the volume was created. Stored as a snapshot
    /// so the exact configuration can be reconstructed even if the mount-type template changes.
    /// </summary>
    public string MountType { get; init; } = "volume";

    public bool ReadOnly { get; init; }

    /// <summary>
    /// Fully resolved Docker driver options (type/o/device, etc.) calculated by the mount-type
    /// provider at create time. Everything required to build the Docker mount is contained here;
    /// provisioning-only values are not persisted.
    /// </summary>
    public string? DriverOptionsJson { get; init; }

    public bool IsProvisioned { get; init; }

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
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

    public bool ValidateRelatedPortsAvailability { get; init; } = true;

    public string? AllowedValuesJson { get; init; }

    public string? ValueMappingsJson { get; init; }

    public List<GameTypeSettingPortMapping> PortMappings { get; init; } = [];
}

public sealed record GameTypeSettingPortMapping
{
    public int Id { get; init; }

    public GameTypeSettingPortMappingRole MappingRole { get; init; } = GameTypeSettingPortMappingRole.Primary;

    public GameTypeSettingPortRelationType RelationType { get; init; } = GameTypeSettingPortRelationType.Direct;

    public int TargetContainerPort { get; init; }

    public string TargetProtocol { get; init; } = "udp";

    public int? CalculationValue { get; init; }

    public bool IsRequired { get; init; } = true;

    public int DisplayOrder { get; init; }
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

public enum VolumeMountType
{
    Volume = 0,
    Bind = 1,
    Tmpfs = 2
}
