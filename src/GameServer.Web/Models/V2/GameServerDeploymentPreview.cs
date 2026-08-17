namespace GameServer.Web.Models.V2;

/// <summary>
/// Web-side mirror of the API's deployment preview payload.
/// </summary>
public sealed record GameServerDeploymentPreview
{
    public string ServiceName { get; init; } = string.Empty;

    public string ServerId { get; init; } = string.Empty;

    public string GameTypeKey { get; init; } = string.Empty;

    public string ImageReference { get; init; } = string.Empty;

    public string VersionTag { get; init; } = string.Empty;

    public bool EnableTTY { get; init; }

    public string VolumeBindingLayout { get; init; } = string.Empty;

    public Dictionary<string, string> Labels { get; init; } = [];

    public List<GameServerPreviewNetwork> Networks { get; init; } = [];

    public List<GameServerPreviewEnvironmentVariable> EnvironmentVariables { get; init; } = [];

    public List<GameServerPreviewPort> Ports { get; init; } = [];

    public List<GameServerPreviewVolume> Volumes { get; init; } = [];

    public List<GameServerValidationIssue> Issues { get; init; } = [];

    public List<string> Notices { get; init; } = [];

    public string RawServiceSpecJson { get; init; } = string.Empty;

    /// <summary>
    /// Preview list of containers that would be created for the service. Used by the UI to pick a terminal target.
    /// </summary>
    public List<GameServerPreviewContainer> Containers { get; init; } = [];
}

public sealed record GameServerPreviewNetwork
{
    public string Name { get; init; } = string.Empty;

    public string Driver { get; init; } = string.Empty;

    public string? Description { get; init; }
}

public sealed record GameServerPreviewContainer
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;
}

public sealed record GameServerPreviewEnvironmentVariable
{
    public string Key { get; init; } = string.Empty;

    public string? Value { get; init; }

    public string? RawValue { get; init; }

    public string? DataType { get; init; }

    public string? Category { get; init; }

    public bool IsExpanded { get; init; }

    public bool UsesDefault { get; init; }
}

public sealed record GameServerPreviewPort
{
    public int ContainerPort { get; init; }

    public int PublishedPort { get; init; }

    public string Protocol { get; init; } = string.Empty;

    public bool Published { get; init; }

    public string PublishMode { get; init; } = string.Empty;

    public string? Description { get; init; }
}

public sealed record GameServerPreviewVolume
{
    public string Usage { get; init; } = string.Empty;

    public string VolumeName { get; init; } = string.Empty;

    public string ContainerPath { get; init; } = string.Empty;

    public string MountType { get; init; } = string.Empty;

    public bool ReadOnly { get; init; }

    public string? DriverOptionsJson { get; init; }

    public int? OwnerUid { get; init; }

    public int? OwnerGid { get; init; }

    public string? Permissions { get; init; }
}
