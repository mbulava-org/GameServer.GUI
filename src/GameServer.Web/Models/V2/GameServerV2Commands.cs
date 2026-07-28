namespace GameServer.Web.Models.V2;

public sealed record SaveGameServerRequest
{
    public string? ServerId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public int GameTypeRevisionId { get; init; }

    public string? ServiceName { get; init; }

    public string? Status { get; init; }

    public List<GameServerSetting> Settings { get; init; } = [];

    [Obsolete("Use VolumeBindingLayout instead.")]
    public List<GameServerConfigurationOption> DockerVolumeOptions { get; init; } = [];

    public string VolumeBindingLayout { get; init; } = "standard";

    public List<GameServerConfigurationOption> NetworkOptions { get; init; } = [];
}

public sealed record GameServerValidationResult
{
    public bool IsValid { get; init; }

    public List<GameServerValidationIssue> Issues { get; init; } = [];

    public List<GameServerResolvedPort> ResolvedPorts { get; init; } = [];

    public List<GameServerResolvedVolume> ResolvedVolumes { get; init; } = [];

    public List<GameServerResolvedWebHost> ResolvedWebHosts { get; init; } = [];

    public List<GameServerConfigurationOption> DockerVolumeOptions { get; init; } = [];

    public List<GameServerConfigurationOption> NetworkOptions { get; init; } = [];
}
