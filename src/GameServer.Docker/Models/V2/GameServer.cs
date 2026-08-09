namespace GameServer.Docker.Models.V2;

public sealed record GameServer
{
    public int Id { get; init; }

    public string ServerId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public int GameTypeRevisionId { get; init; }

    public string ServiceName { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }

    public DateTime? LastDeployedAt { get; init; }

    public DateTime? LastSeenAt { get; init; }

    public bool IsDeleted { get; init; }

    public List<GameServerSetting> Settings { get; init; } = [];

    public List<GameServerVolume> Volumes { get; init; } = [];
}

public sealed record GameServerSetting
{
    public int Id { get; init; }

    public string SettingKey { get; init; } = string.Empty;

    public string? Value { get; init; }
}
