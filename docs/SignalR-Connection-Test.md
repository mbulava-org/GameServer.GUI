# SignalR Connection Test Guide

**API Server:** `http://192.168.10.50:5163/` (Beta Port)  
**Date:** 2025

## Quick Test Checklist

### 1. Verify API Server is Running

Test basic API endpoints:

```bash
# Test dashboard endpoint
curl http://192.168.10.50:5163/api/dashboard/servers

# Test game types endpoint
curl http://192.168.10.50:5163/api/gametypes

# Test specific server endpoint (replace with actual server ID)
curl http://192.168.10.50:5163/api/servers/{server-id}
```

**Expected:** All should return 200 OK with JSON data

### 2. Test SignalR Hub Endpoints

SignalR hubs require a "negotiate" handshake before establishing connection:

```bash
# Test Resource Monitoring Hub
curl -X POST http://192.168.10.50:5163/hubs/resources/negotiate

# Test Container Console Hub
curl -X POST http://192.168.10.50:5163/hubs/console/negotiate
```

**Expected Results:**

? **If hubs are configured:** Returns 200 OK with JSON containing `connectionId`, `availableTransports`, etc.

```json
{
  "connectionId": "abc123...",
  "connectionToken": "xyz789...",
  "negotiateVersion": 1,
  "availableTransports": [
    {
      "transport": "WebSockets",
      "transferFormats": ["Text", "Binary"]
    }
  ]
}
```

? **If hubs are NOT configured:** Returns 404 Not Found

```
Cannot find the endpoint
```

### 3. Test from Blazor UI

#### Test ResourceMonitor Component

1. Navigate to a server details page that includes the ResourceMonitor component
2. Click "Start monitoring" button
3. Observe the connection status badge

**Success Indicators:**
- Badge changes from "Stopped" ? "Connecting..." ? "Live"
- CPU, Memory, Network, and Disk gauges start displaying data
- Green notification: "Monitoring Started - Now monitoring {container-id}"
- Data updates every 2 seconds (or configured interval)

**Failure Indicators:**
- Badge shows "Connecting..." then returns to "Stopped"
- Red notification: "Connection Failed" with error message
- Browser console shows: `Response status code does not indicate success: 404 (Not Found)`

#### Test ContainerConsole Component

1. Navigate to `/servers/{server-id}/console`
2. Click "Connect" button
3. Observe the terminal

**Success Indicators:**
- Badge changes from "Disconnected" ? "Connecting..." ? "Connected"
- Terminal shows: "Connected to {server-id} console"
- Terminal displays container output
- You can type commands and see output
- Green notification: "Console Connected"

**Failure Indicators:**
- Badge shows "Connecting..." then returns to "Disconnected"
- Red notification: "Connection Failed" with error message
- Terminal shows: "Connection failed: Response status code does not indicate success: 404 (Not Found)"

## Browser Developer Tools Inspection

### Check Network Tab

1. Open Browser Developer Tools (F12)
2. Go to Network tab
3. Attempt to connect ResourceMonitor or ContainerConsole

**Look for these requests:**

```
POST http://192.168.10.50:5163/hubs/resources/negotiate
POST http://192.168.10.50:5163/hubs/console/negotiate
```

**Success:** Status 200, Response contains SignalR connection info

**Failure:** Status 404, Response body says "Cannot find the endpoint"

### Check Console Tab

**If 404 Error:**
```
Microsoft.AspNetCore.Http.Connections.Client.HttpConnection: Error: 
Failed to start the connection: Error: Response status code does not 
indicate success: 404 (Not Found).
```

**If Connection Success:**
```
Microsoft.AspNetCore.SignalR.Client.HubConnection: Information: 
HubConnection created.

Microsoft.AspNetCore.SignalR.Client.HubConnection: Information: 
HubConnection connected successfully.
```

## Current Configuration

### Client Configuration (Blazor App)

**Location:** `src/GameServer.Web/appsettings.Development.json`

```json
{
  "GameServerDockerApi": {
    "BaseUri": "http://192.168.10.50:5163/"
  }
}
```

### Component Hub URL Construction

**ResourceMonitor.razor:**
```csharp
var baseUri = ApiConfig.Value.BaseUri?.TrimEnd('/') ?? "http://localhost:5164";
var hubUrl = $"{baseUri}/hubs/resources";
// Result: http://192.168.10.50:5163/hubs/resources
```

**ContainerConsole.razor:**
```csharp
var baseUri = ApiConfig.Value.BaseUri?.TrimEnd('/') ?? "http://localhost:5164";
var hubUrl = $"{baseUri}/hubs/console";
// Result: http://192.168.10.50:5163/hubs/console
```

## What SignalR Hubs Should Do

### ResourcesHub (`/hubs/resources`)

**Purpose:** Stream real-time container resource metrics

**Client ? Server Methods:**
- `SubscribeToServer(string serverId, int intervalSeconds)` - Start monitoring
- `SubscribeToMultipleServers(string[] serverIds, int intervalSeconds)` - Monitor multiple
- `GetSnapshot(string serverId)` - One-time metrics
- `UpdateInterval(int intervalSeconds)` - Change update frequency
- `Unsubscribe()` - Stop monitoring

**Server ? Client Methods:**
- `ReceiveResourceUpdate(ServerResourceUsage usage)` - Periodic metrics update
- `OnSubscribed(string serverId, int intervalSeconds)` - Confirmation
- `OnUnsubscribed()` - Unsubscribe confirmation
- `OnError(string error)` - Error messages

**Metrics Streamed:**
- CPU usage percentage
- Memory usage (bytes and percentage)
- Network I/O (RX/TX bytes and rates)
- Disk I/O (read/write bytes and rates)
- Container health status
- Replica count

### ConsoleHub (`/hubs/console`)

**Purpose:** Provide interactive terminal access to containers

**Client ? Server Methods:**
- `AttachToContainer(string containerId)` - Connect to container shell
- `SendInput(string input)` - Send command/input to container stdin
- `ExecCommand(string containerId, string command, string[] args)` - Execute one-off command
- `DisconnectFromContainer()` - Close terminal session

**Server ? Client Methods:**
- `ReceiveOutput(string output)` - Container stdout
- `ReceiveError(string error)` - Container stderr or hub errors
- `OnConnected(string containerId)` - Attachment confirmation
- `OnDisconnected()` - Disconnection confirmation
- `ReceiveCommandOutput(string output)` - ExecCommand results

**Features:**
- Full bidirectional communication
- Real-time stdout/stderr streaming
- Interactive command execution
- Persistent connection per container

## Troubleshooting

### Issue: 404 Not Found

**Cause:** SignalR hubs not mapped on API server

**Solution:** On the API server project, ensure `Program.cs` includes:

```csharp
// Add SignalR services
builder.Services.AddSignalR();

// Map hub endpoints (after app.Build())
app.MapHub<ResourcesHub>("/hubs/resources");
app.MapHub<ConsoleHub>("/hubs/console");
```

### Issue: 401 Unauthorized

**Cause:** Hub requires authentication but client doesn't have credentials

**Solution:** Add authentication to SignalR clients:

```csharp
var consoleClient = new ContainerConsoleClient(hubUrl);
// Configure authentication before connecting
```

### Issue: Connection Timeout

**Cause:** Network issues or server overload

**Solution:** 
- Check firewall rules for port 5163
- Verify API server is accessible from client machine
- Check server logs for errors

### Issue: Connection Established but No Data

**Cause:** Hub methods not implemented or not being called

**Solution:**
- Verify hub server code implements all required methods
- Check server logs for exceptions
- Ensure background services are running (for resource monitoring)

## Expected Behavior Summary

| Feature | Endpoint | Status Should Be | Data Flow |
|---------|----------|-----------------|-----------|
| REST APIs | `/api/*` | ? Working (200 OK) | Request ? Response |
| Resource Hub | `/hubs/resources` | ? Check with negotiate | Bidirectional streaming |
| Console Hub | `/hubs/console` | ? Check with negotiate | Bidirectional interactive |

## Next Steps

1. **Run the curl tests** above to verify hub endpoints
2. **Test from UI** using the steps in section 3
3. **Review server logs** for any SignalR-related errors
4. **If 404 errors persist**, configure SignalR hubs on API server

---

**Note:** The client-side Blazor components are fully implemented and ready to use. Once the SignalR hubs are confirmed working on the API server, both ResourceMonitor and ContainerConsole will function without any code changes.
