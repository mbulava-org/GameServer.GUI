namespace GameServer.API.Dtos.V2;

public sealed record SaveGameServerRequestDto
{
    public string? ServerId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public int GameTypeRevisionId { get; init; }

    public string? ServiceName { get; init; }

    public string? Status { get; init; }

    public List<GameServerSettingDto> Settings { get; init; } = [];

    public List<GameServerPortDto> Ports { get; init; } = [];

    [Obsolete("Use VolumeBindingLayout instead.")]
    public List<GameServerConfigurationOptionDto> DockerVolumeOptions { get; init; } = [];

    public string VolumeBindingLayout { get; init; } = "standard";

    public List<GameServerConfigurationOptionDto> NetworkOptions { get; init; } = [];

    /// <summary>
    /// Optional group ID to assign the new game server to upon creation.
    /// </summary>
    public int? InitialGroupId { get; init; }

    /// <summary>
    /// Access level for the initial group ("Edit" or "View").
    /// </summary>
    public string InitialGroupAccessLevel { get; init; } = "Edit";
}

public sealed record GameServerPortDto
{
    public int ContainerPort { get; init; }

    public string Protocol { get; init; } = "tcp";

    public int PublishedPort { get; init; }
}

public sealed record GameServerValidationResultDto
{
    public bool IsValid { get; init; }

    public List<GameServerValidationIssueDto> Issues { get; init; } = [];

    public List<GameServerResolvedPortDto> ResolvedPorts { get; init; } = [];

    public List<GameServerResolvedVolumeDto> ResolvedVolumes { get; init; } = [];

    public List<GameServerResolvedWebHostDto> ResolvedWebHosts { get; init; } = [];

    public List<GameServerConfigurationOptionDto> DockerVolumeOptions { get; init; } = [];

    public List<GameServerConfigurationOptionDto> NetworkOptions { get; init; } = [];
}
