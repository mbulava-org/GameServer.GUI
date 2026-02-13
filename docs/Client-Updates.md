# GameServer.Docker.Client Updates

## Overview

The **GameServer.Docker.Client** is auto-generated via NSwag from the API's OpenAPI specification. After rebuilding, it will automatically include all the new agent-related endpoints and models.

## New Features in Generated Client

### 1. New API Methods

After regeneration, the client will include these new methods:

#### C# Client

```csharp
// In GameServerClient.cs (auto-generated):

/// <summary>
/// Get available node agents in the swarm
/// </summary>
Task<AgentDiscoveryResponse> GetNodeAgentsAsync(CancellationToken cancellationToken = default);

/// <summary>
/// Get real-time container statistics via node agent
/// </summary>
Task<ContainerStats> GetContainerStatsAsync(string id, CancellationToken cancellationToken = default);

/// <summary>
/// Get current resource usage for a server (enhanced with real-time stats)
/// </summary>
Task<ServerResourceUsage> GetResourceUsageAsync(string id, CancellationToken cancellationToken = default);
```

#### TypeScript Client

```typescript
// In generated TypeScript client:

/**
 * Get available node agents in the swarm
 */
getNodeAgents(): Promise<AgentDiscoveryResponse>;

/**
 * Get real-time container statistics via node agent
 */
getContainerStats(id: string): Promise<ContainerStats>;

/**
 * Get current resource usage for a server
 */
getResourceUsage(id: string): Promise<ServerResourceUsage>;
```

### 2. New Models

The following models will be generated in the client:

```csharp
// C# Models (auto-generated):

public class AgentDiscoveryResponse
{
    public DateTime Timestamp { get; set; }
    public int AgentCount { get; set; }
    public List<AgentInfo> Agents { get; set; }
}

public class AgentInfo
{
    public string NodeId { get; set; }
    public string NodeName { get; set; }
    public string TaskId { get; set; }
    public string InternalUrl { get; set; }
    public bool IsHealthy { get; set; }
    public DateTime DiscoveredAt { get; set; }
}

public class ContainerStats
{
    public string ContainerId { get; set; }
    public DateTime Timestamp { get; set; }
    
    // CPU
    public double CpuUsagePercent { get; set; }
    public ulong CpuTotalUsage { get; set; }
    public ulong CpuSystemUsage { get; set; }
    public uint OnlineCpus { get; set; }
    
    // Memory
    public ulong MemoryUsageBytes { get; set; }
    public ulong MemoryLimitBytes { get; set; }
    public double MemoryUsagePercent { get; set; }
    public ulong MemoryMaxUsageBytes { get; set; }
    
    // Network
    public long NetworkRxBytes { get; set; }
    public long NetworkTxBytes { get; set; }
    
    // Block I/O
    public long BlockReadBytes { get; set; }
    public long BlockWriteBytes { get; set; }
    
    // Processes
    public ulong Pids { get; set; }
}

// Enhanced existing model:
public partial class ServerResourceUsage
{
    // Existing properties...
    
    // NEW: Real-time container stats
    public ContainerStats? RealTimeStats { get; set; }
    public bool HasRealTimeStats { get; set; }
}
```

### 3. Enhanced Existing Methods

The `GetResourceUsage` method now returns enhanced data:

**Before:**
```csharp
var usage = await client.GetResourceUsageAsync(serverId);
// Only had service-level data: replicas, limits, task states
```

**After:**
```csharp
var usage = await client.GetResourceUsageAsync(serverId);

// Still has service-level data:
Console.WriteLine($"Running replicas: {usage.RunningReplicas}/{usage.DesiredReplicas}");
Console.WriteLine($"Memory limit: {usage.ServiceMemoryLimitPerReplica} bytes");

// NEW: Real-time container stats (if available):
if (usage.HasRealTimeStats && usage.RealTimeStats != null)
{
    Console.WriteLine($"CPU Usage: {usage.RealTimeStats.CpuUsagePercent}%");
    Console.WriteLine($"Memory Usage: {usage.RealTimeStats.MemoryUsagePercent}%");
    Console.WriteLine($"Network RX: {usage.RealTimeStats.NetworkRxBytes} bytes");
    Console.WriteLine($"Network TX: {usage.RealTimeStats.NetworkTxBytes} bytes");
}
```

## How to Regenerate the Client

### Option 1: Rebuild the Client Project

```bash
cd src/GameServer.Docker.Client
dotnet build
```

The NSwag MSBuild target will automatically:
1. Read the API's OpenAPI spec
2. Generate C# client code
3. Generate TypeScript client code (if configured)

### Option 2: Manual NSwag Run

```bash
cd src/GameServer.Docker.Client
nswag run nswag.json
```

### Option 3: Full Solution Build

```bash
# From solution root
dotnet build
```

This will rebuild both the API and client, regenerating all client code.

## Usage Examples

### Example 1: Discover Agents

```csharp
using var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
var client = new GameServerClient(httpClient);

var agents = await client.GetNodeAgentsAsync();

Console.WriteLine($"Found {agents.AgentCount} agents:");
foreach (var agent in agents.Agents)
{
    Console.WriteLine($"  {agent.NodeName} ({agent.NodeId})");
    Console.WriteLine($"    URL: {agent.InternalUrl}");
    Console.WriteLine($"    Healthy: {agent.IsHealthy}");
}
```

### Example 2: Get Real-Time Stats

```csharp
var stats = await client.GetContainerStatsAsync("my-server-id");

Console.WriteLine($"Container: {stats.ContainerId}");
Console.WriteLine($"CPU: {stats.CpuUsagePercent}% ({stats.OnlineCpus} cores)");
Console.WriteLine($"Memory: {stats.MemoryUsageBytes:N0} / {stats.MemoryLimitBytes:N0} bytes ({stats.MemoryUsagePercent}%)");
Console.WriteLine($"Network RX: {stats.NetworkRxBytes:N0} bytes");
Console.WriteLine($"Network TX: {stats.NetworkTxBytes:N0} bytes");
```

### Example 3: Monitor Resources with Real-Time Stats

```csharp
var resources = await client.GetResourceUsageAsync("my-server-id");

Console.WriteLine("=== Service-Level Info ===");
Console.WriteLine($"Status: {resources.ServiceStatus}");
Console.WriteLine($"Replicas: {resources.RunningReplicas}/{resources.DesiredReplicas}");
Console.WriteLine($"CPU Limit: {resources.ServiceCpuLimitPerReplica} nanoCPUs");
Console.WriteLine($"Memory Limit: {resources.ServiceMemoryLimitPerReplica:N0} bytes");

if (resources.HasRealTimeStats)
{
    Console.WriteLine("\n=== Real-Time Stats ===");
    var rt = resources.RealTimeStats!;
    Console.WriteLine($"Actual CPU: {rt.CpuUsagePercent}%");
    Console.WriteLine($"Actual Memory: {rt.MemoryUsageBytes:N0} bytes ({rt.MemoryUsagePercent}%)");
    Console.WriteLine($"Network I/O: ?{rt.NetworkRxBytes:N0} ?{rt.NetworkTxBytes:N0}");
}
else
{
    Console.WriteLine("\n??  Real-time stats not available (agent not reachable or container not running)");
}
```

### Example 4: TypeScript/JavaScript Usage

```typescript
import { GameServerClient, AgentDiscoveryResponse, ContainerStats } from './generated-client';

const client = new GameServerClient('http://localhost:5000');

// Discover agents
const agents: AgentDiscoveryResponse = await client.getNodeAgents();
console.log(`Found ${agents.agentCount} agents`);

// Get real-time stats
const stats: ContainerStats = await client.getContainerStats('my-server-id');
console.log(`CPU: ${stats.cpuUsagePercent}%`);
console.log(`Memory: ${stats.memoryUsagePercent}%`);

// Get resources with real-time stats
const resources = await client.getResourceUsage('my-server-id');
if (resources.hasRealTimeStats) {
    console.log(`Real-time CPU: ${resources.realTimeStats.cpuUsagePercent}%`);
}
```

## Breaking Changes

**None!** All new endpoints and models are additive. Existing client code will continue to work without modification.

The only change is that `ServerResourceUsage` now has additional properties:
- `RealTimeStats` (nullable)
- `HasRealTimeStats` (boolean)

These are additive and won't break existing code.

## Testing the Client

### 1. Regenerate Client

```bash
cd src/GameServer.Docker.Client
dotnet build
```

### 2. Verify Generated Files

Check that new files are generated:
- `**/*Client.g.cs` - Generated C# client
- `**/*.ts` (if TypeScript enabled) - Generated TypeScript client

### 3. Test New Methods

Create a test console app:

```csharp
// Program.cs
using GameServer.Docker.Client;

var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
var client = new GameServerClient(httpClient);

// Test agent discovery
Console.WriteLine("Testing agent discovery...");
var agents = await client.GetNodeAgentsAsync();
Console.WriteLine($"? Found {agents.AgentCount} agents");

// Test stats (if you have a running server)
if (agents.AgentCount > 0)
{
    Console.WriteLine("\nTesting real-time stats...");
    try
    {
        var stats = await client.GetContainerStatsAsync("test-server");
        Console.WriteLine($"? CPU: {stats.CpuUsagePercent}%");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"??  Stats not available: {ex.Message}");
    }
}

Console.WriteLine("\n? Client tests complete!");
```

## Deployment

### NuGet Package

If you publish the client as a NuGet package:

```bash
# Update version in .csproj
dotnet pack -c Release

# Publish to NuGet (if configured)
dotnet nuget push bin/Release/GameServer.Docker.Client.*.nupkg
```

### Internal Package Feed

```bash
# Publish to internal feed
dotnet nuget push bin/Release/GameServer.Docker.Client.*.nupkg \
  --source http://your-internal-feed/
```

## Summary

? **Auto-generates** new client methods for agent endpoints  
? **Type-safe** models for all new data structures  
? **No breaking changes** - fully backward compatible  
? **C# and TypeScript** clients both updated  
? **Ready to use** after simple rebuild  

Simply rebuild the client project, and you'll have fully typed, IntelliSense-enabled access to all the new agent functionality!
