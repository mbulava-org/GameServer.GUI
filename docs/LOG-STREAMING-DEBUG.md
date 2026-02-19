# Log Streaming Debug Guide

## Issue
"Container not found. The server may be stopped or not yet started." error when trying to stream logs, but Refresh works fine.

## Root Cause
- **Refresh** uses `GetServiceLogsAsync(serverId)` which queries by service name (works)
- **Stream** uses `StreamServerLogs(serverId)` which needs the container ID (may be null)

## Why ContainerId Might Be Null

1. **Server model was loaded before we added ContainerId property**
   - The API client wasn't regenerated yet
   - The server list in UI is cached

2. **Container is still starting**
   - ContainerId is only populated when a task is in Running/Starting/Preparing state
   - If the container just started, the task might not be ready yet

## How to Debug

### Step 1: Check if ContainerId is populated
1. Open browser DevTools (F12)
2. Go to Network tab
3. Find the API call to `/api/gameserver` or `/api/dashboard/servers`
4. Check the response - does the server object have a `containerId` field?
5. If no: The API client needs to be regenerated
6. If yes but null: The container might not be running yet

### Step 2: Check server logs
Look for this log message in GameServer.Docker logs:
```
Server {ServerId} found: Name={Name}, Status={Status}, ServiceName={ServiceName}, ContainerId={ContainerId}
```

This will tell you:
- Is the server being found?
- What's the current status?
- Is ContainerId populated?

### Step 3: Check container status
```bash
docker service ps <service-name> --no-trunc
```

Look for the container ID in the output. If there's a container ID but it's not in the API response, the issue is in `TryCastGameServer`.

## Quick Fixes

### Fix 1: Restart the debugging session
**The app is running in debug mode, so code changes aren't applied yet.**

1. Stop debugging
2. Rebuild solution
3. Start debugging again
4. Navigate to server details
5. Try log streaming

### Fix 2: Force reload server data
1. Navigate away from the server details page
2. Navigate back
3. This will reload the server data with the new ContainerId

### Fix 3: Check if API client was regenerated
```bash
# Rebuild the client to regenerate from OpenAPI spec
dotnet build src\GameServer.Docker.Client\GameServer.Docker.Client.csproj
```

Then check if `src\GameServer.Docker.Client\GameServer.Docker.Client.v1.g.cs` contains the `ContainerId` property:
```bash
Get-Content src\GameServer.Docker.Client\GameServer.Docker.Client.v1.g.cs | Select-String -Pattern "ContainerId"
```

## Expected Log Output (Success)

When streaming works correctly, you should see:
```
[INFO] Client {ConnectionId} starting log stream for server {ServerId}
[INFO] Server {ServerId} found: Name=..., Status=Running, ServiceName=..., ContainerId=abc123...
[INFO] Streaming logs for server {ServerId}, container {ContainerId}
```

## Expected Log Output (Failure - ContainerId Null)

When ContainerId is null:
```
[INFO] Client {ConnectionId} starting log stream for server {ServerId}
[INFO] Server {ServerId} found: Name=..., Status=Running, ServiceName=..., ContainerId=
[WARN] Could not find running container for server {ServerId}. Server state: Running, ServiceName: ...
```

## Code Changes Made

### 1. Added ContainerId to GameServer model
```csharp
public class GameServer
{
    // ... other properties
    public string? ContainerId { get; set; }
}
```

### 2. Populated ContainerId in DockerServiceHelper.TryCastGameServer
```csharp
var activeTask = tasks
    .Where(t => activeStates.Contains(t.Status?.State ?? TaskState.Shutdown))
    .OrderByDescending(t => t.UpdatedAt)
    .FirstOrDefault();

item.ContainerId = activeTask?.Status?.ContainerStatus?.ContainerID;
```

### 3. Updated ServerLogsHub to use ContainerId
```csharp
var containerId = server.ContainerId;
if (string.IsNullOrEmpty(containerId))
{
    // Fallback to other lookup methods
}
```

### 4. Fixed MultiplexedStream reading
Changed from reading chunks to properly handling line breaks:
- Accumulates bytes in a StringBuilder
- Splits by newlines
- Yields complete lines only

## Next Steps

**Since the app is running in debug mode:**
1. **Stop debugging**
2. **Rebuild solution** - this will regenerate the API client with ContainerId
3. **Start debugging again**
4. **Navigate to a server's details page**
5. **Try the log streaming**

If it still doesn't work after restart, check the logs for the "Server found" message to see if ContainerId is being populated.
