namespace GameServer.Docker.Dtos.V2;

/// <summary>
/// Dry-run description of the Swarm service that would be created for a V2 GameServer.
/// </summary>
public sealed record GameServerDeploymentPreviewDto
{
    public string ServiceName { get; init; } = string.Empty;

    public string ServerId { get; init; } = string.Empty;

    public string GameTypeKey { get; init; } = string.Empty;

    public string ImageReference { get; init; } = string.Empty;

    public string VersionTag { get; init; } = string.Empty;

    public bool EnableTTY { get; init; }

    public string VolumeBindingLayout { get; init; } = string.Empty;

    public Dictionary<string, string> Labels { get; init; } = [];

    public List<GameServerPreviewNetworkDto> Networks { get; init; } = [];

    public List<GameServerPreviewEnvironmentVariableDto> EnvironmentVariables { get; init; } = [];

    public List<GameServerPreviewPortDto> Ports { get; init; } = [];

    public List<GameServerPreviewVolumeDto> Volumes { get; init; } = [];

    public List<GameServerValidationIssueDto> Issues { get; init; } = [];

    /// <summary>
    /// Non-blocking informational messages explaining gaps in the generated spec.
    /// </summary>
    public List<string> Notices { get; init; } = [];

    /// <summary>
    /// Indented JSON of the exact <c>ServiceCreateParameters</c> that would be sent to Docker.
    /// </summary>
    public string RawServiceSpecJson { get; init; } = string.Empty;
}

public sealed record GameServerPreviewNetworkDto
{
    public string Name { get; init; } = string.Empty;

    public string Driver { get; init; } = string.Empty;

    public string? Description { get; init; }
}

public sealed record GameServerPreviewEnvironmentVariableDto
{
    public string Key { get; init; } = string.Empty;

    /// <summary>Value after all calculation and variable expansion.</summary>
    public string? Value { get; init; }

    /// <summary>Value as stored, before server-variable expansion.</summary>
    public string? RawValue { get; init; }

    public string? DataType { get; init; }

    public string? Category { get; init; }

    /// <summary>True when server-variable token expansion changed the value.</summary>
    public bool IsExpanded { get; init; }

    /// <summary>True when the effective value came from the revision default.</summary>
    public bool UsesDefault { get; init; }
}

public sealed record GameServerPreviewPortDto
{
    public int ContainerPort { get; init; }

    public int PublishedPort { get; init; }

    public string Protocol { get; init; } = string.Empty;

    public bool Published { get; init; }

    public string PublishMode { get; init; } = string.Empty;

    public string? Description { get; init; }
}

public sealed record GameServerPreviewVolumeDto
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
