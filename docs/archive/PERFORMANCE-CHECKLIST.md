# Performance Optimization Checklist

This document outlines patterns and anti-patterns to prevent performance issues in GameServer.Docker.

## 🚨 Critical Anti-Patterns to Avoid

### 1. Unfiltered Task Queries
**NEVER** fetch all tasks without filtering by service:

```csharp
// ❌ BAD - Fetches ALL tasks across entire swarm (100s of tasks)
var allTasks = await serviceOperations.ListTasksAsync(new TasksListParameters());

// ✅ GOOD - Filter tasks by specific service ID
var tasks = await GetTasksForSwarmServiceAsync(serviceId);

// ✅ GOOD - Filter tasks by service in parallel for multiple services
var taskFetchTasks = services.Select(async svc =>
{
    var tasks = await GetTasksForSwarmServiceAsync(svc.ID);
    return new { ServiceId = svc.ID, Tasks = tasks };
});
var taskResults = await Task.WhenAll(taskFetchTasks);
```

### 2. Unfiltered Service Queries
**ALWAYS** filter services by the `gameserver.docker.managed` label:

```csharp
// ❌ BAD - Fetches ALL services including non-game-server services
var services = await serviceOperations.ListServicesAsync();

// ✅ GOOD - Filter by managed label
var filters = new ServiceFilter
{
    Label = [$"{ServiceLabels.Managed}={ServiceLabels.ManagedValue}"]
};
var services = await serviceOperations.ListServicesAsync(new ServicesListParameters { Filters = filters });
```

### 3. Repeated Database Queries
**ALWAYS** cache frequently-read, rarely-changed data:

```csharp
// ❌ BAD - Hits database on every request
var gameTypes = await repository.GetAllAsync();

// ✅ GOOD - Use IMemoryCache with appropriate expiration
var gameTypes = await memoryCache.GetOrCreateAsync(cacheKey, async entry =>
{
    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
    return await repository.GetAllAsync();
});
```

## 📊 Performance Targets

| Operation | Target Time | Warning Threshold | Critical Threshold |
|-----------|-------------|-------------------|-------------------|
| List GameServers | < 500ms | > 2s | > 5s |
| Get Single GameServer | < 200ms | > 1s | > 2s |
| Get GameTypes | < 50ms (cached) | > 500ms | > 1s |
| Create GameServer | < 3s | > 10s | > 20s |

## 🔍 Code Review Checklist

Before merging any changes to `DockerServiceHelper`, `ServiceOperationsViaAgent`, or repository classes:

- [ ] All `ListTasksAsync` calls include service ID filter via `TasksListParameters`
- [ ] All `ListServicesAsync` calls include label filters via `ServiceFilter`
- [ ] Parallel processing used for operations on collections (`Task.WhenAll`)
- [ ] Database queries for read-heavy data use `IMemoryCache`
- [ ] Cache invalidation implemented for mutable cached data
- [ ] Log statements show what filters are being applied
- [ ] Performance impact tested with production-scale data (100+ tasks, 10+ services)

## 🎯 Optimization Patterns

### Parallel Task Fetching
When you need tasks for multiple services:

```csharp
// Fetch tasks for each service in parallel
var taskFetchTasks = services.Select(async svc =>
{
    var tasks = await GetTasksForSwarmServiceAsync(svc.ID);
    return new { ServiceId = svc.ID, Tasks = tasks };
});

var taskResults = await Task.WhenAll(taskFetchTasks);
var tasksByService = taskResults.ToDictionary(r => r.ServiceId, r => r.Tasks);
```

### Pre-Filtering Services
Reduce the number of services to process before fetching related data:

```csharp
// Filter at the source to minimize data transfer and processing
var filters = new ServiceFilter
{
    Label = [$"{ServiceLabels.Managed}={ServiceLabels.ManagedValue}"]
};
var managedServices = await serviceOperations.ListServicesAsync(
    new ServicesListParameters { Filters = filters }
);
```

### Cache with Invalidation
For frequently-read, occasionally-written data:

```csharp
// Read path - use cache
public async Task<List<GameType>> GetAllAsync()
{
    return await memoryCache.GetOrCreateAsync(ALL_GAMETYPES_CACHE_KEY, async entry =>
    {
        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
        return await dbContext.GameTypes.ToListAsync();
    });
}

// Write path - invalidate cache
public async Task<GameType> UpdateAsync(GameType gameType)
{
    dbContext.GameTypes.Update(gameType);
    await dbContext.SaveChangesAsync();
    InvalidateCache(); // Remove cached entries
    return gameType;
}
```

## 📝 Testing Performance

When testing performance-critical code:

1. **Use production-scale test data**: 100+ tasks, 10+ services
2. **Measure with logging**: Add timestamps to identify slow operations
3. **Test both code paths**: Single-item operations AND list operations
4. **Monitor Docker API calls**: Check network tab or agent logs for API volume
5. **Verify parallel execution**: Ensure `Task.WhenAll` is actually running in parallel

## 🔧 Debugging Slow Operations

If an operation is slow:

1. Check logs for task/service counts being fetched
2. Look for "Fetching all tasks" or similar unfiltered queries
3. Verify label filters are being applied (check `ServiceFilter` and `TasksListParameters`)
4. Check if parallel processing is used (`Task.WhenAll`)
5. Measure database query time separately from Docker API time
6. Use Application Insights or logging to identify bottlenecks

## 💡 Remember

- **Docker Swarm has hundreds of tasks** - always filter!
- **Not all services are game servers** - filter by labels!
- **GameTypes change rarely** - cache them!
- **Multiple operations can run in parallel** - use `Task.WhenAll`!
- **Test BOTH single-item AND list endpoints** - different code paths!

## 🚦 When in Doubt

If you're unsure whether an operation will perform well at scale:

1. Add logging to show counts of items fetched
2. Test with production-like data volumes
3. Monitor API call counts to Docker/database
4. Ask: "Will this scale to 100 services and 500 tasks?"
