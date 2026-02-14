# Client Update Checklist

## What You Need to Do

### ? Already Done
- [x] API endpoints properly documented with `[ProducesResponseType]`
- [x] New models created (`ContainerStats`, `AgentDiscoveryResponse`, `AgentInfo`)
- [x] Controller returns proper typed responses
- [x] Build successful

### ?? Action Required

#### 1. Rebuild the Client Project

**Simple approach:**
```bash
cd src/GameServer.Docker.Client
dotnet build
```

This will:
- Generate new C# client methods
- Generate new TypeScript client methods (if configured)
- Include all new models and endpoints

#### 2. Verify Generated Code

After build, check for new generated files:
```bash
# Look for generated files
ls src/GameServer.Docker.Client/**/*.g.cs

# Should include methods like:
# - GetNodeAgentsAsync()
# - GetContainerStatsAsync()
# - Enhanced GetResourceUsageAsync()
```

#### 3. Test the Client (Optional)

Create a simple test:
```csharp
var client = new GameServerClient(new HttpClient { 
    BaseAddress = new Uri("http://localhost:5000") 
});

// Test agent discovery
var agents = await client.GetNodeAgentsAsync();
Console.WriteLine($"Found {agents.AgentCount} agents");

// Test stats
var stats = await client.GetContainerStatsAsync("server-id");
Console.WriteLine($"CPU: {stats.CpuUsagePercent}%");
```

#### 4. Update Package Version (If Publishing)

In `src/GameServer.Docker.Client/GameServer.Docker.Client.csproj`:
```xml
<PropertyGroup>
  <Version>2.0.0</Version> <!-- Bump version -->
  <PackageReleaseNotes>
    - Added agent discovery endpoint
    - Added real-time container statistics
    - Enhanced resource monitoring with real-time data
  </PackageReleaseNotes>
</PropertyGroup>
```

#### 5. Publish Package (If Applicable)

```bash
cd src/GameServer.Docker.Client
dotnet pack -c Release
dotnet nuget push bin/Release/GameServer.Docker.Client.*.nupkg
```

## What the Client Will Gain

### New Methods
```csharp
? GetNodeAgentsAsync()              // Discover agents
? GetContainerStatsAsync(serverId)  // Real-time stats
? GetResourceUsageAsync(serverId)   // Enhanced with real-time
```

### New Models
```csharp
? AgentDiscoveryResponse
? AgentInfo
? ContainerStats
? ServerResourceUsage (enhanced)
```

### Breaking Changes
```
? None - fully backward compatible!
```

## Quick Start for Client Users

### Install Updated Client
```bash
# From your consuming application
dotnet add package GameServer.Docker.Client --version 2.0.0
```

### Use New Features
```csharp
using GameServer.Docker.Client;

var client = new GameServerClient(httpClient);

// Discover agents
var agents = await client.GetNodeAgentsAsync();

// Get real-time stats
var stats = await client.GetContainerStatsAsync(serverId);

// Get resources (now includes real-time data)
var resources = await client.GetResourceUsageAsync(serverId);
if (resources.HasRealTimeStats)
{
    Console.WriteLine($"CPU: {resources.RealTimeStats.CpuUsagePercent}%");
}
```

## Summary

**Action Required:** Just rebuild the client project!

```bash
dotnet build src/GameServer.Docker.Client/GameServer.Docker.Client.csproj
```

That's it! The NSwag tool will automatically generate all the new client code from your OpenAPI spec. ?
