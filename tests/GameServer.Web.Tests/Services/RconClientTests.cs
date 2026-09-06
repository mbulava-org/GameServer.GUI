using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using GameServer.Web.Services.Extensions;
using Microsoft.Extensions.Logging;
using Moq;

namespace GameServer.Web.Tests.Services;

public class RconClientTests
{
    private readonly Mock<ILogger<RconClient>> _mockLogger = new();

    [Fact]
    public async Task ExecuteAsync_WithInvalidPort_ReturnsError()
    {
        var client = new RconClient(_mockLogger.Object);
        var context = new RconRequestContext
        {
            ServiceName = "localhost",
            Port = 0,
            Password = "secret-password"
        };

        var result = await client.ExecuteAsync(context, "help");
        Assert.False(result.Success);
        Assert.Contains("Invalid RCON port", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnreachableHost_ReturnsConnectionError()
    {
        var client = new RconClient(_mockLogger.Object);
        var context = new RconRequestContext
        {
            ServiceName = "127.0.0.1",
            Port = 59999, // Unused port
            Password = "secret-password"
        };

        var result = await client.ExecuteAsync(context, "help");
        Assert.False(result.Success);
        Assert.Contains("Could not connect to RCON", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_WithSuccessfulAuthAndCommand_ReturnsResponse()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var serverTask = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var stream = client.GetStream();

            // 1. Read auth packet
            var lenBuf = new byte[4];
            await stream.ReadExactlyAsync(lenBuf);
            var len = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);
            var payload = new byte[len];
            await stream.ReadExactlyAsync(payload);

            // Respond to auth: Success (requestId = 1, type = 2)
            var authResp = RconPacket.Encode(1, RconPacket.TypeExecCommand, "");
            await stream.WriteAsync(authResp);

            // 2. Read command packet
            await stream.ReadExactlyAsync(lenBuf);
            len = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);
            payload = new byte[len];
            await stream.ReadExactlyAsync(payload);

            // Respond to command
            var cmdResp = RconPacket.Encode(2, RconPacket.TypeResponseValue, "Command output: OK");
            await stream.WriteAsync(cmdResp);
        });

        try
        {
            var client = new RconClient(_mockLogger.Object);
            var context = new RconRequestContext
            {
                ServiceName = "127.0.0.1",
                Port = port,
                Password = "valid-password"
            };

            var result = await client.ExecuteAsync(context, "info");
            Assert.True(result.Success);
            Assert.Equal("Command output: OK", result.Response);
        }
        finally
        {
            listener.Stop();
            await serverTask;
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenAuthFails_ReturnsAuthError()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var serverTask = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var stream = client.GetStream();

            // Read auth packet
            var lenBuf = new byte[4];
            await stream.ReadExactlyAsync(lenBuf);
            var len = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);
            var payload = new byte[len];
            await stream.ReadExactlyAsync(payload);

            // Respond with auth failure (requestId = -1)
            var authResp = RconPacket.Encode(-1, RconPacket.TypeExecCommand, "");
            await stream.WriteAsync(authResp);
        });

        try
        {
            var client = new RconClient(_mockLogger.Object);
            var context = new RconRequestContext
            {
                ServiceName = "127.0.0.1",
                Port = port,
                Password = "wrong-password"
            };

            var result = await client.ExecuteAsync(context, "info");
            Assert.False(result.Success);
            Assert.Contains("RCON authentication failed", result.Error);
        }
        finally
        {
            listener.Stop();
            await serverTask;
        }
    }
}
