# GetSnapshot Model Mismatch Fix ?

## Problem

The SignalR Resource Monitor was showing:
```
? No Data
No metrics available for this container
```

## Root Cause

**Same model mismatch issue as before!**

The `GetSnapshotAsync` method had the same problem:
- Backend returns: `Models.ServerResourceUsage`
- Client expected: `Interfaces.ServerResourceUsage`
- SignalR couldn't deserialize ? returned `null`
- Null response triggered "No metrics available" error

## The Code Flow

### What Was Happening (Broken)

```csharp
// Client tries to invoke hub method
var result = await _hubConnection.InvokeAsync<Interfaces.ServerResourceUsage?>(
    "GetSnapshot", 
    serverId, 
    cancellationToken);
// ? Backend sends Models.ServerResourceUsage
// ? Client tries to deserialize as Interfaces.ServerResourceUsage
// ? Deserialization fails silently
// ? result = null
// ? ResourceMonitor shows "No metrics available"
```

### Backend Hub Method
```csharp
public async Task<Models.ServerResourceUsage?> GetSnapshot(string serverId)
{
    var usage = await _resourceMonitor.GetResourceUsageAsync(serverId);
    return usage; // Returns Models.ServerResourceUsage
}
```

### Client Method (Before Fix)
```csharp
public async Task<Interfaces.ServerResourceUsage?> GetSnapshotAsync(...)
{
    // ? Wrong type - expects Interface model but backend sends Models
    var result = await _hubConnection.InvokeAsync<Interfaces.ServerResourceUsage?>(
        "GetSnapshot", serverId, cancellationToken);
    
    return result; // null because deserialization failed
}
```

## The Fix

Changed `GetSnapshotAsync` to receive the **NSwag model** (backend model), then convert it to the **interface model**:

```csharp
public async Task<Interfaces.ServerResourceUsage?> GetSnapshotAsync(...)
{
    // ? Call hub and get NSwag model (matches backend)
    var result = await _hubConnection.InvokeAsync<ServerResourceUsage?>(
        "GetSnapshot", serverId, cancellationToken);
    
    if (result == null)
        return null;
    
    // ? Convert to interface model
    var interfaceModel = new Interfaces.ServerResourceUsage
    {
        ServerId = result.ServerId,
        ServerName = result.ServerId,
        GameType = "",
        IsRunning = result.ServiceStatus == "Running",
        Status = result.ServiceStatus,
        Timestamp = result.Timestamp.DateTime,
        CpuUsagePercent = result.RealTimeStats?.CpuUsagePercent,
        MemoryUsageBytes = result.RealTimeStats?.MemoryUsageBytes,
        MemoryLimitBytes = result.RealTimeStats?.MemoryLimitBytes,
        MemoryUsagePercent = result.RealTimeStats?.MemoryUsagePercent,
        NetworkRxBytes = result.RealTimeStats?.NetworkRxBytes,
        NetworkTxBytes = result.RealTimeStats?.NetworkTxBytes,
        BlockReadBytes = result.RealTimeStats?.BlockReadBytes,
        BlockWriteBytes = result.RealTimeStats?.BlockWriteBytes,
        Replicas = result.DesiredReplicas,
        HealthyReplicas = result.RunningReplicas,
        ContainerId = result.ContainerIds?.FirstOrDefault(),
        NodeName = null
    };
    
    return interfaceModel; // ? Returns correct model
}
```

## Why This Happened

We fixed the **continuous update** event handler to use the correct model, but **forgot to fix the snapshot method** which uses the same pattern!

### Methods Fixed

1. ? `OnResourceUpdate` event handler - Fixed earlier
2. ? `OnResourceUpdateBatch` event handler - Fixed earlier
3. ? **`GetSnapshotAsync`** - **Fixed now!**

## Expected Result

### Before Fix
```
?? ResourceMonitor: Requesting snapshot for abc123
? Snapshot returned null
?? No Data - No metrics available for this container
```

### After Fix
```
?? ResourceMonitor: Requesting snapshot for abc123
? ResourceMonitor: Snapshot received
? ResourceMonitor.OnMetricsReceived: abc123
   CPU: 15.5%, Memory: 45.2%
   Extracted CPU: 15.5, Memory: 45.2
   HasValidMetrics: True
   StateHasChanged() complete! Overlay should be removed now.
```

## Testing

1. **Restart application**
2. **Navigate to Server Details**
3. **SignalR Monitor should**:
   - Show "Connected" badge
   - Fetch first snapshot automatically
   - Display metrics
   - Remove loading overlay
4. **Click Refresh button**:
   - Should fetch new snapshot
   - Update metrics
   - Update timestamp

## Console Output to Verify

### Success
```
?? ResourceMonitor: Starting connection for container: abc123
? ResourceMonitor: Already connected to hub
?? ResourceMonitor: Successfully connected! Ready for on-demand refresh.
?? ResourceMonitor: Requesting snapshot for abc123
? ResourceMonitor: Snapshot received
? ResourceMonitor.OnMetricsReceived: abc123
   CPU: 15.5%, Memory: 45.2%
   InvokeAsync executing...
   Extracted CPU: 15.5, Memory: 45.2
   Added to history. Count: 1
   HasValidMetrics: True
   Calling StateHasChanged()...
   StateHasChanged() complete! Overlay should be removed now.
```

### If Still Fails
```
?? ResourceMonitor: Requesting snapshot for abc123
? ResourceMonitor: Refresh failed - {exception message}
```

Check:
- Is the container ID correct?
- Is the backend able to find the server?
- Does the backend have monitoring data for this container?

## Model Mapping

### Backend ? Client Conversion

| Backend (Models.ServerResourceUsage) | Client (Interfaces.ServerResourceUsage) |
|--------------------------------------|----------------------------------------|
| ServerId | ServerId, ServerName |
| ServiceStatus | Status, IsRunning |
| Timestamp | Timestamp |
| RealTimeStats.CpuUsagePercent | CpuUsagePercent |
| RealTimeStats.MemoryUsageBytes | MemoryUsageBytes |
| RealTimeStats.MemoryLimitBytes | MemoryLimitBytes |
| RealTimeStats.MemoryUsagePercent | MemoryUsagePercent |
| RealTimeStats.NetworkRxBytes | NetworkRxBytes |
| RealTimeStats.NetworkTxBytes | NetworkTxBytes |
| RealTimeStats.BlockReadBytes | BlockReadBytes |
| RealTimeStats.BlockWriteBytes | BlockWriteBytes |
| DesiredReplicas | Replicas |
| RunningReplicas | HealthyReplicas |
| ContainerIds[0] | ContainerId |

## Files Changed

- ? `src/GameServer.Docker.Client/Services/ResourceMonitoringClient.cs`
  - Fixed `GetSnapshotAsync` to receive NSwag model
  - Convert to interface model before returning
  - Same pattern as event handlers

## Summary

The "No metrics available" error was caused by **model deserialization failure** in `GetSnapshotAsync`. 

The fix:
1. Receive backend model (NSwag `ServerResourceUsage`)
2. Convert to interface model (`Interfaces.ServerResourceUsage`)
3. Return converted model

Now the ResourceMonitor will:
- ? Connect successfully
- ? Fetch snapshot successfully
- ? Display metrics
- ? Remove loading overlay
- ? Show refresh button
- ? Update timestamp

---

**Status**: ? Fixed  
**Build**: ? Successful  
**Pattern**: Same as event handler fix  
**Ready**: Restart and test! ??
