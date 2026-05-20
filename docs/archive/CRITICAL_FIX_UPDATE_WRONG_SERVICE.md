# 🚨 CRITICAL BUG FIX: Update Operation Modifying Wrong Service

## The Problem

When updating a GameServer (e.g., Valheim1), the code was potentially updating the **wrong service** in Docker Swarm!

### Root Cause

In `DockerServiceHelper.cs` line 681-697, the update logic:

1. ✅ Correctly fetches the existing server by `ServerId` at line 664: `var existing = await GetGameServerById(server.ServerId);`
2. ❌ **Then throws away that information** and searches again by `Name` at line 681-689
3. ❌ Uses `services.First()` at line 697, which could return the WRONG service if:
   - Multiple services have similar names
   - The Name filter doesn't work as expected
   - There are duplicate services

### The Bug (lines 681-689)

```csharp
// WRONG: Filters by Name instead of ServerId
var serviceFilter = new ServiceFilter
{
    Name = new[] { existing.ServiceName }  // ❌ Name might not be unique!
};

var services = await serviceOperations.ListServicesAsync(new ServicesListParameters
{
    Filters = serviceFilter
});

var service = services.First();  // ❌ Could be the wrong service!
```

## The Fix

Replace lines 676-697 in `src/GameServer.Docker/Services/DockerServiceHelper.cs`:

```csharp
else
{
    logger.LogInformation($"Updating existing GameServer: {server.Name} ({server.ServerId})");

    // Get the existing service from Docker by ServerId label filter
    // IMPORTANT: Use ServerId (not Name) to ensure we update the correct service
    var serviceFilter = new ServiceFilter
    {
        Label = new[] { $"{ServiceLabels.ServerId}={server.ServerId}" }  // ✅ Use unique ServerId!
    };

    var services = await serviceOperations.ListServicesAsync(new ServicesListParameters
    {
        Filters = serviceFilter
    });

    if (!services.Any())
    {
        logger.LogError("Failed to find existing service for update with ServerId: {ServerId}", server.ServerId);
        throw new InvalidOperationException($"Existing service with ServerId '{server.ServerId}' not found for update.");
    }

    if (services.Count > 1)
    {
        // ✅ Detect and report duplicate services
        logger.LogError("❌ CRITICAL: Multiple services found with ServerId={ServerId}! Services: {ServiceNames}", 
            server.ServerId, 
            string.Join(", ", services.Select(s => $"{s.Spec?.Name}({s.ID})")));
        throw new InvalidOperationException($"Multiple services found with ServerId '{server.ServerId}'. This indicates duplicate services in Docker Swarm!");
    }

    var service = services.First();
    logger.LogInformation("Found service to update: ID={ServiceId}, Name={ServiceName}", service.ID, service.Spec?.Name);
```

## Why This Fixes It

1. ✅ **Uses unique ServerId label** instead of Name for filtering
2. ✅ **Detects duplicate services** if multiple services have the same ServerId
3. ✅ **Logs which service** is being updated for debugging
4. ✅ **Consistent with GetGameServerById()** which also uses ServerId label filter

## Impact

- **Before**: Could update the wrong service if Name filter matches multiple services
- **After**: Always updates the correct service by using the unique ServerId label
- **Bonus**: Detects and prevents updates when duplicate services exist
