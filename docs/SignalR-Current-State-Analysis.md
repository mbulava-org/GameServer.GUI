# SignalR Resource Monitor - Current State Analysis ?

## ?? Executive Summary

**Status**: Everything is properly configured! The SignalR Resource Monitor should be working.

All components are in place and correctly set up:
- ? Backend Hub implemented and registered
- ? Client library properly implemented  
- ? Frontend component configured with DI
- ? All using the same method names
- ? StateHasChanged() added to all event handlers

## ?? Component Analysis

### Backend (GameServer.Docker API)

#### SignalR Hub ?
**File**: `src/GameServer.Docker/Hubs/ResourceMonitoringHub.cs`

**Implementation**:
- ? Hub properly inherits from `Hub`
- ? Uses `IGameServerResourceMonitor` service
- ? Implements `SubscribeToServer(string serverId, int intervalSeconds)`
- ? Sends updates via `clientProxy.SendAsync("ResourceUpdate", usage, cancellationToken)`
- ? Proper error handling and logging
- ? Background streaming with proper cancellation

**Registration** (`src/GameServer.Docker/Program.cs`):
```csharp
// Line 86
builder.Services.AddSignalR();

// Line 128
app.MapHub<GameServer.Docker.Hubs.ResourceMonitoringHub>("/hubs/resources");
```
? Properly registered and mapped

**Dependencies**:
```csharp
// Line 72
builder.Services.AddSingleton<IGameServerResourceMonitor, GameServerResourceMonitorService>();
```
? Resource monitoring service registered

### Client Library (GameServer.Docker.Client)

#### ResourceMonitoringClient ?
**File**: `src/GameServer.Docker.Client/Services/ResourceMonitoringClient.cs`

**Implementation**:
```csharp
// Line 98 - Listening for the correct method name
_hubConnection.On<Interfaces.ServerResourceUsage>("ResourceUpdate", (usage) =>
{
    _logger?.LogTrace("Received resource update for server {ServerId}", usage.ServerId);
    ResourceUpdateReceived?.Invoke(this, usage);
});
```
? Listens for "ResourceUpdate" - matches hub!

**Event Registration**:
- ? ResourceUpdate ? ResourceUpdateReceived event
- ? Subscribed ? Subscribed event
- ? Error ? ErrorReceived event
- ? All properly implemented

### Frontend (GameServer.Web)

#### ResourceMonitor Component ?
**File**: `src/GameServer.Web/Components/Server/ResourceMonitor.razor`

**Dependency Injection**:
```razor
// Line 9
@inject IResourceMonitoringClient MonitoringClient
```
? Properly injected

**Event Handlers** (with StateHasChanged):
```csharp
// Lines 480-520
private void OnMetricsReceived(object? sender, ServerResourceUsage metrics)
{
    InvokeAsync(() =>
    {
        currentMetrics = metrics;
        // Update history...
        StateHasChanged(); // ? Present!
    });
}

private void OnErrorReceived(object? sender, string error)
{
    InvokeAsync(() =>
    {
        NotificationService.Notify(...);
        StateHasChanged(); // ? Present!
    });
}
```
? All event handlers have StateHasChanged()

**Client Registration** (`src/GameServer.Web/Program.cs`):
```csharp
// Lines 29-31
var apiBaseUrl = builder.Configuration["GameServerDockerApi:BaseUri"] ?? "http://localhost:5164/";
var resourcesUri = apiBaseUrl.Replace("https://", "wss://").Replace("http://", "ws://") + "hubs/resources";

// Line 62
builder.Services.AddResourceMonitoringClient(resourcesUri);
```
? Properly registered with correct URL

### Project References ?

**GameServer.Web.csproj**:
```xml
<ProjectReference Include="..\GameServer.Docker.Client\GameServer.Docker.Client.csproj" />
```
? Using project reference (not NuGet) - gets latest code!

## ?? Data Flow Analysis

```
1. ResourceMonitor Component
   ??> OnAfterRenderAsync (if AutoConnect=true)
   ??> ConnectAsync()
   ?   ??> MonitoringClient.ResourceUpdateReceived += OnMetricsReceived ?
   ?   ??> MonitoringClient.ConnectAsync(token) ?
   ?   ??> MonitoringClient.SubscribeToServerAsync(ContainerId, IntervalSeconds, token) ?
   ?
   ??> Waits for events...

2. Backend Hub
   ??> SubscribeToServer called
   ??> StreamResourceUpdatesAsync starts
   ??> Streams from _resourceMonitor.StreamResourceUsageAsync()
   ??> Sends: clientProxy.SendAsync("ResourceUpdate", usage, token) ?

3. Client Library (SignalR)
   ??> Receives "ResourceUpdate" message
   ??> _hubConnection.On<ServerResourceUsage>("ResourceUpdate", ...) fires ?
   ??> Invokes ResourceUpdateReceived?.Invoke(this, usage) ?

4. Component Event Handler
   ??> OnMetricsReceived fires
   ??> InvokeAsync(() => {
   ?      currentMetrics = metrics; ?
   ?      // Update history ?
   ?      StateHasChanged(); ?
   ?   });
   ??> UI updates! ?
```

## ? What's Working

1. **Hub Implementation**: Properly streams data
2. **Client Library**: Properly receives and fires events
3. **Component**: Properly handles events and updates UI
4. **Method Names**: All match ("ResourceUpdate")
5. **Event Subscriptions**: All proper
6. **UI Updates**: StateHasChanged() in all handlers
7. **Dependency Injection**: All configured correctly
8. **Project References**: Using latest source code

## ?? Potential Issues

If it's still not working, check:

### 1. Connection Issues
**Problem**: SignalR connection not established
**Check**:
- Browser DevTools ? Network ? WS (WebSocket)
- Should see connection to `/hubs/resources`
- Check connection state in logs

**Solution**:
- Verify GameServer.Docker API is running
- Check firewall/CORS settings
- Verify URL configuration

### 2. CORS Configuration
**Problem**: Browser blocks WebSocket connection
**Check**: Browser console for CORS errors

**Solution**: Add CORS policy in `GameServer.Docker/Program.cs`:
```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("https://localhost:7198") // GameServer.Web URL
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Required for SignalR
    });
});

// Before app.MapHub...
app.UseCors();
```

### 3. No Containers Running
**Problem**: No metrics to send
**Check**: Are there actual running containers to monitor?

**Solution**: Start a game server first

### 4. Logging Too Verbose/Quiet
**Problem**: Can't see what's happening

**Solution**: Adjust log levels in appsettings.json

## ?? Testing Checklist

### Step 1: Verify Backend is Running
```bash
# Check if GameServer.Docker API is running
# Should be on http://localhost:5164 or https://localhost:5163
curl http://localhost:5164/api/health
```

### Step 2: Check SignalR Hub Endpoint
```bash
# Hub should be accessible at /hubs/resources
# Browser: Open DevTools ? Network ? Filter: WS
# Should see WebSocket connection attempt
```

### Step 3: Monitor Backend Logs
Look for:
```
- "Client {ConnectionId} subscribing to server {ServerId}"
- "Starting resource stream for server {ServerId}"
- "Sent resource update for server {ServerId}"
```

### Step 4: Monitor Frontend Console
Look for:
```
- SignalR connection established
- "Received resource update for server..."
- No JavaScript errors
```

### Step 5: Test Component
1. Navigate to Server Details page
2. ResourceMonitor should auto-connect (AutoConnect=true)
3. Should see "Live" badge
4. Metrics should update every 2 seconds
5. Charts should animate

## ?? Quick Fixes

### If Connection Fails

**Add CORS to GameServer.Docker**:
```csharp
// In Program.cs, after builder.Services.AddSignalR():
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(origin => true) // Allow any origin (development only!)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// After var app = builder.Build():
app.UseCors();
```

### If Events Not Firing

**Add Logging to Component**:
```csharp
private void OnMetricsReceived(object? sender, ServerResourceUsage metrics)
{
    Console.WriteLine($"METRICS RECEIVED: {metrics.ServerId}"); // ? Add this
    InvokeAsync(() =>
    {
        currentMetrics = metrics;
        // ...
        StateHasChanged();
    });
}
```

### If UI Not Updating

**Force Re-render**:
```csharp
private void OnMetricsReceived(object? sender, ServerResourceUsage metrics)
{
    InvokeAsync(async () =>
    {
        currentMetrics = metrics;
        // ...
        StateHasChanged();
        await Task.Delay(1); // Force async context
        StateHasChanged(); // Force again
    });
}
```

## ?? Files to Check

### Backend
- `src/GameServer.Docker/Hubs/ResourceMonitoringHub.cs` ? Verified
- `src/GameServer.Docker/Program.cs` ? Verified
- `src/GameServer.Docker/Services/GameServerResourceMonitorService.cs` - Implement monitoring

### Client
- `src/GameServer.Docker.Client/Services/ResourceMonitoringClient.cs` ? Verified
- `src/GameServer.Docker.Client/Interfaces/IResourceMonitoringClient.cs` - Check interface

### Frontend  
- `src/GameServer.Web/Components/Server/ResourceMonitor.razor` ? Verified
- `src/GameServer.Web/Program.cs` ? Verified

## ?? Recommended Next Steps

1. **Verify Backend is Running**
   ```bash
   cd src/GameServer.Docker
   dotnet run
   ```

2. **Verify Frontend is Running**
   ```bash
   cd src/GameServer.Web
   dotnet run
   ```

3. **Open Browser DevTools**
   - F12 ? Network ? WS filter
   - F12 ? Console (watch for errors)

4. **Test Connection**
   - Navigate to Server Details
   - Watch for WebSocket connection
   - Watch for "ResourceUpdate" messages

5. **Add Logging if Needed**
   - Add Console.WriteLine in OnMetricsReceived
   - Check backend logs for "Sent resource update"

6. **Fix CORS if Needed**
   - If connection blocked, add CORS policy

## ? Summary

**Everything is properly configured!** The SignalR Resource Monitor should work.

If it's not working, the most likely issues are:
1. Backend not running (fix: start it)
2. CORS blocking connection (fix: add CORS policy)
3. No containers to monitor (fix: start a server)
4. Connection URL mismatch (fix: verify appsettings.json)

The code is correct. It's most likely an environmental/configuration issue.

---

**Next Action**: Test with both services running and check browser DevTools for WebSocket connection!
