# Refactor Plan: Replace IDockerClient with IServiceOperations

## 🎯 Goal

**Remove all direct IDockerClient usage from Primary Service and use IServiceOperations instead.**

This is the **proper architectural fix** that makes all services work correctly in both Direct and Agent modes.

---

## 🚨 Current Problem

We've been making `IDockerClient` nullable/optional in multiple services:
- ❌ NodeAgentDiscoveryService  
- ❌ ServerLifecycleService  
- ❌ PortAllocator  

**This is a band-aid!** The correct solution: **Use IServiceOperations everywhere.**

---

## ✅ Why IServiceOperations?

### The Abstraction Layer

```
┌─────────────────────────────────────┐
│   Primary Service (Any Service)    │
│                                     │
│  Uses: IServiceOperations           │ ← Abstraction!
└─────────────────┬───────────────────┘
                  │
      ┌───────────┴──────────┐
      │                      │
      ▼                      ▼
┌─────────────────┐  ┌──────────────────┐
│ Direct Mode     │  │  Agent Mode      │
│                 │  │                  │
│ → IDockerClient │  │ → Node Agents    │
│ → Local Socket  │  │ → HTTP/SignalR   │
└─────────────────┘  └──────────────────┘
```

### Benefits
1. ✅ **Works in both modes** - No nullable/optional dependencies
2. ✅ **Clean architecture** - Single abstraction layer
3. ✅ **No runtime checks** - DI handles mode selection
4. ✅ **Testable** - Mock IServiceOperations easily
5. ✅ **Scalable** - Add more implementations (k8s, etc.)

---

## 🔧 Services to Refactor

### 1. NodeAgentDiscoveryService (DEPRECATED)

**Current:**
```csharp
public NodeAgentDiscoveryService(IDockerClient? client, ...)
{
    _client = client;
}

// Uses _client.Tasks.ListAsync()
```

**Recommendation:** 
- **Don't refactor** - This service is deprecated
- Already has `EnableBackgroundDiscovery=false` option
- Will be **removed** in future version
- Agent registration (push-based) replaces it

**Status:** ✅ Leave as-is (optional IDockerClient is fine for deprecated code)

---

### 2. ServerLifecycleService ⚡ HIGH PRIORITY

**Current:**
```csharp
public class ServerLifecycleService
{
    private readonly IDockerClient? _client;
    
    public async Task StartAsync(string serviceName)
    {
        if (_client == null) throw new InvalidOperationException(...);
        await _client.Swarm.UpdateServiceAsync(...);
    }
}
```

**Refactored:**
```csharp
public class ServerLifecycleService
{
    private readonly IServiceOperations _serviceOps;
    
    public ServerLifecycleService(IServiceOperations serviceOps)
    {
        _serviceOps = serviceOps;
    }
    
    public async Task StartAsync(string serviceName)
    {
        // Get current service spec
        var service = await _serviceOps.InspectServiceAsync(serviceName);
        
        // Update replica count to 1
        var updateParams = new ServiceUpdateParameters
        {
            Service = service.Spec,
            Version = service.Version.Index
        };
        updateParams.Service.Mode.Replicated.Replicas = 1;
        
        await _serviceOps.UpdateServiceAsync(serviceName, updateParams);
    }
    
    public async Task StopAsync(string serviceName)
    {
        var service = await _serviceOps.InspectServiceAsync(serviceName);
        var updateParams = new ServiceUpdateParameters
        {
            Service = service.Spec,
            Version = service.Version.Index
        };
        updateParams.Service.Mode.Replicated.Replicas = 0;
        
        await _serviceOps.UpdateServiceAsync(serviceName, updateParams);
    }
}
```

**DI Registration:**
```csharp
// No factory needed! Just inject IServiceOperations
builder.Services.AddScoped<ServerLifecycleService>();
```

**Impact:** ✅ Works in both Agent and Direct modes

---

### 3. PortAllocator ⚡ HIGH PRIORITY

**Current:**
```csharp
public class PortAllocator
{
    private readonly IDockerClient? client;
    
    public async Task<bool> IsPortAvailableAsync(int port, string? protocol = null)
    {
        if (client == null) throw new InvalidOperationException(...);
        var services = await client.Swarm.ListServicesAsync();
        // Check ports...
    }
}
```

**Refactored:**
```csharp
public class PortAllocator
{
    private readonly IServiceOperations _serviceOps;
    
    public PortAllocator(
        IServiceOperations serviceOps,
        IOptions<PortAllocation> portOptions)
    {
        _serviceOps = serviceOps;
        _portOptions = portOptions.Value;
    }
    
    public async Task<bool> IsPortAvailableAsync(int port, string? protocol = null)
    {
        if (port < 1024) return false;
        if (port < _portOptions.StartPort || port > _portOptions.EndPort) return false;
        
        protocol = (protocol ?? "tcp").ToLowerInvariant();
        
        // Use IServiceOperations to list services
        var services = await _serviceOps.ListServicesAsync();
        
        foreach (var svc in services)
        {
            if (svc.Endpoint?.Ports == null || !svc.Endpoint.Ports.Any()) 
                continue;
                
            foreach (var p in svc.Endpoint.Ports)
            {
                if (p.PublishedPort == port && p.Protocol.ToLowerInvariant() == protocol)
                {
                    return false;
                }
            }
        }
        
        return true;
    }
}
```

**DI Registration:**
```csharp
// No factory needed! IServiceOperations handles mode switching
builder.Services.AddSingleton<PortAllocator>();
```

**Impact:** ✅ Works in both Agent and Direct modes

---

### 4. DockerServiceHelper

**Check if it uses IDockerClient...**

Let me check:

---

## 📋 Refactoring Checklist

### Phase 1: ServerLifecycleService (30 min)
- [ ] Update constructor to accept `IServiceOperations`
- [ ] Refactor `StartAsync()` to use `InspectServiceAsync` + `UpdateServiceAsync`
- [ ] Refactor `StopAsync()` to use `InspectServiceAsync` + `UpdateServiceAsync`
- [ ] Refactor `RestartAsync()` (already calls Start/Stop)
- [ ] Update DI registration in Program.cs (remove factory)
- [ ] Build and test
- [ ] Commit

### Phase 2: PortAllocator (20 min)
- [ ] Update constructor to accept `IServiceOperations`
- [ ] Replace `client.Swarm.ListServicesAsync()` with `_serviceOps.ListServicesAsync()`
- [ ] Update DI registration in Program.cs (remove factory)
- [ ] Build and test
- [ ] Commit

### Phase 3: DockerServiceHelper (Check if needed)
- [ ] Search for IDockerClient usage
- [ ] Refactor if needed
- [ ] Commit

### Phase 4: Cleanup
- [ ] Remove all nullable IDockerClient patterns
- [ ] Remove runtime exception throws
- [ ] Remove deprecation warnings
- [ ] Update documentation
- [ ] Final build and test
- [ ] Commit

---

## 🎯 Expected Outcome

### Before (Current - Band-aided)
```csharp
// Each service has optional IDockerClient
public MyService(IDockerClient? client = null) // ❌ Band-aid
{
    if (client == null) throw new Exception(...); // ❌ Runtime checks
}

// DI registration needs factories
builder.Services.AddSingleton<MyService>(sp => { // ❌ Complex
    var client = mode == "Direct" ? sp.GetRequiredService<IDockerClient>() : null;
    return new MyService(client);
});
```

### After (Proper - Clean Architecture)
```csharp
// Each service uses IServiceOperations
public MyService(IServiceOperations serviceOps) // ✅ Clean
{
    _serviceOps = serviceOps;
}

// DI registration is simple
builder.Services.AddSingleton<MyService>(); // ✅ Simple
// IServiceOperations factory already handles mode switching!
```

---

## 💪 Benefits

1. ✅ **Cleaner code** - No nullable checks
2. ✅ **No runtime exceptions** - DI handles everything
3. ✅ **Works in both modes** - Automatically
4. ✅ **Easier to test** - Mock IServiceOperations
5. ✅ **Proper architecture** - Follows abstraction pattern
6. ✅ **Future-proof** - Easy to add k8s, etc.

---

## 🚀 Let's Do It!

Want me to refactor these services now? It'll take about 60-90 minutes total:

1. **ServerLifecycleService** - Start/Stop/Restart using IServiceOperations
2. **PortAllocator** - List services using IServiceOperations
3. **Remove all nullable IDockerClient patterns**
4. **Simplify DI registration**

This is the **right way** to fix the architecture! 🎯
