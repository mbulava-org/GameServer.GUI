using System.Buffers.Binary;
using System.Text;
using GameServer.Web.Services.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameServer.Web.Tests.Services.Extensions;

public class RconClientTests
{
    [Fact]
    public void Encode_ProducesValidSourceRconFrame()
    {
        var packet = RconPacket.Encode(requestId: 7, RconPacket.TypeAuth, "hunter2");

        var bodyBytes = Encoding.UTF8.GetBytes("hunter2");
        var expectedLength = 4 + 4 + bodyBytes.Length + 2;

        Assert.Equal(4 + expectedLength, packet.Length);
        Assert.Equal(expectedLength, BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(0)));
        Assert.Equal(7, BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(4)));
        Assert.Equal(RconPacket.TypeAuth, BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(8)));
        Assert.Equal(bodyBytes, packet.AsSpan(12, bodyBytes.Length).ToArray());
        Assert.Equal(0, packet[^1]);
        Assert.Equal(0, packet[^2]);
    }

    [Fact]
    public void Encode_EmptyBody_HasMinimumLength()
    {
        var packet = RconPacket.Encode(requestId: 1, RconPacket.TypeExecCommand, string.Empty);

        Assert.Equal(14, packet.Length); // 4 length prefix + 10 payload
        Assert.Equal(10, BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(0)));
    }

    [Fact]
    public void DecodePayload_RoundTripsEncodedPacket()
    {
        var encoded = RconPacket.Encode(requestId: 42, RconPacket.TypeResponseValue, "Welcome to the server!");
        var payload = encoded.AsSpan(4); // strip length prefix

        var decoded = RconPacket.DecodePayload(payload);

        Assert.Equal(42, decoded.RequestId);
        Assert.Equal(RconPacket.TypeResponseValue, decoded.Type);
        Assert.Equal("Welcome to the server!", decoded.Body);
    }

    [Fact]
    public void DecodePayload_EmptyBody_ReturnsEmptyString()
    {
        var encoded = RconPacket.Encode(requestId: 3, RconPacket.TypeResponseValue, string.Empty);

        var decoded = RconPacket.DecodePayload(encoded.AsSpan(4));

        Assert.Equal(string.Empty, decoded.Body);
    }

    [Fact]
    public void DecodePayload_Utf8Body_RoundTrips()
    {
        var encoded = RconPacket.Encode(requestId: 5, RconPacket.TypeResponseValue, "héllo wörld ✓");

        var decoded = RconPacket.DecodePayload(encoded.AsSpan(4));

        Assert.Equal("héllo wörld ✓", decoded.Body);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidPort_ReturnsFailure()
    {
        var client = new RconClient(NullLogger<RconClient>.Instance);
        var context = new RconRequestContext { ServiceName = "svc", Port = 0, Password = "pw" };

        var result = await client.ExecuteAsync(context, "Info");

        Assert.False(result.Success);
        Assert.Contains("port", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_MissingPassword_Throws()
    {
        var client = new RconClient(NullLogger<RconClient>.Instance);
        var context = new RconRequestContext { ServiceName = "svc", Port = 25575, Password = " " };

        await Assert.ThrowsAsync<ArgumentException>(() => client.ExecuteAsync(context, "Info"));
    }

    [Fact]
    public async Task ExecuteAsync_MissingCommand_Throws()
    {
        var client = new RconClient(NullLogger<RconClient>.Instance);
        var context = new RconRequestContext { ServiceName = "svc", Port = 25575, Password = "pw" };

        await Assert.ThrowsAsync<ArgumentException>(() => client.ExecuteAsync(context, ""));
    }
}
