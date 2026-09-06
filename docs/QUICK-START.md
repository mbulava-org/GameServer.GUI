# Quick Start Guide - GameServer.Docker

Get up and running with GameServer.Docker in minutes! This guide covers both local development and Docker Swarm deployment.

---

## 📋 Table of Contents

1. [Local Development Setup](#-local-development-setup)
2. [Docker Swarm Deployment](#-docker-swarm-deployment)
3. [Creating Your First Game Server](#-creating-your-first-game-server)
4. [Verification & Testing](#-verification--testing)
5. [Troubleshooting](#-troubleshooting)

---

## 🏠 Local Development Setup

### Prerequisites

- **.NET 10 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Docker Desktop** with Swarm mode enabled
- **Visual Studio 2026** or **VS Code** (optional)
- **Git**

### Step 1: Clone the Repository

```bash
git clone https://github.com/mbulava-org/GameServer.GUI.git
cd GameServer.GUI
```

### Step 2: Initialize Docker Swarm

```bash
# Check if Swarm is already initialized
docker info | grep "Swarm: active"

# If not active, initialize Swarm
docker swarm init
```

### Step 3: Build the Projects

```bash
# Restore dependencies
dotnet restore

# Build all projects
dotnet build
```

### Step 4: Initialize the Database

No manual step is required. On startup the API applies any pending EF Core migrations for the configured provider, creating the database on first run. Built-in seed data (such as the default mount types) ships with the migrations.

The default provider is SQLite at `./data/gameserver-v2.db`. To use MySQL instead, set `V2Database:Provider` to `MySql` and supply `ConnectionStrings:GameServerV2MySqlDb`.

**See [guides/DATABASE-INITIALIZATION.md](guides/DATABASE-INITIALIZATION.md) for providers, configuration, and how to add migrations.**

### Step 5: Run the API Service

Open a terminal and start the API:

```bash
cd src/GameServer.API
dotnet run
```

The API will start at:
- **HTTP**: http://localhost:5164
- **HTTPS**: https://localhost:7145
- **Scalar API Reference**: http://localhost:5164/scalar/v1
- **OpenAPI Document**: http://localhost:5164/openapi/v1.json

### Step 6: Run the Web UI (Optional)

Open a **second terminal** and start the Web UI:

```bash
cd src/GameServer.Web
dotnet run
```

The Web UI will start at:
- **HTTP**: http://localhost:5102
- **HTTPS**: https://localhost:7198

### Step 7: Verify Local Setup

Open your browser:
- **Web UI**: http://localhost:5102
- **Scalar API Docs**: http://localhost:5164/scalar/v1

You should see the dashboard with no servers yet. Time to create one!

---

## 🐳 Docker Swarm Deployment

This section covers deploying GameServer to a Docker Swarm cluster for production use.

### Prerequisites

- **Docker Swarm cluster** (manager + workers)
- **Docker images** built and pushed to a registry
- **Overlay network** for service communication

### Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────┐
│                          Docker Swarm Cluster                           │
│                                                                         │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │  Manager Node                                                     │  │
│  │  • PostgreSQL 16 (V2 Database Backend)                            │  │
│  │  • GameServer.API (REST API & Modular Core Host)                  │  │
│  │  • GameServer.Web (Blazor Web UI)                                 │  │
│  │  • GameServer.Docker.Agent (Global Daemon)                        │  │
│  │  • Optional: GameServer.Orchestration.Host / Monitoring.Host     │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                         │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │  Worker Node 1                                                    │  │
│  │  • GameServer.Docker.Agent (Container stats, logs, exec, attach)  │  │
│  │  • Managed Game Server Containers (Palworld, Minecraft, etc.)     │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                         │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │  Worker Node 2                                                    │  │
│  │  • GameServer.Docker.Agent                                        │  │
│  │  • Managed Game Server Containers                                 │  │
│  └───────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────┘
```

### Step 1: Build Docker Images

```bash
# Build GameServer.API
docker build -t your-registry/gameserver-api:latest \
  -f src/GameServer.API/Dockerfile \
  --build-arg VERSION_NUMBER=0.1.0.0 \
  .

# Build GameServer.Web UI
docker build -t your-registry/gameserver-web:latest \
  -f src/GameServer.Web/Dockerfile \
  --build-arg VERSION_NUMBER=0.1.0.0 \
  .

# Build GameServer.Docker.Agent
docker build -t your-registry/gameserver-agent:latest \
  -f src/GameServer.Docker.Agent/Dockerfile \
  --build-arg VERSION_NUMBER=0.2.0.0 \
  .

# Optional: Build standalone Orchestration Host
docker build -t your-registry/gameserver-orchestration:latest \
  -f src/GameServer.Orchestration.Host/Dockerfile \
  --build-arg VERSION_NUMBER=0.1.0.0 \
  .

# Optional: Build standalone Monitoring Host
docker build -t your-registry/gameserver-monitoring:latest \
  -f src/GameServer.Monitoring.Host/Dockerfile \
  --build-arg VERSION_NUMBER=0.1.0.0 \
  .
```

### Step 2: Push Images to Registry

```bash
# Login to your registry
docker login your-registry

# Push primary images
docker push your-registry/gameserver-api:latest
docker push your-registry/gameserver-web:latest
docker push your-registry/gameserver-agent:latest

# Optional standalone hosts
# docker push your-registry/gameserver-orchestration:latest
# docker push your-registry/gameserver-monitoring:latest
```

### Step 3: Create Docker Stack File

Create `docker-stack.yml` (see also [`docs/samples/docker-stack.yml`](samples/docker-stack.yml) and [`docs/samples/docker-stack.modular.yml`](samples/docker-stack.modular.yml)):

```yaml
version: "3.8"

services:
  # 1. PostgreSQL Database (V2 Data Store)
  gameserver-postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: gameserver-v2
      POSTGRES_USER: gameserver
      POSTGRES_PASSWORD: gameserver_secure_password
    volumes:
      - postgres-data:/var/lib/postgresql/data
    networks:
      - gameserver-network
    deploy:
      replicas: 1
      placement:
        constraints:
          - node.role == manager
      restart_policy:
        condition: on-failure
        delay: 5s
        max_attempts: 3
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U gameserver -d gameserver-v2"]
      interval: 10s
      timeout: 5s
      retries: 5

  # 2. Primary Service (REST API & Modular Host)
  gameserver-api:
    image: your-registry/gameserver-api:latest
    ports:
      - "5164:8080"  # API port & Scalar UI (/scalar/v1)
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080
      - V2Database__Provider=PostgreSql
      - V2Database__ConnectionStringName=GameServerV2PostgresDb
      - ConnectionStrings__GameServerV2PostgresDb=Host=gameserver-postgres;Database=gameserver-v2;Username=gameserver;Password=gameserver_secure_password
      - PortAllocation__StartPort=25565
      - PortAllocation__EndPort=35565
      - NetworkOptions__NetworkName=gameserver-network
      - NodeAgentOptions__ServiceName=gameserver_gameserver-agent
      - NodeAgentOptions__NetworkName=gameserver-network
      - NodeAgentOptions__Port=8080
      - NodeAgentOptions__EnableBackgroundDiscovery=false
      - ServiceOperations__Mode=Agent
    volumes:
      - gameserver-data:/data
    networks:
      - gameserver-network
    deploy:
      replicas: 1
      placement:
        constraints:
          - node.role == manager
      restart_policy:
        condition: on-failure
        delay: 5s
        max_attempts: 3
    depends_on:
      - gameserver-postgres

  # 3. Web UI
  gameserver-web:
    image: your-registry/gameserver-web:latest
    ports:
      - "5102:8080"  # Web UI port
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080
      - GameServerDockerApi__BaseUri=http://gameserver-api:8080/
    networks:
      - gameserver-network
    deploy:
      replicas: 1
      placement:
        constraints:
          - node.role == manager
      restart_policy:
        condition: on-failure
        delay: 5s
        max_attempts: 3
    depends_on:
      - gameserver-api

  # 4. Node Agents (one per node)
  gameserver-agent:
    image: your-registry/gameserver-agent:latest
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080
      - AgentRegistration__PrimaryServiceUrl=http://gameserver-api:8080/
      - AgentRegistration__HeartbeatIntervalSeconds=30
      - AgentRegistration__Enabled=true
      - AGENT_HOST={{.Node.Hostname}}
      - NODE_NAME={{.Node.Hostname}}
      - NODE_ID={{.Node.ID}}
      - ContainerStatsStreamOptions__MaxStreamDurationSeconds=10
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro
    networks:
      - gameserver-network
    deploy:
      mode: global  # Deploy to every node (including managers)
      restart_policy:
        condition: any
        delay: 5s

networks:
  gameserver-network:
    driver: overlay
    attachable: true

volumes:
  postgres-data:
    driver: local
  gameserver-data:
    driver: local
```

### Step 4: Deploy the Stack

```bash
# Deploy the stack
docker stack deploy -c docker-stack.yml gameserver

# Verify deployment
docker stack services gameserver
```

**Expected output:**
```
ID             NAME                            MODE         REPLICAS   IMAGE
abc123def456   gameserver_gameserver-postgres  replicated   1/1        postgres:16-alpine
def456ghi789   gameserver_gameserver-api       replicated   1/1        your-registry/gameserver-api:latest
ghi789jkl012   gameserver_gameserver-web       replicated   1/1        your-registry/gameserver-web:latest
mno345pqr678   gameserver_gameserver-agent     global       3/3        your-registry/gameserver-agent:latest
```

### Step 5: Verify Services

**Check service logs:**

```bash
# PostgreSQL logs
docker service logs gameserver_gameserver-postgres --follow

# API Service logs
docker service logs gameserver_gameserver-api --follow

# Web UI logs
docker service logs gameserver_gameserver-web --follow

# Agent logs
docker service logs gameserver_gameserver-agent --follow
```

**Look for successful startup messages:**

**Primary Service (`gameserver-api`):**
```
[INFO] Service operations mode: AGENT
[INFO] Agent registered: Node=worker-1, Manager=False
[INFO] Agent registered: Node=manager-1, Manager=True
[INFO] WebHost built successfully. Configuring middleware...
```

**Agents:**
```
[INFO] Agent initialized: IsManager=True, Hostname=manager-1
[INFO] Connected to Primary Service at http://gameserver-api:8080/
[INFO] Heartbeat sent: Containers=0, Status=Healthy
```

### Step 6: Access the Application

Once deployed, access:
- **Web UI**: http://your-manager-ip:5102
- **Scalar API Docs**: http://your-manager-ip:5164/scalar/v1
- **OpenAPI Schema**: http://your-manager-ip:5164/openapi/v1.json

### Configuration Options

#### Environment Variables

**GameServer.API (Central API & Orchestrator):**

| Variable | Description | Default |
|----------|-------------|---------|
| `V2Database__Provider` | V2 DB provider: `PostgreSql`, `Sqlite`, `MySql` | `PostgreSql` |
| `V2Database__ConnectionStringName` | Connection string key to use | `GameServerV2PostgresDb` |
| `ConnectionStrings__GameServerV2PostgresDb` | PostgreSQL V2 connection string | `Host=...;Database=...` |
| `ConnectionStrings__GameServerV2Db` | SQLite V2 database path | `Data Source=/data/gameserver-v2.db` |
| `ConnectionStrings__GameServerV2MySqlDb` | MySQL V2 connection string | _(optional)_ |
| `PortAllocation__StartPort` | First port to allocate | `25565` |
| `PortAllocation__EndPort` | Last port to allocate | `35565` |
| `PortAllocation__ReservedPortRanges` | Excluded ports/ranges (e.g., `8080,9000-9100`) | _(empty)_ |
| `ServiceOperations__Mode` | Container/service operations mode (`Agent` or `Local`) | `Agent` |
| `NetworkOptions__NetworkName` | Shared overlay network name | `gameserver-network` |
| `NetworkOptions__LoadBalancerNetwork` | Docker overlay network for Traefik | `traefik-public` |
| `NetworkOptions__LoadBalancerProvider` | Load balancer provider | `traefik` |
| `NodeAgentOptions__ServiceName` | Swarm service name of agent | `gameserver_gameserver-agent` |
| `NodeAgentOptions__NetworkName` | Swarm network name for agent communication | `gameserver-network` |
| `NodeAgentOptions__Port` | Internal agent listening port | `8080` |
| `NodeAgentOptions__EnableBackgroundDiscovery` | Enable Swarm polling-based agent discovery | `false` |
| `GameTypeExtensions__AllowedAssemblies` | Assemblies allowed for UI extension tabs | `GameServer.Web` |
| `MountTypeConfigs` | Managed in database via `/settings/mount-types` UI or API; seed defaults exist for `volume`, `bind`, `tmpfs`, `nfs`. | — |

**GameServer.Docker.Agent:**

| Variable | Description | Default |
|----------|-------------|---------|
| `AgentRegistration__PrimaryServiceUrl` | URL of primary service | Required (`http://gameserver-api:8080/`) |
| `AgentRegistration__HeartbeatIntervalSeconds` | Heartbeat interval | `30` |
| `AgentRegistration__Enabled` | Enable push registration | `true` |
| `AgentRegistration__Capabilities` | Comma-separated capabilities: `logs,exec,stats,attach,services` | `logs,exec,stats,attach,services` |
| `AgentRegistration__ConnectionTimeoutSeconds` | SignalR connection timeout | `30` |
| `AgentRegistration__ReconnectDelaySeconds` | Reconnect delays in seconds | `0,2,10,30` |
| `AGENT_HOST` / `NODE_NAME` | Agent hostname/IP | Node hostname |
| `ContainerStatsStreamOptions__MaxStreamDurationSeconds` | Stats streaming timeout | `10` |

**GameServer.Web:**

| Variable | Description | Default |
|----------|-------------|---------|
| `GameServerDockerApi__BaseUri` | Base URL of `GameServer.API` | `http://localhost:5164/` |
**Standalone Host Microservices (Optional):**

- **`GameServer.Orchestration.Host`**: Dedicated agent registration and terminal session manager host.
- **`GameServer.Monitoring.Host`**: Dedicated resource monitoring, server log streaming, and container attach hub host.

### Scaling

**Scale Web UI:**
```bash
docker service scale gameserver_gameserver-web=3
```

**Add worker nodes:**
```bash
# On manager node, get join token
docker swarm join-token worker

# On new worker node
docker swarm join --token <token> <manager-ip>:2377
```

Agents will automatically deploy to new nodes (global mode).

---

## 🎮 V2 GameType & GameServer Workflow

The V2 system adds revision-based GameType management and a more normalized server model. Use the V2 paths for all new work.

### Creating a V2 GameType

1. **Navigate to**: http://localhost:5102/gametypes-v2/new
2. **Basic tab** — Set a unique key (slug), display name, type, and optional thumbnail/docs URLs.
3. **Revisions tab** — The new draft revision is auto-selected; fill in the Docker image reference and version tag.
4. **Ports tab** — Add the container ports your image exposes.
5. **Volumes tab** — Define volume mounts with a usage category (`config`, `saves`, `backups`, `gamefiles`, `logs`).
6. **Settings tab** — Add environment variable definitions with data types and optional port mapping rules.
7. **Web Hosts tab** _(optional)_ — Add web endpoint definitions (e.g. map a setting port to a web UI path).
8. **Detection tab** _(optional)_ — Enter the image reference and scan Docker image metadata to auto-populate ports and volumes.
9. **Review tab** — Review cross-tab validation and the diff against the saved state.
10. Click **Save** — persists both the GameType and the draft revision in one step.
11. Click **Publish** on the revision to make it available for server creation.

### Creating a V2 Game Server

1. **Navigate to**: http://localhost:5102/gameservers-v2/new
2. Select a V2 GameType and a published revision.
3. Override any settings you need (ports and volumes come from the revision).
4. Click **Create** — the server is validated then deployed.

### Using the V2 API

#### Create a V2 GameType

```bash
curl -X POST http://localhost:5164/api/v2/gametypes \
  -H "Content-Type: application/json" \
  -d '{
    "key": "minecraft-java",
    "displayName": "Minecraft Java Edition",
    "description": "Vanilla Minecraft Java server",
    "type": "survival",
    "isActive": true
  }'
```

#### Add and Publish a Revision

```bash
# Add revision
curl -X POST http://localhost:5164/api/v2/gametypes/minecraft-java/revisions \
  -H "Content-Type: application/json" \
  -d '{
    "imageReference": "itzg/minecraft-server",
    "versionTag": "latest",
    "enableTTY": false,
    "notes": "Vanilla latest"
  }'

# Publish (replace {revisionId} with the returned ID)
curl -X POST http://localhost:5164/api/v2/gametypes/minecraft-java/revisions/{revisionId}/publish
curl -X POST http://localhost:5164/api/v2/gametypes/minecraft-java/revisions/{revisionId}/set-current
```

#### Create a V2 Game Server

```bash
# Validate first (optional but recommended)
curl -X POST http://localhost:5164/api/v2/gameservers/validate \
  -H "Content-Type: application/json" \
  -d '{
    "name": "My Minecraft Server",
    "gameTypeRevisionId": "{revisionId}",
    "settings": { "EULA": "true", "MEMORY": "2G" }
  }'

# Create
curl -X POST http://localhost:5164/api/v2/gameservers \
  -H "Content-Type: application/json" \
  -d '{
    "name": "My Minecraft Server",
    "gameTypeRevisionId": "{revisionId}",
    "settings": { "EULA": "true", "MEMORY": "2G" }
  }'
```

#### Import a Portable GameType Package

```bash
# Import the bundled Minecraft Java preset
curl -X POST http://localhost:5164/api/v2/gametypes/import \
  -H "Content-Type: application/json" \
  -d @docs/samples/gametype-imports/minecraft-java.portable.json
```

---

## 🎮 Creating Your First Game Server (V2)

### Using the Web UI

1. **Navigate to**: http://localhost:5102/gameservers-v2/new
2. **Select Game Type** - Choose "Minecraft"
3. **Select Revision** - Pick the published revision to deploy
4. **Basic Info** - Name your server
5. **Game Settings** - Configure:
   - `EULA`: `true` (required)
   - `VERSION`: `LATEST`
   - `MEMORY`: `2G`
6. **Review & Create** - Click "Create Server"

### Using the API

```bash
# Create a V2 Minecraft server
curl -X POST http://localhost:5164/api/v2/gameservers \
  -H "Content-Type: application/json" \
  -d '{
    "name": "My Minecraft Server",
    "description": "My first game server",
    "gameTypeRevisionId": 1,
    "settings": [
      { "settingKey": "EULA", "value": "true" },
      { "settingKey": "VERSION", "value": "LATEST" },
      { "settingKey": "MEMORY", "value": "2G" },
      { "settingKey": "MAX_PLAYERS", "value": "20" }
    ]
  }'
```

### Verify Server Creation

**Check service status:**
```bash
docker service ls | grep minecraft

# Get service details
docker service ps <service-id>
```

**View logs:**
```bash
docker service logs <service-id> --follow
```

---

## ✅ Verification & Testing

### Health Checks

**Check API health:**
```bash
curl http://localhost:5164/health
```

**Check Agent health:**
```bash
# Get agent service tasks
docker service ps gameserver_gameserver-agent

# Check logs
docker service logs gameserver_gameserver-agent | grep "Heartbeat"
```

### Test Agent Registration

```bash
# View registered agents via API
curl http://localhost:5164/api/agents | jq

# Expected output:
# [
#   {
#     "nodeId": "abc123",
#     "nodeName": "worker-1",
#     "isManager": false,
#     "endpoint": "http://10.0.1.5:8080",
#     "lastHeartbeat": "2026-03-23T10:30:00Z",
#     "status": "Healthy"
#   }
# ]
```

### Test Container Operations

**List containers on a specific node:**
```bash
curl http://localhost:5164/api/containers | jq
```

**View container logs:**
```bash
curl http://localhost:5164/api/containers/{containerId}/logs
```

---

## 🔧 Troubleshooting

### Common Issues

#### Issue: Agents not registering

**Symptoms:**
- Primary Service shows "No agents registered"
- Agent logs show connection errors

**Solutions:**
1. Verify network connectivity:
   ```bash
   # From agent container
   docker exec <agent-container> curl http://gameserver-docker:8080/health
   ```

2. Check overlay network:
   ```bash
   docker network ls | grep gameserver-network
   docker network inspect gameserver-network
   ```

3. Verify `PrimaryServiceUrl` is correct:
   ```bash
   docker service inspect gameserver_gameserver-agent --format '{{.Spec.TaskTemplate.ContainerSpec.Env}}'
   ```

#### Issue: Game server won't start

**Solutions:**
1. Check service logs:
   ```bash
   docker service logs <service-name> --tail 100
   ```

2. Verify port availability:
   ```bash
   docker ps | grep <port>
   ```

3. Check service labels:
   ```bash
   docker service inspect <service-id> --format '{{json .Spec.Labels}}' | jq
   ```

#### Issue: Database not persisting

**Solutions:**
1. Check volume mount:
   ```bash
   docker volume ls | grep gameserver
   docker volume inspect gameserver_gameserver-data
   ```

2. Verify database file:
   ```bash
   docker exec <api-container> ls -la /data/
   ```

### Debug Mode

Enable verbose logging:

```yaml
# In docker-stack.yml
environment:
  - Logging__LogLevel__Default=Debug
  - Logging__LogLevel__GameServer.Docker=Debug
```

### Useful Commands

```bash
# View all stack services
docker stack services gameserver

# View service tasks (replicas)
docker service ps gameserver_gameserver-docker

# Scale a service
docker service scale gameserver_gameserver-web=2

# Update a service (rolling update)
docker service update --image your-registry/gameserver-docker:v2 gameserver_gameserver-docker

# Remove the stack
docker stack rm gameserver

# View logs from all replicas
docker service logs gameserver_gameserver-docker --follow --tail 100
```

---

## 📚 Next Steps

### Learn More

- **[ARCHITECTURE.md](ARCHITECTURE.md)** - Understand the system design
- **[CURRENT-FEATURES.md](CURRENT-FEATURES.md)** - See all features
- **[Agent Registration Flow](guides/Agent-Registration-Flow.md)** - How agents register with the Primary Service
- **[V2 GameType Assembly](guides/V2-GameType-Assembly-Instructions.md)** - Create custom game types
- **[GameType UI Extensions](guides/GameType-UI-Extensions.md)** - Build custom extension tabs (Palworld API, RCON, etc.)
- **[CONTRIBUTING.md](CONTRIBUTING.md)** - Contribute to the project

### Advanced Topics

- **Adding Custom Game Types** - [V2 GameType Assembly](guides/V2-GameType-Assembly-Instructions.md)
- **Setting Data Types** - [V2 Settings & Metadata](guides/V2-GameType-Settings-And-Metadata.md)
- **Port Mappings** - [V2 Ports & Web Hosts](guides/V2-Ports-And-WebHosts.md)
- **UI Extension Tabs** - [GameType UI Extensions](guides/GameType-UI-Extensions.md)
- **Performance Tuning** - [Performance Optimizations](architecture/PERFORMANCE-OPTIMIZATIONS.md)
- **Security** - [Agent Security](architecture/Agent-Security.md)

---

## 🎯 Quick Reference

### Start Services Locally

```bash
# Terminal 1: API
cd src/GameServer.API && dotnet run

# Terminal 2: Web UI
cd src/GameServer.Web && dotnet run
```

### Deploy to Swarm

```bash
# One command deployment
docker stack deploy -c docker-stack.yml gameserver
```

### Check Status

```bash
# Service status
docker stack services gameserver

# Logs
docker service logs gameserver_gameserver-api --follow
```

### Access URLs

- **Web UI**: http://localhost:5102
- **Scalar API Docs**: http://localhost:5164/scalar/v1
- **OpenAPI Schema**: http://localhost:5164/openapi/v1.json
- **Health**: http://localhost:5164/health

---

**Happy Gaming!** 🎮🚀

For issues or questions, see [CONTRIBUTING.md](CONTRIBUTING.md) or open a GitHub issue.

