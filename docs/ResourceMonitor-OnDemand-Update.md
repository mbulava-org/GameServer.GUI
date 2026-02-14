# ResourceMonitor - On-Demand Refresh Update ?

## Changes Made

Converted the ResourceMonitor from **continuous auto-updating** to **on-demand manual refresh**.

### What Changed

#### 1. Removed Continuous Monitoring ?
**Before**: Component subscribed to live updates every 2 seconds
**After**: Component connects once, then fetches snapshots on-demand

#### 2. Added Refresh Button ?
- New "Refresh" button (?? icon)
- Disabled while refreshing to prevent double-clicks
- Only visible when connected and has metrics

#### 3. Added Last Update Timestamp ?
- Shows "Last update: HH:mm:ss"
- Updates with each refresh
- Helps users know data freshness

#### 4. Updated Connection Logic ?
- Only subscribes to error events (not continuous updates)
- Connects to hub
- Auto-fetches first snapshot after connecting
- Ready for manual refreshes

#### 5. Loading Overlay Fixed ?
- Overlay condition: `!isConnected || currentMetrics == null || !HasValidMetrics()`
- Automatically removed when `currentMetrics` is set and `HasValidMetrics()` returns true
- Added debug logging to verify removal

## User Experience

### Before (Continuous)
```
1. Component connects
2. Subscribes to live updates
3. Receives data every 2 seconds automatically
4. Network traffic constant
5. Backend streaming continuously
```

### After (On-Demand)
```
1. User clicks "Connect"
2. Component connects to hub
3. First snapshot fetched automatically
4. Overlay disappears when data arrives
5. User clicks "Refresh" when needed
6. Only fetches data when requested
7. Reduced network traffic
8. Reduced backend load
```

## UI Layout

```
????????????????????????????????????????????????????????????
? ???  Real-Time Resource Monitor (SignalR)               ?
?                                                          ?
?  [Last update: 14:35:21]  [?? Refresh] [? Connected] [?? Disconnect]  ?
????????????????????????????????????????????????????????????
?                                                          ?
?   CPU Usage         Memory Usage      Network I/O        ?
?   ??????????       ??????????        RX: 1.2 MB        ?
?   ?  15.5% ?       ?  45.2% ?        TX: 800 KB        ?
?   ??????????       ??????????                           ?
?                                        Disk I/O          ?
?                                        Read: 500 KB      ?
?                                        Write: 200 KB     ?
????????????????????????????????????????????????????????????
```

## Button States

### Disconnected
```
[?? Connect]
```
- Text: "Disconnected" badge (red)
- Action: Connects to hub and fetches first snapshot

### Connected (No Data Yet)
```
[?? Refresh] [? Connected] [?? Disconnect]
```
- Refresh button disabled (no data to refresh)
- Shows loading overlay

### Connected (With Data)
```
[Last update: 14:35:21]  [?? Refresh] [? Connected] [?? Disconnect]
```
- All buttons enabled
- Shows last update time
- Refresh button fetches new snapshot

### Refreshing
```
[Last update: 14:35:21]  [?? Refresh (disabled)] [? Connected] [?? Disconnect]
```
- Refresh button temporarily disabled
- Prevents multiple concurrent requests

## Technical Implementation

### Connection (No Subscription)
```csharp
private async Task ConnectAsync()
{
    // Only subscribe to errors (not continuous updates)
    MonitoringClient.ErrorReceived += OnErrorReceived;
    
    // Connect to hub
    await MonitoringClient.ConnectAsync(connectionCts.Token);
    
    // Fetch first snapshot
    await RefreshMetricsAsync();
}
```

### Refresh (On-Demand Snapshot)
```csharp
private async Task RefreshMetricsAsync()
{
    isRefreshing = true;
    
    // Get one-time snapshot
    var snapshot = await MonitoringClient.GetSnapshotAsync(ContainerId, CancellationToken.None);
    
    if (snapshot != null)
    {
        // Process as if it were a continuous update
        OnMetricsReceived(this, snapshot);
    }
    
    isRefreshing = false;
}
```

### Overlay Removal
```razor
@if (!isConnected || currentMetrics == null || !HasValidMetrics())
{
    <div class="loading-overlay">
        <!-- Overlay content -->
    </div>
}
```

When `RefreshMetricsAsync()` succeeds:
1. `OnMetricsReceived()` is called
2. `currentMetrics = metrics` (no longer null)
3. `HasValidMetrics()` returns true
4. Overlay condition becomes false
5. Overlay removed from DOM
6. Metrics visible! ?

## Benefits

### 1. Reduced Network Traffic ?
- No continuous WebSocket messages
- Data fetched only when needed
- Bandwidth friendly

### 2. Reduced Backend Load ?
- No constant metric streaming
- Snapshots generated on-demand
- Scales better with many users

### 3. User Control ?
- Users decide when to refresh
- No "stale data worry" (timestamp shown)
- Clear action button

### 4. Battery Friendly ?
- Less background activity
- Lower CPU usage
- Better for mobile/laptop

### 5. Better for Debugging ?
- Console logs show each refresh
- Easy to verify overlay removal
- Clear state transitions

## Console Output

### Connect Flow
```
?? ResourceMonitor: Starting connection for container: abc123
?? ResourceMonitor: Subscribing to error events...
? ResourceMonitor: Error events subscribed
? ResourceMonitor: Already connected to hub
?? ResourceMonitor: Successfully connected! Ready for on-demand refresh.
?? ResourceMonitor: Requesting snapshot for abc123
? ResourceMonitor: Snapshot received
? ResourceMonitor.OnMetricsReceived: abc123
   CPU: 15.5%, Memory: 45.2%
   IsConnected: True, HasMetrics: False
   InvokeAsync executing...
   Extracted CPU: 15.5, Memory: 45.2
   Added to history. Count: 1
   HasValidMetrics: True
   Calling StateHasChanged()...
   StateHasChanged() complete! Overlay should be removed now.
```

### Manual Refresh
```
?? ResourceMonitor: Requesting snapshot for abc123
? ResourceMonitor: Snapshot received
? ResourceMonitor.OnMetricsReceived: abc123
   (same as above)
```

## REST API vs SignalR Monitor

### Separation Maintained ?

**REST API Monitor** (above):
- Uses HTTP polling
- Shows basic metrics
- Simple display
- Always visible

**SignalR Monitor** (below):
- Uses SignalR snapshots
- Shows detailed real-time metrics
- Rich gauges and charts
- On-demand refresh
- Title: "Real-Time Resource Monitor (SignalR)"

Both monitors can coexist:
- REST API for quick overview
- SignalR for detailed on-demand metrics

## Files Changed

- ? `src/GameServer.Web/Components/Server/ResourceMonitor.razor`
  - Removed continuous subscription
  - Added refresh button
  - Added last update timestamp
  - Changed title to clarify it's SignalR
  - Added `RefreshMetricsAsync()` method
  - Removed `UpdateIntervalSeconds` parameter
  - Added `isRefreshing` state
  - Added `lastUpdateTime` field
  - Updated connection to only subscribe to errors
  - Auto-fetches first snapshot after connecting

## Testing

### Test Scenario 1: First Connection
1. Navigate to server details page
2. ResourceMonitor shows with "Disconnected" badge
3. Click "Connect" button
4. **Expect**: 
   - Badge changes to "Connected"
   - Loading overlay shows "Waiting for data..."
   - First snapshot fetched automatically
   - Overlay disappears
   - Metrics visible
   - Last update time shown

### Test Scenario 2: Manual Refresh
1. After connected with data visible
2. Click "Refresh" button
3. **Expect**:
   - Button temporarily disabled
   - New snapshot fetched
   - Metrics update
   - Last update time updates
   - Button enabled again

### Test Scenario 3: Disconnect
1. While connected
2. Click "Disconnect" button
3. **Expect**:
   - Badge changes to "Disconnected"
   - Metrics cleared
   - Last update time cleared
   - Refresh button hidden
   - Loading overlay shows "Not connected"

## Overlay Removal Verification

The loading overlay will be removed when:
1. ? `isConnected` is true
2. ? `currentMetrics` is not null
3. ? `HasValidMetrics()` returns true

Debug in console:
```
HasValidMetrics: True
Calling StateHasChanged()...
StateHasChanged() complete! Overlay should be removed now.
```

If overlay doesn't disappear, check console for:
- `HasValidMetrics: False` (data has no valid metrics)
- `currentMetrics` still null (snapshot failed)
- `isConnected: False` (connection lost)

## Summary

**ResourceMonitor is now on-demand!**

? No continuous updates (reduced load)  
? Refresh button for manual updates  
? Last update timestamp shown  
? First snapshot auto-fetched on connect  
? Loading overlay properly removed  
? Clear separation from REST API monitor  
? Better user control  
? Better performance  

---

**Status**: ? Implemented  
**Build**: ? Successful  
**Mode**: On-Demand Refresh  
**Ready**: Yes! ??
