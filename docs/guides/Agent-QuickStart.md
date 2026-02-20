# Quick Start: Deploy GameServer Agent

## Prerequisites
- Docker Swarm initialized
- Overlay network created

## Step 1: Create Network (if not exists)
```bash
docker network create --driver overlay --attachable gameserver-network
```

## Step 2: Build Agent Image
```bash
# From repository root
docker build -f src/GameServer.Docker.Agent/Dockerfile -t gameserver-agent:latest .
```

## Step 3: Deploy Agent to Swarm
```bash
docker stack deploy -c docker-stack-agent.yml gameserver-agent
```

## Step 4: Verify Deployment
```bash
# Check service
docker service ls | grep gameserver-agent

# Check tasks (one per node)
docker service ps gameserver-agent

# Test health endpoint
curl http://localhost:8080/health
```

## Step 5: Update Management Service
The management service will automatically discover and use the agents.
No additional configuration needed!

## Testing

### Test Agent Directly
```bash
# Get agent health
curl http://localhost:8080/health

# List containers on node
curl http://localhost:8080/containers

# Get container stats (replace CONTAINER_ID)
curl http://localhost:8080/containers/CONTAINER_ID/stats
```

### Test via Management API
```bash
# Discover agents
curl http://localhost:5000/api/servers/agents

# Get server stats (includes real-time container data)
curl http://localhost:5000/api/servers/{serverId}/stats

# Get server resources (includes real-time data)
curl http://localhost:5000/api/servers/{serverId}/resources
```

## Troubleshooting

### No agents discovered
```bash
# Check agent service
docker service ls

# Check agent logs
docker service logs gameserver-agent

# Verify network
docker network inspect gameserver-network
```

### Stats not showing
- Ensure server has running container
- Verify agent is on same node as container
- Check agent health: `curl http://gameserver-agent:8080/health`

## Scaling

Agents automatically scale with your swarm:
- Add a new node ? Agent automatically deploys
- Remove a node ? Agent automatically removed

## Resource Usage

Per agent:
- **CPU**: ~0.1 cores
- **Memory**: ~50-100 MB
- **Network**: Minimal (only when stats requested)

## Uninstall

```bash
docker stack rm gameserver-agent
```
