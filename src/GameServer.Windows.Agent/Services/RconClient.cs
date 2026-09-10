using System.Net.Sockets;
using System.Text;

namespace GameServer.Windows.Agent.Services;

public sealed class RconClient : IDisposable
{
    private const int SERVERDATA_AUTH = 3;
    private const int SERVERDATA_AUTH_RESPONSE = 2;
    private const int SERVERDATA_EXECCOMMAND = 2;
    private const int SERVERDATA_RESPONSE_VALUE = 0;

    private readonly string _host;
    private readonly int _port;
    private readonly string _password;
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private int _packetId;
    private bool _authenticated;

    public RconClient(string host, int port, string password)
    {
        _host = host;
        _port = port;
        _password = password;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _tcpClient = new TcpClient();
        await _tcpClient.ConnectAsync(_host, _port, cancellationToken).ConfigureAwait(false);
        _stream = _tcpClient.GetStream();

        // Authenticate
        var authId = Interlocked.Increment(ref _packetId);
        await SendPacketAsync(authId, SERVERDATA_AUTH, _password, cancellationToken).ConfigureAwait(false);

        var (respId, respType, _) = await ReadPacketAsync(cancellationToken).ConfigureAwait(false);
        if (respId == -1 || respType != SERVERDATA_AUTH_RESPONSE)
        {
            throw new UnauthorizedAccessException("RCON authentication failed.");
        }

        _authenticated = true;
    }

    public async Task<string> ExecuteCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        if (!_authenticated || _stream == null)
        {
            await ConnectAsync(cancellationToken).ConfigureAwait(false);
        }

        var cmdId = Interlocked.Increment(ref _packetId);
        await SendPacketAsync(cmdId, SERVERDATA_EXECCOMMAND, command, cancellationToken).ConfigureAwait(false);

        var (_, _, body) = await ReadPacketAsync(cancellationToken).ConfigureAwait(false);
        return body;
    }

    private async Task SendPacketAsync(int id, int type, string body, CancellationToken cancellationToken)
    {
        if (_stream == null) throw new InvalidOperationException("Not connected to RCON server.");

        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var packetLength = 4 + 4 + bodyBytes.Length + 2; // ID (4) + Type (4) + Body (N) + 2 null bytes

        using var ms = new MemoryStream(packetLength + 4);
        using var writer = new BinaryWriter(ms);

        writer.Write(packetLength);
        writer.Write(id);
        writer.Write(type);
        writer.Write(bodyBytes);
        writer.Write((byte)0);
        writer.Write((byte)0);

        var data = ms.ToArray();
        await _stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<(int Id, int Type, string Body)> ReadPacketAsync(CancellationToken cancellationToken)
    {
        if (_stream == null) throw new InvalidOperationException("Not connected to RCON server.");

        var lengthBuffer = new byte[4];
        await ReadExactAsync(_stream, lengthBuffer, 4, cancellationToken).ConfigureAwait(false);
        var packetLength = BitConverter.ToInt32(lengthBuffer, 0);

        if (packetLength < 10 || packetLength > 4096 * 10)
        {
            throw new InvalidDataException($"Invalid RCON packet length: {packetLength}");
        }

        var packetBuffer = new byte[packetLength];
        await ReadExactAsync(_stream, packetBuffer, packetLength, cancellationToken).ConfigureAwait(false);

        var id = BitConverter.ToInt32(packetBuffer, 0);
        var type = BitConverter.ToInt32(packetBuffer, 4);
        var bodyLength = packetLength - 8 - 2;
        var body = bodyLength > 0 ? Encoding.UTF8.GetString(packetBuffer, 8, bodyLength) : string.Empty;

        return (id, type, body);
    }

    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, int count, CancellationToken cancellationToken)
    {
        var totalRead = 0;
        while (totalRead < count)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead, count - totalRead), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("Remote RCON server closed the connection.");
            }
            totalRead += read;
        }
    }

    public void Dispose()
    {
        _stream?.Dispose();
        _tcpClient?.Dispose();
    }
}
