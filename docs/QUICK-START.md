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
cd src/GameServer.Docker
dotnet run
```

The API will start at:
- **HTTP**: http://localhost:5164
- **HTTPS**: https://localhost:7145
- **Swagger UI**: http://localhost:5164/swagger

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
- **API Docs**: http://localhost:5164/swagger

You should see the dashboard with no servers yet. Time to create one!

---

## 🐳 Docker Swarm Deployment

This section covers deploying GameServer.Docker to a Docker Swarm cluster for production use.

### Prerequisites

- **Docker Swarm cluster** (manager + workers)
- **Docker images** built and pushed to a registry
- **Overlay network** for service communication

### Architecture Overview

```
┌─────────────────────────────────────────────────┐
│              Docker Swarm Cluster               │
│                                                 │
│  ┌──────────────────────────────────────────┐   │
│  │  Manager Node                            │   │
│  │  • GameServer.Docker (Primary Service)   │   │
│  │  • GameServer.Web (Web UI)               │   │
│  │  • GameServer.Docker.Agent               │   │
│  └──────────────────────────────────────────┘   │
│                                                 │
│  ┌──────────────────────────────────────────┐   │
│  │  Worker Node 1                           │   │
│  │  • GameServer.Docker.Agent               │   │
│  │  • Game Server Containers                │   │
│  └──────────────────────────────────────────┘   │
│                                                 │
│  ┌─────────────────────────────────────────-─┐  │
│  │  Worker Node 2                            │  │
│  │  • GameServer.Docker.Agent                │  │
│  │  • Game Server Containers                 │  │
│  └─────────────────────────────────────────-─┘  │
└─────────────────────────────────────────────────┘
```

### Step 1: Build Docker Images

```bash
# Build GameServer.Docker API
docker build -t your-registry/gameserver-docker:latest \
  -f src/GameServer.Docker/Dockerfile \
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
```

### Step 2: Push Images to Registry

```bash
# Login to your registry (Docker Hub, GitHub Container Registry, etc.)
docker login your-registry

# Push images
docker push your-registry/gameserver-docker:latest
docker push your-registry/gameserver-web:latest
docker push your-registry/gameserver-agent:latest
```

### Step 3: Create Docker Stack File

Create `docker-stack.yml`:

```yaml
version: "3.8"

services:
  # Primary Service (API & Orchestration)
  gameserver-docker:
    image: your-registry/gameserver-docker:latest
    ports:
      - "5164:8080"  # API port
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - NodeAgentOptions__EnableBackgroundDiscovery=false
      - ConnectionStrings__GameServerV2Db=Data Source=/data/gameserver-v2.db
      - PortAllocation__StartPort=25565
      - PortAllocation__EndPort=35565
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

  # Web UI
  gameserver-web:
    image: your-registry/gameserver-web:latest
    ports:
      - "5102:8080"  # Web UI port
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - GameServerApiUrl=http://gameserver-docker:8080
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
      - gameserver-docker

  # Node Agents (one per node)
  gameserver-agent:
    image: your-registry/gameserver-agent:latest
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - AgentRegistration__PrimaryServiceUrl=http://gameserver-docker:8080
      - AgentRegistration__HeartbeatIntervalSeconds=30
      - AgentRegistration__Enabled=true
      - AGENT_HOST={{.Node.Hostname}}
      - NODE_NAME={{.Node.Hostname}}
      - NODE_ID={{.Node.ID}}
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
abc123def456   gameserver_gameserver-docker    replicated   1/1        your-registry/gameserver-docker:latest
ghi789jkl012   gameserver_gameserver-web       replicated   1/1        your-registry/gameserver-web:latest
mno345pqr678   gameserver_gameserver-agent     global       3/3        your-registry/gameserver-agent:latest
```

### Step 5: Verify Services

**Check service logs:**

```bash
# Primary Service logs
docker service logs gameserver_gameserver-docker --follow

# Web UI logs
docker service logs gameserver_gameserver-web --follow

# Agent logs
docker service logs gameserver_gameserver-agent --follow
```

**Look for successful startup messages:**

**Primary Service:**
```
[INFO] Service operations mode: AGENT
[INFO] Agent registered: Node=worker-1, Manager=False
[INFO] Agent registered: Node=manager-1, Manager=True
[INFO] All agents registered successfully
```

**Agents:**
```
[INFO] Agent initialized: IsManager=True, Hostname=manager-1
[INFO] Connected to Primary Service at http://gameserver-docker:8080
[INFO] Heartbeat sent: Containers=0, Status=Healthy
```

### Step 6: Access the Application

Once deployed, access:
- **Web UI**: http://your-manager-ip:5102
- **API Docs**: http://your-manager-ip:5164/swagger

### Configuration Options

#### Environment Variables

**GameServer.Docker (Primary Service):**

| Variable | Description | Default |
|----------|-------------|---------|
| `PortAllocation__StartPort` | First port to allocate | `25565` |
| `PortAllocation__EndPort` | Last port to allocate | `35565` |
| `ConnectionStrings__GameServerV2Db` | SQLite V2 database path | `Data Source=/data/gameserver-v2.db` |
| `ConnectionStrings__GameServerV2MySqlDb` | MySQL V2 connection string | _(optional)_ |
| `ConnectionStrings__GameServerV2PostgresDb` | PostgreSQL V2 connection string | _(optional, coming soon)_ |
| `V2Database__Provider` | V2 DB provider: `Sqlite`, `MySql`, `PostgreSql` | `Sqlite` |
| `V2Database__ConnectionStringName` | Connection string key to use | `GameServerV2Db` |
| `NetworkOptions__LoadBalancerNetwork` | Docker overlay network for Traefik | `traefik-public` |
| `NetworkOptions__LoadBalancerProvider` | Load balancer provider | `traefik` |
| `MountTypeConfigs` | Mount-type configuration is stored in the V2 database and managed through the `/settings/mount-types` UI; no environment variable override exists. Known defaults are seeded automatically for `volume`, `bind`, `tmpfs`, and `nfs`. | — |
| `NodeAgentOptions__EnableBackgroundDiscovery` | Enable Swarm polling-based agent discovery | `false` |

**V2 SQLite example:**

```yaml
environment:
  - ConnectionStrings__GameServerV2Db=Data Source=/data/gameserver-v2.db
  - V2Database__Provider=Sqlite
```

**V2 MySQL example:**

```yaml
environment:
  - ConnectionStrings__GameServerV2MySqlDb=Server=mysql;Database=gameserver-v2;Uid=gsuser;Pwd=gspass
  - V2Database__Provider=MySql
  - V2Database__ConnectionStringName=GameServerV2MySqlDb
```

**V2 PostgreSQL example (experimental / coming soon):**

```yaml
environment:
  - ConnectionStrings__GameServerV2PostgresDb=Host=postgres;Database=gameserver_v2;Username=gsuser;Password=gspass
  - V2Database__Provider=PostgreSql
  - V2Database__ConnectionStringName=GameServerV2PostgresDb
```

**GameServer.Docker.Agent:**

| Variable | Description | Default |
|----------|-------------|---------|
| `AgentRegistration__PrimaryServiceUrl` | URL of primary service | Required |
| `AgentRegistration__HeartbeatIntervalSeconds` | Heartbeat interval | `30` |
| `AgentRegistration__Enabled` | Enable push registration | `true` |
| `AgentRegistration__Capabilities` | Comma-separated capabilities: `logs,exec,stats,attach,services` | `logs,exec,stats,attach,services` |
| `AgentRegistration__ConnectionTimeoutSeconds` | SignalR connection timeout | `30` |
| `AgentRegistration__ReconnectDelaySeconds` | Reconnect delays in seconds | `0,2,10,30` |
| `AGENT_HOST` / `NODE_NAME` | Agent hostname/IP | Node hostname |

**GameServer.Web:**

| Variable | Description | Default |
|----------|-------------|---------|
| `GameServerDockerApi__BaseUri` | Base URL of `GameServer.Docker` API | `http://localhost:5164/` |

**The agent only needs `AgentRegistration__PrimaryServiceUrl` to register with the Primary Service. See the swarm deployment section above for a complete stack file.**

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
- **[CONTRIBUTING.md](CONTRIBUTING.md)** - Contribute to the project

### Advanced Topics

- **Adding Custom Game Types** - [V2 GameType Assembly](guides/V2-GameType-Assembly-Instructions.md)
- **Setting Data Types** - [V2 Settings & Metadata](guides/V2-GameType-Settings-And-Metadata.md)
- **Port Mappings** - [V2 Ports & Web Hosts](guides/V2-Ports-And-WebHosts.md)
- **Performance Tuning** - [Performance Optimizations](architecture/PERFORMANCE-OPTIMIZATIONS.md)
- **Security** - [Agent Security](architecture/Agent-Security.md)

---

## 🎯 Quick Reference

### Start Services Locally

```bash
# Terminal 1: API
cd src/GameServer.Docker && dotnet run

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
docker service logs gameserver_gameserver-docker --follow
```

### Access URLs

- **Web UI**: http://localhost:5102
- **API**: http://localhost:5164/swagger
- **Health**: http://localhost:5164/health

---

**Happy Gaming!** 🎮🚀

For issues or questions, see [CONTRIBUTING.md](CONTRIBUTING.md) or open a GitHub issue.

---

## Step 1: Verify Configuration

Game types and extended metadata are now stored in the V2 database. Ensure your `appsettings.Development.json` (or `appsettings.json`) points to the desired V2 provider:

```json
{
  "ConnectionStrings": {
    "GameServerV2Db": "Data Source=./data/gameserver-v2.db"
  },
  "V2Database": {
    "Provider": "Sqlite",
    "ConnectionStringName": "GameServerV2Db"
  }
}
```

---

## Step 2: Start the Application

Run your application:

```bash
dotnet run --project src/GameServer.Docker
```

On first startup the V2 database is initialized and any built-in seed data is applied through `DatabaseInitializationService`.

---

## Step 3: Explore the API

### Get Minecraft Extended Metadata

```bash
curl http://localhost:5000/api/gametypes/extended/minecraft | jq
```

**Response:**
```json
{
  "gameTypeKey": "minecraft",
  "enableTTY": true,
  "attachStdin": false,
  "settingsMetadata": {
    "EULA": {
      "key": "EULA",
      "description": "You must accept the Minecraft EULA...",
      "isRequired": true,
      "cannotBeEmpty": true,
      "dataType": "boolean",
      "category": "Legal",
      "displayOrder": 1
    },
    // ... more settings
  }
}
```

### Get All Extended Metadata

```bash
curl http://localhost:5000/api/gametypes/extended | jq
```

---

## Step 4: Create Extended Metadata for Your Game Type

### Example: Terraria Server

```bash
curl -X POST http://localhost:5000/api/gametypes/extended \
  -H "Content-Type: application/json" \
  -d '{
    "gameTypeKey": "terraria",
    "enableTTY": true,
    "attachStdin": false,
    "settingsMetadata": {
      "WORLD_NAME": {
        "key": "WORLD_NAME",
        "description": "Name of the world to create or load",
        "isRequired": true,
        "cannotBeEmpty": true,
        "dataType": "string",
        "category": "World",
        "displayOrder": 1,
        "placeholder": "MyWorld"
      },
      "MAX_PLAYERS": {
        "key": "MAX_PLAYERS",
        "description": "Maximum number of players (1-255)",
        "dataType": "number",
        "category": "Server",
        "displayOrder": 2,
        "validationPattern": "^([1-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$",
        "validationMessage": "Must be between 1 and 255"
      },
      "SERVER_PORT": {
        "key": "SERVER_PORT",
        "description": "Server port number",
        "dataType": "port",
        "mapsToContainerPort": true,
        "portProtocol": "tcp",
        "category": "Network",
        "displayOrder": 3,
        "placeholder": "7777"
      }
    }
  }'
```

---

## Step 5: Use Extended Metadata in Your Application

### Validate Server Settings

```csharp
[ApiController]
[Route("api/gameservers")]
public class GameServerController : ControllerBase
{
    private readonly GameTypeMetadataApplier _metadataApplier;
    
    [HttpPost]
    public async Task<IActionResult> CreateServer([FromBody] GameServer server)
    {
        // Validate using extended metadata
        var errors = await _metadataApplier.ValidateSettings(server, server.GameType);
        
        if (errors.Any())
        {
            return BadRequest(new 
            { 
                message = "Validation failed", 
                errors 
            });
        }
        
        // Continue with server creation...
        return Ok(server);
    }
}
```

### Apply Metadata to Container

```csharp
// In your deployment service using V2 models
private async Task<ServiceSpec> BuildServiceSpecAsync(
    GameServerDetailDto server,
    GameTypeRevisionDetailDto revision)
{
    var containerSpec = new ContainerSpec
    {
        Image = revision.ImageReference,
        Env = BuildEnvironmentVariables(server, revision),
        // ... other properties
    };

    var ports = revision.Ports
        .Select(p => new PortConfig
        {
            TargetPort = p.ContainerPort,
            PublishedPort = p.PublishedPort,
            Protocol = p.Protocol
        })
        .ToList();

    // Continue building...
}
```

---

## Step 6: Test the System

### Test 1: Validation with Missing Required Field

```bash
# This should fail because EULA is required
curl -X POST http://localhost:5000/api/gameservers \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Test Server",
    "gameType": "minecraft",
    "settings": {
      "VERSION": "LATEST"
    }
  }'
```

**Expected Response:**
```json
{
  "message": "Validation failed",
  "errors": [
    "Setting 'EULA' is required but not provided. You must accept the Minecraft EULA..."
  ]
}
```

### Test 2: Valid Server Creation

```bash
curl -X POST http://localhost:5000/api/gameservers \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Test Server",
    "gameType": "minecraft",
    "settings": {
      "EULA": "true",
      "VERSION": "LATEST",
      "MEMORY": "2G",
      "MAX_PLAYERS": "10"
    }
  }'
```

**Expected:** Server created successfully with TTY enabled!

### Test 3: Dynamic Port Mapping

```bash
# Create server with custom port
curl -X POST http://localhost:5000/api/gameservers \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Custom Port Server",
    "gameType": "minecraft",
    "settings": {
      "EULA": "true",
      "SERVER_PORT": "25566"
    }
  }'
```

**Expected:** Container exposes port 25566 in addition to default 25565!

---

## Step 7: Update Individual Setting Metadata

```bash
# Make MEMORY setting required
curl -X PUT http://localhost:5000/api/gametypes/extended/minecraft/settings/MEMORY \
  -H "Content-Type: application/json" \
  -d '{
    "key": "MEMORY",
    "description": "Server memory allocation (REQUIRED)",
    "isRequired": true,
    "cannotBeEmpty": true,
    "dataType": "string",
    "category": "Performance",
    "validationPattern": "^\\d+[MG]$",
    "validationMessage": "Must be a number followed by M or G (e.g., 1G, 2048M)"
  }'
```

---

## Common Use Cases

### 1. Make a Setting Required

```bash
curl -X PUT http://localhost:5000/api/gametypes/extended/{gameType}/settings/{settingKey} \
  -H "Content-Type: application/json" \
  -d '{ "key": "...", "isRequired": true, ... }'
```

### 2. Add Port Mapping

```bash
curl -X PUT http://localhost:5000/api/gametypes/extended/{gameType}/settings/{settingKey} \
  -H "Content-Type: application/json" \
  -d '{ 
    "key": "SERVER_PORT", 
    "dataType": "port", 
    "mapsToContainerPort": true,
    "portProtocol": "tcp"
  }'
```

### 3. Add Validation Pattern

```bash
curl -X PUT http://localhost:5000/api/gametypes/extended/{gameType}/settings/{settingKey} \
  -H "Content-Type: application/json" \
  -d '{ 
    "key": "EMAIL",
    "validationPattern": "^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}$",
    "validationMessage": "Must be a valid email address"
  }'
```

### 4. Enable TTY for Interactive Servers

```bash
curl -X POST http://localhost:5000/api/gametypes/extended \
  -H "Content-Type: application/json" \
  -d '{ 
    "gameTypeKey": "your-game",
    "enableTTY": true,
    "attachStdin": true
  }'
```

---

## Verification

### Check File Persistence

```bash
# View the extended metadata file
cat /data/game-types-extended.json | jq

# Restart the service
docker restart gameserver-docker

# Verify data is still there
curl http://localhost:5000/api/gametypes/extended/minecraft | jq
```

**Expected:** All your metadata is preserved after restart! ?

---

## Troubleshooting

### Issue: Metadata not persisting
**Solution:** Check file permissions and path configuration

```bash
# Check file exists
ls -la /data/game-types-extended.json

# Check logs
docker logs gameserver-docker | grep "GameTypeExtendedMetadata"
```

### Issue: Validation not working
**Solution:** Ensure GameTypeMetadataApplier is injected and used in your controller

```csharp
// In ConfigureServices
services.AddSingleton<GameTypeMetadataApplier>();

// In Controller constructor
public GameServerController(GameTypeMetadataApplier metadataApplier)
{
    _metadataApplier = metadataApplier;
}
```

### Issue: TTY not being applied
**Solution:** Ensure you're calling ApplyMetadata when building ContainerSpec

```csharp
containerSpec = await _metadataApplier.ApplyMetadata(containerSpec, server.GameType);
```

---

## Next Steps

1. ? Explore the full documentation: `docs/GameType-Extended-Metadata.md`
2. ? Check integration examples: `docs/GameType-Extended-Metadata-Integration.md`
3. ? Add metadata for your game types
4. ? Build UI forms using the categorization features
5. ? Implement validation in your controllers

---

## Quick Reference

### File Locations
- V2 Repository: `src/GameServer.Docker/Repositories/V2/GameTypeRepository.cs`
- V2 Service: `src/GameServer.Docker/Services/V2/GameTypeCommandService.cs`
- Docs: `docs/GameType-Extended-Metadata.md`

### Key Services
- `IGameTypeRepository` (V2) - CRUD operations for game types, revisions, and metadata
- `GameTypeCommandService` - Create/update revisions and metadata

### API Endpoints (V2)
- `GET /api/v2/gametypes` - List all
- `GET /api/v2/gametypes/{key}` - Get one
- `POST /api/v2/gametypes` - Create
- `PUT /api/v2/gametypes/{key}` - Update
- `DELETE /api/v2/gametypes/{key}` - Delete
- `POST /api/v2/gametypes/{key}/revisions` - Add revision

---

**You're all set!** Start extending your game types with rich metadata! ??
