namespace GameServer.Web.Services.Extensions;

/// <summary>
/// Client for the Palworld dedicated server REST API. See
/// https://tech.palworldgame.com/dedicated-server-rest-api for the protocol.
/// The client is always addressed at the container/service level and Basic-auth'd
/// with the admin password on every request. Callers should treat any operational
/// exception as a transient extension failure and surface it in the UI.
/// </summary>
public interface IPalworldApiClient
{
    Task<PalworldServerInfo?> GetInfoAsync(PalworldRequestContext context, CancellationToken cancellationToken = default);

    Task<PalworldPlayersResponse?> GetPlayersAsync(PalworldRequestContext context, CancellationToken cancellationToken = default);

    Task AnnounceAsync(PalworldRequestContext context, string message, CancellationToken cancellationToken = default);

    Task KickPlayerAsync(PalworldRequestContext context, string userId, string? message, CancellationToken cancellationToken = default);

    Task BanPlayerAsync(PalworldRequestContext context, string userId, string? message, CancellationToken cancellationToken = default);

    Task SaveWorldAsync(PalworldRequestContext context, CancellationToken cancellationToken = default);

    Task ShutdownAsync(PalworldRequestContext context, int waitSeconds, string? message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Per-request context used by <see cref="IPalworldApiClient"/>. Extension tabs
/// build this from the current <c>GameServerDetail</c>.
/// </summary>
public sealed record PalworldRequestContext
{
    public required string ServiceName { get; init; }

    public required int Port { get; init; }

    public required string AdminPassword { get; init; }
}

public sealed record PalworldServerInfo
{
    public string? Version { get; init; }

    public string? ServerName { get; init; }

    public string? Description { get; init; }

    public string? WorldGuid { get; init; }
}

public sealed record PalworldPlayersResponse
{
    public List<PalworldPlayer> Players { get; init; } = [];
}

public sealed record PalworldPlayer
{
    public string? Name { get; init; }

    public string? PlayerId { get; init; }

    public string? UserId { get; init; }

    public string? Ip { get; init; }

    public double Ping { get; init; }

    public double LocationX { get; init; }

    public double LocationY { get; init; }

    public int Level { get; init; }
}
