# Agent Registration Migration Guide

## Overview

GameServer.Docker is migrating from **pull-based agent discovery** (Primary Service queries Docker Swarm) to **push-based agent registration** (Agents connect and register themselves).

## Current Status (Phase 3)

✅ **Both systems run in parallel**
- Old discovery system is **deprecated but still enabled by default**
- New registration system runs alongside
- Automatic fallback ensures no disruption

## Why Migrate?

### Old System (Pull-Based Discovery)
- Primary Service polls Docker Swarm API every 15 seconds
- Queries all tasks to find agent containers
- Extracts overlay network IPs from task metadata
- High Docker API load on manager nodes
- Only works with Docker Swarm

### New System (Push-Based Registration)
- Agents connect to Primary Service via SignalR
- Agents send heartbeats with container list every 30 seconds
- O(1) container-to-agent lookups (in-memory dictionary)
- No Docker API queries needed for agent discovery
- Works with any orchestrator (Swarm, K8s, standalone Docker)

### Performance Comparison

| Operation | Old System | New System |
|-----------|-----------|------------|
| Find agent for container | O(n) Docker API query + task filtering | O(1) dictionary lookup |
| API calls per minute | ~4 Docker queries (every 15s) | 0 Docker queries |
| Network overhead | Query all tasks every 15s | Heartbeat per agent every 30s |
| Latency | 100-500ms (Docker API) | <1ms (in-memory) |

## Migration Steps

### Step 1: Verify Agent Registration (Current)

**Check agents are registering:**

```bash
# Watch Primary Service logs for registration messages
docker service logs -f gameserver-docker | grep "Agent registered"

# Expected output:
# [INFO] Agent registered: Node=worker-1 (abc123), ConnectionId=xyz, Url=http://172.18.0.5:8080
```

**Check agent logs:**

```bash
# Watch agent logs
docker service logs -f gameserver-agent | grep "registered with Primary"

# Expected output:
# [INFO] Agent registered with Primary Service: Node=worker-1 (abc123)
```

### Step 2: Monitor Log Output

Look for these log markers in Primary Service:

```
✅ Found agent via REGISTRY (push-based) for container abc123...
```

If you see this frequently, agents are registering successfully!

If you see this:
```
⚠️ Agent not found in registry, falling back to Docker Swarm query (pull-based discovery)
✅ Found agent via DISCOVERY (pull-based) for container abc123...
```

Then the agent hasn't registered yet (or registration is disabled).

### Step 3: Disable Legacy Discovery (Future)

Once you're confident agents are registering reliably:

**Update `appsettings.json`:**

```json
{
  "NodeAgentOptions": {
    "EnableBackgroundDiscovery": false
  }
}
```

**Or set environment variable:**

```yaml
environment:
  - NodeAgentOptions__EnableBackgroundDiscovery=false
```

**Restart Primary Service:**

```bash
docker service update --force gameserver-docker
```

**Check logs:**

```
⚠️ Background agent discovery is DISABLED. Using agent registration system only.
```

### Step 4: Verify Everything Works

1. **Create a game server** - should start normally
2. **View logs** - should stream without errors
3. **Open terminal** - should attach successfully
4. **Monitor resources** - stats should display

If any operation fails:
- Re-enable legacy discovery temporarily
- Check agent registration configuration
- Verify network connectivity between agents and Primary Service

## Configuration Reference

### Agent Configuration (`appsettings.json`)

```json
{
  "AgentRegistration": {
    "PrimaryServiceUrl": "http://gameserver-docker:8080",
    "HeartbeatIntervalSeconds": 30,
    "Enabled": true,
    "Capabilities": ["logs", "exec", "stats", "attach"],
    "ConnectionTimeoutSeconds": 30,
    "ReconnectDelaySeconds": [0, 2, 10, 30]
  }
}
```

**Environment Variables:**

```bash
AgentRegistration__PrimaryServiceUrl=http://gameserver-docker:8080
AgentRegistration__HeartbeatIntervalSeconds=30
AgentRegistration__Enabled=true
```

### Primary Service Configuration

```json
{
  "NodeAgentOptions": {
    "EnableBackgroundDiscovery": true  // Set to false to disable legacy discovery
  }
}
```

## Troubleshooting

### Agents Not Registering

**Check agent logs:**

```bash
docker service logs gameserver-agent | grep -i "registration\|error"
```

**Common issues:**

1. **Wrong Primary Service URL**
   - Check `AgentRegistration:PrimaryServiceUrl` in agent config
   - Ensure agents can reach Primary Service (network connectivity)
   - Test: `curl http://gameserver-docker:8080/hubs/agentregistration` from agent

2. **SignalR connection fails**
   - Check firewall rules
   - Verify CORS configuration in Primary Service
   - Check WebSocket support

3. **Agent registration disabled**
   - Verify `AgentRegistration:Enabled=true` in agent config

### Primary Service Not Seeing Agents

**Check Primary Service logs:**

```bash
docker service logs gameserver-docker | grep -i "agent\|registry"
```

**Check registry status:**

Add a debug endpoint to check registered agents:

```csharp
// In a controller
[HttpGet("debug/agents")]
public IActionResult GetRegisteredAgents([FromServices] IAgentRegistry registry)
{
    var agents = registry.GetAllAgents();
    return Ok(new {
        TotalAgents = agents.Count,
        HealthyAgents = agents.Count(a => a.IsHealthy),
        Agents = agents.Select(a => new {
            a.NodeId,
            a.NodeName,
            a.InternalUrl,
            a.IsHealthy,
            a.LastHeartbeat,
            SecondsSinceLastHeartbeat = (DateTime.UtcNow - a.LastHeartbeat).TotalSeconds
        })
    });
}
```

### Performance Issues

**Heartbeat interval too aggressive:**

```json
{
  "AgentRegistration": {
    "HeartbeatIntervalSeconds": 60  // Increase from default 30
  }
}
```

**Registry not being used:**

Check logs for:
```
✅ Found agent via REGISTRY (push-based)
```

If not present, agents aren't registering or registration is disabled.

## Rollback Plan

If issues occur after disabling legacy discovery:

1. **Re-enable legacy discovery:**

```json
{
  "NodeAgentOptions": {
    "EnableBackgroundDiscovery": true
  }
}
```

2. **Restart Primary Service**

3. **System automatically falls back to Docker Swarm queries**

4. **Investigate agent registration issues before trying again**

## Timeline

- **Current (Phase 3)**: Both systems running, deprecation warnings
- **Next Release**: Legacy discovery disabled by default (opt-in to enable)
- **Future Release**: Legacy discovery code removed entirely

## Support

If you encounter issues during migration:

1. Check this guide
2. Review logs with log markers (✅, ⚠️)
3. Verify configuration against this guide
4. Open a GitHub issue with:
   - Primary Service logs
   - Agent logs
   - Configuration files (redact secrets)
