# ResourceMonitor Model Analysis

## Answer: **YES** - The Models Exist Properly in the Current Source, But **NOT** in the Published NuGet Package

## Summary

The `ServerResourceUsage` and `ContainerStats` models **DO exist correctly** in the GameServer.Docker.Client source code, but they are **NOT present in the published NuGet package version 0.0.2.119-beta** that GameServer.Web is currently using.

## Current Situation

### ? What EXISTS in GameServer.Docker.Client Source (Current Build)

The generated client file `GameServer.Docker.Client.v1.g.cs` contains:

#### ServerResourceUsage Model
```csharp
public partial class ServerResourceUsage
{
    // Service Identity
    public string ServerId { get; set; }
    public string ServiceId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    
    // Service-level properties...
    public int DesiredReplicas { get; set; }
    public int RunningReplicas { get; set; }
    public int FailedTasks { get; set; }
    // ... other service properties
    
    // ? THE IMPORTANT PROPERTIES:
    public ContainerStats RealTimeStats { get; set; }
    public bool HasRealTimeStats { get; set; }
    public string ServiceStatus { get; set; }
}
```

#### ContainerStats Model
```csharp
public partial class ContainerStats
{
    public string ContainerId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    
    // CPU
    public double CpuUsagePercent { get; set; }
    public ulong CpuTotalUsage { get; set; }
    public ulong CpuSystemUsage { get; set; }
    public int OnlineCpus { get; set; }
    
    // Memory
    public ulong MemoryUsageBytes { get; set; }
    public ulong MemoryLimitBytes { get; set; }
    public double MemoryUsagePercent { get; set; }
    public ulong MemoryMaxUsageBytes { get; set; }
    
    // Network
    public long NetworkRxBytes { get; set; }
    public long NetworkTxBytes { get; set; }
    
    // Disk I/O
    public long BlockReadBytes { get; set; }
    public long BlockWriteBytes { get; set; }
    
    // Processes
    public ulong Pids { get; set; }
}
```

### ? What's MISSING in Published NuGet Package v0.0.2.119-beta

The NuGet package that GameServer.Web currently references **does not have**:
- ? `ServerResourceUsage.RealTimeStats` property
- ? `ServerResourceUsage.HasRealTimeStats` property
- ? `ContainerStats` class

This is why the reflection-based compatibility layer was necessary.

## Why the Mismatch?

### How GameServer.Docker.Client Works

1. **NSwag Code Generation**: The client is auto-generated from the GameServer.Docker API's OpenAPI specification
   ```xml
   <Target Name="NSwag" AfterTargets="BeforeBuild">
       <Exec Command="$(NSwagExe_Net100) run nswag.json" />
   </Target>
   ```

2. **Generated File**: `GameServer.Docker.Client.v1.g.cs` contains all API clients and models

3. **Package Publishing**: When built in Release mode, it creates a NuGet package

### The Problem

The **v0.0.2.119-beta** package was published **BEFORE** the `ServerResourceUsage` and `ContainerStats` models were added to the GameServer.Docker API. The current source has these models, but they haven't been published to NuGet yet.

## What Needs to Happen

### Option 1: Publish New Package Version (Recommended)

1. **Build GameServer.Docker** with the updated models:
   ```bash
   cd C:\Users\mbula\source\repos\mbulava-org\GameServer.Docker\src\GameServer.Docker.Client
   dotnet build -c Release
   ```

2. **Publish to NuGet**:
   ```bash
   dotnet nuget push bin/Release/GameServer.Docker.Client.*.nupkg --source https://api.nuget.org/v3/index.json
   ```

3. **Update GameServer.Web reference**:
   ```bash
   cd C:\Users\mbula\source\repos\mbulava-org\GameServer.GUI\src\GameServer.Web
   dotnet add package GameServer.Docker.Client --version [NEW_VERSION]
   ```

4. **Remove reflection code** from ResourceMonitor.razor and use direct property access:
   ```csharp
   // Instead of reflection-based GetCpuValue():
   var cpu = currentMetrics.RealTimeStats?.CpuUsagePercent ?? 0;
   ```

### Option 2: Keep Reflection Layer (Current Solution)

- ? Works with current package (v0.0.2.119-beta)
- ? Will work with new package when published
- ?? Slower performance due to reflection
- ?? More complex code

## Verification

The models exist correctly in the source. You can verify by checking:

```bash
# Check generated client file
Get-Content "C:\Users\mbula\source\repos\mbulava-org\GameServer.Docker\src\GameServer.Docker.Client\GameServer.Docker.Client.v1.g.cs" | Select-String "class ServerResourceUsage" -Context 0,5

# Check for RealTimeStats property
Get-Content "C:\Users\mbula\source\repos\mbulava-org\GameServer.Docker\src\GameServer.Docker.Client\GameServer.Docker.Client.v1.g.cs" | Select-String "realTimeStats"

# Check for ContainerStats class
Get-Content "C:\Users\mbula\source\repos\mbulava-org\GameServer.Docker\src\GameServer.Docker.Client\GameServer.Docker.Client.v1.g.cs" | Select-String "class ContainerStats" -Context 0,5
```

All of these searches confirm the models exist in the current source.

## Recommendation

**Publish a new version of GameServer.Docker.Client** (e.g., v0.0.3 or v0.1.0) with the updated models, then update GameServer.Web to use it. This will:

1. ? Enable direct property access (better performance)
2. ? Simplify the ResourceMonitor code
3. ? Provide IntelliSense support
4. ? Enable compile-time type checking
5. ? Remove the reflection overhead

## Current Status

- **GameServer.Docker Source**: ? Models exist correctly
- **GameServer.Docker.Client.v1.g.cs**: ? Models are generated
- **Published NuGet Package**: ? Old version without models
- **GameServer.Web.csproj**: ?? Using old package (v0.0.2.119-beta)
- **ResourceMonitor.razor**: ? Using reflection compatibility layer as workaround

The reflection-based solution in ResourceMonitor is a **temporary workaround** that allows development to continue while waiting for the new package version to be published.
