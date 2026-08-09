# Agent Capability Filtering by Node Role

## 🎯 Architecture Principle

**Capabilities must match what the Docker daemon can actually do on that node!**

In Docker Swarm:
- **Manager nodes:** Full API access (containers + services + swarm)
- **Worker nodes:** Container-only API access (logs, exec, stats)

---

## 🚨 Problem (Before)

**All agents advertised ALL capabilities regardless of node role:**

```
Worker Node (newdev-docker-002):
  Capabilities: logs, exec, stats, attach, services ❌
  Manager: False
  
  Problem: Claims "services" but CAN'T actually do:
    - docker service create
    - docker service update
    - docker service ls
    - docker task ls
```

**Result:** Primary Service might route service operations to worker nodes → **API fails!**

---

## ✅ Solution (After)

**Capabilities are now filtered based on node role:**

### Manager Node
```
Manager Node (newdev-docker-001):
  Capabilities: logs, exec, stats, attach, services ✅
  Manager: True
  
  Can do: Everything!
    ✅ Container operations (logs, exec, stats, attach)
    ✅ Service operations (create, update, delete, list)
    ✅ Task operations (list, inspect)
    ✅ Node operations (list, inspect)
    ✅ Swarm operations (inspect, update)
```

### Worker Node
```
Worker Node (newdev-docker-002):
  Capabilities: logs, exec, stats, attach ✅
  Manager: False
  
  Can do: Container operations only!
    ✅ Container operations (logs, exec, stats, attach)
    ❌ Service operations (filtered out!)
    ❌ Task operations (filtered out!)
    ❌ Node operations (filtered out!)
    ❌ Swarm operations (filtered out!)
```

---

## 🔧 Implementation

### Filtering Logic

```csharp
private static List<string> FilterCapabilitiesByNodeRole(
    List<string> configuredCapabilities, 
    bool isManagerNode)
{
    // Manager-only capabilities (require Docker Swarm manager API access)
    var managerOnlyCapabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "services",  // IDockerClient.Swarm.* service operations
        "tasks",     // IDockerClient.Tasks.* operations
        "nodes",     // IDockerClient.Swarm.* node operations
        "swarm"      // IDockerClient.Swarm.* cluster operations
    };

    if (!isManagerNode)
    {
        // Worker nodes: Filter out manager-only capabilities
        return configuredCapabilities
            .Where(cap => !managerOnlyCapabilities.Contains(cap))
            .ToList();
    }

    // Manager nodes: Keep all capabilities
    return configuredCapabilities;
}
```

### When Applied

Filtering happens at **registration time** (and re-registration after reconnect):

```csharp
private async Task RegisterAsync()
{
    // Filter based on node role detected from Docker daemon
    var capabilities = FilterCapabilitiesByNodeRole(_options.Capabilities, _isManagerNode);
    
    // Register with filtered capabilities
    await _hubConnection.InvokeAsync("RegisterAgent", new {
        NodeId = _nodeId,
        Capabilities = capabilities,  // ✅ Filtered!
        IsManagerNode = _isManagerNode
    });
}
```

---

## 📊 Capability Matrix

| Capability | API Endpoint | Manager | Worker | Notes |
|------------|-------------|---------|--------|-------|
| **logs** | `IContainerOperations.GetContainerLogsAsync` | ✅ | ✅ | Container logs |
| **exec** | `IContainerOperations.ExecCreateContainerAsync` | ✅ | ✅ | Execute commands |
| **stats** | `IContainerOperations.GetContainerStatsAsync` | ✅ | ✅ | Resource stats |
| **attach** | `IContainerOperations.AttachContainerAsync` | ✅ | ✅ | TTY attach |
| **services** | `ISwarmOperations.*` | ✅ | ❌ | Create/update services |
| **tasks** | `ITaskOperations.*` | ✅ | ❌ | List/inspect tasks |
| **nodes** | `ISwarmOperations.*` nodes | ✅ | ❌ | Manage nodes |
| **swarm** | `ISwarmOperations.*` config | ✅ | ❌ | Swarm config |

---

## 🎯 Routing Impact

### Before (Broken)
```
Primary Service needs to create a service:
  → Picks any agent with "services" capability
  → Might pick worker node
  → Docker API fails: "This node is not a swarm manager"
  → Operation fails ❌
```

### After (Fixed)
```
Primary Service needs to create a service:
  → Filters agents by "services" capability
  → Only manager nodes have this capability
  → Routes to manager agent
  → Docker API succeeds ✅
```

---

## 🔍 Logging

### Manager Node Registration
```
[INF] Agent initialized: IsManager=True
[INF] Agent registered: Capabilities=logs, exec, stats, attach, services, Manager=True
      ✅ All capabilities advertised
```

### Worker Node Registration
```
[INF] Agent initialized: IsManager=False
[INF] Agent registered: Capabilities=logs, exec, stats, attach, Manager=False
      ✅ "services" filtered out!
```

---

## ⚙️ Configuration

### Default (Recommended)

```json
{
  "AgentRegistration": {
    "Capabilities": ["logs", "exec", "stats", "attach", "services"]
  }
}
```

**Behavior:**
- Manager nodes: All 5 capabilities
- Worker nodes: 4 capabilities (services removed)

### Container Operations Only

```json
{
  "AgentRegistration": {
    "Capabilities": ["logs", "exec", "stats", "attach"]
  }
}
```

**Behavior:**
- Manager nodes: 4 capabilities (no services)
- Worker nodes: 4 capabilities (same)

### Custom

```json
{
  "AgentRegistration": {
    "Capabilities": ["logs", "stats"]  // Minimal
  }
}
```

---

## 🧪 Testing

### Verify Capability Filtering

1. **Deploy agents:**
```bash
docker service scale gameserver-agent=5
```

2. **Check logs for manager:**
```bash
docker service logs gameserver-agent | grep "Manager=True"
```

**Expected:**
```
Agent registered: Capabilities=logs, exec, stats, attach, services, Manager=True
                                                          ^^^^^^^^ ✅
```

3. **Check logs for workers:**
```bash
docker service logs gameserver-agent | grep "Manager=False"
```

**Expected:**
```
Agent registered: Capabilities=logs, exec, stats, attach, Manager=False
                                                          (no services!) ✅
```

### Test Service Operations

```bash
# Try creating a service via Primary Service
curl -X POST http://gameserver-docker:8080/api/servers \
  -H "Content-Type: application/json" \
  -d '{"gameType":"minecraft","name":"test"}'
```

**Expected:** Primary Service routes to manager agent, operation succeeds ✅

---

## 🎓 Docker Swarm API Restrictions

### Manager API Endpoints (Restricted)

These require manager node access:
```csharp
client.Swarm.CreateServiceAsync()     // ❌ Workers
client.Swarm.UpdateServiceAsync()     // ❌ Workers
client.Swarm.RemoveServiceAsync()     // ❌ Workers
client.Swarm.ListServicesAsync()      // ❌ Workers
client.Tasks.ListAsync()              // ❌ Workers
client.Swarm.ListNodesAsync()         // ❌ Workers
```

**Error if called on worker:**
```
This node is not a swarm manager. Worker nodes can't be used to view or modify cluster state.
```

### Container API Endpoints (Unrestricted)

These work on all nodes:
```csharp
client.Containers.GetContainerLogsAsync()    // ✅ All nodes
client.Containers.ExecCreateContainerAsync() // ✅ All nodes
client.Containers.GetContainerStatsAsync()   // ✅ All nodes
client.Containers.AttachContainerAsync()     // ✅ All nodes
```

---

## 📋 Architecture Benefits

1. ✅ **Correct routing:** Operations go to capable nodes
2. ✅ **No false advertising:** Agents only claim what they can do
3. ✅ **Clear errors:** If no manager available, fail fast
4. ✅ **Scalable:** Add 100 worker nodes, 3 managers - works!
5. ✅ **Resilient:** Manager goes down? Use another manager

---

## 🔮 Future Enhancements

### Load Balancing Among Managers

If multiple managers exist, distribute service operations:

```csharp
// Get all manager agents with "services" capability
var managerAgents = _agentRegistry
    .GetAgents()
    .Where(a => a.Capabilities.Contains("services"))
    .ToList();

// Round-robin or least-busy selection
var selectedAgent = SelectAgent(managerAgents);
```

### Health-Based Capability Adjustment

Dynamically adjust capabilities based on node health:

```csharp
// If node is unhealthy, reduce capabilities
if (nodeHealth == "unhealthy")
{
    capabilities = capabilities.Where(c => c == "logs").ToList();
}
```

---

## 📝 Related

- **Docker Swarm Roles:** Manager vs Worker nodes
- **IServiceOperations:** Should route to manager agents for service ops
- **ServiceOperationsViaAgent:** Uses agent registry to find capable agents
- **Agent Discovery:** Filters agents by capability for operations

---

**Status:** ✅ Fixed  
**Commit:** 59b8474  
**Impact:** Critical (correct routing in multi-node Swarm)  
**Breaking Changes:** None (better behavior)

---

## 🎉 Expected Logs (After Deploy)

```
Manager Node:
[INF] Agent registered: Capabilities=logs, exec, stats, attach, services, Manager=True ✅

Worker Nodes:
[INF] Agent registered: Capabilities=logs, exec, stats, attach, Manager=False ✅
[INF] Agent registered: Capabilities=logs, exec, stats, attach, Manager=False ✅
[INF] Agent registered: Capabilities=logs, exec, stats, attach, Manager=False ✅
[INF] Agent registered: Capabilities=logs, exec, stats, attach, Manager=False ✅
```

Perfect separation! 🎯
