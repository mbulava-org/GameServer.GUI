using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;

namespace GameServer.Web.Services.Extensions;

/// <summary>
/// Source RCON (Valve) protocol client. Opens a TCP connection per command:
/// connect, authenticate, execute, disconnect. Single-packet responses only,
/// which matches Palworld and is sufficient for typical admin commands.
/// The RCON password is never logged.
/// </summary>
public sealed class RconClient : IRconClient
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(10);

    private readonly ILogger<RconClient> _logger;

    public RconClient(ILogger<RconClient> logger)
    {
        _logger = logger;
    }

    public async Task<RconCommandResult> ExecuteAsync(RconRequestContext context, string command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(context.ServiceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(context.Password);
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        if (context.Port <= 0 || context.Port > 65535)
        {
            return new RconCommandResult { Success = false, Error = $"Invalid RCON port {context.Port}." };
        }

        try
        {
            using var tcpClient = new TcpClient();
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(ConnectTimeout);
            await tcpClient.ConnectAsync(context.ServiceName, context.Port, connectCts.Token).ConfigureAwait(false);

            var stream = tcpClient.GetStream();

            // Authenticate
            var authPacket = RconPacket.Encode(requestId: 1, RconPacket.TypeAuth, context.Password);
            await stream.WriteAsync(authPacket, cancellationToken).ConfigureAwait(false);

            var authResponse = await ReadPacketAsync(stream, cancellationToken).ConfigureAwait(false);
            if (authResponse.RequestId == -1)
            {
                _logger.LogWarning("RCON authentication failed for {Service}:{Port}", context.ServiceName, context.Port);
                return new RconCommandResult { Success = false, Error = "RCON authentication failed. Check the RCON password." };
            }

            // Execute command
            var commandPacket = RconPacket.Encode(requestId: 2, RconPacket.TypeExecCommand, command);
            await stream.WriteAsync(commandPacket, cancellationToken).ConfigureAwait(false);

            var response = await ReadPacketAsync(stream, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("RCON command executed on {Service}:{Port} (command/password redacted)", context.ServiceName, context.Port);

            return new RconCommandResult { Success = true, Response = response.Body };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new RconCommandResult { Success = false, Error = "RCON request timed out." };
        }
        catch (SocketException ex)
        {
            _logger.LogWarning(ex, "RCON connection failed for {Service}:{Port}", context.ServiceName, context.Port);
            return new RconCommandResult { Success = false, Error = $"Could not connect to RCON at {context.ServiceName}:{context.Port}." };
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "RCON I/O failure for {Service}:{Port}", context.ServiceName, context.Port);
            return new RconCommandResult { Success = false, Error = "RCON connection was interrupted." };
        }
    }

    private static async Task<RconPacket.Packet> ReadPacketAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        readCts.CancelAfter(ReadTimeout);

        var lengthBuffer = new byte[4];
        await ReadExactAsync(stream, lengthBuffer, readCts.Token).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);

        if (length < 10 || length > RconPacket.MaxPacketSize)
        {
            throw new IOException($"Invalid RCON packet length {length}.");
        }

        var payload = new byte[length];
        await ReadExactAsync(stream, payload, readCts.Token).ConfigureAwait(false);

        return RconPacket.DecodePayload(payload);
    }

    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(read), cancellationToken).ConfigureAwait(false);
            if (n == 0)
            {
                throw new IOException("RCON connection closed by remote host.");
            }

            read += n;
        }
    }
}

/// <summary>
/// Source RCON packet framing helpers. Packet layout (little-endian):
/// [int32 length][int32 requestId][int32 type][body bytes][0x00][0x00]
/// where length counts everything after the length field.
/// </summary>
internal static class RconPacket
{
    public const int TypeAuth = 3;
    public const int TypeExecCommand = 2;
    public const int TypeResponseValue = 0;
    public const int MaxPacketSize = 4096;

    public static byte[] Encode(int requestId, int type, string body)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var length = 4 + 4 + bodyBytes.Length + 2;
        var buffer = new byte[4 + length];

        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0), length);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(4), requestId);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(8), type);
        bodyBytes.CopyTo(buffer.AsSpan(12));
        // Two trailing null bytes are already zero-initialized.
        return buffer;
    }

    /// <summary>Decodes the payload portion (everything after the length prefix).</summary>
    public static Packet DecodePayload(ReadOnlySpan<byte> payload)
    {
        var requestId = BinaryPrimitives.ReadInt32LittleEndian(payload[..4]);
        var type = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(4, 4));
        var bodySpan = payload[8..];

        // Strip the two trailing null terminators.
        var end = bodySpan.Length;
        while (end > 0 && bodySpan[end - 1] == 0)
        {
            end--;
        }

        var body = Encoding.UTF8.GetString(bodySpan[..end]);
        return new Packet(requestId, type, body);
    }

    internal readonly record struct Packet(int RequestId, int Type, string Body);
}
