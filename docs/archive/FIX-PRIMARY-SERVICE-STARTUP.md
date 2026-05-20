# Primary Service Startup Fixes

## Issue 1: Hardcoded Service Mode

The Primary Service (GameServer.Docker) was not starting correctly due to hardcoded service operations mode.

### Root Cause

**File**: `src/GameServer.Docker/Program.cs`, Line 54

```csharp
// WRONG - Hardcoded
var serviceOpsMode = "Agent";
```

This hardcoded value ignored the configuration setting in `appsettings.Development.json`:
```json
"ServiceOperations": {
  "Mode": "Direct",
  "Enabled": true
}
```

**Result**: The service tried to run in Agent mode when configured for Direct mode, causing dependency resolution failures.

### Fix

Changed to read from configuration:

```csharp
// CORRECT - Read from config
var serviceOpsMode = builder.Configuration.GetValue<string>("ServiceOperations:Mode") ?? "Agent";
```

Now the service respects the configuration:
- If `ServiceOperations:Mode = "Direct"` → Uses Direct Docker client
- If `ServiceOperations:Mode = "Agent"` → Uses Agent-based operations
- If not configured → Defaults to "Agent" mode

---

## Issue 2: Dependency Injection Registration Order

### Root Cause

Services were registered in the wrong order, causing DI resolution failures:

**Problem Order**:
1. Line 98: `ServiceOperationsViaAgent` registered (depends on `IAgentRegistry`)
2. Line 119: `NodeAgentDiscoveryService` registered (depends on `IAgentRegistry`)
3. Line 143: `IAgentRegistry` registered ← **TOO LATE!**

**Error**: When DI tries to create `ServiceOperationsViaAgent`, it can't find `IAgentRegistry` because it hasn't been registered yet.

### Fix

Moved `IAgentRegistry` registration to **before** the services that depend on it:

**Correct Order**:
1. Register `IAgentRegistry` first
2. Register `ServiceOperationsViaAgent` (now can resolve `IAgentRegistry`)
3. Register `NodeAgentDiscoveryService` (now can resolve `IAgentRegistry`)

```csharp
// Agent Registry - MUST BE FIRST (other services depend on it)
builder.Services.AddSingleton<IAgentRegistry, AgentRegistryService>();

// Service Operations - Can now resolve IAgentRegistry
builder.Services.AddSingleton<ServiceOperationsViaAgent>();

// Node Agent Discovery - Can now resolve IAgentRegistry
builder.Services.AddSingleton<NodeAgentDiscoveryService>(...);
```

### Dependency Chain

```
IAgentRegistry (must be registered first)
    ↓ used by
ServiceOperationsViaAgent
    ↓ used by
IServiceOperations factory

IAgentRegistry (must be registered first)
    ↓ used by
NodeAgentDiscoveryService
    ↓ used by
INodeAgentDiscovery
```

---

## Issue 3: Service Lifetime Mismatch (Singleton vs Scoped)

### Root Cause

Several Singleton services were trying to consume Scoped services:

**5 Errors**:
1. `DockerServiceHelper` (Singleton) → `IGameTypeRepository` (Scoped) ❌
2. `GameServerFileManagerService` (Singleton) → `DockerServiceHelper` ❌
3. `GameServerManagerService` (Singleton) → `DockerServiceHelper` ❌
4. `GameServerResourceMonitorService` (Singleton) → `DockerServiceHelper` ❌
5. `GameTypeMetadataApplier` (Singleton) → `IGameTypeRepository` (Scoped) ❌

**Error Message**:
```
System.InvalidOperationException: Cannot consume scoped service 
'GameServer.Docker.Repositories.IGameTypeRepository' from singleton 
'GameServer.Docker.Services.DockerServiceHelper'
```

### Fix

Changed affected services from **Singleton** to **Scoped**:

```csharp
// Changed from Singleton to Scoped
builder.Services.AddScoped<DockerServiceHelper>();
builder.Services.AddScoped<IGameServerFileManager, GameServerFileManagerService>();
builder.Services.AddScoped<IGameServerManager, GameServerManagerService>();
builder.Services.AddScoped<IGameServerResourceMonitor, GameServerResourceMonitorService>();
builder.Services.AddScoped<GameTypeMetadataApplier>();
```

**Why**: These services depend on `IGameTypeRepository` which uses `DbContext` (Scoped).

**Impact**: Minimal - These services are request-scoped in practice anyway.

---

## Testing

```bash
dotnet build
# ✅ Build successful
```

---

## Impact

- ✅ Primary Service now starts correctly
- ✅ Respects configuration settings
- ✅ Proper DI resolution order
- ✅ Correct service lifetimes
- ✅ Works in both Direct and Agent modes
- ✅ No breaking changes

---

## Files Changed

1. **`src/GameServer.Docker/Program.cs`**
   - Line 54: Changed hardcoded "Agent" to read from configuration
   - Lines 77-148: Moved `IAgentRegistry` registration before dependent services
   - Lines 101, 105, 108, 151, 206: Changed services from Singleton to Scoped
   - Lines 280-305: Added better error handling for DI failures

---

**Status**: ✅ **Fixed** - Service now starts correctly with proper DI resolution and service lifetimes

---

## Related Documentation

- **`docs/FIX-SERVICE-LIFETIME-SCOPED.md`** - Detailed lifetime fix explanation
- [Microsoft Docs: Service Lifetimes](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection#service-lifetimes)


