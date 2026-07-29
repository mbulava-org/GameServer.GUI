# GameServer.Docker - Global Agent Implementation

## Overview

This implementation adds a **global node agent architecture** to GameServer.Docker, enabling real-time container statistics and operations. The agent runs as a global service (one instance per node) and provides direct access to Docker daemon on each node for operations that require container-level access.

## Architecture

```
???????????????????????????????????????????????????
?  Management Service (Swarm Manager Node)        ?
?  ????????????????????????????????????????????  ?
?  ?  GameServerController                     ?  ?
?  ?  - /api/servers/{id}/stats (new)         ?  ?
?  ?  - /api/servers/{id}/resources (enhanced)?  ?
?  ?  - /api/servers/agents (new)             ?  ?
?  ????????????????????????????????????????????  ?
?                     ?                            ?
?  ????????????????????????????????????????????  ?
?  ?  NodeAgentDiscoveryService                ?  ?
?  ?  - Discovers agents via Swarm API        ?  ?
?  ?  - Maps containers to nodes              ?  ?
?  ?  - Routes requests to agents             ?  ?
?  ????????????????????????????????????????????  ?
?                     ?                            ?
?  ????????????????????????????????????????????  ?
?  ?  GameServerResourceMonitorService        ?  ?
?  ?  - Service-level data (Swarm API)       ?  ?
?  ?  - Real-time stats (via agents)          ?  ?
?  ????????????????????????????????????????????  ?
???????????????????????????????????????????????????
                      ? HTTP
        ??????????????????????????????
        ?             ?              ?
???????????????? ????????????? ??????????????
? Node 1       ? ? Node 2    ? ? Node 3     ?
?  Agent       ? ?  Agent    ? ?  Agent     ?
?  :8080       ? ?  :8080    ? ?  :8080     ?
?    ?         ? ?    ?      ? ?    ?       ?
? Docker       ? ? Docker    ? ? Docker     ?
? Socket (RO)  ? ? Socket(RO)? ? Socket(RO) ?
?    ?         ? ?    ?      ? ?    ?       ?
? Containers   ? ? Containers? ? Containers ?
???????????????? ????????????? ??????????????
```

## Components

### 1. **GameServer.Docker.Agent** (New)
Lightweight ASP.NET Core minimal API that runs on each node.

**Responsibilities:**
- Real-time container CPU, memory, network, and I/O statistics
- Container log access
- Container inspection
- Read-only access to local Docker daemon

**Endpoints:**
- `GET /health` - Health check
- `GET /containers/{id}/stats` - Real-time stats
- `GET /containers/{id}/logs?tail=N` - Container logs
- `GET /containers/{id}/inspect` - Container details
- `GET /containers` - List containers on node

**Deployment:** Global service (one per node)

**Resources:** ~0.1 CPU, ~50-100MB RAM

### 2. **NodeAgentDiscoveryService** (New)
Service in main application for agent discovery and communication.

**Responsibilities:**
- Discover agents via Swarm API
- Map containers to nodes
- Route requests to appropriate agents
- Cache agent endpoints (30s TTL)
- Health checking

**Methods:**
- `DiscoverAgentsAsync()` - Find all agents
- `GetAgentForContainerAsync(containerId)` - Find agent for container
- `GetAgentForServerAsync(serverId)` - Find agent for game server
- `GetContainerStatsAsync(containerId)` - Get real-time stats
- `GetContainerLogsAsync(containerId, tail)` - Get container logs

### 3. **Enhanced Resource Monitoring**
Updated `GameServerResourceMonitorService` to combine Swarm-level and container-level data.

**Data Sources:**
- **Swarm API:** Service specs, task states, resource limits
- **Node Agents:** Real-time CPU/memory/network stats

**Models Updated:**
- `ServerResourceUsage` - Added `RealTimeStats` property
- `ContainerStats` - New model for agent-provided stats

### 4. **New API Endpoints**

#### Get Real-Time Container Stats
```
GET /api/servers/{id}/stats
```
Returns real-time CPU, memory, network, and I/O statistics via node agent.

#### Discover Agents
```
GET /api/servers/agents
```
Returns list of all discovered agents in the swarm.

#### Enhanced Resource Usage
```
GET /api/servers/{id}/resources
```
Now includes both service-level and real-time container stats.

## Deployment

### Prerequisites
1. Docker Swarm initialized
2. Overlay network created:
   ```bash
   docker network create --driver overlay --attachable gameserver-network
   ```

### Quick Start

#### Option 1: Using Deployment Scripts

**Linux/Mac:**
```bash
chmod +x deploy-agent.sh
./deploy-agent.sh
```

**Windows:**
```cmd
deploy-agent.bat
```

#### Option 2: Manual Deployment

```bash
# Build agent image
docker build -f src/GameServer.Docker.Agent/Dockerfile -t gameserver-agent:latest .

# Deploy to swarm
docker stack deploy -c docker-stack-agent.yml gameserver-agent

# Verify
docker service ls | grep gameserver-agent
docker service ps gameserver-agent
```

### Configuration

The agent stack file (`docker-stack-agent.yml`) configures:
- **Mode:** Global (one per node)
- **Placement:** Worker nodes only (optional)
- **Resources:** 0.1-0.25 CPU, 128-256MB RAM
- **Network:** gameserver-network (overlay)
- **Volume:** Docker socket (read-only)
- **Health Check:** Built-in
- **Environment:** Auto-configured via Swarm

### Verification

```bash
# Check agent service
docker service ls

# Check agent tasks (one per node)
docker service ps gameserver-agent

# Test health endpoint
curl http://localhost:8080/health

# View logs
docker service logs -f gameserver-agent
```

## Usage

### Via Management API

```bash
# Discover all agents
curl http://localhost:5000/api/servers/agents

# Get real-time stats for a server
curl http://localhost:5000/api/servers/{serverId}/stats

# Get resource usage (includes real-time stats)
curl http://localhost:5000/api/servers/{serverId}/resources
```

### Direct Agent Access (for debugging)

```bash
# From another container on the same network
curl http://gameserver-agent:8080/health
curl http://gameserver-agent:8080/containers
curl http://gameserver-agent:8080/containers/{containerId}/stats
```

## Data Flow

### Real-Time Stats Request
1. Client calls `GET /api/servers/{serverId}/stats`
2. Controller calls `NodeAgentDiscovery.GetAgentForServerAsync(serverId)`
3. Discovery service:
   - Gets running container ID from Swarm API
   - Finds which node the container is on
   - Discovers agent on that node (from cache or fresh)
4. Discovery service calls agent's `/containers/{id}/stats` endpoint
5. Agent queries local Docker daemon
6. Stats returned to client

### Resource Monitoring Stream
1. Client calls `GET /api/servers/{serverId}/resources`
2. Resource monitor service:
   - Gets service-level data from Swarm API
   - Calls NodeAgentDiscovery for real-time stats
   - Combines both data sets
3. Returns hybrid result with both service and container data

## What's Available Where

| Feature | Swarm API | Node Agent | Used In |
|---------|-----------|------------|---------|
| Service spec | ? | ? | Resource monitor |
| Task states | ? | ? | Resource monitor |
| Resource limits | ? | ? | Resource monitor |
| Real-time CPU % | ? | ? | Stats endpoint, Resource monitor |
| Real-time Memory | ? | ? | Stats endpoint, Resource monitor |
| Network I/O | ? | ? | Stats endpoint |
| Block I/O | ? | ? | Stats endpoint |
| Service logs | ? | ? | Logs endpoint |
| Container logs | ? | ? | Agent (not yet exposed) |

## Security

### Agent Security
- **Docker Socket:** Read-only access
- **Permissions:** Cannot create, modify, or delete containers
- **Network:** Internal overlay only (not exposed externally)
- **Resources:** Limited via deployment constraints

### API Security
- Agents accessed via internal DNS only
- Management API should be secured (add authentication)
- Consider network policies for production

## Monitoring & Operations

### Health Checks
- Agents have built-in health checks (every 30s)
- Management service checks agent health during discovery
- Unhealthy agents excluded from routing

### Logging
- Agents use structured logging (Serilog)
- Logs accessible via `docker service logs gameserver-agent`
- Log rotation configured (max 10MB, 3 files)

### Scaling
- Agents automatically scale with cluster
- Add node ? Agent automatically deployed
- Remove node ? Agent automatically removed

### Resource Usage
Per agent:
- **CPU:** ~0.1 cores (idle), ~0.2 cores (active)
- **Memory:** ~50MB (idle), ~100MB (active)
- **Network:** Minimal (only when stats requested)
- **Disk:** ~50MB (image), minimal logs

## Troubleshooting

### No Agents Discovered
```bash
# Check agent service
docker service ls | grep gameserver-agent

# Check agent tasks
docker service ps gameserver-agent

# Check agent logs
docker service logs gameserver-agent

# Verify network
docker network inspect gameserver-network
```

### Stats Not Available
1. Verify server has running container:
   ```bash
   curl http://localhost:5000/api/servers/{serverId}
   ```

2. Check if agent exists on container's node:
   ```bash
   curl http://localhost:5000/api/servers/agents
   ```

3. Test agent directly (from within network):
   ```bash
   docker run --rm --network gameserver-network alpine wget -O- http://gameserver-agent:8080/health
   ```

### Agent Can't Access Docker Socket
- Verify socket mount in stack file
- Check agent logs for permission errors
- Ensure Docker socket is accessible on host

### High Resource Usage
- Check number of concurrent stat requests
- Review polling intervals in resource monitor
- Consider adjusting cache timeout

## Performance Considerations

### Caching
- Agent endpoints cached for 30 seconds
- Reduces Swarm API calls
- Adjustable via `_cacheTimeout` in NodeAgentDiscoveryService

### Polling Intervals
- Resource monitor polls every 2 seconds (real-time stats)
- Service-level data also refreshed every 2 seconds
- Adjustable based on requirements

### HTTP Timeouts
- Agent HTTP client timeout: 5 seconds
- Stats query timeout: 5 seconds
- Adjust for slow networks or high load

## Upgrading

### Update Agent Version
```bash
# Build new version
docker build -f src/GameServer.Docker.Agent/Dockerfile -t gameserver-agent:v1.1.0 .

# Tag and push
docker tag gameserver-agent:v1.1.0 registry/gameserver-agent:v1.1.0
docker push registry/gameserver-agent:v1.1.0

# Update stack
TAG=v1.1.0 docker stack deploy -c docker-stack-agent.yml gameserver-agent

# Monitor rollout
docker service ps gameserver-agent
```

### Rolling Updates
- Agents update one at a time (global service behavior)
- Brief stat unavailability during node update
- Service-level monitoring continues during updates

## Uninstall

```bash
# Remove agent stack
docker stack rm gameserver-agent

# Verify removal
docker service ls
docker network ls
```

## Development

### Building
```bash
# Build agent
dotnet build src/GameServer.Docker.Agent/GameServer.Docker.Agent.csproj

# Build entire solution
dotnet build GameServer.Docker.sln
```

### Testing
```bash
# Run agent locally (requires Docker socket access)
cd src/GameServer.Docker.Agent
dotnet run

# Test endpoints
curl http://localhost:8080/health
curl http://localhost:8080/containers
```

### Debugging
- Agents log to stdout (structured JSON)
- Use `docker service logs -f gameserver-agent`
- Enable detailed errors in appsettings.json
- Set log level to Debug for verbose output

## Future Enhancements

Potential improvements:
1. **Container Exec**: Execute commands in containers via agent
2. **File Operations**: Read/write files in containers via agent
3. **Metrics Export**: Prometheus metrics from agents
4. **Event Streaming**: Stream Docker events in real-time
5. **gRPC**: Replace HTTP with gRPC for better performance
6. **Agent Clustering**: Agent-to-agent communication
7. **Enhanced Caching**: Redis-based shared cache for agents

## References

- [Quick Start Guide](../QUICK-START.md)
- [Detailed Agent README](../../src/GameServer.Docker.Agent/README.md)
- [Docker Swarm Documentation](https://docs.docker.com/engine/swarm/)
- [Docker.DotNet Library](https://github.com/dotnet/Docker.DotNet)
