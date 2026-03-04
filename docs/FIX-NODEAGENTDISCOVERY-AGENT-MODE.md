# Fix: NodeAgentDiscoveryService in Agent Mode

## 🚨 Problem

**Primary Service crashed on startup when `ServiceOperations:Mode=Agent`:**

```
System.InvalidOperationException: IDockerClient is not available when ServiceOperations:Mode=Agent
   at GameServer.Docker.Program.<>c.<Main>b__0_2(IServiceProvider sp) in /src/Program.cs:line 69
```

### Root Cause

`NodeAgentDiscoveryService` **always** required `IDockerClient` in its constructor, but in **Agent mode**, we intentionally throw an exception when anything tries to resolve `IDockerClient` (to prevent accidental usage).

**The conflict:**
- Agent mode: `IDockerClient` → Exception ❌
- NodeAgentDiscoveryService: Requires `IDockerClient` → DI fails ❌

---

## ✅ Solution

**Made `IDockerClient` optional/nullable in `NodeAgentDiscoveryService`:**

### Code Changes

#### 1. Constructor Parameter (Optional)

```csharp
// Before (REQUIRED)
public NodeAgentDiscoveryService(
    IDockerClient client, // ❌ Required
    ...)

// After (OPTIONAL)
public NodeAgentDiscoveryService(
    ILogger<NodeAgentDiscoveryService> logger,
    ...,
    IDockerClient? client = null) // ✅ Optional
```

#### 2. Field Declaration (Nullable)

```csharp
// Before
private readonly IDockerClient _client;

// After
private readonly IDockerClient? _client;
```

#### 3. Null Checks Before Use

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    // Check if IDockerClient is available
    if (_client == null)
    {
        _logger.LogWarning(
            "⚠️ IDockerClient is not available (likely running in Agent mode). " +
            "Background agent discovery via Docker Swarm polling is not possible.");
        return; // Exit early
    }
    
    // Continue with Docker Swarm discovery...
}
```

#### 4. Conditional DI Registration

```csharp
// In Program.cs
builder.Services.AddSingleton<NodeAgentDiscoveryService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<NodeAgentDiscoveryService>>();
    // ... other dependencies

    // Only provide IDockerClient in Direct mode
    IDockerClient? dockerClient = null;
    if (serviceOpsMode.Equals("Direct", StringComparison.OrdinalIgnoreCase))
    {
        dockerClient = sp.GetRequiredService<IDockerClient>();
    }

    return new NodeAgentDiscoveryService(
        logger,
        ...,
        dockerClient); // null in Agent mode, instance in Direct mode
});
```

---

## 📊 Behavior by Mode

### Direct Mode
- `IDockerClient` **provided** ✅
- Background discovery **enabled**
- Polls Docker Swarm for agent tasks
- **Works as before** (no changes)

### Agent Mode
- `IDockerClient` **null** ✅
- Background discovery **disabled automatically**
- Logs warning and exits early
- **Relies solely on agent registration**

---

## 🎯 Impact

| Aspect | Before | After |
|--------|--------|-------|
| **Agent Mode Startup** | Crashes ❌ | Works ✅ |
| **Direct Mode** | Works ✅ | Works ✅ |
| **Background Discovery (Agent mode)** | N/A | Disabled (logs warning) |
| **Background Discovery (Direct mode)** | Works ✅ | Works ✅ |
| **Agent Registration** | Works ✅ | Works ✅ |

---

## 🔍 Logging

### Agent Mode (IDockerClient not available)

```
[WRN] ⚠️ IDockerClient is not available (likely running in Agent mode).
      Background agent discovery via Docker Swarm polling is not possible.
      Using agent registration system only.
```

### Direct Mode (IDockerClient available)

```
[WRN] ⚠️ DEPRECATION WARNING: Background agent discovery via Docker Swarm 
      polling is deprecated. This feature will be removed in a future version.
[INF] Node Agent Discovery background service starting (refresh interval: 30s)
```

---

## ⚙️ Configuration

No configuration changes needed! The system automatically detects the mode.

### Agent Mode (Recommended for Swarm)

```json
{
  "ServiceOperations": {
    "Mode": "Agent"
  }
}
```

**Result:** No Docker Swarm polling, agents register via SignalR

### Direct Mode (Legacy)

```json
{
  "ServiceOperations": {
    "Mode": "Direct"
  }
}
```

**Result:** Docker Swarm polling enabled (deprecated)

---

## 🧪 Testing

### Test Agent Mode

1. **Set configuration:**
```json
{
  "ServiceOperations": { "Mode": "Agent" }
}
```

2. **Start Primary Service:**
```bash
docker service scale gameserver-docker=1
```

3. **Check logs:**
```bash
docker service logs gameserver-docker | grep "IDockerClient"
```

**Expected:**
```
[WRN] ⚠️ IDockerClient is not available (likely running in Agent mode)
```

**Should NOT see:**
```
❌ System.InvalidOperationException: IDockerClient is not available
```

### Test Direct Mode

1. **Set configuration:**
```json
{
  "ServiceOperations": { "Mode": "Direct" }
}
```

2. **Check logs:**

**Expected:**
```
[INF] Node Agent Discovery background service starting
```

---

## 🎓 Lessons Learned

### 1. Optional Dependencies in Constructors

When a service might not need a dependency in all scenarios:
```csharp
// ✅ Good: Optional parameter
public MyService(ILogger logger, IOptionalDep? optional = null)

// ❌ Bad: Required parameter
public MyService(ILogger logger, IOptionalDep required)
```

### 2. Conditional DI Registration

Use factory methods when dependencies vary by configuration:
```csharp
builder.Services.AddSingleton<MyService>(sp =>
{
    var dep = condition ? sp.GetRequiredService<IDep>() : null;
    return new MyService(..., dep);
});
```

### 3. Fail Fast with Clear Messages

```csharp
if (requiredDependency == null)
{
    _logger.LogWarning("Clear explanation of why and what to do");
    return; // Fail gracefully
}
```

---

## 📝 Related

- **Agent Registration:** `AgentRegistrationHub` (new push-based system)
- **Node Agent Discovery:** `NodeAgentDiscoveryService` (deprecated pull-based system)
- **Service Operations:** `IServiceOperations` (abstraction layer)
- **Configuration:** `ServiceOperations:Mode` (Agent vs Direct)

---

**Status:** ✅ Fixed  
**Commit:** c7bc9c6  
**Impact:** Critical (fixes Agent mode startup)  
**Breaking Changes:** None (backward compatible)
