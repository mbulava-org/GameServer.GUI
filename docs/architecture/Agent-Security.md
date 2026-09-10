# Agent Security - Internal Network Only

## Architecture Change: No External Exposure

The agent API is **NOT exposed** outside the Docker overlay network. This is much more secure!

### How It Works

```
???????????????????????????????????????????????
?  Primary Service (on gameserver-network)     ?
?  ?????????????????????????????????????????? ?
?  ?  Discovers agent overlay IPs via       ? ?
?  ?  task.NetworksAttachments              ? ?
?  ?????????????????????????????????????????? ?
?????????????????????????????????????????????-?
                  ? Internal Overlay Network
                  ? (gameserver-network)
        ?????????????????????
        ?         ?         ?
???????????????????? ???????????????????
? Node 1           ? ? Node 2          ?
?  Agent           ? ?  Agent          ?
?  10.0.1.3:8080   ? ?  10.0.1.5:8080  ?
?  (overlay IP)    ? ?  (overlay IP)   ?
?  ? Internal only? ?  ? Internal only?
???????????????????? ???????????????????
```

### Key Changes

1. **No Host Port Binding**
   - Agents NOT accessible from outside the swarm
   - No port 8080 conflicts
   - No firewall configuration needed

2. **Overlay Network IPs**
   - Agents get IPs on `gameserver-network` (e.g., 10.0.1.x)
   - Primary service discovers these IPs via `task.NetworksAttachments`
   - Routes directly to correct agent on overlay network

3. **Automatic Routing**
   - Each agent has unique overlay IP
   - Primary service caches these IPs (30s)
   - No load balancing issues - correct agent every time

## Discovery Implementation

```csharp
// In NodeAgentDiscoveryService.cs:

// Get the overlay network IP from task's NetworkAttachments
var networkAttachment = task.NetworksAttachments?
    .FirstOrDefault(na => na.Network?.Spec?.Name == "gameserver-network");

var agentIp = networkAttachment?.Addresses?.FirstOrDefault()?.Split('/')[0];
// Example: "10.0.1.3/24" -> "10.0.1.3"

var internalUrl = $"http://{agentIp}:{AGENT_PORT}";
// Example: "http://10.0.1.3:8080"
```

## Security Benefits

### ? What This Achieves:

1. **Zero External Exposure**
   - Agent API only accessible within swarm overlay network
   - No risk of external attacks on agent endpoints
   - No need for API authentication (internal only)

2. **Simpler Firewall Rules**
   - No need to open port 8080 on nodes
   - Overlay network handles all routing
   - Docker's built-in network security

3. **Better Isolation**
   - Agents can only be reached by services on same network
   - Primary service must be on `gameserver-network`
   - Clear security boundary

4. **No Port Conflicts**
   - Port 8080 only used within containers
   - No conflicts with other services on nodes
   - Easier to deploy

### Network Requirements

**Before (Host Port Binding):**
```yaml
ports:
  - target: 8080
    published: 8080
    protocol: tcp
    mode: host  # ? Exposed on host
```
- ? Accessible from outside swarm
- ? Port conflicts possible
- ? Firewall rules needed

**Now (Overlay Network Only):**
```yaml
# No ports section!
networks:
  - gameserver-network
```
- ? Internal only
- ? No port conflicts
- ? No firewall changes needed

## Deployment

### Prerequisites

**Only requirement:** Primary service must be on `gameserver-network`

```yaml
# In your primary service stack:
services:
  gameserver-management:
    networks:
      - gameserver-network

networks:
  gameserver-network:
    external: true
```

### Deploy Agent

```bash
# Create network (if not exists)
docker network create --driver overlay --attachable gameserver-network

# Deploy agents - no port exposure!
docker stack deploy -c docker-stack-agent.yml gameserver-agent

# Verify
docker service ps gameserver-agent
```

### Verify Network Connectivity

```bash
# From primary service container, exercise an agent directly
docker exec {primary-container} curl http://{agent-overlay-ip}:8080/health
docker exec {primary-container} curl http://{agent-overlay-ip}:8080/api/containers
```

## Testing

### 1. Confirm Agents Have Registered

Agents push registration to the primary service over `/hubs/agentregistration`. Verify from the primary service's logs (search for `AgentRegistrationHub`) that each expected node has connected and reported its overlay IP.

Example registration payload logged by the hub:
```json
{
  "nodeId": "abc123",
  "nodeName": "node-1",
  "internalUrl": "http://10.0.1.3:8080",
  "capabilities": ["containers", "services", "tasks"],
  "isHealthy": true
}
```

### 2. Test Stats (Uses Correct Agent)

Open the V2 server details page (`/gameservers-v2/{serverId}`) or connect a client to `/hubs/resources` and subscribe to a `serverId`.

Primary service:
1. Finds container on node-2
2. Looks up the registered agent for node-2 (overlay IP 10.0.1.5)
3. Calls `http://10.0.1.5:8080/api/containers/{id}/stats`
4. Fans real-time stats out to hub subscribers

### 3. Verify External Isolation
```bash
# Try to reach agent from outside swarm
curl http://{node-ip}:8080/health
# Should fail - connection refused or timeout ?

# Only works from inside overlay network
docker run --rm --network gameserver-network alpine \
  wget -O- http://10.0.1.3:8080/health
# Works! ?
```

## Advantages Over Host Port Binding

| Aspect | Host Ports | Overlay Network (Current) |
|--------|------------|---------------------------|
| Security | ? Exposed externally | ? Internal only |
| Port Conflicts | ? Possible | ? Impossible |
| Firewall Rules | ? Required | ? Not needed |
| Complexity | ? Higher | ? Lower |
| Routing | ? Direct | ? Direct (via overlay) |
| Performance | ? Slightly faster | ? Near identical |

## Performance Impact

Overlay network adds minimal latency:
- Host ports: ~1ms
- Overlay network: ~1-2ms
- Negligible for stats/logs operations

Benefits of overlay outweigh the microsecond difference!

## Troubleshooting

### Agent IPs Not Discovered

**Symptom:** No agents show as registered in the primary service's `AgentRegistrationHub` logs, or hub-driven operations fail with "no agent available".

**Check:**
```bash
# Verify agents running
docker service ps gameserver-agent

# Check task network attachments
docker service inspect gameserver-agent

# Look for NetworkAttachments with gameserver-network
```

**Fix:** Ensure `gameserver-network` exists and agents are on it

### Can't Reach Agent

**Symptom:** Health checks fail or timeouts

**Check:**
```bash
# Verify primary service on same network
docker service inspect {primary-service} | grep Networks

# Test from primary service
docker exec {primary-container} ping {agent-overlay-ip}
docker exec {primary-container} curl http://{agent-overlay-ip}:8080/health
```

**Fix:** Ensure both services on `gameserver-network`

### Wrong Agent Responding

**Symptom:** "Container not found on this node" errors

**Cause:** This shouldn't happen now! Each agent has unique IP.

**Debug:**
- Inspect `AgentRegistrationHub` logs on the primary service; each connected agent reports its `nodeId`, `nodeName`, and `internalUrl`.
- Each IP should map to a distinct node.

## Summary

? **More Secure:** Agent API not exposed outside swarm  
? **Simpler:** No port conflicts, no firewall rules  
? **Correct Routing:** Each agent has unique overlay IP  
? **Production Ready:** Internal network only, as it should be  

The agent architecture now follows Docker security best practices by keeping internal services on internal networks!
