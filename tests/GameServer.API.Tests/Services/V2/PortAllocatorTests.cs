using Docker.DotNet.Models;
using GameServer.API.Configurations;
using GameServer.API.Interfaces;
using GameServer.API.Services;
using Moq;

namespace GameServer.API.Tests.Services.V2;

public class PortAllocatorTests
{
    // Default allocation range used by most tests (1024–65535, nothing reserved)
    private static readonly PortAllocation DefaultAllocation = new() { StartPort = 1024, EndPort = 65535 };

    private static PortAllocator CreateAllocator(
        IList<SwarmService>? swarmServices = null,
        PortAllocation? portAllocation = null)
    {
        var ops = new Mock<IServiceOperations>();
        ops.Setup(x => x.ListServicesAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(swarmServices ?? []);

        return new PortAllocator(ops.Object, portAllocation ?? DefaultAllocation);
    }

    private static SwarmService BuildSwarmService(uint publishedPort, string protocol = "tcp")
    {
        return new SwarmService
        {
            Endpoint = new Endpoint
            {
                Ports = [new PortConfig { PublishedPort = publishedPort, Protocol = protocol }]
            }
        };
    }

    // ── Branch 1: well-known / reserved-system ports ─────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(80)]
    [InlineData(443)]
    [InlineData(1023)]
    public async Task IsProtocolPortAvailable_WhenPortBelowThreshold_ReturnsFalse(uint port)
    {
        var allocator = CreateAllocator();
        var result = await allocator.IsProtocolPortAvailable(port);
        Assert.False(result);
    }

    // ── Branch 2: port outside the configured allocation range ───────────────

    [Fact]
    public async Task IsProtocolPortAvailable_WhenPortBelowAllocationStart_ReturnsFalse()
    {
        var allocation = new PortAllocation { StartPort = 25000, EndPort = 30000 };
        var allocator = CreateAllocator(portAllocation: allocation);

        var result = await allocator.IsProtocolPortAvailable(24999);
        Assert.False(result);
    }

    [Fact]
    public async Task IsProtocolPortAvailable_WhenPortAboveAllocationEnd_ReturnsFalse()
    {
        var allocation = new PortAllocation { StartPort = 25000, EndPort = 30000 };
        var allocator = CreateAllocator(portAllocation: allocation);

        var result = await allocator.IsProtocolPortAvailable(30001);
        Assert.False(result);
    }

    [Fact]
    public async Task IsProtocolPortAvailable_WhenPortAtAllocationBoundary_ReturnsTrue()
    {
        var allocation = new PortAllocation { StartPort = 25000, EndPort = 30000 };
        var allocator = CreateAllocator(portAllocation: allocation);

        Assert.True(await allocator.IsProtocolPortAvailable(25000));
        Assert.True(await allocator.IsProtocolPortAvailable(30000));
    }

    // ── Branch 3: port in a configured reserved range ────────────────────────

    [Fact]
    public async Task IsProtocolPortAvailable_WhenPortIsReservedSingleEntry_ReturnsFalse()
    {
        var allocation = new PortAllocation
        {
            StartPort = 1024,
            EndPort = 65535,
            ReservedPortRanges = ["22,2222,9443"]
        };
        var allocator = CreateAllocator(portAllocation: allocation);

        Assert.False(await allocator.IsProtocolPortAvailable(22));
        Assert.False(await allocator.IsProtocolPortAvailable(2222));
        Assert.False(await allocator.IsProtocolPortAvailable(9443));
    }

    [Fact]
    public async Task IsProtocolPortAvailable_WhenPortIsReservedRange_ReturnsFalse()
    {
        var allocation = new PortAllocation
        {
            StartPort = 1024,
            EndPort = 65535,
            ReservedPortRanges = ["8000-8080"]
        };
        var allocator = CreateAllocator(portAllocation: allocation);

        Assert.False(await allocator.IsProtocolPortAvailable(8000));
        Assert.False(await allocator.IsProtocolPortAvailable(8040));
        Assert.False(await allocator.IsProtocolPortAvailable(8080));
    }

    [Fact]
    public async Task IsProtocolPortAvailable_WhenPortJustOutsideReservedRange_ReturnsTrue()
    {
        var allocation = new PortAllocation
        {
            StartPort = 1024,
            EndPort = 65535,
            ReservedPortRanges = ["8000-8080"]
        };
        var allocator = CreateAllocator(portAllocation: allocation);

        Assert.True(await allocator.IsProtocolPortAvailable(7999));
        Assert.True(await allocator.IsProtocolPortAvailable(8081));
    }

    // ── Branch 4: port already used by a Swarm service ───────────────────────

    [Fact]
    public async Task IsProtocolPortAvailable_WhenPortUsedBySwarmService_ReturnsFalse()
    {
        var services = new List<SwarmService> { BuildSwarmService(25565, "tcp") };
        var allocator = CreateAllocator(swarmServices: services);

        var result = await allocator.IsProtocolPortAvailable(25565, "tcp");
        Assert.False(result);
    }

    [Fact]
    public async Task IsProtocolPortAvailable_WhenSamePortDifferentProtocol_ReturnsTrue()
    {
        // TCP is taken, but UDP is free
        var services = new List<SwarmService> { BuildSwarmService(25565, "tcp") };
        var allocator = CreateAllocator(swarmServices: services);

        var result = await allocator.IsProtocolPortAvailable(25565, "udp");
        Assert.True(result);
    }

    [Fact]
    public async Task IsProtocolPortAvailable_WhenProtocolPassedUppercase_NormalisedCorrectly()
    {
        var services = new List<SwarmService> { BuildSwarmService(25565, "tcp") };
        var allocator = CreateAllocator(swarmServices: services);

        // "TCP" should match the stored "tcp" entry
        var result = await allocator.IsProtocolPortAvailable(25565, "TCP");
        Assert.False(result);
    }

    [Fact]
    public async Task IsProtocolPortAvailable_WhenNullProtocol_DefaultsToTcpCheck()
    {
        var services = new List<SwarmService> { BuildSwarmService(25565, "tcp") };
        var allocator = CreateAllocator(swarmServices: services);

        // null protocol → defaults to "tcp"
        var result = await allocator.IsProtocolPortAvailable(25565, null!);
        Assert.False(result);
    }

    // ── Happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task IsProtocolPortAvailable_WhenPortFreeAndInRange_ReturnsTrue()
    {
        var allocator = CreateAllocator(swarmServices: []);
        var result = await allocator.IsProtocolPortAvailable(25565, "tcp");
        Assert.True(result);
    }

    [Fact]
    public async Task IsProtocolPortAvailable_WhenNoSwarmServicesExist_ReturnsTrue()
    {
        var allocator = CreateAllocator(swarmServices: []);
        Assert.True(await allocator.IsProtocolPortAvailable(27015));
    }

    [Fact]
    public async Task IsProtocolPortAvailable_WhenSwarmServiceHasNoEndpointPorts_ReturnsTrue()
    {
        // Service exists but exposes no published ports
        var services = new List<SwarmService>
        {
            new() { Endpoint = new Endpoint { Ports = [] } }
        };
        var allocator = CreateAllocator(swarmServices: services);
        Assert.True(await allocator.IsProtocolPortAvailable(25565));
    }
}
