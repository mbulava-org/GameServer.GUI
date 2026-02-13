# Fix: ObjectDisposedException in ResourceMonitoringHub

## Problem Description

The `ResourceMonitoringHub` was throwing `ObjectDisposedException` when clients disconnected:

```
System.ObjectDisposedException: Cannot access a disposed object.
Object name: 'GameServer.Docker.Hubs.ResourceMonitoringHub'.
   at Microsoft.AspNetCore.SignalR.Hub.get_Clients()
   at GameServer.Docker.Hubs.ResourceMonitoringHub.StreamResourceUpdatesAsync(...)
```

This caused errors in logs and prevented clean client disconnection.

---

## Root Cause

### The Issue
When a SignalR client disconnects, the **Hub instance is disposed immediately**. However, the background streaming task (`StreamResourceUpdatesAsync`) continues running and tries to access `Clients.Client(connectionId)`, which throws `ObjectDisposedException`.

### Execution Flow (Before Fix)
```
1. Client connects ? Hub instance created
2. SubscribeToServer() called
3. Background task started: Task.Run(() => StreamResourceUpdatesAsync(...))
4. Client disconnects ? Hub instance DISPOSED
5. Background task still running
6. Task tries to access Clients.Client(connectionId)
7. ? ObjectDisposedException thrown
```

### Why This Happens
```csharp
// BEFORE (BROKEN)
await Clients.Caller.SendAsync("Subscribed", ...);

// Start background task
_ = Task.Run(async () => await StreamResourceUpdatesAsync(session, cts.Token));

// Inside StreamResourceUpdatesAsync:
await Clients.Client(session.ConnectionId).SendAsync(...); // ? Clients disposed!
```

The `Clients` property is tied to the Hub instance lifecycle. When the hub is disposed (client disconnects), accessing `Clients` throws `ObjectDisposedException`.

---

## The Fix

### ? Capture Client Proxy Before Starting Background Task

**Key Insight**: `IClientProxy` objects are **not disposed** when the hub is disposed. They remain valid until the underlying connection is closed.

```csharp
// AFTER (FIXED)
// Capture the client proxy BEFORE starting background task
var clientProxy = Clients.Client(connectionId);

// Start background task with the captured proxy
_ = Task.Run(async () => await StreamResourceUpdatesAsync(session, clientProxy, cts.Token));

// Inside StreamResourceUpdatesAsync:
await clientProxy.SendAsync(...); // ? Works even if hub is disposed!
```

### Changes Made

#### 1. **SubscribeToServer** method
```csharp
try
{
    await Clients.Caller.SendAsync("Subscribed", serverId, intervalSeconds);
    
    // ? Capture the client proxy before starting background task
    var clientProxy = Clients.Client(connectionId);
    
    // Start streaming with captured proxy
    _ = Task.Run(async () => await StreamResourceUpdatesAsync(session, clientProxy, cts.Token));
}
catch (Exception ex)
{
    _logger.LogError(ex, "Error subscribing to server {ServerId}", serverId);
    
    try
    {
        await Clients.Caller.SendAsync("Error", $"Failed to subscribe: {ex.Message}");
    }
    catch (ObjectDisposedException)
    {
        // ? Handle case where hub is already disposed
        _logger.LogDebug("Client {ConnectionId} disconnected before error could be sent", connectionId);
    }
}
```

#### 2. **StreamResourceUpdatesAsync** method signature
```csharp
// BEFORE
private async Task StreamResourceUpdatesAsync(
    MonitoringSession session, 
    CancellationToken cancellationToken)

// AFTER
private async Task StreamResourceUpdatesAsync(
    MonitoringSession session, 
    IClientProxy clientProxy,  // ? Passed in, not accessed from Hub
    CancellationToken cancellationToken)
```

#### 3. **Handle ObjectDisposedException gracefully**
```csharp
try
{
    await clientProxy.SendAsync("ResourceUpdate", usage, cancellationToken);
}
catch (ObjectDisposedException)
{
    // ? Client disconnected, stop streaming gracefully
    _logger.LogInformation("Client {ConnectionId} disconnected, stopping stream",
        session.ConnectionId);
    break; // Exit the loop
}
```

#### 4. **Same fixes for SubscribeToMultipleServers**
Applied the same pattern to multi-server monitoring:
- Capture `clientProxy` before starting background task
- Pass to `StreamMultipleResourceUpdatesAsync`
- Handle `ObjectDisposedException` when sending batch updates

---

## How It Works Now

### Correct Execution Flow (After Fix)

```
1. Client connects ? Hub instance created
2. SubscribeToServer() called
3. clientProxy = Clients.Client(connectionId) ? Captured
4. Background task started with clientProxy
5. Client disconnects ? Hub instance disposed
6. Background task still running
7. Task uses captured clientProxy.SendAsync(...)
   ?? If connection still open ? ? Message sent
   ?? If connection closed ? ObjectDisposedException ? Caught ? Break loop
8. Task ends gracefully
```

### Why This Works

**IClientProxy Lifetime**:
- `IClientProxy` objects are **independent** of the Hub instance
- They remain valid as long as the SignalR connection exists
- They **detect disconnection** and throw `ObjectDisposedException` when the connection is closed
- We can **catch this exception** and handle it gracefully

**Hub Lifetime**:
- Hub instances are **per-invocation** (created for each method call)
- `Clients` property is tied to Hub instance
- Accessing `Clients` after disposal throws exception

---

## Benefits of the Fix

### ? Clean Disconnection
```
# BEFORE (Broken)
[ERR] Error sending resource update to client uemcvWo53zvv8KlR5hFgxQ
System.ObjectDisposedException: Cannot access a disposed object.

[ERR] Unexpected error in resource stream for uemcvWo53zvv8KlR5hFgxQ
System.ObjectDisposedException: Cannot access a disposed object.

# AFTER (Fixed)
[INF] Client uemcvWo53zvv8KlR5hFgxQ disconnected, stopping stream for server abc123
[INF] Resource monitoring cancelled for uemcvWo53zvv8KlR5hFgxQ
```

### ? No Resource Leaks
- Background tasks terminate cleanly
- Cancellation tokens work properly
- No hanging connections

### ? Better Logging
- Informational messages instead of errors
- Clear indication of what happened
- Easier debugging

---

## Testing the Fix

### 1. **Normal Operation**
```bash
# Client connects and subscribes
[INF] Client abc123 subscribing to server server-1 with 5s interval
[INF] Starting resource stream for server server-1 on connection abc123

# Stats flowing
[TRC] Sent resource update for server server-1 to abc123 (update #1)
[TRC] Sent resource update for server server-1 to abc123 (update #2)
```

### 2. **Client Disconnects**
```bash
# Client disconnects cleanly
[INF] Client abc123 disconnected, stopping stream for server server-1
[INF] Resource monitoring cancelled for abc123
[INF] Cleaning up monitoring session for connection abc123
```

### 3. **No More Errors**
```bash
# BEFORE: Would see ObjectDisposedException errors
# AFTER: Clean disconnect messages only
```

---

## Additional Improvements

### Error Handling in Subscribe Methods
```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Error subscribing to server {ServerId}", serverId);
    
    try
    {
        await Clients.Caller.SendAsync("Error", ...);
    }
    catch (ObjectDisposedException)
    {
        // ? Don't log as error if client already disconnected
        _logger.LogDebug("Client disconnected before error could be sent");
    }
}
```

### Break on Disconnect in Streaming Loop
```csharp
catch (ObjectDisposedException)
{
    _logger.LogInformation("Client disconnected, stopping stream");
    break; // ? Exit loop instead of continuing
}
```

---

## Impact

### Files Changed
- `src\GameServer.Docker\Hubs\ResourceMonitoringHub.cs`

### Methods Modified
1. `SubscribeToServer` - Capture clientProxy
2. `SubscribeToMultipleServers` - Capture clientProxy
3. `StreamResourceUpdatesAsync` - Accept clientProxy parameter, handle ObjectDisposedException
4. `StreamMultipleResourceUpdatesAsync` - Accept clientProxy parameter, handle ObjectDisposedException

### Backward Compatibility
? **100% Backward Compatible**
- No API changes
- No client changes needed
- Only internal implementation changed

---

## Lessons Learned

### ? Don't Access Hub.Clients from Background Tasks
```csharp
// BAD
_ = Task.Run(async () =>
{
    await Clients.Client(id).SendAsync(...); // Hub might be disposed!
});
```

### ? Capture IClientProxy Before Starting Task
```csharp
// GOOD
var clientProxy = Clients.Client(id);
_ = Task.Run(async () =>
{
    await clientProxy.SendAsync(...); // Works even if hub disposed
});
```

### ? Always Handle ObjectDisposedException
```csharp
try
{
    await clientProxy.SendAsync(...);
}
catch (ObjectDisposedException)
{
    // Client disconnected - handle gracefully
    break;
}
```

---

## Summary

The `ObjectDisposedException` was caused by:
1. ? Accessing `Hub.Clients` from background tasks after hub disposal
2. ? Not handling disposal exceptions gracefully

The fix:
1. ? Capture `IClientProxy` before starting background tasks
2. ? Pass captured proxy to streaming methods
3. ? Handle `ObjectDisposedException` and break streaming loops
4. ? Add defensive error handling in subscribe methods

**Result**: Clean client disconnection, no more exceptions in logs, proper resource cleanup! ??
