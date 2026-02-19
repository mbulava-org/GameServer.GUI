# Diagnostic Steps for Log Streaming Issue

## 1. Check Browser Console

Open browser DevTools (F12) and look for:

### Expected if working:
```
[Information] Connecting to ServerLogs hub at http://...
[Information] Hub connection established successfully  
[Information] Starting log stream for server {serverId}
```

### If you see errors like:
```
Error: Failed to complete negotiation with the server: Error: ...
WebSocket connection to 'ws://...' failed
```

This means the SignalR connection itself is failing.

## 2. Check Network Tab

1. Open Network tab in DevTools
2. Click "Stream Logs"
3. Look for a request to `/hubs/serverlogs/negotiate`
4. Check its status:
   - **101 Switching Protocols** = Good! WebSocket connected
   - **404 Not Found** = Hub endpoint not registered
   - **500 Server Error** = Server-side error

## 3. Check Server Logs

In your GameServer.Docker console output, look for:

### When client connects:
```
[INFO] Client {connectionId} starting log stream for server {serverId}
[INFO] Server {serverId} found: Name=..., Status=..., ContainerId=...
```

### If ContainerId is empty:
```
[INFO] Server {serverId} found: Name=..., Status=Running, ContainerId=
```
**This means the ContainerId is NULL** - the issue is in DockerServiceHelper.TryCastGameServer

### If you see:
```
[WARN] Could not find running container for server {serverId}
```
This confirms ContainerId is null/empty.

## 4. Quick API Test

Test if the server API returns ContainerId:

```powershell
# Replace with your actual server ID
curl http://localhost:5164/api/gameserver/{serverId}
```

Look in the response for `"containerId": "abc123..."` - if it's null or missing, that's the problem.

## 5. Common Issues & Fixes

### Issue: ContainerId is NULL
**Cause:** Server list was cached before we added ContainerId property

**Fix:**
```powershell
# Restart the GameServer.Docker API completely
# Stop debugging and start again
```

### Issue: WebSocket connection refused
**Cause:** SignalR hub not properly registered or CORS issue

**Check Program.cs has:**
```csharp
app.MapHub<ServerLogsHub>("/hubs/serverlogs");
app.UseCors();
```

### Issue: 404 on /hubs/serverlogs
**Cause:** Hub not registered or wrong URL

**Check:**
1. Is `app.MapHub<Hubs.ServerLogsHub>("/hubs/serverlogs");` in Program.cs?
2. Is the URL correct in ServerLogsViewer? Should be: `${apiBaseUri}/hubs/serverlogs`

### Issue: MultiplexedStream error
**Cause:** Container exists but Docker API call fails

**Check Docker logs:**
```powershell
docker service logs gameserver-docker
```

## 6. Test with cURL

Test the hub endpoint directly:

```powershell
# Test SignalR negotiate
curl -v http://localhost:5164/hubs/serverlogs/negotiate

# Should return JSON with connectionId and availableTransports
```

## 7. Enable Detailed Client Logging

The ServerLogsViewer already has `.ConfigureLogging(LogLevel.Debug)` enabled, so check the browser console for detailed SignalR client logs.

## 8. Test ResourceMonitor

If ResourceMonitor works but ServerLogsViewer doesn't:
1. Both use the same base URL configuration
2. Both connect to similar hubs
3. The difference must be in the streaming method or hub implementation

Try this:
- Does ResourceMonitor connect successfully?
- What's the URL it connects to? (Check Network tab)
- Compare to ServerLogsViewer URL

## Quick Diagnostic Command

Run this in PowerShell to check everything:

```powershell
# Check if API is running
Invoke-WebRequest -Uri "http://localhost:5164/api/gameserver" -Method GET

# Check if hub negotiate works
Invoke-WebRequest -Uri "http://localhost:5164/hubs/serverlogs/negotiate" -Method POST

# Check SignalR hub is registered
Get-Content "src\GameServer.Docker\Program.cs" | Select-String -Pattern "MapHub.*ServerLogsHub"
```

## What to Share if Still Broken

Please provide:
1. **Browser console output** (all errors)
2. **Network tab** (screenshot of failed request)
3. **GameServer.Docker console logs** (the startup and when you try to connect)
4. **Output of API test:** `curl http://localhost:5164/api/gameserver/{serverId} | ConvertFrom-Json | Select-Object containerId`

This will help identify exactly where the connection is failing.
