using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GameServer.Docker.Data
{
    /// <summary>
    /// Database entity for GameType
    /// </summary>
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
        [MaxLength(500)]
        public string Image { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? ThumbnailUrl { get; set; }

        [MaxLength(500)]
        public string? DocumentationUrl { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<PortEntity> Ports { get; set; } = new List<PortEntity>();
        public virtual ICollection<VolumeEntity> Volumes { get; set; } = new List<VolumeEntity>();
        public virtual ICollection<DefaultSettingEntity> DefaultSettings { get; set; } = new List<DefaultSettingEntity>();
        public virtual ExtendedMetadataEntity? ExtendedMetadata { get; set; }
    }

    /// <summary>
    /// Database entity for Port
    /// </summary>
    public class PortEntity
    {
        public int Id { get; set; }
        public int GameTypeId { get; set; }
        public int Port { get; set; }

        [Required]
        [MaxLength(10)]
        public string Protocol { get; set; } = "tcp";

        public bool IsDefaultPort { get; set; } = false;
        public string? Description { get; set; }
        public int DisplayOrder { get; set; } = 0;

        // Navigation property
        [ForeignKey(nameof(GameTypeId))]
        public virtual GameTypeEntity GameType { get; set; } = null!;
    }

    /// <summary>
    /// Database entity for Volume
    /// </summary>
    public class VolumeEntity
    {
        public int Id { get; set; }
        public int GameTypeId { get; set; }

        [Required]
        [MaxLength(500)]
        public string Source { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string Target { get; set; } = string.Empty;

        public bool ReadOnly { get; set; } = false;
        public string? Description { get; set; }
        public int DisplayOrder { get; set; } = 0;

        // Navigation property
        [ForeignKey(nameof(GameTypeId))]
        public virtual GameTypeEntity GameType { get; set; } = null!;
    }

    /// <summary>
    /// Database entity for DefaultSetting
    /// </summary>
    public class DefaultSettingEntity
    {
        public int Id { get; set; }
        public int GameTypeId { get; set; }

        [Required]
        [MaxLength(200)]
        public string SettingKey { get; set; } = string.Empty;

        public string? SettingValue { get; set; }
        public string? Description { get; set; }
        public int DisplayOrder { get; set; } = 0;

        // Navigation properties
        [ForeignKey(nameof(GameTypeId))]
        public virtual GameTypeEntity GameType { get; set; } = null!;
        
        // Optional 1:1 relationship with SettingsMetadata
        public virtual SettingMetadataEntity? SettingsMetadata { get; set; }
    }

    /// <summary>
    /// Database entity for ExtendedMetadata
    /// </summary>
    public class ExtendedMetadataEntity
    {
        public int Id { get; set; }
        public int GameTypeId { get; set; }
        public bool EnableTTY { get; set; } = false;
        public string? CustomPropertiesJson { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        [ForeignKey(nameof(GameTypeId))]
        public virtual GameTypeEntity GameType { get; set; } = null!;
        
        // Note: SettingsMetadata now belongs to DefaultSettings, not ExtendedMetadata
    }

    /// <summary>
    /// Database entity for SettingMetadata
    /// </summary>
    public class SettingMetadataEntity
    {
        public int Id { get; set; }
        public int DefaultSettingId { get; set; }  // References the DefaultSetting this metadata describes

        public string? Description { get; set; }  // UI description (can override DefaultSetting.Description)
        public bool IsRequired { get; set; } = false;
        public bool CannotBeEmpty { get; set; } = false;

        [MaxLength(50)]
        public string? DataType { get; set; }

        [MaxLength(100)]
        public string? Category { get; set; }

        public int DisplayOrder { get; set; } = 0;
        public string? Placeholder { get; set; }
        public string? ValidationPattern { get; set; }
        public string? ValidationMessage { get; set; }

        // Port-specific fields
        public bool MapsToContainerPort { get; set; } = false;
        public int? LinkedContainerPort { get; set; }

        [MaxLength(10)]
        public string PortProtocol { get; set; } = "tcp";

        public string? SynchronizedWithSetting { get; set; }
        public bool AutoAllocatePort { get; set; } = false;
        public bool ValidateRelatedPortsAvailability { get; set; } = true;

        // List-specific fields
        [MaxLength(10)]
        public string ListDelimiter { get; set; } = ",";

        // Enum-specific fields (stored as JSON)
        public string? AllowedValuesJson { get; set; }
        public string? ValueMappingsJson { get; set; }

        // Navigation properties
        [ForeignKey(nameof(DefaultSettingId))]
        public virtual DefaultSettingEntity DefaultSetting { get; set; } = null!;
        public virtual PortValidationEntity? PortValidation { get; set; }
        public virtual ICollection<PortRelationshipEntity> PortRelationships { get; set; } = new List<PortRelationshipEntity>();
    }

    /// <summary>
    /// Database entity for PortValidation
    /// </summary>
    public class PortValidationEntity
    {
        public int Id { get; set; }
        public int SettingMetadataId { get; set; }
        public int MinPort { get; set; } = 1024;
        public int MaxPort { get; set; } = 65535;
        public string? ReservedPortsJson { get; set; }
        public bool CheckAvailability { get; set; } = true;
        public bool IsUserEditable { get; set; } = true;
        public string? SuggestedPortsJson { get; set; }
        public string? ValidationMessage { get; set; }

        // Navigation property
        [ForeignKey(nameof(SettingMetadataId))]
        public virtual SettingMetadataEntity SettingMetadata { get; set; } = null!;
    }

    /// <summary>
    /// Database entity for PortRelationship
    /// </summary>
    public class PortRelationshipEntity
    {
        public int Id { get; set; }
        public int SettingMetadataId { get; set; }
        public int RelationType { get; set; } // 0=Offset, 1=Fixed, 2=Multiplier
        public int TargetContainerPort { get; set; }

        [MaxLength(10)]
        public string TargetProtocol { get; set; } = "udp";

        public int OffsetValue { get; set; } = 0;
        public int? FixedValue { get; set; }
        public string? Description { get; set; }
        public bool IsRequired { get; set; } = true;
        public int DisplayOrder { get; set; } = 0;

        // Navigation property
        [ForeignKey(nameof(SettingMetadataId))]
        public virtual SettingMetadataEntity SettingMetadata { get; set; } = null!;
    }
}
