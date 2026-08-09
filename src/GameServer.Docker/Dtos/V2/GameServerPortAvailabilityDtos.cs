namespace GameServer.Docker.Dtos.V2;

/// <summary>
/// Request payload for a point-in-time published-port availability check.
/// </summary>
public sealed record GameServerPortAvailabilityRequestDto
{
    /// <summary>
    /// The server the ports belong to. Services owned by this server are ignored so an
    /// existing server does not conflict with its own currently published ports.
    /// </summary>
    public string? ServerId { get; init; }

    public List<GameServerPortAvailabilityRequestPortDto> Ports { get; init; } = [];
}

/// <summary>
/// A single published port to check.
/// </summary>
public sealed record GameServerPortAvailabilityRequestPortDto
{
    /// <summary>
    /// Caller-supplied identifier (the GameTypePort id) echoed back so the UI can correlate results.
    /// </summary>
    public int PortId { get; init; }

    public int Port { get; init; }

    public string Protocol { get; init; } = "tcp";
}

/// <summary>
/// Result of a published-port availability check.
/// </summary>
public sealed record GameServerPortAvailabilityResultDto
{
    public List<GameServerPortAvailabilityDto> Ports { get; init; } = [];
}

/// <summary>
/// Availability outcome for a single published port.
/// </summary>
public sealed record GameServerPortAvailabilityDto
{
    public int PortId { get; init; }

    public int Port { get; init; }

    public string Protocol { get; init; } = "tcp";

    public bool IsAvailable { get; init; }

    /// <summary>
    /// Human-readable explanation when the port is unavailable; null when available.
    /// </summary>
    public string? Reason { get; init; }
}
