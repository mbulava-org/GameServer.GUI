# Log Streaming Debug Analysis - 2026-02-18

## Issues Found in Logs

### Issue 1: ServerLogsHub Times Out (PRIMARY ISSUE)

**Symptoms:**
```
[20:54:49] ServerLogsHub: Connected to Node Agent, streaming logs
[20:55:49] HTTP GET /hubs/serverlogs responded 101 in 60124ms  ? 60 second timeout!
```

**Analysis:**
- ? Hub finds server correctly
- ? Hub finds container ID: `9b3da5f0d7985afa89be0226d1c99a7d391aff543accc3ecdc543749b70737b2`
- ? Hub finds Node Agent: `http://172.16.1.84:8080`
- ? Hub connects to Node Agent's SignalR hub
- ? **No logs are streamed** - connection sits idle for 60 seconds then times out

**Root Cause:**
The Node Agent's `StreamContainerLogs` is being called but not returning any data. This could be:
1. ContainerService.StreamContainerLogsAsync() is not yielding any lines
2. The Docker container logs API call is timing out
3. The logs are being read but not properly split into lines

**Expected Logs (Missing):**
```
[Node Agent] Client {id} starting log stream for container {id}
[Node Agent] Reading container logs...
[Node Agent] Yielding log line...
```

### Issue 2: ContainerTerminal Wrong Hub (404)

**Symptoms:**
```
[20:55:50] HTTP POST /hubs/terminal/negotiate responded 404
```

**Analysis:**
- ? ContainerTerminal trying to connect to `/hubs/terminal`
- ? No such hub exists (only `/hubs/console`, `/hubs/serverlogs`, `/hubs/resources`)

**Root Cause:**
ContainerTerminal has wrong hub URL. Should be `/hubs/console` to use ContainerConsoleHub.

## Solutions

### Fix 1: Debug Node Agent Log Streaming

**Check Node Agent logs for errors:**
```bash
docker service logs gameserver-docker-agent
```

**Look for:**
- Errors calling Docker API for logs
- Exceptions in StreamContainerLogsAsync
- Connection issues

**Possible fixes:**

**A) Add more logging to Node Agent:**
```csharp
// In GameServer.Docker.Agent\Services\ContainerService.cs
public async IAsyncEnumerable<string> StreamContainerLogsAsync(...)
{
    _logger.LogInformation("Starting log stream for container {ContainerId}", containerId);
    
    try
    {
        var logStream = await _dockerClient.Containers.GetContainerLogsAsync(...);
        _logger.LogDebug("Got log stream from Docker");
        
        // ... stream logs ...
        
        yield return line;
        _logger.LogTrace("Yielded log line");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error streaming logs");
        throw;
    }
}
```

**B) Check if MultiplexedStream is being read correctly:**

The Node Agent might have the same issue ServerLogsHub had - not properly handling line breaks in the MultiplexedStream.

### Fix 2: Register Terminal Hub OR Fix ContainerTerminal URL

**Option A: Register missing /hubs/terminal** (if Terminal should be separate from Console)

In `GameServer.Docker\Program.cs`:
```csharp
app.MapHub<Hubs.TerminalHub>("/hubs/terminal");
```

Then create TerminalHub.cs for exec operations.

**Option B: Fix ContainerTerminal to use /hubs/console** (simpler)

ContainerTerminal should use ContainerConsoleHub which already handles both attach and exec.

## Action Plan

### Immediate Actions

1. **Get Node Agent logs** to see why no logs are streaming
```bash
docker service logs gameserver-docker-agent --tail 100 --follow
```

2. **Test Node Agent directly** 
```bash
curl http://172.16.1.84:8080/api/containers/9b3da5f0d7985afa89be0226d1c99a7d391aff543accc3ecdc543749b70737b2/logs?tail=10
```

3. **Check if container actually has logs**
```bash
docker logs 9b3da5f0d7985afa89be0226d1c99a7d391aff543accc3ecdc543749b70737b2 --tail 10
```

### If Node Agent Logs Are Empty

This means the Node Agent's ContainerService.StreamContainerLogsAsync() has a bug. Need to:
1. Check the MultiplexedStream reading logic
2. Ensure lines are being properly split and yielded
3. Add error handling and logging

### If Logs Exist But Not Streaming

This means there's a SignalR streaming issue. Need to:
1. Check SignalR connection stays open
2. Verify `IAsyncEnumerable` is working across SignalR
3. Check for buffering issues

## Quick Test

**Test Node Agent HTTP endpoint first (non-streaming):**
```powershell
$uri = "http://172.16.1.84:8080/api/containers/9b3da5f0d7985afa89be0226d1c99a7d391aff543accc3ecdc543749b70737b2/logs?tail=10"
Invoke-RestMethod -Uri $uri
```

If this returns logs, then the issue is in the SignalR streaming.
If this doesn't return logs, then the issue is in ContainerService reading Docker logs.

## Current State

? **Working:**
- Server discovery
- Container ID lookup  
- Node Agent discovery
- SignalR connection establishment
- ResourceMonitor (uses stats, not logs)

? **Not Working:**
- Log streaming from Node Agent
- Terminal connection (wrong hub URL)

## Next Steps

1. Check Node Agent logs for errors
2. Test Node Agent HTTP endpoint directly
3. Fix ContainerService if it's not reading logs
4. Fix ContainerTerminal hub URL
5. Re-test

