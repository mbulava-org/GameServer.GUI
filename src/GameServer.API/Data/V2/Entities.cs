using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GameServer.API.Models.V2;

namespace GameServer.API.Data.V2;

public class GameTypeEntity
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = "docker";

    [MaxLength(500)]
    public string? ThumbnailUrl { get; set; }

    [MaxLength(500)]
    public string? DocumentationUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public int? CurrentRevisionId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<GameTypeRevisionEntity> Revisions { get; set; } = new List<GameTypeRevisionEntity>();
}

public class GameTypeRevisionEntity
{
    public int Id { get; set; }

    public int GameTypeId { get; set; }

    [Required]
    [MaxLength(100)]
    public string VersionTag { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string ImageReference { get; set; } = string.Empty;

    [MaxLength(250)]
    public string? ImageDigest { get; set; }

    public bool EnableTTY { get; set; }

    public string? Notes { get; set; }

    public bool IsPublished { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(GameTypeId))]
    public virtual GameTypeEntity GameType { get; set; } = null!;

    public virtual ICollection<GameTypePortEntity> Ports { get; set; } = new List<GameTypePortEntity>();

    public virtual ICollection<GameTypeVolumeEntity> Volumes { get; set; } = new List<GameTypeVolumeEntity>();

    public virtual ICollection<GameTypeSettingDefinitionEntity> SettingDefinitions { get; set; } = new List<GameTypeSettingDefinitionEntity>();

    public virtual ICollection<GameTypeWebHostEntity> WebHosts { get; set; } = new List<GameTypeWebHostEntity>();

    public virtual ICollection<GameServerEntity> Servers { get; set; } = new List<GameServerEntity>();
}

public class GameTypePortEntity
{
    public int Id { get; set; }

    public int GameTypeRevisionId { get; set; }

    public int ContainerPort { get; set; }

    [Required]
    [MaxLength(10)]
    public string Protocol { get; set; } = "tcp";

    public bool AdvertisedPort { get; set; }

    public string? Description { get; set; }

    public int DisplayOrder { get; set; }

    [ForeignKey(nameof(GameTypeRevisionId))]
    public virtual GameTypeRevisionEntity GameTypeRevision { get; set; } = null!;
}

public class GameTypeVolumeEntity
{
    public int Id { get; set; }

    public int GameTypeRevisionId { get; set; }

    [Required]
    [MaxLength(500)]
    public string Source { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int DisplayOrder { get; set; }

    [Required]
    [MaxLength(100)]
    public string Usage { get; set; } = string.Empty;

    /// <summary>
    /// Mount-type code referencing MountTypeConfig.Key.
    /// Not enforced as a database FK so existing data continues to load.
    /// </summary>
    [MaxLength(50)]
    public string? MountType { get; set; } = "volume";

    public bool ReadOnly { get; set; }

    public int? OwnerUid { get; set; }

    public int? OwnerGid { get; set; }

    /// <summary>
    /// Optional revision setting key whose numeric value resolves the owner UID at deploy time.
    /// </summary>
    [MaxLength(200)]
    public string? OwnerUidVariable { get; set; }

    /// <summary>
    /// Optional revision setting key whose numeric value resolves the owner GID at deploy time.
    /// </summary>
    [MaxLength(200)]
    public string? OwnerGidVariable { get; set; }

    [MaxLength(10)]
    public string? Permissions { get; set; }

    /// <summary>
    /// When true, the NFS target directory is ensured to exist (and default permissions
    /// pre-applied where provided) before the container starts. Only meaningful for nfs mounts.
    /// </summary>
    public bool EnsureNfsPathExists { get; set; }

    public bool Required { get; set; } = true;

    [ForeignKey(nameof(GameTypeRevisionId))]
    public virtual GameTypeRevisionEntity GameTypeRevision { get; set; } = null!;
}

public class GameTypeSettingDefinitionEntity
{
    public int Id { get; set; }

    public int GameTypeRevisionId { get; set; }

    [Required]
    [MaxLength(200)]
    public string SettingKey { get; set; } = string.Empty;

    public string? DefaultValue { get; set; }

    public string? Description { get; set; }

    public int DisplayOrder { get; set; }

    [ForeignKey(nameof(GameTypeRevisionId))]
    public virtual GameTypeRevisionEntity GameTypeRevision { get; set; } = null!;

    public virtual GameTypeSettingMetadataEntity? Metadata { get; set; }
}

public class GameTypeSettingMetadataEntity
{
    public int Id { get; set; }

    public int GameTypeSettingDefinitionId { get; set; }

    [MaxLength(50)]
    public string? DataType { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    public bool IsRequired { get; set; }

    public bool CannotBeEmpty { get; set; }

    public string? Placeholder { get; set; }

    public string? ValidationPattern { get; set; }

    public string? ValidationMessage { get; set; }

    public bool AutoAllocatePort { get; set; }

    public bool ValidateRelatedPortsAvailability { get; set; } = true;

    public string? AllowedValuesJson { get; set; }

    public string? ValueMappingsJson { get; set; }

    [ForeignKey(nameof(GameTypeSettingDefinitionId))]
    public virtual GameTypeSettingDefinitionEntity GameTypeSettingDefinition { get; set; } = null!;

    public virtual ICollection<GameTypeSettingPortMappingEntity> PortMappings { get; set; } = new List<GameTypeSettingPortMappingEntity>();
}

public class GameTypeSettingPortMappingEntity
{
    public int Id { get; set; }

    public int GameTypeSettingMetadataId { get; set; }

    public GameTypeSettingPortMappingRole MappingRole { get; set; } = GameTypeSettingPortMappingRole.Primary;

    public GameTypeSettingPortRelationType RelationType { get; set; } = GameTypeSettingPortRelationType.Direct;

    public int TargetContainerPort { get; set; }

    [Required]
    [MaxLength(10)]
    public string TargetProtocol { get; set; } = "udp";

    public int? CalculationValue { get; set; }

    public bool IsRequired { get; set; } = true;

    public int DisplayOrder { get; set; }

    [ForeignKey(nameof(GameTypeSettingMetadataId))]
    public virtual GameTypeSettingMetadataEntity GameTypeSettingMetadata { get; set; } = null!;
}

public class GameTypeWebHostEntity
{
    public int Id { get; set; }

    public int GameTypeRevisionId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [MaxLength(200)]
    public string? PathSegment { get; set; }

    public int? ContainerPort { get; set; }

    [MaxLength(200)]
    public string? ContainerPortVariable { get; set; }

    [MaxLength(500)]
    public string? EnabledWhen { get; set; }

    public int DisplayOrder { get; set; }

    [ForeignKey(nameof(GameTypeRevisionId))]
    public virtual GameTypeRevisionEntity GameTypeRevision { get; set; } = null!;
}

public class GameServerEntity
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string ServerId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int GameTypeRevisionId { get; set; }

    [Required]
    [MaxLength(200)]
    public string ServiceName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastDeployedAt { get; set; }

    public DateTime? LastSeenAt { get; set; }

    public bool IsDeleted { get; set; }

    [ForeignKey(nameof(GameTypeRevisionId))]
    public virtual GameTypeRevisionEntity GameTypeRevision { get; set; } = null!;

    public virtual ICollection<GameServerSettingEntity> Settings { get; set; } = new List<GameServerSettingEntity>();

    public virtual ICollection<GameServerVolumeEntity> Volumes { get; set; } = new List<GameServerVolumeEntity>();

    public virtual ICollection<GameServerPortEntity> Ports { get; set; } = new List<GameServerPortEntity>();
}

public class GameServerVolumeEntity
{
    public int Id { get; set; }

    public int GameServerId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Usage { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string ContainerPath { get; set; } = string.Empty;

    /// <summary>
    /// Calculated docker volume name (from the mount type's SourcePathTemplate). Also the
    /// {Source} value for the Docker mount and the folder name for provisioned backends.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string VolumeName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string MountType { get; set; } = "volume";

    public bool ReadOnly { get; set; }

    /// <summary>
    /// Fully resolved Docker driver options (type/o/device, etc.). Everything required to build
    /// the Docker mount is contained here; provisioning-only values are not persisted.
    /// </summary>
    public string? DriverOptionsJson { get; set; }

    public bool IsProvisioned { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(GameServerId))]
    public virtual GameServerEntity GameServer { get; set; } = null!;
}

public class GameServerSettingEntity
{
    public int Id { get; set; }

    public int GameServerId { get; set; }

    [Required]
    [MaxLength(200)]
    public string SettingKey { get; set; } = string.Empty;

    public string? Value { get; set; }

    [ForeignKey(nameof(GameServerId))]
    public virtual GameServerEntity GameServer { get; set; } = null!;
}

public class GameServerPortEntity
{
    public int Id { get; set; }

    public int GameServerId { get; set; }

    public int ContainerPort { get; set; }

    [Required]
    [MaxLength(10)]
    public string Protocol { get; set; } = "tcp";

    public int PublishedPort { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(GameServerId))]
    public virtual GameServerEntity GameServer { get; set; } = null!;
}

public class MountTypeConfigEntity
{
    [Key]
    [MaxLength(50)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// Template used to compute the name of the Docker named volume created for this mount type.
    /// Must resolve to a unique value per server/volume combination.
    /// </summary>
    [MaxLength(500)]
    public string? VolumeNameFormat { get; set; }

    /// <summary>
    /// JSON-serialized free-form key/value options for this mount type.
    /// </summary>
    public string? OptionsJson { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
