# Quick Start: Agent-Based Architecture

This guide helps you quickly get started with the new agent-based architecture where the Primary Service doesn't need a direct Docker connection.

## TL;DR

```yaml
# docker-compose.yml (simplified)
services:
  gameserver-docker:
    image: gameserver-docker:latest
    environment:
      - ServiceOperations__Mode=Agent  # 🎯 NEW: No Docker connection needed!
      - NodeAgentOptions__EnableBackgroundDiscovery=false  # Optional: disable legacy discovery
    # NO Docker socket mount needed!
    
  gameserver-agent:
    image: gameserver-agent:latest
    deploy:
      mode: global  # One per node
    environment:
      - AgentRegistration__PrimaryServiceUrl=http://gameserver-docker:8080
      - AGENT_HOST=${AGENT_HOST}  # Node's overlay network IP
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro  # Agents need Docker access
```

## Configuration Modes

### Mode 1: Agent Mode (Recommended - No Docker Connection)

**Primary Service** (`appsettings.json`):
```json
{
  "ServiceOperations": {
    "Mode": "Agent",
    "Enabled": true
  },
  "NodeAgentOptions": {
    "EnableBackgroundDiscovery": false
  }
}
```

**Benefits:**
- ✅ Primary Service doesn't need Docker socket
- ✅ Better security (no Docker access from primary)
- ✅ Faster container lookups (in-memory registry)
- ✅ Works with any orchestrator (not just Swarm)

### Mode 2: Direct Mode (Legacy - Backward Compatible)

**Primary Service** (`appsettings.json`):
```json
{
  "ServiceOperations": {
    "Mode": "Direct",
    "Enabled": true
  }
}
```

**Requires:**
- ❌ Primary Service needs Docker socket mount
- ❌ Docker client configuration
- ⚠️ Only works with Docker Swarm

## Step-by-Step Setup

### Step 1: Deploy Primary Service (Agent Mode)

```yaml
# docker-stack.yml
version: "3.8"

services:
  gameserver-docker:
    image: your-registry/gameserver-docker:latest
    ports:
      - "8080:8080"
    environment:
      - ServiceOperations__Mode=Agent
      - NodeAgentOptions__EnableBackgroundDiscovery=false
      - ASPNETCORE_ENVIRONMENT=Production
    networks:
      - gameserver-network
    deploy:
      replicas: 1
      placement:
        constraints:
          - node.role == manager
```

### Step 2: Deploy Agents (One Per Node)

```yaml
  gameserver-agent:
    image: your-registry/gameserver-agent:latest
    environment:
      - AgentRegistration__PrimaryServiceUrl=http://gameserver-docker:8080
      - AgentRegistration__HeartbeatIntervalSeconds=30
      - AGENT_HOST={{.Node.Hostname}}  # Swarm template
      - NODE_NAME={{.Node.Hostname}}
      - LOG_LEVEL=Information
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro
    networks:
      - gameserver-network
    deploy:
      mode: global  # Deploy to every node
      restart_policy:
        condition: any
```

### Step 3: Deploy Game Server Network

```yaml
networks:
  gameserver-network:
    driver: overlay
    attachable: true
```

### Step 4: Deploy Stack

```bash
docker stack deploy -c docker-stack.yml gameserver
```

### Step 5: Verify Deployment

**Check Primary Service logs:**
```bash
docker service logs gameserver_gameserver-docker

# Look for:
# 🔄 Service operations mode: AGENT (via manager node agent)
# [INFO] Agent registered: Node=worker-1 (abc123), Manager=False
# [INFO] Agent registered: Node=manager-1 (xyz789), Manager=True
```

**Check Agent logs:**
```bash
docker service logs gameserver_gameserver-agent

# Look for:
# [INFO] Agent initialized: IsManager=True
# [INFO] Agent registered with Primary Service
# [INFO] Heartbeat sent: Containers=3
```

### Step 6: Create a Test Server

Via Web UI or API:
```bash
curl -X POST http://localhost:8080/api/gameserver \
  -H "Content-Type: application/json" \
  -d '{
    "name": "My Test Server",
    "gameType": "minecraft",
    "settings": {...}
  }'
```

**Check logs for:**
```
[INFO] Creating service via agent: my-test-server on manager manager-1
✅ Found agent via REGISTRY (push-based) for container abc123
```

## Environment Variables Reference

### Primary Service

| Variable | Default | Description |
|----------|---------|-------------|
| `ServiceOperations__Mode` | `Direct` | `Agent` or `Direct` |
| `ServiceOperations__Enabled` | `true` | Enable service operations |
| `NodeAgentOptions__EnableBackgroundDiscovery` | `true` | Legacy discovery (set to `false` in Agent mode) |

### Agent

| Variable | Default | Description |
|----------|---------|-------------|
| `AgentRegistration__PrimaryServiceUrl` | - | **Required**: URL of Primary Service |
| `AgentRegistration__HeartbeatIntervalSeconds` | `30` | Heartbeat frequency |
| `AgentRegistration__Enabled` | `true` | Enable registration |
| `AGENT_HOST` | hostname | Agent's network address |
| `NODE_NAME` | hostname | Node display name |
| `LOG_LEVEL` | `Information` | Log level |

## Troubleshooting

### Agents Not Registering

**Problem:** Primary logs don't show agent registration

**Solution:**
1. Check network connectivity:
   ```bash
   docker exec $(docker ps -q -f name=gameserver-agent) curl http://gameserver-docker:8080/health
   ```

2. Check agent logs for errors:
   ```bash
   docker service logs gameserver_gameserver-agent | grep -i error
   ```

3. Verify Primary Service hub endpoint:
   ```bash
   curl http://localhost:8080/hubs/agentregistration
   ```

### Service Operations Fail

**Problem:** "No healthy manager agent available"

**Solution:**
1. Verify manager agent is registered:
   ```bash
   docker service logs gameserver_gameserver-docker | grep "Manager=True"
   ```

2. Check manager agent health:
   ```bash
   # From manager node
   curl http://localhost:8080/health
   ```

3. Ensure at least one manager node has agent

### Container Operations Fail

**Problem:** Can't view logs or open console

**Solution:**
1. Check agent is on correct node:
   ```bash
   # Find which node container is on
   docker service ps <service-name>
   
   # Check agent on that node
   docker node inspect <node-name>
   ```

2. Verify agent heartbeat includes container:
   ```bash
   docker service logs gameserver_gameserver-docker | grep "Heartbeat.*<container-id>"
   ```

## Migration from Direct to Agent Mode

### Step 1: Add Agents

Deploy agents while keeping Primary in Direct mode:

```bash
# Agents can coexist with Direct mode
docker service create \
  --name gameserver-agent \
  --mode global \
  ...
```

### Step 2: Monitor Registration

Wait for all agents to register:

```bash
docker service logs gameserver_gameserver-docker | grep "Agent registered"
```

### Step 3: Switch Mode

Update Primary Service:

```bash
docker service update \
  --env-add ServiceOperations__Mode=Agent \
  --env-add NodeAgentOptions__EnableBackgroundDiscovery=false \
  gameserver_gameserver-docker
```

### Step 4: Verify

Check logs for mode switch:

```bash
docker service logs gameserver_gameserver-docker | tail -50

# Should show:
# 🔄 Service operations mode: AGENT (via manager node agent)
```

### Step 5: Test Operations

Create a new server and verify it works.

## Rollback Procedure

If issues occur:

```bash
docker service update \
  --env-add ServiceOperations__Mode=Direct \
  --env-add NodeAgentOptions__EnableBackgroundDiscovery=true \
  gameserver_gameserver-docker
```

System immediately falls back to direct Docker API calls.

## Performance Tips

### 1. Adjust Heartbeat Interval

For many servers, increase interval:

```yaml
environment:
  - AgentRegistration__HeartbeatIntervalSeconds=60  # Less frequent
```

### 2. Enable Only on Needed Nodes

Worker-only nodes don't need service management:

```yaml
deploy:
  mode: global
  placement:
    constraints:
      - node.role != manager  # Only workers
```

### 3. Monitor Resource Usage

```bash
# Check agent CPU/memory
docker stats $(docker ps -q -f name=gameserver-agent)
```

## Next Steps

1. ✅ Read [AGENT-ARCHITECTURE-TESTING.md](./AGENT-ARCHITECTURE-TESTING.md) for comprehensive testing
2. ✅ Review [AGENT-REGISTRATION-MIGRATION.md](./AGENT-REGISTRATION-MIGRATION.md) for migration details
3. ✅ Check [ARCHITECTURE.md](./ARCHITECTURE.md) for architecture overview
4. ✅ Monitor logs for first 24 hours
5. ✅ Set up alerting for agent disconnections
