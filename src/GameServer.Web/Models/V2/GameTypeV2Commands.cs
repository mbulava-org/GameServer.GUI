namespace GameServer.Web.Models.V2;

public sealed record SaveGameTypeRevisionRequest
{
    public string VersionTag { get; init; } = string.Empty;

    public string ImageReference { get; init; } = string.Empty;

    public string? ImageDigest { get; init; }

    public bool EnableTTY { get; init; }

    public string? ReadyLogPattern { get; init; }

    public string? Notes { get; init; }

    public bool IsPublished { get; init; }

    public List<GameTypePort> Ports { get; init; } = [];

    public List<GameTypeVolume> Volumes { get; init; } = [];

    public List<GameTypeSettingDefinition> SettingDefinitions { get; init; } = [];

    public List<GameTypeWebHost> WebHosts { get; init; } = [];
}

public sealed record PublishRevisionRequest
{
    public bool SetAsCurrentRevision { get; init; }
}

public sealed record PortableGameTypePackage
{
    public string FormatVersion { get; init; } = "1.0";

    public PortableGameType GameType { get; init; } = new();
}

public sealed record PortableGameType
{
    public string Key { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string Type { get; init; } = "docker";

    public string? ThumbnailUrl { get; init; }

    public string? DocumentationUrl { get; init; }

    public bool IsActive { get; init; } = true;

    public string? CurrentRevisionVersionTag { get; init; }

    public List<PortableGameTypeRevision> Revisions { get; init; } = [];
}

public sealed record PortableGameTypeRevision
{
    public string VersionTag { get; init; } = string.Empty;

    public string ImageReference { get; init; } = string.Empty;

    public string? ImageDigest { get; init; }

    public bool EnableTTY { get; init; }

    public string? ReadyLogPattern { get; init; }

    public string? Notes { get; init; }

    public bool IsPublished { get; init; }

    public List<PortableGameTypePort> Ports { get; init; } = [];

    public List<PortableGameTypeVolume> Volumes { get; init; } = [];

    public List<PortableGameTypeSettingDefinition> SettingDefinitions { get; init; } = [];

    public List<PortableGameTypeWebHost> WebHosts { get; init; } = [];
}

public sealed record PortableGameTypePort
{
    public int ContainerPort { get; init; }

    public string Protocol { get; init; } = string.Empty;

    public bool AdvertisedPort { get; init; }

    public string? Description { get; init; }

    public int DisplayOrder { get; init; }
}

public sealed record PortableGameTypeVolume
{
    public string Source { get; init; } = string.Empty;

    public string? Description { get; init; }

    public int DisplayOrder { get; init; }

    public string Usage { get; init; } = string.Empty;
}

public sealed record PortableGameTypeSettingDefinition
{
    public string SettingKey { get; init; } = string.Empty;

    public string? DefaultValue { get; init; }

    public string? Description { get; init; }

    public int DisplayOrder { get; init; }

    public PortableGameTypeSettingMetadata? Metadata { get; init; }
}

public sealed record PortableGameTypeSettingMetadata
{
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

    public List<PortableGameTypeSettingPortMapping> PortMappings { get; init; } = [];
}

public sealed record PortableGameTypeSettingPortMapping
{
    public string MappingRole { get; init; } = string.Empty;

    public string RelationType { get; init; } = string.Empty;

    public int TargetContainerPort { get; init; }

    public string TargetProtocol { get; init; } = string.Empty;

    public int? CalculationValue { get; init; }

    public bool IsRequired { get; init; }

    public int DisplayOrder { get; init; }
}

public sealed record PortableGameTypeWebHost
{
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? PathSegment { get; init; }

    public int? ContainerPort { get; init; }

    public string? ContainerPortVariable { get; init; }

    public string? EnabledWhen { get; init; }

    public int DisplayOrder { get; init; }
}

public sealed record DetectGameTypeSetupRequest
{
    public string ImageReference { get; init; } = string.Empty;

    public string? VersionTag { get; init; }
}

public sealed record CompareGameTypeSetupRequest
{
    public string ImageReference { get; init; } = string.Empty;

    public string? VersionTag { get; init; }

    public int RevisionId { get; init; }
}

public sealed record GameTypeSetupDetectionResult
{
    public string ImageReference { get; init; } = string.Empty;

    public string VersionTag { get; init; } = string.Empty;

    public string? ImageDigest { get; init; }

    public List<DetectedPort> Ports { get; init; } = [];

    public List<DetectedSetting> Settings { get; init; } = [];

    public List<DetectedVolume> Volumes { get; init; } = [];
}

public sealed record GameTypeSetupComparisonResult
{
    public GameTypeSetupDetectionResult Detection { get; init; } = new();

    public int RevisionId { get; init; }

    public string RevisionVersionTag { get; init; } = string.Empty;

    public bool HasChanges { get; init; }

    public bool DigestChanged { get; init; }

    public List<string> AddedPorts { get; init; } = [];

    public List<string> RemovedPorts { get; init; } = [];

    public List<string> AddedVolumes { get; init; } = [];

    public List<string> RemovedVolumes { get; init; } = [];

    public List<string> AddedSettings { get; init; } = [];

    public List<string> RemovedSettings { get; init; } = [];

    public List<ChangedSetting> ChangedSettings { get; init; } = [];
}

public sealed record ChangedSetting
{
    public string Key { get; init; } = string.Empty;

    public string? RevisionValue { get; init; }

    public string? DetectedValue { get; init; }
}

public sealed record DetectedPort
{
    public int ContainerPort { get; init; }

    public string Protocol { get; init; } = string.Empty;
}

public sealed record DetectedSetting
{
    public string Key { get; init; } = string.Empty;

    public string? DefaultValue { get; init; }

    public List<DetectedSettingPortMapping> PortMappings { get; init; } = [];
}

public sealed record DetectedSettingPortMapping
{
    public string MappingRole { get; init; } = string.Empty;

    public string RelationType { get; init; } = string.Empty;

    public int TargetContainerPort { get; init; }

    public string TargetProtocol { get; init; } = string.Empty;

    public int? CalculationValue { get; init; }

    public bool IsRequired { get; init; }
}

public sealed record DetectedVolume
{
    public string ContainerPath { get; init; } = string.Empty;
}
