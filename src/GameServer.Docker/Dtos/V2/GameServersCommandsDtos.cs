namespace GameServer.Docker.Dtos.V2;

public sealed record SaveGameServerRequestDto
{
    public string? ServerId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public int GameTypeRevisionId { get; init; }

    public string? ServiceName { get; init; }

    public string? Status { get; init; }

    public List<GameServerSettingDto> Settings { get; init; } = [];

    [Obsolete("Use VolumeBindingLayout instead.")]
    public List<GameServerConfigurationOptionDto> DockerVolumeOptions { get; init; } = [];

    public string VolumeBindingLayout { get; init; } = "standard";

    public List<GameServerConfigurationOptionDto> NetworkOptions { get; init; } = [];
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
