# GameServer.Docker.Agent

A lightweight ASP.NET Core Web API that runs on every Docker Swarm node and registers with the Primary API Service (`GameServer.Docker`). The agent performs the container operations (logs, exec, stats, attach) that the Primary Service delegates to the node that hosts the container.

## Purpose

The Node Agent:
- Registers with the Primary Service through a SignalR connection (`/hubs/agentregistration`).
- Sends periodic heartbeats listing the containers running on the local node.
- Executes container operations requested by the Primary Service through its own HTTP/SignalR endpoints.

This solves the problem of accessing container-level metrics in Docker Swarm, where the manager node cannot directly query containers on worker nodes.

## Architecture

```
???????????????????????????????????????????
?     GameServer.Docker API (Manager)     ?
?                                         ?
?  AgentRegistry                          ?
?  ??> Tracks registered agents          ?
?     and container-to-agent mappings     ?
???????????????????????????????????????????
               ?
               ? SignalR Registration
               ? + Heartbeats
               ?
????????????????????????????????????????????
?   GameServer.Docker.Agent (Every Node)  ?
?                                          ?
?   Controllers/                           ?
?   ?? HealthController                   ?
?   ?? ContainersController                ?
?                                          ?
?   Services/                              ?
?   ?? AgentRegistrationService            ?
?   ?? ContainerService                    ?
?      ??> Docker.DotNet Client           ?
????????????????????????????????????????????
               ?
               ? Unix Socket
               ?
        ???????????????????
        ?  Docker Daemon  ?
        ?  (Local Node)   ?
        ???????????????????
```

## Project Structure

```
GameServer.Docker.Agent/
??? Program.cs                      # Application entry point
??? Controllers/
?   ??? HealthController.cs         # Health check endpoint
?   ??? ContainersController.cs     # Container operations
??? Services/
?   ??? AgentRegistrationService.cs # Primary Service registration/heartbeats
?   ??? ContainerService.cs         # Docker container interaction
??? Interfaces/
?   ??? IContainerService.cs        # Service interface
??? Models/
    ??? ResponseModels.cs           # Response DTOs
```

## API Endpoints

### Health Check
```
GET /health
```
Returns agent health status, node name, and version.

**Response:**
```json
{
  "status": "healthy",
  "timestamp": "2024-01-15T10:30:00Z",
  "nodeName": "worker-1",
  "version": "1.0.0"
}
```

### Container Statistics
```
GET /containers/{id}/stats
```
Get real-time statistics for a specific container.

**Timeout:** 10 seconds - if Docker doesn't respond within this time, a 408 Request Timeout is returned.

**Response:**
```json
{
  "containerId": "abc123...",
  "timestamp": "2024-01-15T10:30:00Z",
  "cpu": {
    "usagePercent": 25.5,
    "totalUsage": 1234567890,
    "systemUsage": 9876543210,
    "onlineCpus": 4
  },
  "memory": {
    "usageBytes": 536870912,
    "limitBytes": 2147483648,
    "usagePercent": 25.0,
    "maxUsageBytes": 637534208
  },
  "network": {
    "rxBytes": 12345678,
    "txBytes": 87654321
  },
  "blockIo": {
    "readBytes": 1048576,
    "writeBytes": 524288
  },
  "pids": 42
}
```

### Container Logs
```
GET /containers/{id}/logs?tail=100
```
Get logs from a specific container.

**Query Parameters:**
- `tail` - Number of log lines to retrieve (default: 100)

**Response:**
```json
{
  "containerId": "abc123...",
  "timestamp": "2024-01-15T10:30:00Z",
  "logLines": 100,
  "logs": [
    "2024-01-15T10:29:00Z Log line 1",
    "2024-01-15T10:29:01Z Log line 2",
    ...
  ]
}
```

### Container Inspection
```
GET /containers/{id}/inspect
```
Get detailed information about a container.

**Response:**
```json
{
  "containerId": "abc123...",
  "name": "/minecraft-server-1",
  "state": {
    "status": "running",
    "running": true,
    "paused": false,
    "restarting": false,
    "pid": 12345,
    "startedAt": "2024-01-15T10:00:00Z",
    "finishedAt": "0001-01-01T00:00:00Z"
  },
  "created": "2024-01-15T09:59:00Z",
  "image": "sha256:def456...",
  "platform": "linux"
}
```

### List Containers
```
GET /containers
```
List all running containers on this node.

**Response:**
```json
{
  "nodeId": "worker-1-abc123",
  "timestamp": "2024-01-15T10:30:00Z",
  "containerCount": 3,
  "containers": [
    {
      "id": "abc123...",
      "names": ["/minecraft-1"],
      "image": "itzg/minecraft-server:latest",
      "state": "running",
      "status": "Up 2 hours"
    },
    ...
  ]
}
```

## Configuration

The agent is configured through the `AgentRegistration` section. Set any of these through `appsettings.json` or environment variables (using `__` as the section separator).

| Setting | Description | Default |
|---------|-------------|---------|
| `AgentRegistration__PrimaryServiceUrl` | URL of the Primary Service (required) | `http://gameserver-docker:8080` |
| `AgentRegistration__HeartbeatIntervalSeconds` | Seconds between heartbeats | `30` |
| `AgentRegistration__Enabled` | Enable push-based registration | `true` |
| `AgentRegistration__Capabilities` | Capabilities advertised by the agent | `logs,exec,stats,attach,services` |
| `AgentRegistration__ConnectionTimeoutSeconds` | SignalR connection timeout | `30` |
| `AgentRegistration__ReconnectDelaySeconds` | Reconnect delays in seconds | `0,2,10,30` |

The agent also uses these environment variables:

- `DOCKER_HOST` - Docker daemon socket URI (default: `unix:///var/run/docker.sock`)
- `NODE_NAME` - Human-readable node name for identification
- `NODE_ID` - Unique node identifier
- `AGENT_HOST` - Hostname/IP the Primary Service should use to reach the agent

## Deployment

### Docker Swarm Service

Deploy as a global service so one replica runs on every Swarm node. The agent needs the Docker socket to perform local container operations and `AgentRegistration__PrimaryServiceUrl` so it can register with the Primary Service.

```yaml
version: '3.8'
services:
  gameserver-agent:
    image: gameserver-docker-agent:latest
    deploy:
      mode: global
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - AgentRegistration__PrimaryServiceUrl=http://gameserver-docker:8080
      - AgentRegistration__HeartbeatIntervalSeconds=30
      - NODE_NAME={{.Node.Hostname}}
      - NODE_ID={{.Node.ID}}
      - DOCKER_HOST=unix:///var/run/docker.sock
    networks:
      - gameserver-network

networks:
  gameserver-network:
    external: true
```

For a complete stack that includes the Primary Service, the Web UI, and the Node Agent, see **[docs/QUICK-START.md](../docs/QUICK-START.md)**.

### Security Considerations

?? **Important Security Notes:**

1. **Read-Only Docker Socket**: The agent mounts the Docker socket as read-only (`:ro`)
2. **Limited Permissions**: Only container stats, logs, and inspection - no container creation/deletion
3. **Network Isolation**: Runs on dedicated overlay network
4. **No Host Network**: Does not use host networking mode

## Dependencies

- **Docker.DotNet** (3.125.15) - Docker API client
- **Serilog.AspNetCore** (10.0.0) - Structured logging
- **.NET 10.0** - Runtime

## Logging

The agent uses Serilog for structured logging with:
- Console output
- Request logging middleware
- Node name enrichment
- Application name enrichment

Log levels:
- **Information** - Normal operations, container requests
- **Debug** - Detailed operation tracing
- **Warning** - Container not found, unusual conditions
- **Error** - Exceptions, failures

## Development

### Build
```bash
dotnet build src/GameServer.Docker.Agent
```

### Run Locally
```bash
cd src/GameServer.Docker.Agent
dotnet run
```

### Test Endpoints
```bash
# Health check
curl http://localhost:8080/health

# Get container stats
curl http://localhost:8080/containers/{container-id}/stats

# Get container logs
curl http://localhost:8080/containers/{container-id}/logs?tail=50
```

## Troubleshooting

### Stats Collection Timeout
```json
{
  "error": "Stats collection timed out for container abc123"
}
```
**HTTP Status:** 408 Request Timeout

**Possible causes:**
- Docker daemon is unresponsive or overloaded
- Container is in an unusual state
- Network issues between agent and Docker daemon
- Container is being created/destroyed

**Solutions:**
- Check Docker daemon health: `docker info`
- Verify container exists: `docker ps -a | grep abc123`
- Check system resources (CPU, memory, disk I/O)
- Retry the request after a short delay

### Container Not Found
```json
{
  "error": "Container abc123 not found on this node"
}
```
- Container may be on a different node
- Container may have stopped
- Container ID may be incorrect

### Permission Denied
- Ensure Docker socket is mounted correctly
- Check socket permissions in the container
- Verify the container has access to `/var/run/docker.sock`

### Agent Not Discovered
- Check network configuration (must be on overlay network)
- Verify service name matches `NodeAgentOptions.ServiceName`
- Check agent is in "running" or "starting" state
- Verify health endpoint returns 200 OK

## Future Enhancements

Planned features:
- Container exec support for interactive commands
- Container event streaming
- Metrics aggregation
- Performance optimizations
- Caching layer for frequently accessed data
- WebSocket support for real-time streaming

## Related Documentation

- [Main API README](../GameServer.Docker/README.md)
- [Node Agent Discovery](../GameServer.Docker/Services/NodeAgentDiscoveryService.cs)
- [Configuration Guide](../../README.md)
