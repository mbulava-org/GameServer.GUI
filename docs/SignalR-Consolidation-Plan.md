# SignalR Resource Monitor - Consolidation & Fix Plan

## Current Status ?

### Repository Structure
The backend services are **already in GameServer.GUI**:
```
GameServer.GUI/
??? src/
?   ??? GameServer.Web/              ? Frontend Blazor app
?   ??? GameServer.Docker/           ? Backend API
?   ??? GameServer.Docker.Agent/     ? Agent service
?   ??? GameServer.Docker.Client/    ? Client library
```

### Project References
GameServer.Web already uses **project reference** (not NuGet):
```xml
<ProjectReference Include="..\GameServer.Docker.Client\GameServer.Docker.Client.csproj" />
```

This means we're using the **latest source code** with all the models (ServerResourceUsage, ContainerStats, etc.)!

## ?? What Needs to Be Fixed

### 1. SignalR Hub Configuration
**Backend (GameServer.Docker API)**:
- Ensure ResourceHub is properly registered
- Ensure SignalR middleware is configured
- Check if hub is accessible at `/hubs/resources`

**Frontend (GameServer.Web)**:
- Ensure client connects to correct hub URL
- Ensure authentication/CORS is configured

### 2. Data Flow Issues
Based on logs showing "data is being returned" but UI not updating:

**Potential Issues**:
- ? StateHasChanged() - Already fixed!
- ? SignalR hub method names mismatch
- ? Event subscriptions not working
- ? Connection timing issues
- ? CORS blocking SignalR

### 3. Client Library Issues
**Potential Problems**:
- Model serialization issues
- Event handler registration
- Connection lifecycle

## ?? Investigation Plan

### Step 1: Verify Backend SignalR Setup
- [ ] Check ResourceHub implementation
- [ ] Check SignalR registration in Program.cs
- [ ] Check hub endpoint mapping
- [ ] Check CORS configuration

### Step 2: Verify Frontend Connection
- [ ] Check hub URL configuration
- [ ] Check connection establishment
- [ ] Check subscription to container
- [ ] Check event handler registration

### Step 3: Test Data Flow
- [ ] Backend: Verify hub sends data
- [ ] Client: Verify client receives data
- [ ] Component: Verify component processes data
- [ ] UI: Verify UI updates

### Step 4: Fix Issues Found
- [ ] Fix any configuration issues
- [ ] Fix any code issues
- [ ] Add logging/diagnostics
- [ ] Test end-to-end

## ?? Diagnostic Questions

1. **Is the SignalR connection established?**
   - Check browser DevTools ? Network ? WS (WebSocket)
   - Should see connection to `/hubs/resources`

2. **Are messages being sent from backend?**
   - Check GameServer.Docker API logs
   - Should see "Sending metrics to client" or similar

3. **Are messages reaching the client?**
   - Check browser console for SignalR messages
   - Should see data in network traffic

4. **Are events firing in the component?**
   - Add console.log in OnMetricsReceived
   - Should fire when data arrives

5. **Is StateHasChanged working?**
   - Already fixed, but verify
   - Should cause re-render

## ??? Action Items

### Immediate
1. Examine GameServer.Docker API SignalR configuration
2. Examine ResourceHub implementation
3. Check for hub method name mismatches
4. Verify CORS allows SignalR from frontend

### If Issues Found
1. Fix backend SignalR configuration
2. Fix client connection logic
3. Add proper error handling
4. Add logging for diagnostics

### After Fixes
1. Test with real container
2. Verify metrics update in real-time
3. Verify charts animate
4. Verify connection recovery works

## ?? Files to Check

### Backend
1. `src/GameServer.Docker/Hubs/ResourceHub.cs` or similar
2. `src/GameServer.Docker/Program.cs` - SignalR registration
3. `src/GameServer.Docker/Services/ResourceMonitoringService.cs` or similar

### Client Library
1. `src/GameServer.Docker.Client/Services/ResourceMonitoringClient.cs`
2. `src/GameServer.Docker.Client/Interfaces/IResourceMonitoringClient.cs`

### Frontend
1. `src/GameServer.Web/Components/Server/ResourceMonitor.razor` ? Already updated
2. `src/GameServer.Web/Program.cs` - Client registration ? Already done

## ?? Expected Behavior

### When Working Correctly
```
Backend (GameServer.Docker):
  ??> ResourceHub receives subscription
  ??> Background service collects metrics every N seconds
  ??> Hub sends metrics to connected clients
  ??> "Metrics sent to client X" logged

Frontend (GameServer.Web):
  ??> ResourceMonitor connects to hub
  ??> Subscribes to container metrics
  ??> ResourceUpdateReceived event fires
  ??> OnMetricsReceived handler executes
  ??> currentMetrics updated
  ??> StateHasChanged() called
  ??> UI updates with new data ?
```

## ?? Next Steps

Let me examine:
1. Backend SignalR hub configuration
2. Hub implementation
3. Client library implementation
4. Any connection or data flow issues

Then I'll provide specific fixes for any problems found.

---

**Status**: Investigation Phase
**Goal**: Get SignalR Resource Monitor working end-to-end
**Approach**: Systematic verification of each component in the data flow
