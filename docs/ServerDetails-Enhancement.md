# ServerDetails Page Enhancement Summary

## Overview
Enhanced the ServerDetails page with a new REST API-based resource monitor component and a conditional TTY Console tab for interactive container access.

## Changes Made

### 1. New Component: ResourceMonitorRest.razor
Created a new component that polls the REST API for server resource usage information.

**Location**: `src/GameServer.Web/Components/Server/ResourceMonitorRest.razor`

**Features**:
- **REST API Polling**: Uses `IGameServerApi.GetResourceUsageAsync()` to fetch resource data
- **Auto-refresh**: Optional automatic refresh with configurable interval (default 30 seconds)
- **Manual Refresh**: Button to manually refresh resource data
- **Service-Level Metrics**: Displays Docker Swarm service-level information:
  - Service status (Running, Starting, Stopped, etc.)
  - Replica counts (Running/Desired)
  - Health percentage
  - Failed tasks count
  - CPU limits per replica and total
  - Memory limits per replica and total
  - Container IDs
  - Service timestamps (created, updated)

**Key Differences from Real-Time Monitor**:
| Feature | ResourceMonitor (SignalR) | ResourceMonitorRest (API) |
|---------|---------------------------|---------------------------|
| Update Method | Push (SignalR streaming) | Pull (HTTP polling) |
| Latency | Sub-second | 5-30 seconds |
| Data Type | Real-time container stats | Service-level metadata |
| CPU/Memory | Live usage % | Configured limits |
| Network/Disk | Live I/O bytes | Not available |
| Resource Usage | Higher (persistent connection) | Lower (periodic requests) |

**Parameters**:
- `ServerId` (required): Server ID to monitor
- `Title`: Custom title for the component
- `AutoRefresh`: Enable automatic polling (default: false)
- `RefreshIntervalSeconds`: Polling interval (default: 30)
- `ShowTimestamps`: Display service timestamps (default: true)

### 2. ServerDetails.razor Updates

#### Added Dependencies
```razor
@inject IGameTypeExtendedMetadataApi ExtendedMetadataApi
```

#### Enhanced Data Loading
- Added `extendedMetadata` field to store game type extended metadata
- Modified `LoadServerAsync()` to fetch extended metadata for TTY feature detection

#### Added Two Monitors Side-by-Side
Updated the Overview tab to show both monitoring components:

```razor
<div class="monitors-stack">
  <ResourceMonitor ContainerId="@ServerId" 
                  Title="Real-Time Monitor (SignalR)"
                  AutoConnect="true" 
                  ShowHistory="true" 
                  UpdateIntervalSeconds="2" />
  
  <div class="mt-3">
    <ResourceMonitorRest ServerId="@ServerId"
                        Title="REST API Monitor"
                        AutoRefresh="true"
                        RefreshIntervalSeconds="5"
                        ShowTimestamps="true" />
  </div>
</div>
```

#### Added TTY Console Tab
New conditional tab that only appears when `GameTypeExtendedMetadata.EnableTTY` is true:

```razor
@if (IsTtyEnabled())
{
  <RadzenTabsItem Text="TTY Console">
    <div class="tab-content">
      @if (IsServerRunning(server?.Status))
      {
        <ContainerConsole ContainerId="@ServerId" 
                         Title="@($"{server.Name} Console")"
                         AutoConnect="false" />
      }
      else
      {
        <!-- Message to start server first -->
      }
    </div>
  </RadzenTabsItem>
}
```

**Tab Behavior**:
- ? Only shows if `extendedMetadata.EnableTTY == true`
- ? Checks if server is running before showing console
- ? Uses `ContainerConsole` component for interactive terminal access
- ? Auto-connect is disabled (user must click Connect button)

#### New Helper Methods
```csharp
private bool IsTtyEnabled()
{
    return extendedMetadata?.EnableTTY == true;
}
```

### 3. Styling Updates

Added CSS for the monitors stack:
```css
.monitors-stack {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}
```

## Use Cases

### Comparing Monitor Types
Users can now see both monitoring approaches simultaneously:
- **Real-Time SignalR Monitor**: For live CPU/Memory/Network/Disk metrics
- **REST API Monitor**: For service health, replica status, and resource limits

This helps with:
- Understanding the difference between container stats vs service configuration
- Debugging why a service might be failing (replica health, failed tasks)
- Comparing live usage against configured limits

### TTY Console Access
For game types that support interactive console (e.g., Minecraft with RCON, Valheim with console commands):
1. Admin sets `EnableTTY: true` in GameTypeExtendedMetadata
2. TTY Console tab appears on ServerDetails page
3. Users can access interactive terminal to execute commands
4. Useful for:
   - Running admin commands
   - Debugging server issues
   - Checking logs interactively
   - Using built-in server consoles

## Testing

### Test REST API Monitor
1. Navigate to a server's details page
2. Observe the "REST API Monitor" card below the real-time monitor
3. Verify service status, replicas, and resource limits are displayed
4. Click refresh button to manually update
5. Wait 5 seconds to verify auto-refresh works

### Test TTY Console Tab
1. Create or edit a game type's extended metadata
2. Set `EnableTTY: true`
3. Create a server of that game type
4. Navigate to server details
5. Verify "TTY Console" tab appears
6. Start the server if not running
7. Click the TTY Console tab
8. Click "Connect" in the ContainerConsole component
9. Verify terminal connection works

### Test Tab Visibility
1. For game types **without** `EnableTTY` set:
   - TTY Console tab should NOT appear
2. For game types **with** `EnableTTY: true`:
   - TTY Console tab SHOULD appear
3. If server is stopped:
   - Tab appears but shows message to start server first

## Benefits

### For Users
- ? **Comprehensive Monitoring**: See both live metrics AND service configuration
- ? **Better Troubleshooting**: Understand why services fail (replica health, resource limits)
- ? **Interactive Access**: Direct console access for supported game types
- ? **Side-by-Side Comparison**: Compare different monitoring approaches

### For Developers
- ? **Separation of Concerns**: REST monitor focuses on service-level data
- ? **Performance Options**: Choose between real-time push vs periodic pull
- ? **Extensibility**: Easy to add more REST-based metrics
- ? **Conditional Features**: TTY tab only for game types that support it

## Future Enhancements

### REST API Monitor
- [ ] Add chart for replica history
- [ ] Show update status progress
- [ ] Display task logs when tasks fail
- [ ] Add CPU/Memory usage gauges when real-time stats are available

### TTY Console Tab
- [ ] Remember connection state across tab switches
- [ ] Add common command shortcuts (e.g., "stop server", "save world")
- [ ] Show console output history before connecting
- [ ] Support multiple container selection for multi-replica services

## Files Modified

1. **Created**: `src/GameServer.Web/Components/Server/ResourceMonitorRest.razor`
   - New REST API-based resource monitor component

2. **Modified**: `src/GameServer.Web/Components/Pages/Servers/ServerDetails.razor`
   - Added `IGameTypeExtendedMetadataApi` injection
   - Added `extendedMetadata` field
   - Updated `LoadServerAsync()` to fetch extended metadata
   - Added second monitor to Overview tab
   - Added conditional TTY Console tab
   - Added `IsTtyEnabled()` helper method
   - Added `.monitors-stack` CSS

## Notes

- The `ContainerId` parameter for `ContainerConsole` uses the `ServerId` which maps to the service name in Docker Swarm
- The console will connect to the first available container for the service
- Both monitors run simultaneously without interfering with each other
- The REST monitor is lighter on resources than the SignalR monitor
- TTY feature requires the game server container to support TTY/interactive mode
