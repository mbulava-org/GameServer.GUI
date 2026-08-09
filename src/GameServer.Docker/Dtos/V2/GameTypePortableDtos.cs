namespace GameServer.Docker.Dtos.V2;

public sealed record PortableGameTypePackageDto
{
    public string FormatVersion { get; init; } = "1.0";

    public PortableGameTypeDto GameType { get; init; } = new();
}

public sealed record PortableGameTypeDto
{
    public string Key { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string Type { get; init; } = "docker";

    public string? ThumbnailUrl { get; init; }

    public string? DocumentationUrl { get; init; }

    public bool IsActive { get; init; } = true;

    public string? CurrentRevisionVersionTag { get; init; }

    public List<PortableGameTypeRevisionDto> Revisions { get; init; } = [];
}

public sealed record PortableGameTypeRevisionDto
{
    public string VersionTag { get; init; } = string.Empty;

    public string ImageReference { get; init; } = string.Empty;

    public string? ImageDigest { get; init; }

    public bool EnableTTY { get; init; }

    public string? Notes { get; init; }

    public bool IsPublished { get; init; }

    public List<PortableGameTypePortDto> Ports { get; init; } = [];

    public List<PortableGameTypeVolumeDto> Volumes { get; init; } = [];

    public List<PortableGameTypeSettingDefinitionDto> SettingDefinitions { get; init; } = [];

    public List<PortableGameTypeWebHostDto> WebHosts { get; init; } = [];
}

public sealed record PortableGameTypePortDto
{
    public int ContainerPort { get; init; }

    public string Protocol { get; init; } = string.Empty;

    public bool AdvertisedPort { get; init; }

    public string? Description { get; init; }

    public int DisplayOrder { get; init; }
}

public sealed record PortableGameTypeVolumeDto
{
    public string Source { get; init; } = string.Empty;

    public string? Description { get; init; }

    public int DisplayOrder { get; init; }

    public string Usage { get; init; } = string.Empty;

    public string MountType { get; init; } = "nfs";

    public bool ReadOnly { get; init; }

    public int? OwnerUid { get; init; }

    public int? OwnerGid { get; init; }

    public string? OwnerUidVariable { get; init; }

    public string? OwnerGidVariable { get; init; }

    public string? Permissions { get; init; }

    public bool EnsureNfsPathExists { get; init; }

    public bool Required { get; init; } = true;
}

public sealed record PortableGameTypeSettingDefinitionDto
{
    public string SettingKey { get; init; } = string.Empty;

    public string? DefaultValue { get; init; }

    public string? Description { get; init; }

    public int DisplayOrder { get; init; }

    public PortableGameTypeSettingMetadataDto? Metadata { get; init; }
}

public sealed record PortableGameTypeSettingMetadataDto
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

    public List<PortableGameTypeSettingPortMappingDto> PortMappings { get; init; } = [];
}

public sealed record PortableGameTypeSettingPortMappingDto
{
    public string MappingRole { get; init; } = string.Empty;

    public string RelationType { get; init; } = string.Empty;

    public int TargetContainerPort { get; init; }

    public string TargetProtocol { get; init; } = string.Empty;

    public int? CalculationValue { get; init; }

    public bool IsRequired { get; init; }

    public int DisplayOrder { get; init; }
}

public sealed record PortableGameTypeWebHostDto
{
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? PathSegment { get; init; }

    public int? ContainerPort { get; init; }

    public string? ContainerPortVariable { get; init; }

    public string? EnabledWhen { get; init; }

    public int DisplayOrder { get; init; }
}
