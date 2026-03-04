# Circular Dependency Fix - NodeAgentDiscoveryService

## Problem Summary

The primary service was getting stuck during startup, failing to call `app.Run()` and never displaying the "Now listening on:" message. The logs showed:

```log
[18:23:38] 🚀 WebHost built successfully. Configuring middleware...
[18:23:38] 🎯 WebHost is ready to accept connections. Database initialization will run in background...
[18:23:38] 🔄 Service operations mode: AGENT (via manager node agent)
[HUNG - No further progress]
```

The service operations mode log appearing **after** the webhost logging indicated a **circular dependency** during service resolution when Hosted Services started.

## Root Cause: Circular Dependency

The circular dependency chain was:

```
app.Run()
 └─> Starts HostedServices
      └─> NodeAgentDiscoveryService (HostedService)
           ├─> IGameServerManager
               ├─> GameServerManagerService
                    ├─> DockerServiceHelper
                         ├─> IServiceOperations (factory method)
                              ├─> ServiceOperationsViaAgent
                                   ├─> IAgentRegistry (OK)
                                   ├─> IHttpClientFactory (OK)
                                   └─> ILogger (OK)
                         ├─> INodeAgentDiscovery
                              └─> NodeAgentDiscoveryService ❌ CIRCULAR!
```

**The problem**: `DockerServiceHelper` depends on `INodeAgentDiscovery`, which resolves to `NodeAgentDiscoveryService`, which depends on `IGameServerManager`, which depends on `DockerServiceHelper` → **infinite loop**.

## Solution: Lazy Resolution via IServiceProvider

Instead of injecting `IGameServerManager` directly into `NodeAgentDiscoveryService`, we inject `IServiceProvider` and resolve `IGameServerManager` **lazily** only when needed.

### Changes Made

#### 1. Modified `NodeAgentDiscoveryService` Constructor

**Before**:
```csharp
public NodeAgentDiscoveryService(
    ILogger<NodeAgentDiscoveryService> logger,
    IHttpClientFactory httpClientFactory,
    IGameServerManager serverManager, // ❌ Causes circular dependency
    IOptions<NodeAgentOptions> agentOptions,
    IAgentRegistry agentRegistry,
    IDockerClient? client = null)
{
    _serverManager = serverManager;
    // ...
}
```

**After**:
```csharp
public NodeAgentDiscoveryService(
    ILogger<NodeAgentDiscoveryService> logger,
    IHttpClientFactory httpClientFactory,
    IServiceProvider serviceProvider, // ✅ Breaks circular dependency
    IOptions<NodeAgentOptions> agentOptions,
    IAgentRegistry agentRegistry,
    IDockerClient? client = null)
{
    _serviceProvider = serviceProvider;
    // ...
}
```

#### 2. Lazy Resolution in Method

**Before**:
```csharp
public async Task<NodeAgentEndpoint?> GetAgentForServerAsync(string serverId)
{
    var containerId = await _serverManager.GetRunningContainerIdAsync(serverId); // ❌ Uses injected field
    // ...
}
```

**After**:
```csharp
public async Task<NodeAgentEndpoint?> GetAgentForServerAsync(string serverId)
{
    // Resolve IGameServerManager lazily to avoid circular dependency during construction
    var serverManager = _serviceProvider.GetRequiredService<IGameServerManager>(); // ✅ Resolved when needed
    
    var containerId = await serverManager.GetRunningContainerIdAsync(serverId);
    // ...
}
```

#### 3. Updated Program.cs Registration

**Before**:
```csharp
builder.Services.AddSingleton<NodeAgentDiscoveryService>(sp =>
{
    var serverManager = sp.GetRequiredService<IGameServerManager>(); // ❌ Triggers circular dependency
    
    return new NodeAgentDiscoveryService(
        logger,
        httpClientFactory,
        serverManager, // ❌ Passes problematic dependency
        agentOptions,
        agentRegistry,
        dockerClient);
});
```

**After**:
```csharp
builder.Services.AddSingleton<NodeAgentDiscoveryService>(sp =>
{
    // No longer resolve IGameServerManager here
    
    return new NodeAgentDiscoveryService(
        logger,
        httpClientFactory,
        sp, // ✅ Pass IServiceProvider instead
        agentOptions,
        agentRegistry,
        dockerClient);
});
```

## Why This Works

### Service Provider is Special

`IServiceProvider` is a **root dependency** that doesn't have dependencies itself, so injecting it never causes circular dependencies.

### Lazy Resolution

By resolving `IGameServerManager` **at method call time** instead of **at construction time**, we break the circular chain:

1. `NodeAgentDiscoveryService` constructor completes (only needs `IServiceProvider`)
2. `DockerServiceHelper` can now resolve (gets `INodeAgentDiscovery`)
3. `IGameServerManager` can now resolve (gets `DockerServiceHelper`)
4. Later, when `GetAgentForServerAsync()` is called, it resolves `IGameServerManager` successfully

### Constructor Order

```
Phase 1: Service Registration (app.Build())
├─> Register NodeAgentDiscoveryService (needs IServiceProvider only) ✅
├─> Register DockerServiceHelper (needs INodeAgentDiscovery) ✅
├─> Register IGameServerManager (needs DockerServiceHelper) ✅
└─> Build completes ✅

Phase 2: HostedService Startup (app.Run())
├─> NodeAgentDiscoveryService starts ✅
└─> ExecuteAsync() runs in background ✅

Phase 3: Method Call (later)
└─> GetAgentForServerAsync()
     └─> Resolves IGameServerManager ✅ (no circular dependency now!)
```

## Trade-offs

### Pros
- ✅ Breaks circular dependency
- ✅ Minimal code changes
- ✅ Preserves existing functionality
- ✅ No performance impact (resolution happens once per method call)

### Cons
- ⚠️ Slightly less explicit dependency graph (IDE won't show `IGameServerManager` dependency)
- ⚠️ Service Locator anti-pattern (but acceptable for breaking circular dependencies)
- ⚠️ Potential for `ServiceNotFoundException` at runtime if not careful

## Alternative Solutions Considered

### 1. Remove Dependency Entirely
**Idea**: Don't use `IGameServerManager` in `NodeAgentDiscoveryService`

**Problem**: `GetAgentForServerAsync()` genuinely needs to look up container IDs, which requires `IGameServerManager`

### 2. Create Intermediate Service
**Idea**: Create `IContainerIdLookup` service that both can depend on

**Problem**: Adds complexity and an extra abstraction layer

### 3. Event-Based Communication
**Idea**: Use events instead of direct method calls

**Problem**: Overly complex for this use case

### 4. Restructure Dependencies
**Idea**: Move `INodeAgentDiscovery` out of `DockerServiceHelper`

**Problem**: `DockerServiceHelper` genuinely needs agent discovery for service logs

## Verification

### Expected Logs After Fix

```log
[18:23:38] 🚀 WebHost built successfully. Configuring middleware...
[18:23:38] 🎯 WebHost is ready to accept connections. Database initialization will run in background...
[18:23:38] Now listening on: http://0.0.0.0:8080 ✅
[18:23:38] 🔄 Service operations mode: AGENT (via manager node agent)
[18:23:38] 🔄 Starting background database initialization...
[18:23:39] Initializing database...
[18:23:40] ✅ Background database initialization complete
[18:23:40] [Agent] Connected to Primary Service successfully ✅
```

### Key Indicators
- **"Now listening on:"** appears (webhost started successfully)
- **No hang** after "Service operations mode" message
- **Agents connect** within seconds

## Files Modified

1. **`src/GameServer.Docker/Services/NodeAgentDiscoveryService.cs`**
   - Changed constructor to accept `IServiceProvider` instead of `IGameServerManager`
   - Updated `GetAgentForServerAsync()` to resolve `IGameServerManager` lazily

2. **`src/GameServer.Docker/Program.cs`**
   - Updated `NodeAgentDiscoveryService` registration to pass `IServiceProvider`

3. **`docs/CIRCULAR-DEPENDENCY-FIX.md`** (this document)
   - Technical explanation and verification steps

## Monitoring

After deployment, monitor for:
- ✅ Successful startup (webhost listening within 5 seconds)
- ✅ Agents connecting successfully
- ✅ No `ServiceNotFoundException` errors in logs
- ✅ `GetAgentForServerAsync()` working correctly when called

## Related Issues

This fix addresses:
- Primary service hanging during startup
- "Now listening on" message never appearing
- Agents timing out during connection retries
- Background services not starting

## References

- [Circular Dependencies in .NET DI](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines#circular-dependencies)
- [Service Locator Pattern](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests#issues-with-the-original-httpclient-class-available-in-net)
- [IServiceProvider Usage](https://learn.microsoft.com/en-us/dotnet/api/system.iserviceprovider)
