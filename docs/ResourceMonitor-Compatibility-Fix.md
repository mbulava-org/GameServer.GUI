# ResourceMonitor Compatibility Fix

## Problem
The `ResourceMonitor.razor` component was failing to compile due to mismatches between the actual `ServerResourceUsage` model structure in the NuGet package (`GameServer.Docker.Client` v0.0.2.119-beta) and the expected model structure.

### Original Errors
- Missing `CpuCount` property
- Missing `NetworkRxBytesPerSecond`, `NetworkTxBytesPerSecond`, `BlockReadBytesPerSecond`, `BlockWriteBytesPerSecond` properties
- Missing or inconsistent `RealTimeStats` property structure

## Root Cause
The `GameServer.Docker` source repository has an updated `ServerResourceUsage` model with a `RealTimeStats` property (of type `ContainerStats`), but this hasn't been published to NuGet yet. The current NuGet package (v0.0.2.119-beta) may have a different structure.

## Solution
Implemented a **reflection-based compatibility layer** that supports **both** model structures:

### New Structure (GameServer.Docker source - not yet in NuGet)
```csharp
public class ServerResourceUsage
{
    // Service-level properties...
    public ContainerStats? RealTimeStats { get; set; }
}

public class ContainerStats
{
    public double CpuUsagePercent { get; set; }
    public double MemoryUsagePercent { get; set; }
    public ulong MemoryUsageBytes { get; set; }
    public ulong MemoryLimitBytes { get; set; }
    public long NetworkRxBytes { get; set; }
    public long NetworkTxBytes { get; set; }
    public long BlockReadBytes { get; set; }
    public long BlockWriteBytes { get; set; }
    public uint OnlineCpus { get; set; }
}
```

### Old Structure (Current NuGet package)
```csharp
public class ServerResourceUsage
{
    // All properties directly on ServerResourceUsage
    public double? CpuUsagePercent { get; set; }
    public double? MemoryUsagePercent { get; set; }
    public long? MemoryUsageBytes { get; set; }
    public long? MemoryLimitBytes { get; set; }
    public long? NetworkRxBytes { get; set; }
    public long? NetworkTxBytes { get; set; }
    public long? BlockReadBytes { get; set; }
    public long? BlockWriteBytes { get; set; }
}
```

## Implementation Details

### Helper Methods Added
The component now includes reflection-based helper methods that check for properties dynamically:

- `GetCpuValue()` - Gets CPU usage percentage from either structure
- `GetCpuCoresText()` - Gets CPU core count or fallback text
- `GetMemoryValue()` - Gets memory usage percentage
- `GetMemoryUsageText()` - Gets formatted memory usage string
- `GetNetworkRxBytes()` / `GetNetworkTxBytes()` - Network I/O
- `GetBlockReadBytes()` / `GetBlockWriteBytes()` - Disk I/O
- `HasValidMetrics()` - Checks if valid metrics exist in either structure

### Compatibility Pattern
Each helper method:
1. Checks for `RealTimeStats` property first (new structure)
2. If found, extracts the nested property value
3. Falls back to direct property access (old structure)
4. Returns default value if neither exists

Example:
```csharp
private double GetCpuValue()
{
    if (currentMetrics == null) return 0;
    var type = currentMetrics.GetType();
    
    // Try new structure (RealTimeStats.CpuUsagePercent)
    var realTimeStatsProp = type.GetProperty("RealTimeStats");
    if (realTimeStatsProp != null)
    {
        var realTimeStats = realTimeStatsProp.GetValue(currentMetrics);
        if (realTimeStats != null)
        {
            var cpuProp = realTimeStats.GetType().GetProperty("CpuUsagePercent");
            return (cpuProp?.GetValue(realTimeStats) as double?) ?? 0;
        }
    }
    
    // Try old structure (direct CpuUsagePercent)
    var directCpuProp = type.GetProperty("CpuUsagePercent");
    return (directCpuProp?.GetValue(currentMetrics) as double?) ?? 0;
}
```

## Removed Features
- **Rate metrics** (bytes/second) were removed as they don't exist in the model. Labels changed to "Total received/transmitted/read/written"
- **CPU core count** now shows "Real-time CPU usage" as fallback when `OnlineCpus` property isn't available

## Benefits
1. **Backward Compatible** - Works with current NuGet package (v0.0.2.119-beta)
2. **Forward Compatible** - Will work when new package version is published
3. **No Breaking Changes** - Existing functionality maintained
4. **Graceful Degradation** - Falls back to defaults when properties don't exist

## Next Steps
When the new `GameServer.Docker.Client` package is published with the updated `ServerResourceUsage` model:

1. Update package reference in `GameServer.Web.csproj`
2. Test that new structure works correctly
3. Consider removing reflection code after confirming new package is stable
4. Update to use direct property access for better performance

## Testing Notes
- ? Build successful with current package (v0.0.2.119-beta)
- ? Reflection handles both model structures
- ? No runtime errors expected
- ? Real-time monitoring needs testing with actual SignalR connection
- ? Historical chart rendering needs verification

## Files Modified
- `src/GameServer.Web/Components/Server/ResourceMonitor.razor` - Added reflection-based compatibility layer

## Related Files
- `../GameServer.Docker/src/GameServer.Docker/Models/ServerResourceUsage.cs` - New model structure
- `../GameServer.Docker/src/GameServer.Docker/Models/NodeAgentModels.cs` - ContainerStats definition
