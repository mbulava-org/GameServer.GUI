namespace GameServer.Web.Services.Extensions;

/// <summary>
/// Server-side Source RCON client used by extension tabs to administer game
/// servers over the classic Valve RCON protocol (Palworld, Minecraft Java,
/// Source engine games, etc.). Runs entirely in <c>GameServer.Web</c>; the
/// browser never sees the RCON password.
/// </summary>
public interface IRconClient
{
    /// <summary>
    /// Connects, authenticates, executes a single command, and disconnects.
    /// </summary>
    /// <returns>The command response body (may be empty for commands with no output).</returns>
    Task<RconCommandResult> ExecuteAsync(RconRequestContext context, string command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Per-request connection context resolved from server settings; mirrors
/// <see cref="PalworldRequestContext"/>.
/// </summary>
public sealed class RconRequestContext
{
    /// <summary>Docker service name used as the host on the internal network.</summary>
    public required string ServiceName { get; init; }

    /// <summary>RCON TCP port.</summary>
    public required int Port { get; init; }

    /// <summary>RCON password. Never logged.</summary>
    public required string Password { get; init; }
}

/// <summary>
/// Result of a single RCON command execution.
/// </summary>
public sealed class RconCommandResult
{
    public required bool Success { get; init; }

    /// <summary>Response body from the server (empty when the command has no output).</summary>
    public string Response { get; init; } = string.Empty;

    /// <summary>Human-readable failure reason when <see cref="Success"/> is false.</summary>
    public string? Error { get; init; }
}
