# Quick Start: Deploy GameServer Agent

## Prerequisites
- Docker Swarm initialized (`docker swarm init` on the manager)
- Overlay network created for both the agent stack and the Primary Service stack
- Primary Service reachable from every Swarm node

## How registration works

Agents use **push-based registration**. Each agent opens a SignalR connection to the Primary Service at `/hubs/agentregistration`, sends its node info, and then sends periodic heartbeats with the list of running containers. The Primary Service never talks directly to the Docker daemon; it delegates service operations to a healthy manager agent and container operations to the agent that hosts the container.

## Step 1: Create Network (if not exists)

```bash
# Must be attachable so manager/worker tasks can join
docker network create --driver overlay --attachable gameserver-network
```

The Primary Service stack and the agent stack must use the **same** network name.

## Step 2: Build Agent Image

```bash
# From repository root
docker build -f src/GameServer.Docker.Agent/Dockerfile -t gameserver-agent:latest .
```

## Step 3: Configure AgentRegistration

The agent must know where the Primary Service is. Set `AgentRegistration:PrimaryServiceUrl` in the agent's configuration.

Example in `src/GameServer.Docker.Agent/appsettings.json`:

```json
{
  "AgentRegistration": {
    "Enabled": true,
    "PrimaryServiceUrl": "http://primary-service-name:8080",
    "HeartbeatIntervalSeconds": 30,
    "ReconnectDelaySeconds": [ 1, 5, 10, 30 ]
  }
}
```

Inside the same overlay network, the Primary Service is usually reachable by its service name and internal port (e.g. `http://gameserver-docker:8080`). If you run the agent outside Swarm in development, use the published host port (e.g. `http://host.docker.internal:5164`).

## Step 4: Deploy Agent to Swarm

The agent must run as a **global service** so that one replica starts on every node:

```bash
docker stack deploy -c docker-stack-agent.yml gameserver-agent
```

Example `docker-stack-agent.yml` excerpt:

```yaml
services:
  agent:
    image: gameserver-agent:latest
    networks:
      - gameserver-network
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      # Override PrimaryServiceUrl via env var if needed
      - AgentRegistration__PrimaryServiceUrl=http://gameserver-docker:8080
    deploy:
      mode: global
      resources:
        limits:
          cpus: '0.1'
          memory: 128M
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro

networks:
  gameserver-network:
    external: true
```

## Step 5: Verify Registration

```bash
# Check agent service
docker service ls | grep gameserver-agent

# Check tasks (one per node)
docker service ps gameserver-agent

# Check agent logs for registration success
docker service logs gameserver-agent --tail 50
```

Look for a log similar to:

```
Agent registered with Primary Service: Node=<node> (<node-id>), Capabilities=..., Manager=<True|False>
```

## Step 6: Confirm Primary Service Sees the Agents

The management service automatically uses registered agents. You can verify discovery in the [Blazor UI](docs/Web-UI.md) or from the agent list endpoint (not shown in this quick-start).

## Capabilities and Manager Agents

- Only **manager** agents advertise `services`, `tasks`, `nodes`, and `swarm` capabilities.
- Service create/update/delete operations require a healthy manager agent.
- Container-level operations (logs, exec, stats, attach) work through any registered agent.

```bash
# Promote a worker to manager (if needed)
docker node promote <node-name>
```

## Testing

### Test Agent Directly

From a Swarm node that is running an agent task:

```bash
# Get agent health
curl http://localhost:8080/health

# List containers on node
curl http://localhost:8080/api/containers

# Get container stats (replace CONTAINER_ID)
curl http://localhost:8080/api/containers/CONTAINER_ID/stats
```

### Test via Management API

These endpoints are available on the Primary Service and route through the correct Node Agent:

```bash
# Live server logs (SignalR /hubs/serverlogs)
# Open the server detail page in the Blazor UI

# Server resources/stats
curl http://localhost:5000/api/servers/{serverId}/resources
curl http://localhost:5000/api/servers/{serverId}/stats
```

## Troubleshooting

### No agents discovered

```bash
# Check agent service
docker service ls

# Check agent logs
docker service logs gameserver-agent

# Verify overlay network is shared with Primary Service
docker network inspect gameserver-network
```

### Agents register but service operations fail

- Ensure at least one registered agent is on a Swarm manager node (`IsManagerNode: true`).
- Confirm the manager agent's capabilities include `services` and `tasks`.
- Check the Primary Service logs for `No healthy manager agent available`.

### Container logs/terminal fail with container not found

- Confirm the container is running (`docker ps` on the node).
- Confirm the agent on that node is registered and healthy.
- Check Primary Service logs to see if it resolved the correct agent for the container.

## Scaling

Agents scale with the Swarm automatically:
- Add a new node -> a new agent task schedules automatically.
- Remove a node -> the Primary Service evicts the agent after missed heartbeats.

## Resource Usage

Per agent:
- **CPU**: ~0.1 cores
- **Memory**: ~50-100 MB
- **Network**: Minimal; heartbeats + live streams when active

## Uninstall

```bash
docker stack rm gameserver-agent
```
