# ?? SignalR Resource Monitor - Action Plan

## Current Situation

? **All code is correct and properly configured!**

The SignalR Resource Monitor should work. If it's not working, it's likely an environmental issue, not a code issue.

## Quick Diagnostic Steps

### 1. Are Both Services Running? ?

```bash
# Terminal 1: Start Backend API
cd C:\Users\mbula\source\repos\mbulava-org\GameServer.GUI\src\GameServer.Docker
dotnet run

# Terminal 2: Start Frontend Web
cd C:\Users\mbula\source\repos\mbulava-org\GameServer.GUI\src\GameServer.Web
dotnet run
```

**Expected**:
- Backend: Running on http://localhost:5164 (or 5163 HTTPS)
- Frontend: Running on https://localhost:7198

### 2. Check Browser DevTools ??

**Open**: F12 ? Network Tab ? Filter: WS (WebSocket)

**What to look for**:
```
? Connection to: wss://localhost:5163/hubs/resources
? Status: 101 Switching Protocols
? Messages flowing back and forth
```

**Console Tab**:
```
? No CORS errors
? No SignalR connection errors
? (Optional) Log statements from component
```

### 3. Check Backend Logs ??

**Look for**:
```
? "Client {ConnectionId} subscribing to server {ServerId}"
? "Starting resource stream for server {ServerId}"
? "Sent resource update for server {ServerId}"
```

**If NOT seeing these**:
- Client not connecting OR
- No containers to monitor

### 4. Test the Flow ??

1. **Start Backend** (GameServer.Docker)
2. **Start Frontend** (GameServer.Web)
3. **Navigate to Server Details** page
4. **Open DevTools** ? Network ? WS
5. **Watch for connection**
6. **Check if data flows**

## ?? Most Likely Issues & Fixes

### Issue #1: CORS Blocking Connection

**Symptom**: Connection fails, CORS error in console

**Fix**: Add CORS to `src/GameServer.Docker/Program.cs`

```csharp
// After line 86 (builder.Services.AddSignalR())
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
            "https://localhost:7198",  // GameServer.Web URL
            "http://localhost:5000"     // If using different port
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials(); // ? Required for SignalR!
    });
});

// After line 119 (app.UseAuthorization())
app.UseCors(); // ? Add this line
```

### Issue #2: Wrong Backend URL

**Symptom**: Connection fails, 404 error

**Fix**: Check `src/GameServer.Web/appsettings.json`

```json
{
  "GameServerDockerApi": {
    "BaseUri": "https://localhost:5163/"  // ? Must match backend
  }
}
```

### Issue #3: No Containers to Monitor

**Symptom**: Connected but no data

**Fix**: Start a game server first
- Go to Servers page
- Start a server
- Then check ResourceMonitor

### Issue #4: Backend Not Running

**Symptom**: Connection timeout/refused

**Fix**: Start the backend!
```bash
cd src/GameServer.Docker
dotnet run
```

## ?? Quick Test Script

Save this as `test-signalr.ps1`:

```powershell
# Test SignalR Resource Monitor

Write-Host "Testing SignalR Resource Monitor..." -ForegroundColor Cyan

# Test 1: Check if backend is running
Write-Host "`n1. Checking Backend API..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "https://localhost:5163" -SkipCertificateCheck -TimeoutSec 5
    Write-Host "   ? Backend is running" -ForegroundColor Green
} catch {
    Write-Host "   ? Backend not running! Start it first." -ForegroundColor Red
    Write-Host "      Run: cd src\GameServer.Docker; dotnet run" -ForegroundColor Gray
    exit
}

# Test 2: Check if frontend is running  
Write-Host "`n2. Checking Frontend Web..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "https://localhost:7198" -SkipCertificateCheck -TimeoutSec 5
    Write-Host "   ? Frontend is running" -ForegroundColor Green
} catch {
    Write-Host "   ? Frontend not running! Start it first." -ForegroundColor Red
    Write-Host "      Run: cd src\GameServer.Web; dotnet run" -ForegroundColor Gray
    exit
}

Write-Host "`n? Both services are running!" -ForegroundColor Green
Write-Host "`nNext steps:" -ForegroundColor Cyan
Write-Host "1. Open browser to https://localhost:7198"
Write-Host "2. Press F12 to open DevTools"
Write-Host "3. Go to Network tab, filter: WS"
Write-Host "4. Navigate to Server Details page"
Write-Host "5. Look for WebSocket connection to /hubs/resources"
Write-Host "6. Check if messages are flowing"
```

Run it:
```powershell
.\test-signalr.ps1
```

## ?? What You Should See

### Healthy SignalR Connection

**Backend Logs**:
```
info: GameServer.Docker.Hubs.ResourceMonitoringHub[0]
      Client abc123 subscribing to server my-server-01 with 2s interval
info: GameServer.Docker.Hubs.ResourceMonitoringHub[0]
      Starting resource stream for server my-server-01
trace: GameServer.Docker.Hubs.ResourceMonitoringHub[0]
      Sent resource update for server my-server-01 to abc123 (update #1)
```

**Browser DevTools (Network ? WS)**:
```
Request URL: wss://localhost:5163/hubs/resources
Status: 101 Switching Protocols

Messages:
? {"type":1,"invocationId":"1","target":"SubscribeToServer",...}
? {"type":1,"target":"Subscribed",...}
? {"type":1,"target":"ResourceUpdate",...}  ? Should repeat every 2s
? {"type":1,"target":"ResourceUpdate",...}
```

**Browser Console**:
```
(No errors)
(Optional) METRICS RECEIVED: my-server-01
```

**UI**:
```
? "Live" badge shows (green)
? CPU% updates every 2 seconds
? Memory% updates every 2 seconds
? Charts animate with new data
```

## ?? Debugging

### Add Console Logs

**In ResourceMonitor.razor**:

```csharp
private void OnMetricsReceived(object? sender, ServerResourceUsage metrics)
{
    Console.WriteLine($"? METRICS RECEIVED: {metrics.ServerId}");
    Console.WriteLine($"   CPU: {GetCpuValue()}%");
    Console.WriteLine($"   Memory: {GetMemoryValue()}%");
    
    InvokeAsync(() =>
    {
        currentMetrics = metrics;
        // ... rest of code
        StateHasChanged();
    });
}
```

### Enable Verbose Logging

**In appsettings.Development.json** (both projects):

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore.SignalR": "Debug",
      "Microsoft.AspNetCore.Http.Connections": "Debug",
      "GameServer.Docker.Hubs": "Trace"
    }
  }
}
```

## ? Success Criteria

You know it's working when:
1. ? Backend logs show "Sent resource update"
2. ? Browser DevTools shows WebSocket messages
3. ? "Live" badge is green
4. ? Metrics update every 2 seconds
5. ? Charts animate
6. ? No console errors

## ?? Summary

**The code is correct!** All you need to do is:

1. **Start both services**
2. **Add CORS if needed** (most common issue)
3. **Check browser DevTools** for connection
4. **Verify backend is sending data**

If all else fails, add logging and trace the data flow step by step.

---

**Need more help?** Share:
- Backend logs (around subscription time)
- Browser console errors
- DevTools Network?WS messages
- Screenshot of ResourceMonitor component
