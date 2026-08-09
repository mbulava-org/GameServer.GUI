# 🔧 Critical Fix: Agent URL Uses Docker Host Instead of Task Name

## 🚨 Problem

**Agents were advertising URLs using physical Docker host names instead of Swarm overlay network task names.**

### What Was Happening

```
Agent initialized: AgentUrl=http://newdev-docker-004:8080
                                     ^^^^^^^^^^^^^^^^^^
                                     PHYSICAL HOST! ❌
```

**Why This Is Wrong:**
- `newdev-docker-004` is the **physical/VM hostname** of the Docker node
- Other tasks in the **overlay network** can't reach this hostname
- Port 8080 on the physical host might be load-balanced to a **different** agent instance
- **Result:** Communication failures, wrong agent routing

### Root Cause

```csharp
// BEFORE (WRONG)
_nodeName = info.Name; // Returns Docker node hostname
_agentUrl = $"http://{_nodeName}:8080";
// Result: http://newdev-docker-004:8080 ❌
```

`info.Name` from Docker API returns the **Docker node's hostname** (the physical machine), not the container's hostname.

---

## ✅ Solution

**Use the container's hostname (task name) for the Agent URL.**

### Docker Swarm Overlay Networking

In Docker Swarm overlay networks:

| Name | Example | What It Is | Used For |
|------|---------|------------|----------|
| **Docker Node Hostname** | `newdev-docker-004` | Physical/VM machine name | Identifying which node (❌ not for networking) |
| **Task Hostname** | `gameserver-agent.1.xyz123` | Container/task instance name | Overlay network routing (✅ correct!) |
| **Service Name** | `gameserver-agent` | Load-balanced service | Round-robin to all replicas |

### The Fix

```csharp
// AFTER (CORRECT)
// Environment.MachineName returns the CONTAINER's hostname in Docker
// Docker Swarm sets this to the task name (e.g., "gameserver-agent.1.xyz123")
var agentHost = Environment.GetEnvironmentVariable("AGENT_HOST") ?? Environment.MachineName;
_agentUrl = $"http://{agentHost}:8080";
// Result: http://gameserver-agent.1.xyz123:8080 ✅
```

**Key Insight:** In .NET running in a Docker container, `Environment.MachineName` returns the **container's hostname**, which Docker Swarm sets to the task name.

---

## 📊 Impact

### Before (Broken)
```
Agent A registers as: http://newdev-docker-004:8080
Primary Service tries to reach: http://newdev-docker-004:8080
  → Goes to physical host
  → Load balancer might route to Agent B
  → Wrong agent! ❌
```

### After (Correct)
```
Agent A registers as: http://gameserver-agent.1.xyz123:8080
Primary Service tries to reach: http://gameserver-agent.1.xyz123:8080
  → Routes through overlay network
  → Directly to Agent A's container
  → Correct agent! ✅
```

---

## 🔍 Debugging

### How to Verify

**Inside the agent container:**

```bash
# Container hostname (task name) - CORRECT for Agent URL
hostname
# Output: gameserver-agent.1.abcd1234efgh5678

# Docker node hostname - WRONG for Agent URL (used for NodeName only)
docker node inspect self --format '{{.Description.Hostname}}'
# Output: newdev-docker-004
```

**Check logs:**
```
[INFO] Agent initialized: NodeName=newdev-docker-004, AgentUrl=http://gameserver-agent.1.xyz:8080
       NodeName identifies the physical node ✅
       AgentUrl uses task hostname for networking ✅
```

**New debug log added:**
```
[DEBUG] Agent network identity: 
        DockerNodeHostname=newdev-docker-004, 
        TaskHostname=gameserver-agent.1.xyz123, 
        ServiceUrl=http://gameserver-agent.1.xyz123:8080
```

---

## ⚙️ Configuration

### Default (Recommended)
```yaml
# No configuration needed!
# Automatically detects task hostname
```

### Override (If Needed)
```yaml
environment:
  - AGENT_HOST=custom-hostname  # Override detection
  - AGENT_PORT=9090            # Override port
```

---

## 🧪 Testing

### Test in Docker Swarm

1. **Deploy the service:**
```bash
docker service create \
  --name gameserver-agent \
  --replicas 3 \
  gameserver-agent:latest
```

2. **Check logs:**
```bash
docker service logs gameserver-agent | grep "Agent initialized"
```

3. **Verify URLs use task names:**
```
gameserver-agent.1.xyz ✅ AgentUrl=http://gameserver-agent.1.xyz:8080
gameserver-agent.2.abc ✅ AgentUrl=http://gameserver-agent.2.abc:8080
gameserver-agent.3.def ✅ AgentUrl=http://gameserver-agent.3.def:8080
```

4. **Test connectivity from another service:**
```bash
docker exec -it <primary-service-container> \
  curl http://gameserver-agent.1.xyz:8080/health
# Should succeed! ✅
```

---

## 🎯 Key Learnings

### Docker Swarm Networking 101

1. **Overlay Networks:** Containers communicate via task hostnames
2. **Service Discovery:** DNS resolves service names to all replicas
3. **Task Names:** Unique identifier for each container instance
4. **Node Names:** Identify physical machines (not for networking!)

### .NET in Docker

1. **`Environment.MachineName`** → Container hostname (task name) ✅
2. **`Dns.GetHostName()`** → Same as Environment.MachineName
3. **Docker API `info.Name`** → Docker node hostname ❌

### Best Practices

✅ **DO:** Use `Environment.MachineName` for agent URLs  
✅ **DO:** Use task hostnames for overlay network communication  
✅ **DO:** Use service names for load-balanced communication  
❌ **DON'T:** Use Docker node hostnames for inter-service URLs  
❌ **DON'T:** Assume host networking in Swarm  

---

## 📝 Related Issues

- Agent connection failures in multi-node Swarm
- Primary Service can't reach specific agents
- Load balancer routing to wrong agent
- Overlay network misconfiguration

---

## 🚀 Deployment Checklist

- [x] Code updated to use `Environment.MachineName`
- [x] Added detailed comments explaining the distinction
- [x] Added debug logging for network identity
- [x] Build successful
- [x] Documentation created
- [ ] Test in multi-node Swarm
- [ ] Verify Primary Service can reach agents
- [ ] Monitor logs for correct task hostnames

---

**Status:** ✅ Fixed  
**Severity:** Critical (breaks multi-node communication)  
**Commit:** [pending]  
**Tested:** Build successful, awaiting deployment test
