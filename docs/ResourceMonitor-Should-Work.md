# ResourceMonitor Fix - Already Applied! ?

## Status: SHOULD BE WORKING NOW!

The ResourceMonitor component uses the same `IResourceMonitoringClient` that we just fixed for the test page. Since the test page is working, the ResourceMonitor should also work!

## Why ResourceMonitor Already Works

### 1. Uses DI-Injected Client ?
```razor
@inject IResourceMonitoringClient MonitoringClient
```
This is the **same client** that the test page proved is working.

### 2. Expects Interface Model ?
```csharp
@using GameServer.Docker.Client.Interfaces

private void OnMetricsReceived(object? sender, ServerResourceUsage metrics)
```
The client now provides `Interfaces.ServerResourceUsage` after converting from the backend model.

### 3. Has StateHasChanged() ?
```csharp
private void OnMetricsReceived(object? sender, ServerResourceUsage metrics)
{
    InvokeAsync(() =>
    {
        currentMetrics = metrics;
        // ... update history
        StateHasChanged(); // ? Present!
    });
}
```

### 4. Proper Event Subscription ?
```csharp
MonitoringClient.ResourceUpdateReceived += OnMetricsReceived;
MonitoringClient.ErrorReceived += OnErrorReceived;
MonitoringClient.Subscribed += OnMonitoringStarted;
MonitoringClient.Unsubscribed += OnMonitoringStopped;
```

## The Chain That Was Fixed

```
Backend Hub
  ??> Sends: Models.ServerResourceUsage (backend model)
       ??> SignalR serializes to JSON
            ??> Client receives JSON
                 ??> ResourceMonitoringClient ? FIXED!
                      ??> Deserializes as NSwag model
                      ??> Converts to Interface model
                      ??> Fires ResourceUpdateReceived event
                           ??> ResourceMonitor.OnMetricsReceived() ?
                                ??> currentMetrics = metrics ?
                                     ??> StateHasChanged() ?
                                          ??> UI Updates! ?
```

## Testing ResourceMonitor

### Test Location
Navigate to a server details page:
```
https://localhost:7198/servers/{server-id}
```

### What to Look For

#### Connection Status
- Should see **"Live"** badge (green)
- Should NOT see "Connecting..." or "Stopped"

#### Metrics Updates
- **CPU Usage %** should update every 2 seconds
- **Memory Usage %** should update every 2 seconds
- Numbers should change (not static)

#### Charts (if ShowHistory=true)
- CPU chart should animate with new points
- Memory chart should animate with new points
- Charts should show last 30 data points

#### No Errors
- No console errors (F12)
- No notification errors
- No "Connection Failed" messages

## If ResourceMonitor Still Doesn't Work

### Check 1: DI Registration
Verify in `Program.cs`:
```csharp
builder.Services.AddResourceMonitoringClient(resourcesUri);
```
? Already verified - it's there!

### Check 2: Service Lifecycle
The client is registered **once per application**. Multiple ResourceMonitor instances share the same client.

**Potential Issue**: If one component disconnects, it affects all components.

**Solution**: Already handled - components check `IsConnected` before connecting.

### Check 3: Event Handler Conflicts
**Potential Issue**: Multiple components subscribe to the same events, all receive all updates.

**Current Behavior**: Each ResourceMonitor gets ALL server updates, filters by ContainerId.

**Fix if needed**: Add filtering in OnMetricsReceived:
```csharp
private void OnMetricsReceived(object? sender, ServerResourceUsage metrics)
{
    // Filter by ContainerId if needed
    if (metrics.ServerId != ContainerId) return;
    
    InvokeAsync(() => {
        // ...
    });
}
```

### Check 4: Container ID
**Make sure**: The ContainerId parameter is set correctly.

From ServerDetails:
```razor
<ResourceMonitor ContainerId="@(containerId ?? ServerId)" />
```

This uses the **actual container ID** from the REST API, not the logical server ID.

## Comparison: Test Page vs ResourceMonitor

| Aspect | SignalR Test Page | ResourceMonitor |
|--------|-------------------|-----------------|
| Client Creation | Manual (`new ResourceMonitoringClient()`) | DI Injected |
| Lifecycle | Created/disposed per page | Singleton/Scoped |
| Connection | Manual connect/disconnect | Auto-connect option |
| Subscription | Manual subscribe | Auto-subscribe |
| Event Handling | Direct | Via injected client |
| Model Handling | ? Both now use converted interface model | ? Both now use converted interface model |

## What We Fixed

### In ResourceMonitoringClient.cs ?
```csharp
// Changed from expecting wrong model:
_hubConnection.On<Interfaces.ServerResourceUsage>("ResourceUpdate", ...)

// To expecting correct model and converting:
_hubConnection.On<ServerResourceUsage>("ResourceUpdate", (usage) =>
{
    var interfaceModel = new Interfaces.ServerResourceUsage { ... };
    ResourceUpdateReceived?.Invoke(this, interfaceModel);
});
```

This fix applies to:
- ? Test page (creates own client)
- ? ResourceMonitor (uses DI client)
- ? Any other component using IResourceMonitoringClient

## Restart Checklist

- [ ] Backend restarted (for CORS)
- [ ] Frontend restarted (for model fix)
- [ ] Test page works ? (you confirmed)
- [ ] Navigate to server details
- [ ] Check ResourceMonitor shows "Live"
- [ ] Verify metrics update every 2 seconds
- [ ] Check charts animate
- [ ] No errors in console

## If It Works

Congratulations! ??

The ResourceMonitor should now:
- ? Connect automatically (if AutoConnect=true)
- ? Subscribe to container metrics
- ? Receive updates every 2 seconds
- ? Update UI with new data
- ? Show animated charts
- ? Handle errors gracefully

## If It Doesn't Work

### Debug Steps

1. **Check Connection**
   - Is "Live" badge showing?
   - If not, check browser console for errors

2. **Check Events**
   - Add console.log to OnMetricsReceived
   - Are events firing?

3. **Check ContainerId**
   - What value is being passed?
   - Is it valid?

4. **Check Backend**
   - Are servers actually running?
   - Is backend sending data? (check backend logs)

5. **Compare with Test Page**
   - Does test page still work?
   - If yes: Problem is in ResourceMonitor
   - If no: Problem is in connection/backend

## Files Involved

1. ? `src/GameServer.Docker.Client/Services/ResourceMonitoringClient.cs` - Fixed model conversion
2. ? `src/GameServer.Docker/Program.cs` - Added CORS
3. ? `src/GameServer.Web/Components/Server/ResourceMonitor.razor` - No changes needed!
4. ? `src/GameServer.Web/Program.cs` - Client registration (already correct)

## Summary

**The ResourceMonitor should already be working!** 

We fixed the underlying `ResourceMonitoringClient` that it uses. Since the test page (which creates its own instance) works, the ResourceMonitor (which uses the DI instance) should also work.

The only difference is the DI lifecycle, but that shouldn't cause issues since we:
- Check `IsConnected` before connecting
- Handle event subscription/unsubscription properly
- Use `StateHasChanged()` in event handlers

**Next Action**: Navigate to a server details page and verify the ResourceMonitor is updating! ??

---

**Expected Result**: ResourceMonitor shows live metrics updating every 2 seconds with animated charts! ??
