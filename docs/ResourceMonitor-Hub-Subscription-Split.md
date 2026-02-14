# ResourceMonitor - Hub Connection & Server Subscription Split ?

## What Changed

Refactored ResourceMonitor to **separate hub connection from server subscription** for cleaner lifecycle management.

### Old Behavior (Single Step)

```
OnAfterRenderAsync (if AutoConnect + ContainerId present)
    ?
ConnectAsync
    ??> Create client
    ??> Connect to hub
    ??> Subscribe to server
    ??> Get snapshot
```

**Problem**: Had to wait for ContainerId before connecting. Couldn't handle ContainerId changes.

### New Behavior (Two Steps)

```
OnInitializedAsync (if AutoConnect)
    ?
ConnectToHubAsync
    ??> Create client
    ??> Connect to hub
    ??> ? Ready for subscriptions

OnParametersSetAsync (when ContainerId available/changes)
    ?
SubscribeToServerFeedAsync
    ??> Subscribe to specific server
    ??> ? Start receiving real-time updates
```

**Benefits**: 
- ? Hub connects immediately
- ? Subscription happens when ContainerId is known
- ? Handles ContainerId changes gracefully
- ? Cleaner separation of concerns

## New Methods

### 1. ConnectToHubAsync() ?
**Purpose**: Connect to SignalR hub (no server subscription yet)

```csharp
private async Task ConnectToHubAsync()
{
    // Create client
    client = new ResourceMonitoringClient(hubUrl!);
    
    // Subscribe to events
    client.ResourceUpdateReceived += OnMetricsReceived;
    client.ErrorReceived += OnErrorReceived;
    
    // Connect to hub
    await client.ConnectAsync(connectionCts.Token);
    
    // If ContainerId already known, subscribe now
    if (!string.IsNullOrEmpty(ContainerId))
    {
        await SubscribeToServerFeedAsync();
    }
}
```

### 2. SubscribeToServerFeedAsync() ?
**Purpose**: Subscribe to a specific server's real-time feed

```csharp
private async Task SubscribeToServerFeedAsync()
{
    if (client == null || !client.IsConnected)
        return;
    
    // Subscribe to server with 1 second interval for real-time
    await client.SubscribeToServerAsync(ContainerId, intervalSeconds: 1, ...);
    
    // Now receiving live updates every second!
}
```

### 3. UnsubscribeFromCurrentAsync() ?
**Purpose**: Unsubscribe from current server (when changing servers)

```csharp
private async Task UnsubscribeFromCurrentAsync()
{
    await client.UnsubscribeAsync(CancellationToken.None);
    
    // Clear current metrics
    currentMetrics = null;
    cpuHistory.Clear();
    memoryHistory.Clear();
}
```

## Lifecycle Flow

### Initialization
```
1. Component created
2. OnInitializedAsync called
   ??> Build hub URL
   ??> If AutoConnect=true ? ConnectToHubAsync()
3. Hub connected ?
4. Waiting for ContainerId...
```

### When ContainerId Available
```
5. OnParametersSetAsync called (ContainerId now available)
6. SubscribeToServerFeedAsync()
7. Client subscribes to server's feed
8. Real-time updates start flowing! ?
```

### When ContainerId Changes
```
9. OnParametersSetAsync called (new ContainerId)
10. UnsubscribeFromCurrentAsync()
    ??> Unsubscribe from old server
11. SubscribeToServerFeedAsync()
    ??> Subscribe to new server
12. Now monitoring new server! ?
```

### Disconnect
```
13. DisconnectAsync()
14. UnsubscribeFromCurrentAsync()
    ??> Unsubscribe from feed
15. Disconnect from hub
16. Clean up ?
```

## UI Updates

### Header Status Badges

**When Hub Connected But Not Subscribed**:
```
[Connected] [?? Disconnect]
```

**When Subscribed to Server**:
```
[Monitoring: abc123...] [?? Refresh] [Live] [?? Disconnect]
```

**The "Live" badge pulses** ? Shows active real-time monitoring!

### Loading Overlay States

More granular states:

1. **Connecting** ? "Connecting to SignalR hub..."
2. **Not Connected** ? "Not connected" + "Will connect automatically"
3. **Waiting for ServerId** ? "Waiting for server ID..." + "No container to monitor"
4. **Subscribing** ? "Subscribing to server feed..." + Container ID
5. **Waiting for Data** ? "Waiting for first update..." + "Receiving real-time data"
6. **No Metrics** ? "No metrics available" + "Container may not be running"

## Real-Time Updates

### Subscription with 1 Second Interval ?

```csharp
await client.SubscribeToServerAsync(ContainerId, intervalSeconds: 1, ...);
```

**Result**: 
- Updates arrive **every second** automatically
- No need for manual refresh (refresh button is for on-demand snapshot)
- True real-time monitoring!

### Event Handler

```csharp
private void OnMetricsReceived(object? sender, ServerResourceUsage metrics)
{
    InvokeAsync(() =>
    {
        currentMetrics = metrics;
        lastUpdateTime = DateTime.Now;
        // Update history charts
        // StateHasChanged() ? UI updates
    });
}
```

**Fires every second** with new data!

## Key Benefits

### 1. Immediate Hub Connection ?
- Hub connects as soon as component initializes
- Don't wait for ContainerId
- Ready to subscribe immediately when ContainerId becomes available

### 2. Dynamic ContainerId Handling ?
- If ContainerId changes ? automatically resubscribe
- Smooth transition between servers
- No reconnection needed

### 3. Cleaner Separation ?
```
Hub Connection ? Infrastructure layer
Server Subscription ? Business logic layer
```

### 4. True Real-Time Monitoring ?
- 1 second update interval
- Continuous data flow
- No manual refresh needed (though available)

### 5. Better Error Handling ?
- Connection errors vs subscription errors
- Can retry subscription without reconnecting
- Clearer logging

## Files Changed

- ? `src/GameServer.Web/Components/Server/ResourceMonitor.razor`
  - Split ConnectAsync into ConnectToHubAsync + SubscribeToServerFeedAsync
  - Added UnsubscribeFromCurrentAsync
  - Updated OnInitializedAsync to connect immediately
  - Added OnParametersSetAsync to handle ContainerId changes
  - Updated UI to show subscription status
  - Added more granular loading overlay states

## Parameters

```csharp
[Parameter] public string? ContainerId { get; set; }
[Parameter] public string? Title { get; set; }
[Parameter] public bool AutoConnect { get; set; } = false;
[Parameter] public bool ShowHistory { get; set; } = true;
[Parameter] public int MaxHistoryPoints { get; set; } = 30;
```

**Usage**:
```razor
<ResourceMonitor ContainerId="@serverId" 
                Title="Real-Time Monitor"
                AutoConnect="true" 
                ShowHistory="true" />
```

## Expected Console Logs

```
ResourceMonitor initialized with hub URL: ws://192.168.10.50:5163/hubs/resources
Connecting to SignalR hub
Creating new ResourceMonitoringClient instance
Subscribing to client events
Events subscribed to own client instance
Connecting to SignalR hub
SignalR hub connected successfully
Subscribing to resource feed for container abc123
Successfully subscribed to resource feed for abc123

(Every second):
Resource update received for server abc123: CPU=15.5%, Memory=45.2%
Processing resource update in UI thread
Extracted metrics - CPU: 15.5, Memory: 45.2
Added to history. Total points: 5
HasValidMetrics: True, updating UI
```

## Comparison: Old vs New

### Old Approach
```
? Wait for ContainerId
? Connect + Subscribe together
? Can't handle ContainerId changes
? Manual refresh only
```

### New Approach
```
? Connect immediately
? Subscribe when ready
? Handle ContainerId changes
? Real-time updates (1s interval)
? Manual refresh available too
```

## Real-Time vs On-Demand

### Real-Time Feed (Primary)
- Continuous updates every 1 second
- Automatic via SignalR subscription
- Shows in charts with history
- "Live" badge pulses

### On-Demand Snapshot (Secondary)
- Refresh button available
- Gets single snapshot via GetSnapshotAsync
- Useful for immediate update without waiting
- Updates same metrics display

**Best of both worlds!** ?

## Summary

ResourceMonitor now:
1. ? Connects to hub immediately
2. ? Subscribes when ContainerId is available
3. ? Handles ContainerId changes dynamically
4. ? Receives real-time updates every second
5. ? Shows clear subscription status
6. ? Has granular loading states
7. ? Professional lifecycle management

---

**Status**: ? Refactored and Improved  
**Build**: ? Successful  
**Pattern**: Clean separation of concerns  
**Ready**: True real-time monitoring! ??
