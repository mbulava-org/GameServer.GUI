# ServerDetails Enhancement - Testing Guide

## Quick Test Checklist

### ? REST API Monitor Component

**Test 1: Initial Display**
- [ ] Navigate to any server details page
- [ ] Verify "REST API Monitor" card appears below "Real-Time Monitor (SignalR)"
- [ ] Verify title shows "Resource Usage (REST API)"
- [ ] Verify refresh button is visible

**Test 2: Manual Refresh**
- [ ] Click the refresh button
- [ ] Verify "Refreshing..." badge appears briefly
- [ ] Verify data loads (Service Status, Replicas, Health %)
- [ ] Verify timestamp updates (e.g., "Updated 14:23:45")

**Test 3: Auto Refresh**
- [ ] Watch the monitor for 5+ seconds
- [ ] Verify data automatically refreshes
- [ ] Verify timestamp updates without clicking refresh

**Test 4: Service Status Display**
- [ ] With server running: Verify status shows "Running" with green badge
- [ ] Stop server: Verify status updates to "Stopped" with gray badge
- [ ] Start server: Verify status updates to "Starting" then "Running"

**Test 5: Replica Health**
- [ ] Verify "Replicas: X / Y" displays correctly
- [ ] Verify health percentage shows (e.g., "100%")
- [ ] Verify circular progress indicator matches percentage

**Test 6: Resource Limits**
- [ ] Verify CPU Limits card shows if configured
- [ ] Verify Memory Limits card shows if configured
- [ ] Verify "Per Replica" values display correctly
- [ ] Verify units are correct (CPUs, MB, GB)

**Test 7: Container IDs**
- [ ] Start a server
- [ ] Verify "Containers" card appears
- [ ] Verify container ID(s) are displayed (truncated to 12 chars)

**Test 8: Error Handling**
- [ ] Stop the GameServer.Docker API
- [ ] Refresh the monitor
- [ ] Verify error message displays
- [ ] Verify "Retry" button appears
- [ ] Start API and click Retry
- [ ] Verify data loads successfully

### ? TTY Console Tab

**Test 9: Tab Visibility - Disabled**
- [ ] Go to Game Types page
- [ ] Select a game type (e.g., "Minecraft")
- [ ] Edit extended metadata
- [ ] Ensure "Enable TTY" is **unchecked** or not set
- [ ] Save
- [ ] Navigate to a server of that game type
- [ ] Verify "TTY Console" tab **does NOT appear**

**Test 10: Tab Visibility - Enabled**
- [ ] Go to Game Types page
- [ ] Select a game type
- [ ] Edit extended metadata
- [ ] **Check** "Enable TTY" checkbox
- [ ] Save
- [ ] Create a new server of that game type (or use existing)
- [ ] Navigate to server details
- [ ] Verify "TTY Console" tab **DOES appear**

**Test 11: Console Tab - Server Stopped**
- [ ] With TTY-enabled game type
- [ ] Ensure server is stopped
- [ ] Click "TTY Console" tab
- [ ] Verify message: "Server must be running to access the console"
- [ ] Verify ContainerConsole component is **not shown**

**Test 12: Console Tab - Server Running**
- [ ] Start the server
- [ ] Click "TTY Console" tab
- [ ] Verify `ContainerConsole` component is displayed
- [ ] Verify title shows server name (e.g., "My Server Console")
- [ ] Verify "Connect" button is available (not auto-connected)

**Test 13: Console Functionality**
- [ ] With server running and TTY Console tab open
- [ ] Click "Connect" button in ContainerConsole
- [ ] Verify connection establishes
- [ ] Verify terminal prompt appears (if supported by game server)
- [ ] Type a command and press Enter
- [ ] Verify output displays
- [ ] Click "Disconnect"
- [ ] Verify connection closes

### ? Layout & Responsive Design

**Test 14: Desktop Layout**
- [ ] View on desktop (1200px+ width)
- [ ] Verify monitors appear side-by-side in right column
- [ ] Verify both monitors are visible without scrolling horizontally
- [ ] Verify spacing between monitors is adequate

**Test 15: Tablet Layout**
- [ ] Resize browser to tablet width (768px - 1200px)
- [ ] Verify monitors stack vertically
- [ ] Verify both monitors remain visible
- [ ] Verify no horizontal scrolling

**Test 16: Mobile Layout**
- [ ] Resize to mobile width (< 768px)
- [ ] Verify monitors stack vertically
- [ ] Verify tabs remain usable
- [ ] Verify console tab works on mobile

### ? Integration Tests

**Test 17: Tab Switching**
- [ ] Open server details
- [ ] Click through all tabs: Overview ? Logs ? Files ? TTY Console (if enabled)
- [ ] Verify each tab loads correctly
- [ ] Return to Overview tab
- [ ] Verify both monitors are still active and updating

**Test 18: Monitor Comparison**
- [ ] Open server details
- [ ] Observe both monitors simultaneously
- [ ] Compare data:
  - Real-Time: Shows actual CPU/Memory usage %
  - REST: Shows service status and limits
- [ ] Verify they show different but complementary data
- [ ] Verify refresh rates differ (SignalR faster than REST)

**Test 19: Multiple Servers**
- [ ] Open Server A details in one tab
- [ ] Open Server B details in another tab
- [ ] Verify both monitors work independently
- [ ] Verify no data cross-contamination

**Test 20: Server Lifecycle**
- [ ] Open server details (server stopped)
- [ ] Start server
- [ ] Verify both monitors update to show running state
- [ ] Stop server
- [ ] Verify both monitors update to show stopped state
- [ ] Verify TTY Console tab shows appropriate message when stopped

## Expected Behavior Summary

### Both Monitors Running
- **Real-Time Monitor (SignalR)**:
  - Updates every 2 seconds
  - Shows live CPU%, Memory%, Network I/O, Disk I/O
  - Shows "Live" badge when connected
  - Displays historical chart

- **REST API Monitor**:
  - Updates every 5 seconds
  - Shows service status, replica health, resource limits
  - Shows "Updated HH:mm:ss" timestamp
  - Displays container IDs

### TTY Console Tab
- **Appears when**: `GameTypeExtendedMetadata.EnableTTY == true`
- **Hidden when**: EnableTTY is false or not set
- **Shows console when**: Server is running
- **Shows message when**: Server is stopped

## Common Issues & Solutions

### Issue: REST Monitor not updating
- **Check**: Browser console for API errors
- **Check**: GameServer.Docker API is running
- **Check**: Server ID is correct
- **Fix**: Click refresh button manually

### Issue: TTY Console tab not appearing
- **Check**: Extended metadata has EnableTTY set to true
- **Check**: Page was refreshed after changing metadata
- **Fix**: Edit game type metadata and ensure EnableTTY is checked

### Issue: Console won't connect
- **Check**: Server is actually running (not just "Starting")
- **Check**: Container ID is valid
- **Check**: SignalR hub connection works
- **Fix**: Wait for server to fully start, then try again

### Issue: Monitors show different data
- **Expected**: This is normal! They show different metrics:
  - SignalR = Live container statistics
  - REST = Service-level configuration and status

## Performance Verification

### Monitor Performance
- [ ] Open browser DevTools ? Network tab
- [ ] Observe SignalR connection (ws://)
- [ ] Observe periodic REST API calls (every 5s)
- [ ] Verify page remains responsive
- [ ] Check CPU usage (should be < 5% with both monitors)

### Memory Checks
- [ ] Open browser DevTools ? Memory tab
- [ ] Take heap snapshot
- [ ] Let monitors run for 5 minutes
- [ ] Take another snapshot
- [ ] Verify no significant memory growth
- [ ] Verify timers are properly cleaned up

## Browser Compatibility

Test in:
- [ ] Chrome/Edge (Chromium)
- [ ] Firefox
- [ ] Safari (if available)

Verify:
- [ ] SignalR connections work
- [ ] REST API calls succeed
- [ ] Console terminal displays correctly
- [ ] All styling renders properly

## Sign-off

- [ ] All tests passed
- [ ] No console errors
- [ ] Performance acceptable
- [ ] Documentation accurate
- [ ] Ready for production

**Tested by**: _______________  
**Date**: _______________  
**Build version**: _______________
