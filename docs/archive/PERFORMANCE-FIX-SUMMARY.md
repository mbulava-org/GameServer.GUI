# Performance Fix: ListGameServersAsync Optimization

## Issue Summary
After initial performance fixes to `GetGameServerById`, users continued experiencing 60+ second delays when viewing the server list/dashboard. Investigation revealed an **incomplete optimization** where only one code path was fixed.

## Timeline

### Initial Problem (Session 1)
- **Symptom**: 2+ minute delays when viewing game servers
- **Root Cause**: Fetching all 131 tasks from Docker Swarm on every request
- **Fix Applied**: Optimized `GetGameServerById` to filter tasks by service ID

### Recurring Problem (Session 2)
- **Symptom**: Still seeing 60+ second delays on dashboard
- **Root Cause**: `ListGameServersAsync` was **never optimized** - still fetching all 131 tasks
- **Evidence**: 
  ```
  [19:09:13 INF] Fetching all tasks from Docker Swarm...
  [19:09:13 WRN] ✅ [ListTasks] Found 131 tasks
  ```
  GUI log: `/api/dashboard/servers` taking 60269ms (60 seconds)

## The Fix

### Before (❌ Slow - 60+ seconds)
```csharp
public async Task<List<Models.GameServer>> ListGameServersAsync()
{
    var services = await serviceOperations.ListServicesAsync(); // All services
    var allTasks = await serviceOperations.ListTasksAsync(new TasksListParameters()); // ALL 131 tasks!
    
    var tasksByService = allTasks
        .GroupBy(t => t.ServiceID)
        .ToDictionary(g => g.Key, g => g.ToList());
        
    var serverTasks = services.Select(svc => TryCastGameServer(svc, tasksByService));
    var servers = (await Task.WhenAll(serverTasks)).Where(s => s != null).ToList();
    return servers;
}
```

**Problems:**
1. Fetched ALL 131 tasks across entire swarm (unfiltered)
2. Fetched ALL services including non-game-server services
3. Then filtered in-memory - wasted network/CPU

### After (✅ Fast - expected < 500ms)
```csharp
public async Task<List<Models.GameServer>> ListGameServersAsync()
{
    // 1. Filter services at the source
    var filters = new ServiceFilter
    {
        Label = [$"{ServiceLabels.Managed}={ServiceLabels.ManagedValue}"]
    };
    var services = await serviceOperations.ListServicesAsync(
        new ServicesListParameters { Filters = filters }
    );
    
    // 2. Fetch tasks ONLY for managed services in parallel
    var taskFetchTasks = services.Select(async svc =>
    {
        var tasks = await GetTasksForSwarmServiceAsync(svc.ID); // Filtered by service!
        return new { ServiceId = svc.ID, Tasks = tasks };
    });
    var taskResults = await Task.WhenAll(taskFetchTasks);
    
    // 3. Build tasksByService from filtered results
    var tasksByService = taskResults.ToDictionary(r => r.ServiceId, r => r.Tasks);
    
    // 4. Convert to GameServers (already filtered)
    var serverTasks = services.Select(svc => TryCastGameServer(svc, tasksByService));
    var servers = (await Task.WhenAll(serverTasks)).Where(s => s != null).ToList();
    return servers;
}
```

**Improvements:**
1. ✅ Filters services by `gameserver.docker.managed` label at API level
2. ✅ Fetches tasks ONLY for managed services (16 services instead of all services)
3. ✅ Fetches tasks per service in parallel (`Task.WhenAll`)
4. ✅ Minimal data transfer - only fetches what's needed

## Expected Performance Impact

### Before Optimization
- **Tasks fetched**: 131 tasks (entire swarm)
- **Services fetched**: ~50 services (all services)
- **API calls**: 2 (services + all tasks)
- **Response time**: 60+ seconds

### After Optimization
- **Tasks fetched**: ~16-32 tasks (only for game servers)
- **Services fetched**: ~16 services (only managed)
- **API calls**: 17 (1 for filtered services + 16 parallel task fetches)
- **Expected response time**: < 500ms

## Why This Happened

### Incomplete Fix Pattern
The initial fix only addressed `GetGameServerById` (single server view) but missed `ListGameServersAsync` (dashboard/list view). This is a common issue when:

1. Multiple code paths exist for similar operations
2. Testing focuses on one path (single server) but not the other (server list)
3. No systematic code review for similar patterns

### Lessons Learned
1. ✅ **Search for similar patterns** - if one method has the issue, others likely do too
2. ✅ **Test all code paths** - test both single-item and list endpoints
3. ✅ **Use performance checklist** - systematic review prevents missed optimizations
4. ✅ **Monitor logs** - production logs reveal which code paths are actually used

## Prevention

Created `docs/PERFORMANCE-CHECKLIST.md` with:
- ❌ Anti-patterns to avoid (unfiltered queries)
- ✅ Optimization patterns to follow
- 📊 Performance targets for each operation
- 🔍 Code review checklist
- 🧪 Testing guidelines

## Verification Steps

After deploying this fix, verify:

1. **Check logs** for "Fetching tasks for managed services in parallel" message
2. **Monitor task counts** - should show ~16-32 tasks instead of 131
3. **Measure response time** - `/api/dashboard/servers` should be < 500ms
4. **Test dashboard** - server list should load quickly
5. **Test single server** - detail page should remain fast (already fixed)

## Files Changed

- `src/GameServer.Docker/Services/DockerServiceHelper.cs` - Fixed `ListGameServersAsync` method
- `docs/PERFORMANCE-CHECKLIST.md` - New prevention guide

## Related Issues

- ✅ Fixed: `GetGameServerById` optimization (Session 1)
- ✅ Fixed: `ListGameServersAsync` optimization (Session 2 - this fix)
- ✅ Fixed: GameType caching with IMemoryCache (Session 1)
- ✅ Fixed: DI scope mismatches (Session 1)
