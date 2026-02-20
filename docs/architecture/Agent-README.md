# GameServer.Docker Agent

Lightweight node agent for real-time container statistics and operations.

## Overview

The agent runs as a **global service** (one instance per node) and provides:
- Real-time container CPU, memory, network, and I/O statistics
- Container logs access
- Container inspection
- Direct access to local Docker daemon

## Deployment

### Prerequisites

1. Docker Swarm initialized
2. Overlay network created:
   ```bash
   docker network create --driver overlay --attachable gameserver-network
   ```

### Build and Deploy

```bash
# Build the agent image
docker build -f src/GameServer.Docker.Agent/Dockerfile -t gameserver-agent:latest .

# Tag for registry (if using remote registry)
docker tag gameserver-agent:latest your-registry/gameserver-agent:latest
docker push your-registry/gameserver-agent:latest

# Deploy to swarm
docker stack deploy -c docker-stack-agent.yml gameserver-agent

# Or with custom registry/tag
REGISTRY=your-registry TAG=v1.0.0 docker stack deploy -c docker-stack-agent.yml gameserver-agent
```

### Verify Deployment

```bash
# Check agent service
docker service ls | grep gameserver-agent

# Check agent tasks (should be one per node)
docker service ps gameserver-agent

# Check agent health
curl http://localhost:8080/health

# View agent logs
docker service logs gameserver-agent
```

## API Endpoints

### Health Check
```
GET /health
```
Returns agent health status and node information.

### Container Stats
```
GET /containers/{id}/stats
```
Get real-time CPU, memory, network, and I/O statistics for a container.

**Response:**
```json
{
  "containerId": "abc123",
  "timestamp": "2024-01-15T10:30:00Z",
  "cpu": {
    "usagePercent": 25.5,
    "totalUsage": 1234567890,
    "systemUsage": 9876543210,
    "onlineCpus": 4
  },
  "memory": {
    "usageBytes": 524288000,
    "limitBytes": 1073741824,
    "usagePercent": 48.8,
    "maxUsageBytes": 600000000
  },
  "network": {
    "rxBytes": 1234567,
    "txBytes": 9876543
  },
  "blockIo": {
    "readBytes": 123456789,
    "writeBytes": 987654321
  },
  "pids": 42
}
```

### Container Logs
```
GET /containers/{id}/logs?tail=100
```
Get last N lines of container logs.

**Parameters:**
- `tail` (optional): Number of lines to retrieve (default: 100)

### Container Inspect
```
GET /containers/{id}/inspect
```
Get detailed container information.

### List Containers
```
GET /containers
```
List all running containers on this node.

## Security

The agent requires **read-only** access to the Docker socket. It:
- ? Cannot create, stop, or modify containers
- ? Cannot modify Docker configuration
- ? Can only read container stats and logs
- ? Runs with minimal resource requirements

## Architecture

```
???????????????????????????????????????????
?      Management Service (Swarm Manager) ?
?  ???????????????????????????????????   ?
?  ?  NodeAgentDiscoveryService      ?   ?
?  ?  - Discovers agents via Swarm   ?   ?
?  ?  - Routes requests to agents    ?   ?
?  ???????????????????????????????????   ?
???????????????????????????????????????????
                  ?
        ?????????????????????
        ?         ?         ?
???????????? ???????????? ???????????
? Node 1   ? ? Node 2   ? ? Node 3  ?
?  Agent   ? ?  Agent   ? ?  Agent  ?
?  :8080   ? ?  :8080   ? ?  :8080  ?
?    ?     ? ?    ?     ? ?    ?    ?
? Docker   ? ? Docker   ? ? Docker  ?
? Socket   ? ? Socket   ? ? Socket  ?
???????????? ???????????? ???????????
```

## Monitoring

The agent includes:
- Prometheus-style `/health` endpoint
- Structured logging with Serilog
- Health checks via Docker
- Automatic restart on failure

## Resource Usage

Typical resource usage per agent:
- **CPU**: 0.1-0.25 cores
- **Memory**: 50-100 MB
- **Network**: Minimal (only when stats requested)
- **Disk**: ~50 MB (image size)

## Troubleshooting

### Agent not starting
```bash
# Check service logs
docker service logs gameserver-agent

# Check for Docker socket mount issues
docker service inspect gameserver-agent
```

### Can't reach agent from management service
```bash
# Verify network connectivity
docker network inspect gameserver-network

# Check agent health from within network
docker run --rm --network gameserver-network alpine wget -O- http://gameserver-agent:8080/health
```

### Stats not available
- Ensure container is on a node with an agent
- Verify agent has access to Docker socket
- Check that container ID is correct

## Configuration

Environment variables:
- `NODE_NAME`: Automatically set by Swarm (`{{.Node.Hostname}}`)
- `NODE_ID`: Automatically set by Swarm (`{{.Node.ID}}`)
- `DOCKER_HOST`: Docker socket URI (default: `unix:///var/run/docker.sock`)
- `ASPNETCORE_ENVIRONMENT`: Environment (default: `Production`)
- `Logging__LogLevel__Default`: Log level (default: `Information`)

## Uninstall

```bash
# Remove the agent stack
docker stack rm gameserver-agent

# Verify removal
docker service ls
```
