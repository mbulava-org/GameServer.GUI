# SignalR Connection Issue - Fix Applied

## Problem
SignalR Test Page can't connect to the backend.

## Root Cause
**CORS (Cross-Origin Resource Sharing) not configured** on the backend API.

When the Blazor frontend (running on one port) tries to connect to SignalR backend (on different port/host), the browser blocks the WebSocket connection unless CORS is explicitly allowed.

## Fix Applied

### 1. Added CORS to Backend ?

**File**: `src/GameServer.Docker/Program.cs`

```csharp
// After builder.Services.AddSignalR():
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(origin => true) // Allow any origin
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // ? Required for SignalR!
    });
});

// After app.UseHttpsRedirection():
app.UseCors(); // ? Enable CORS middleware
```

### 2. Fixed WebSocket URL ?

**File**: `src/GameServer.Web/Components/Pages/SignalRTest.razor`

```csharp
// Convert http/https to ws/wss for WebSocket
var wsUri = baseUri.Replace("https://", "wss://").Replace("http://", "ws://");
hubUrl = $"{wsUri}/hubs/resources";
```

### 3. Enhanced Error Messages ?

Added detailed error logging to help diagnose issues:
- HTTP request errors
- Timeout errors
- CORS detection
- Connection diagnostics

## How to Test

### Step 1: Restart Backend API

**The backend MUST be restarted for CORS to take effect!**

```bash
# Stop the backend if running
# Then start it:
cd C:\Users\mbula\source\repos\mbulava-org\GameServer.GUI\src\GameServer.Docker
dotnet run
```

Look for startup message:
```
info: GameServer.Docker[0]
      Starting GameServer.Docker Version - X.X.X
```

### Step 2: Restart Frontend

```bash
# Stop the frontend (Shift+F5)
# Then start it (F5)
```

Or:
```bash
cd C:\Users\mbula\source\repos\mbulava-org\GameServer.GUI\src\GameServer.Web
dotnet run
```

### Step 3: Test Connection

1. Navigate to: `https://localhost:7198/signalr-test`
2. Check the log messages:
   ```
   [INFO] Test page initialized. Base URI: http://192.168.10.50:5163/
   [INFO] SignalR Hub URL: ws://192.168.10.50:5163/hubs/resources
   ```
3. Click **"Connect"** button
4. Watch the log for success or detailed error messages

## Expected Result

### ? Success Log

```
[INFO] Creating ResourceMonitoringClient...
[INFO] Target: ws://192.168.10.50:5163/hubs/resources
[INFO] Connecting to SignalR hub...
[INFO] If this hangs, check:
[INFO]   1. Backend is running
[INFO]   2. Hub URL is correct
[INFO]   3. CORS is configured
[INFO]   4. Firewall allows connection
[SUCCESS] ? Connected successfully!
[INFO] Connection ID established with hub
```

## If Still Failing

### Check 1: Backend is Running

```bash
# Check if backend is accessible
curl http://192.168.10.50:5163

# Should return API response, not "Connection refused"
```

### Check 2: Hub Endpoint Exists

```bash
# SignalR hubs use WebSocket upgrade
# You won't get a normal HTTP response, but shouldn't get 404
curl http://192.168.10.50:5163/hubs/resources
```

### Check 3: Firewall

```bash
# On backend machine, check if port is listening
netstat -an | findstr 5163

# Should show:
# TCP    0.0.0.0:5163           0.0.0.0:0              LISTENING
```

### Check 4: Browser Console

Open browser DevTools (F12):

**Console Tab** - Look for:
```
? WebSocket connection failed
? CORS error
? Failed to connect to ws://...
```

**Network Tab** - Filter: WS
```
Should see WebSocket connection attempt
Status should be "101 Switching Protocols" if successful
```

## Error Messages Explained

### "Network error: Connection refused"
- **Cause**: Backend not running or wrong URL
- **Fix**: Start backend, verify URL in appsettings

### "Connection timeout"
- **Cause**: Backend not responding, firewall blocking
- **Fix**: Check backend logs, verify firewall rules

### "CORS ERROR DETECTED"
- **Cause**: Backend doesn't have CORS configured
- **Fix**: Already applied! Restart backend.

### "Access-Control-Allow-Origin"
- **Cause**: CORS policy doesn't allow credentials
- **Fix**: Already applied! Restart backend.

## Configuration Reference

### Your Current Setup

**Backend**: `http://192.168.10.50:5163/`  
**Frontend**: `https://localhost:7198/`  
**Hub URL**: `ws://192.168.10.50:5163/hubs/resources`

**Note**: Using `http` for backend, `https` for frontend is normal in development.

### appsettings.Development.json

```json
{
  "GameServerDockerApi": {
    "BaseUri": "http://192.168.10.50:5163/"
  }
}
```

## Production CORS Configuration

For production, replace the permissive CORS with specific origins:

```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
            "https://your-production-domain.com",
            "https://localhost:7198" // Keep for local development
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});
```

## Testing Checklist

- [ ] Backend restarted
- [ ] Frontend restarted
- [ ] Test page shows correct hub URL
- [ ] Click "Connect" button
- [ ] Connection succeeds
- [ ] No CORS errors in browser console
- [ ] Can subscribe to server
- [ ] Data flows correctly

## Files Modified

1. **src/GameServer.Docker/Program.cs** - Added CORS configuration
2. **src/GameServer.Web/Components/Pages/SignalRTest.razor** - Enhanced diagnostics

## Status

? **CORS Configuration Added**  
? **Enhanced Error Messages Added**  
? **WebSocket URL Fixed**  
?? **MUST RESTART BACKEND AND FRONTEND**  

## Summary

The connection failure was due to **missing CORS configuration** on the backend. This is a common issue when:
- Frontend and backend on different ports
- Frontend and backend on different domains
- WebSocket connections (SignalR)

The fix allows the browser to make cross-origin WebSocket connections with credentials (required for SignalR).

**Restart both services and test again!** The enhanced error messages will now help identify any remaining issues.

---

**Next Action**: Restart backend ? Restart frontend ? Test `/signalr-test` ? Report results
