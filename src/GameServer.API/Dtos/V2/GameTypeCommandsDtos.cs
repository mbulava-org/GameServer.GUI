namespace GameServer.API.Dtos.V2;

public sealed record SaveGameTypeRequestDto
{
    public string Key { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string Type { get; init; } = "docker";

    public string? ThumbnailUrl { get; init; }

    public string? DocumentationUrl { get; init; }

    public bool IsActive { get; init; } = true;
}

public sealed record SaveGameTypeRevisionRequestDto
{
    public string VersionTag { get; init; } = string.Empty;

    public string ImageReference { get; init; } = string.Empty;

    public string? ImageDigest { get; init; }

    public bool EnableTTY { get; init; }

    public string? Notes { get; init; }

    public bool IsPublished { get; init; }

    public List<GameTypePortDto> Ports { get; init; } = [];

    public List<GameTypeVolumeDto> Volumes { get; init; } = [];

    public List<GameTypeSettingDefinitionDto> SettingDefinitions { get; init; } = [];

    public List<GameTypeWebHostDto> WebHosts { get; init; } = [];
}

public sealed record PublishRevisionRequestDto
{
    public bool SetAsCurrentRevision { get; init; }
}

public sealed record DetectGameTypeSetupRequestDto
{
    public string ImageReference { get; init; } = string.Empty;

    public string? VersionTag { get; init; }
}

public sealed record CompareGameTypeSetupRequestDto
{
    public string ImageReference { get; init; } = string.Empty;

    public string? VersionTag { get; init; }

    public int RevisionId { get; init; }
}

public sealed record GameTypeSetupDetectionResultDto
{
    public string ImageReference { get; init; } = string.Empty;

    public string VersionTag { get; init; } = string.Empty;

    public string? ImageDigest { get; init; }

    public List<DetectedPortDto> Ports { get; init; } = [];

    public List<DetectedSettingDto> Settings { get; init; } = [];

    public List<DetectedVolumeDto> Volumes { get; init; } = [];
}

public sealed record GameTypeSetupComparisonResultDto
{
    public GameTypeSetupDetectionResultDto Detection { get; init; } = new();

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

    public List<ChangedSettingDto> ChangedSettings { get; init; } = [];
}

public sealed record ChangedSettingDto
{
    public string Key { get; init; } = string.Empty;

    public string? RevisionValue { get; init; }

    public string? DetectedValue { get; init; }
}

public sealed record DetectedPortDto
{
    public int ContainerPort { get; init; }

    public string Protocol { get; init; } = string.Empty;
}

public sealed record DetectedSettingDto
{
    public string Key { get; init; } = string.Empty;

    public string? DefaultValue { get; init; }

    public List<DetectedSettingPortMappingDto> PortMappings { get; init; } = [];
}

public sealed record DetectedSettingPortMappingDto
{
    public string MappingRole { get; init; } = string.Empty;

    public string RelationType { get; init; } = string.Empty;

    public int TargetContainerPort { get; init; }

    public string TargetProtocol { get; init; } = string.Empty;

    public int? CalculationValue { get; init; }

    public string? Description { get; init; }

    public bool IsRequired { get; init; }
}

public sealed record DetectedVolumeDto
{
    public string ContainerPath { get; init; } = string.Empty;
}
