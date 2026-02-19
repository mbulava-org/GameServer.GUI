# ServerLogsViewer - Improved Connection Pattern

## Problem
The ServerLogsViewer component was failing to connect and stream logs, showing "Container not found" errors even though the container was running and ResourceMonitor worked fine.

## Root Cause Analysis

### What Was Wrong
The original ServerLogsViewer had a **complex connection lifecycle**:
1. Called `InitializeHubConnectionAsync()` in `OnInitializedAsync()`
2. Then tried to call it again in `StartStreaming()` if disconnected
3. Connection setup was scattered across multiple methods
4. No proper logging to diagnose connection issues

### Why ResourceMonitor Works
ResourceMonitor follows a **simpler, cleaner pattern**:
1. **Connects to hub immediately** in `OnInitializedAsync()` 
2. **Separates connection from data subscription**
3. **Uses proper event handlers** for connection lifecycle
4. **Has comprehensive logging** at every step

## Solution

### Changed ServerLogsViewer to Match ResourceMonitor Pattern

**Before:**
```csharp
protected override async Task OnInitializedAsync()
{
    await InitializeHubConnectionAsync();  // Separate method
    if (!string.IsNullOrWhiteSpace(ServerId))
    {
        await StartStreaming();
    }
}

private async Task InitializeHubConnectionAsync()
{
    // ... connection setup in separate method
    // ... minimal logging
}
```

**After:**
```csharp
protected override async Task OnInitializedAsync()
{
    // Build hub URL and connect INLINE
    var apiBaseUri = ApiConfig.Value.BaseUri.TrimEnd('/');
    var hubUrl = $"{apiBaseUri}/hubs/serverlogs";
    
    _logger?.LogInformation("Connecting to ServerLogs hub at {HubUrl}", hubUrl);

    hubConnection = new HubConnectionBuilder()
        .WithUrl(hubUrl)
        .WithAutomaticReconnect(...)
        .ConfigureLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);  // ? ADD LOGGING
        })
        .Build();

    // Setup event handlers with better error messages
    hubConnection.Closed += error =>
    {
        if (error != null)
        {
            _logger?.LogError(error, "Hub connection closed with error");  // ? BETTER LOGGING
            // ... show error details in notification
        }
        // ...
    };

    try
    {
        await hubConnection.StartAsync();
        connectionStatus = "Connected";
        _logger?.LogInformation("Hub connection established successfully");  // ? CONFIRM SUCCESS
    }
    catch (Exception ex)
    {
        _logger?.LogError(ex, "Failed to connect to hub");  // ? LOG FAILURES
        // ... show error to user
    }

    // Auto-connect if requested
    if (AutoConnect && !string.IsNullOrWhiteSpace(ServerId))
    {
        await StartStreaming();
    }
}
```

### Key Improvements

1. **Inline Connection Setup** - Everything happens in `OnInitializedAsync()`, no separate method
2. **Comprehensive Logging** - Logs at every step: connecting, connected, errors, streaming start
3. **Added `ConfigureLogging`** - Hub connection now logs at Debug level
4. **Better Error Handling** - More detailed error messages in logs and notifications
5. **AutoConnect Parameter** - Added like ResourceMonitor for consistency
6. **Removed Duplicate Code** - Eliminated the separate `InitializeHubConnectionAsync()` method

### StartStreaming Improvements

**Added better logging:**
```csharp
private async Task StartStreaming()
{
    // ... connection check ...
    
    _logger?.LogInformation("Starting log stream for server {ServerId}", ServerId);  // ? LOG START
    
    await foreach (var logLine in hubConnection!.StreamAsync<string>(...))
    {
        // ... process logs ...
    }
}
catch (OperationCanceledException)
{
    _logger?.LogDebug("Log streaming cancelled for server {ServerId}", ServerId);  // ? LOG CANCELLATION
}
```

## Debugging Steps

When testing after restart, check these logs to diagnose issues:

### Expected Success Pattern
```
[INFO] Connecting to ServerLogs hub at http://localhost:5164/hubs/serverlogs
[INFO] Hub connection established successfully
[INFO] Starting log stream for server {serverId}
[DEBUG] Client {connectionId} starting log stream for server {serverId}
[INFO] Server {serverId} found: Name=..., Status=Running, ContainerId=abc123...
[INFO] Streaming logs for server {serverId}, container {containerId}
```

### If Container Not Found
```
[INFO] Server {serverId} found: Name=..., Status=Running, ContainerId=  ? Empty!
[WARN] Could not find running container for server {serverId}
```
**Fix:** Check that `DockerServiceHelper.TryCastGameServer` is populating ContainerId

### If Connection Fails
```
[ERROR] Failed to connect to hub
System.Net.Http.HttpRequestException: Connection refused
```
**Fix:** Check that GameServer.Docker API is running and hub URL is correct

### If Streaming Fails
```
[INFO] Starting log stream for server {serverId}
[ERROR] Failed to stream logs for server {serverId}
Microsoft.AspNetCore.SignalR.Client.HubException: ...
```
**Check:** The inner exception for the actual error

## Testing Checklist

After restarting the app:

- [ ] Open browser DevTools Console
- [ ] Navigate to Server Details page with a running server  
- [ ] Check Network tab - is there a WebSocket connection to `/hubs/serverlogs`?
- [ ] Click "Stream Logs" button
- [ ] Check browser console for any errors
- [ ] Check GameServer.Docker logs for the connection logs above
- [ ] Verify logs start streaming in the UI

## Comparison: ResourceMonitor vs ServerLogsViewer

| Aspect | ResourceMonitor | ServerLogsViewer (Fixed) |
|--------|----------------|-------------------------|
| Connection Setup | Inline in `OnInitializedAsync()` | ? Now inline too |
| Logging | Comprehensive Debug/Info logs | ? Now comprehensive |
| Error Handling | Detailed error messages | ? Now detailed |
| Event Handlers | Proper lifecycle events | ? Now proper |
| AutoConnect | Yes | ? Added |
| Connection Retry | Automatic reconnect | ? Already had it |

## Files Changed

1. **`src\GameServer.Web\Components\Server\ServerLogsViewer.razor`**
   - Moved connection setup inline to `OnInitializedAsync()`
   - Added `ConfigureLogging` with Debug level
   - Enhanced logging throughout
   - Added `AutoConnect` parameter
   - Removed duplicate `InitializeHubConnectionAsync()` method
   - Improved error messages in `StartStreaming()`

2. **`src\GameServer.Docker\Hubs\ServerLogsHub.cs`**
   - Added logging for server details
   - Fixed MultiplexedStream line reading

3. **`src\GameServer.Docker\Models\GameServer.cs`**
   - Added `ContainerId` property

4. **`src\GameServer.Docker\Services\DockerServiceHelper.cs`**
   - Populate `ContainerId` in `TryCastGameServer()`

## Next Steps

**You MUST restart the debugging session for changes to take effect:**

1. Stop debugging (Shift+F5)
2. Rebuild solution (Ctrl+Shift+B)  
3. Start debugging (F5)
4. Navigate to a running server's details
5. Watch the logs and try streaming

The enhanced logging will show exactly where the issue is if it still doesn't work.
