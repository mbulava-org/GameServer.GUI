# SignalR Test Page - Usage Guide

## Overview

A comprehensive diagnostic page to test and verify SignalR Resource Monitor connectivity and functionality.

**URL**: `https://localhost:7198/signalr-test`

## Features

### 1. Connection Status Panel
- **Hub URL Display**: Shows the configured SignalR hub endpoint
- **Connection State**: Real-time connection status badge
  - ?? Connected (green)
  - ?? Connecting (yellow)
  - ?? Disconnected (red)
- **Error Messages**: Displays connection errors if any occur
- **Last Message Time**: Timestamp of the most recent message received
- **Message Counter**: Total number of messages received
- **Connect/Disconnect Controls**: Manual connection management
- **Clear Logs Button**: Reset the message log

### 2. Subscription Controls Panel
- **Server ID Input**: Enter the server ID or container ID to monitor
- **Update Interval**: Configure how often updates are sent (1-60 seconds)
- **Subscribe/Unsubscribe**: Start/stop monitoring a specific server
- **Get Snapshot**: Request a one-time snapshot of current metrics
- **Currently Monitoring Display**: Shows active subscription details

### 3. SignalR Message Log
- **Real-time Log**: All SignalR events with timestamps
- **Color-coded Messages**:
  - ?? INFO: General information
  - ?? SUCCESS: Successful operations
  - ?? WARNING: Warnings
  - ?? ERROR: Errors
  - ?? DATA: Resource data updates
- **Automatic Scrolling**: Latest messages appear at top
- **Limited History**: Keeps last 100 messages

### 4. Latest Resource Data Panel
- **Server Information**: ID, name, game type, status
- **Resource Metrics**: CPU, memory, network, disk I/O
- **Container Details**: Container ID, node name
- **Replica Information**: Healthy replicas count
- **Raw JSON**: Complete data structure for debugging

## How to Use

### Step 1: Access the Test Page

```
https://localhost:7198/signalr-test
```

### Step 2: Connect to SignalR Hub

1. The page shows the hub URL (from appsettings.json)
2. Click **"Connect"** button
3. Watch for connection status to change to **"Connected"** (green)
4. Check the log for "? Connected successfully!"

**If connection fails**:
- Check that GameServer.Docker API is running
- Verify the hub URL in appsettings.json
- Check for CORS errors in browser console (F12)
- Review the error message displayed

### Step 3: Subscribe to a Server

1. Enter a **Server ID** in the text box
   - Use a server ID from your Servers page
   - Or use a Docker container ID
2. Set the **Update Interval** (default: 2 seconds)
3. Click **"Subscribe"** button
4. Watch the log for "? Subscribed" message
5. Resource updates should start flowing immediately

### Step 4: Monitor Real-time Updates

- **Message Log**: Shows each "?? ResourceUpdate" as it arrives
- **Message Counter**: Increments with each update
- **Last Message Time**: Updates with each message
- **Latest Data Panel**: Shows the most recent metrics

### Step 5: Test Snapshot Feature

1. Enter a Server ID
2. Click **"Get Snapshot"** button
3. Watch for one-time data response
4. Data appears in the "Latest Resource Data" panel

### Step 6: Disconnect

1. Click **"Unsubscribe"** to stop monitoring (if subscribed)
2. Click **"Disconnect"** to close the SignalR connection

## Expected Behavior

### Healthy Connection

```
[INFO] Test page initialized. Hub URL: https://localhost:5163/hubs/resources
[INFO] Creating ResourceMonitoringClient...
[INFO] Connecting to SignalR hub...
[SUCCESS] ? Connected successfully!
[INFO] Subscribing to server: my-server-01 with 2s interval
[SUCCESS] ? Subscribed: my-server-01 (2s interval)
[DATA] ?? ResourceUpdate: my-server-01 - CPU: 15.42%, Memory: 45.67%
[DATA] ?? ResourceUpdate: my-server-01 - CPU: 15.38%, Memory: 45.69%
[DATA] ?? ResourceUpdate: my-server-01 - CPU: 15.44%, Memory: 45.68%
```

### Connection Failure

```
[INFO] Test page initialized. Hub URL: https://localhost:5163/hubs/resources
[INFO] Creating ResourceMonitoringClient...
[INFO] Connecting to SignalR hub...
[ERROR] ? Connection failed: Connection refused
```

**Common Causes**:
- Backend not running
- Wrong URL in configuration
- Firewall blocking connection
- CORS not configured

## Troubleshooting

### Problem: Can't Connect

**Check List**:
1. ? Is GameServer.Docker API running?
   ```bash
   cd src/GameServer.Docker
   dotnet run
   ```
2. ? Is the hub URL correct?
   - Check `appsettings.json` ? `GameServerDockerApi:BaseUri`
   - Should match backend URL
3. ? CORS configured?
   - Check backend Program.cs
   - Should have `AddCors()` and `UseCors()`
4. ? Browser console errors?
   - Press F12 ? Console tab
   - Look for CORS or connection errors

### Problem: Connected But No Data

**Check List**:
1. ? Did you subscribe?
   - Enter Server ID and click "Subscribe"
2. ? Does the server exist?
   - Check the Servers page
   - Verify the Server ID is correct
3. ? Is the server running?
   - Stopped servers don't send metrics
4. ? Backend logs?
   - Check GameServer.Docker logs
   - Look for "Starting resource stream"

### Problem: Data Arrives But UI Doesn't Update

**Check**:
1. Look at the **Message Counter**
   - If incrementing: Data is arriving
   - Problem is with UI update
2. Check **Last Message Time**
   - If updating: StateHasChanged() working
3. Check **Message Log**
   - If messages appear: Events firing correctly

**This is the test page problem, not the actual component!**

### Problem: Subscription Fails

**Check**:
1. ? Server ID valid?
   - Must be an actual server in the system
2. ? Backend has resource monitor?
   - Check `IGameServerResourceMonitor` is registered
3. ? Backend logs?
   - Look for subscription errors

## Interpreting Results

### ? Everything Working

**Signs**:
- Connection status: **Connected** (green)
- Messages flowing every N seconds
- Message counter incrementing
- Latest Data panel updating
- No errors in log

**Conclusion**: SignalR is working correctly!

### ?? Partial Success

**Connection OK, No Data**:
- SignalR connection works
- Subscription might be failing
- Check Server ID is valid
- Check backend has monitoring service

**Data Arrives, UI Frozen**:
- Backend sending data
- Client receiving data
- UI not updating (StateHasChanged issue)
- **This is a component bug, not SignalR**

### ? Complete Failure

**Can't Connect**:
- Backend not running
- URL configuration wrong
- CORS blocking
- Network issues

## Testing Scenarios

### Scenario 1: Basic Connection Test

1. Click "Connect"
2. Should connect successfully
3. Click "Disconnect"
4. Should disconnect cleanly

**Result**: ? SignalR hub is accessible

### Scenario 2: Subscribe/Unsubscribe Test

1. Connect
2. Enter Server ID
3. Subscribe
4. Watch for updates
5. Unsubscribe
6. Updates should stop

**Result**: ? Subscription mechanism works

### Scenario 3: Reconnection Test

1. Connect and subscribe
2. Disconnect
3. Connect again
4. Subscribe again
5. Should work without issues

**Result**: ? Multiple connections handled properly

### Scenario 4: Invalid Server Test

1. Connect
2. Enter invalid Server ID (e.g., "nonexistent")
3. Subscribe
4. Should see error or no data

**Result**: ? Error handling works

### Scenario 5: Snapshot Test

1. Connect
2. Enter valid Server ID
3. Click "Get Snapshot"
4. Should receive one-time data

**Result**: ? Snapshot endpoint works

## Debugging with Test Page

### Enable Verbose Logging

Add to the component code temporarily:

```csharp
private void OnResourceUpdate(object? sender, ServerResourceUsage usage)
{
    Console.WriteLine($"RAW DATA: {System.Text.Json.JsonSerializer.Serialize(usage)}");
    InvokeAsync(() =>
    {
        // ... rest of code
    });
}
```

### Check Browser DevTools

**Network Tab**:
1. Filter: WS (WebSocket)
2. Look for connection to `/hubs/resources`
3. Check messages tab
4. Should see "ResourceUpdate" messages

**Console Tab**:
1. Look for JavaScript errors
2. Look for console.log statements
3. Check for CORS errors

### Compare with Real Component

If test page works but ResourceMonitor doesn't:
1. ? SignalR is working (backend + network OK)
2. ? Problem is in ResourceMonitor component
3. Check component's event handlers
4. Check component's StateHasChanged calls

If test page fails:
1. ? SignalR not working properly
2. Fix connection issues first
3. Then test ResourceMonitor

## Files

**Test Page**: `src/GameServer.Web/Components/Pages/SignalRTest.razor`

**Related Components**:
- ResourceMonitor.razor (actual component)
- IResourceMonitoringClient (interface)
- ResourceMonitoringClient (implementation)

## Quick Reference

### Connection Status Badges
- ?? **Connected**: SignalR connection established
- ?? **Connecting**: Connection in progress
- ?? **Disconnected**: Not connected

### Log Message Types
- **[INFO]**: General information
- **[SUCCESS]**: Successful operation
- **[WARNING]**: Warning message
- **[ERROR]**: Error occurred
- **[DATA]**: Resource data received

### Key Events
- **Connect**: Establish SignalR connection
- **Subscribe**: Start monitoring a server
- **ResourceUpdate**: Data received from hub
- **Unsubscribe**: Stop monitoring
- **Disconnect**: Close SignalR connection

## Summary

The SignalR Test Page is a diagnostic tool to:
1. ? Verify SignalR hub is accessible
2. ? Test connection establishment
3. ? Verify data flow
4. ? Debug subscription issues
5. ? Inspect raw data structure
6. ? Validate event handling

Use it to diagnose issues before debugging the actual ResourceMonitor component!

---

**Access**: `https://localhost:7198/signalr-test`  
**Purpose**: SignalR connectivity testing and diagnostics  
**Created**: For GameServer.GUI SignalR troubleshooting
