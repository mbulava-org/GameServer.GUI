# ResourceMonitor Diagnostic Logging Added ?

## Problem
Data is coming through SignalR but the ResourceMonitor component isn't updating the UI.

## Solution Applied
Added **comprehensive console logging** matching the working SignalR test page to diagnose exactly where the issue is.

## Logging Added

### Connection Process
```csharp
?? ResourceMonitor: Starting connection for container: {ContainerId}
?? ResourceMonitor: Subscribing to events...
? ResourceMonitor: Events subscribed
?? ResourceMonitor: Connecting to hub...
? ResourceMonitor: Hub connected
?? ResourceMonitor: Subscribing to server {ContainerId} with {UpdateIntervalSeconds}s interval...
? ResourceMonitor: Subscribed to server
?? ResourceMonitor: Successfully connected and monitoring {ContainerId}
```

### Event Handler
```csharp
? ResourceMonitor.OnMetricsReceived: {ServerId}
   CPU: {CpuUsagePercent}%, Memory: {MemoryUsagePercent}%
   IsConnected: {isConnected}, HasMetrics: {currentMetrics != null}
   InvokeAsync executing...
   Extracted CPU: {cpu}, Memory: {memory}
   Added to history. Count: {cpuHistory.Count}
   Calling StateHasChanged()...
   StateHasChanged() complete!
```

### Error Cases
```csharp
? ResourceMonitor: No container ID provided
?? ResourceMonitor: Already connected or connecting
? ResourceMonitor: Connection failed - {exception details}
```

## How to Use

1. **Open Browser DevTools**: Press F12
2. **Go to Console Tab**: Clear existing messages
3. **Navigate to Server Details**: Watch console output
4. **Look for**:
   - Connection messages (?? ?? ?)
   - Event firing messages (? ResourceMonitor.OnMetricsReceived)
   - StateHasChanged calls
   - Any error messages (?)

## Diagnostic Scenarios

### Scenario 1: Events Not Firing At All

**Console shows**:
```
?? ResourceMonitor: Starting connection...
?? ResourceMonitor: Subscribing to events...
? ResourceMonitor: Events subscribed
? ResourceMonitor: Subscribed to server
(nothing else)
```

**Diagnosis**: Backend not sending data OR subscription failed
**Check**: 
- Backend logs (is it sending?)
- SignalR test page (does it work?)
- Container ID (is it correct?)

### Scenario 2: Events Fire But UI Doesn't Update

**Console shows**:
```
? ResourceMonitor.OnMetricsReceived: server-01
   CPU: 15.5%, Memory: 45.2%
   InvokeAsync executing...
   Extracted CPU: 15.5, Memory: 45.2
   Added to history. Count: 5
   Calling StateHasChanged()...
   StateHasChanged() complete!
(repeats every 2 seconds)
```

**Diagnosis**: Events work, StateHasChanged called, but UI frozen
**Possible causes**:
- Loading overlay covering metrics (check z-index)
- Conditional rendering preventing display
- Component not in render tree
- Browser rendering issue

**Fix**: Check if overlay condition is correct

### Scenario 3: InvokeAsync Never Executes

**Console shows**:
```
? ResourceMonitor.OnMetricsReceived: server-01
   CPU: 15.5%, Memory: 45.2%
(no InvokeAsync message)
```

**Diagnosis**: InvokeAsync failed or component disposed
**Check**:
- Component lifecycle (is it disposed?)
- Exceptions in InvokeAsync (check browser errors)

### Scenario 4: Wrong Container ID

**Console shows**:
```
? ResourceMonitor.OnMetricsReceived: different-server-id
   CPU: 15.5%, Memory: 45.2%
```

**Diagnosis**: Receiving metrics for wrong server
**Cause**: All components using shared DI client receive ALL events
**Fix**: Add filtering by ContainerId

### Scenario 5: No Metrics in Data

**Console shows**:
```
? ResourceMonitor.OnMetricsReceived: server-01
   CPU: %, Memory: %
   Extracted CPU: 0, Memory: 0
```

**Diagnosis**: Data arrives but has no actual metrics
**Check**: Backend model conversion (CpuUsagePercent, MemoryUsagePercent)

## Comparing with Test Page

### Test Page (Working)
```csharp
private void OnResourceUpdate(object? sender, ServerResourceUsage usage)
{
    InvokeAsync(() =>
    {
        latestData = usage;
        AddLog("DATA", $"?? ResourceUpdate: {usage.ServerId}");
        StateHasChanged();
    });
}
```

### ResourceMonitor (Now Matching)
```csharp
private void OnMetricsReceived(object? sender, ServerResourceUsage metrics)
{
    Console.WriteLine($"? ResourceMonitor.OnMetricsReceived: {metrics.ServerId}");
    InvokeAsync(() =>
    {
        currentMetrics = metrics;
        // ... update history
        StateHasChanged();
    });
}
```

**Pattern is identical!** Both use:
1. Console logging
2. InvokeAsync
3. Update data
4. StateHasChanged()

## Expected Output

### Healthy Flow
```
?? ResourceMonitor: Starting connection for container: abc123
?? ResourceMonitor: Subscribing to events...
? ResourceMonitor: Events subscribed
? ResourceMonitor: Already connected to hub
?? ResourceMonitor: Subscribing to server abc123 with 2s interval...
? ResourceMonitor: Subscribed to server
?? ResourceMonitor: Successfully connected and monitoring abc123

(after 2 seconds)
? ResourceMonitor.OnMetricsReceived: abc123
   CPU: 15.5%, Memory: 45.2%
   IsConnected: true, HasMetrics: False
   InvokeAsync executing...
   Extracted CPU: 15.5, Memory: 45.2
   Added to history. Count: 1
   Calling StateHasChanged()...
   StateHasChanged() complete!

(after 2 more seconds)
? ResourceMonitor.OnMetricsReceived: abc123
   CPU: 15.7%, Memory: 45.3%
   IsConnected: true, HasMetrics: True
   InvokeAsync executing...
   Extracted CPU: 15.7, Memory: 45.3
   Added to history. Count: 2
   Calling StateHasChanged()...
   StateHasChanged() complete!

(continues every 2 seconds)
```

## Potential Issues to Look For

### Issue 1: Multiple Components
If multiple ResourceMonitor components are on the page, all receive events:
```
? ResourceMonitor.OnMetricsReceived: abc123  (Component 1)
? ResourceMonitor.OnMetricsReceived: abc123  (Component 2)
? ResourceMonitor.OnMetricsReceived: abc123  (Component 3)
```

**Solution**: Add filtering by ContainerId in event handler

### Issue 2: Event Subscription Conflicts
If component reconnects without unsubscribing:
```
? ResourceMonitor.OnMetricsReceived: abc123
? ResourceMonitor.OnMetricsReceived: abc123  (duplicate!)
```

**Solution**: Always unsubscribe in DisconnectAsync/DisposeAsync

### Issue 3: Loading Overlay Blocking UI
Even if data updates, overlay might be visible:
```
(metrics updating in background)
(but overlay says "Waiting for data...")
```

**Check**: Console shows `HasMetrics: True` but overlay still visible?
**Fix**: Check overlay condition logic

## Next Steps

1. **Restart application** (Shift+F5, F5)
2. **Open DevTools console** (F12)
3. **Navigate to server details page**
4. **Watch console output**
5. **Report findings**:
   - Are events firing?
   - Is InvokeAsync executing?
   - Is StateHasChanged() being called?
   - What do you see in the UI?

## Files Modified

- ? `src/GameServer.Web/Components/Server/ResourceMonitor.razor`
  - Added console logging to OnMetricsReceived
  - Added console logging to ConnectAsync
  - Matches test page logging pattern

## Status

? **Build Successful**  
? **Diagnostic Logging Added**  
?? **Restart Required**  
?? **Ready to Debug**

The logging will reveal exactly where the data flow breaks!

---

**Action**: Restart app, open console, navigate to server details, and share console output!
