namespace GameServer.Web.Models.V2;

/// <summary>
/// Web-side mirror of the API's published-port availability request.
/// </summary>
public sealed record GameServerPortAvailabilityRequest
{
    public string? ServerId { get; init; }

    public List<GameServerPortAvailabilityRequestPort> Ports { get; init; } = [];
}

/// <summary>
/// A single published port to check.
/// </summary>
public sealed record GameServerPortAvailabilityRequestPort
{
    /// <summary>
    /// The GameTypePort id, echoed back so results can be correlated to the UI row.
    /// </summary>
    public int PortId { get; init; }

    public int Port { get; init; }

    public string Protocol { get; init; } = "tcp";
}

/// <summary>
/// Web-side mirror of the API's published-port availability response.
/// </summary>
public sealed record GameServerPortAvailabilityResult
{
    public List<GameServerPortAvailability> Ports { get; init; } = [];
}

/// <summary>
/// Availability outcome for a single published port.
/// </summary>
public sealed record GameServerPortAvailability
{
    public int PortId { get; init; }

    public int Port { get; init; }

    public string Protocol { get; init; } = "tcp";

    public bool IsAvailable { get; init; }

    public string? Reason { get; init; }
}
