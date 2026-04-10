namespace GameServer.Web.Models.V2;

public sealed record SaveGameTypeRevisionRequest
{
    public string VersionTag { get; init; } = string.Empty;

    public string ImageReference { get; init; } = string.Empty;

    public string? ImageDigest { get; init; }

    public bool EnableTTY { get; init; }

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
