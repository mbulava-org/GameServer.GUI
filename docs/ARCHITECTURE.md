# GameServer Architecture Overview & Mandatory Patterns

**📖 READ THIS FIRST before implementing any new feature or fixing bugs**

## System Architecture

### Agent Registration (Push-Based Model)

**The system uses push-based agent registration:**

```
┌────────────────────────────────────────────────────────┐
│   Central Service Host                                 │
│   (GameServer.API or GameServer.Orchestration.Host)    │
│                                                        │
│   ┌──────────────────────────────────────────────┐     │
│   │  AgentRegistry (In-Memory)                   │     │
│   │  - Agent metadata & capabilities             │     │
│   │  - Container → Agent mappings                │     │
│   │  - O(1) route resolution                     │     │
│   └──────────────────────────────────────────────┘     │
└──────────────────────────▲─────────────────────────────┘
                           │ SignalR Registration
                           │ + Heartbeats (every 30s)
                           │
    ┌──────────────────────┼──────────────────────┬──────────────────────┐
    │                      │                      │                      │
┌───▼────────────┐   ┌─────▼──────────┐   ┌───────▼────────┐   ┌─────────▼────────┐
│ Node Agent 1   │   │ Node Agent 2   │   │ Node Agent 3   │   │ Node Agent N     │
│ (Swarm Manager)│   │ (Swarm Worker) │   │ (Swarm Worker) │   │ (Worker/Host)    │
│                │   │                │   │                │   │                  │
│ Docker Socket  │   │ Docker Socket  │   │ Docker Socket  │   │ Docker Socket    │
└────────────────┘   └────────────────┘   └────────────────┘   └──────────────────┘
```

**Benefits:**
- ✅ No direct Docker daemon queries needed from Central API
- ✅ Real-time agent health tracking via heartbeats
- ✅ O(1) container-to-agent lookups (in-memory dictionary, not API calls)
- ✅ Agents can run outside Docker Swarm (standalone Docker, K8s, Windows, etc.)
- ✅ Primary Service can run completely containerized without host Docker socket access

**Configuration:**
- **Agent**: `appsettings.json` → `AgentRegistration:PrimaryServiceUrl`
- **Primary Host**: Agents connect to `/hubs/agentregistration`

**Multi-node requirements:**
- Agents and Central Service must share the same overlay network.
- At least one registered agent must report `IsManagerNode = true` and include the `services`, `tasks`, `nodes`, and `swarm` capabilities so service create/update/delete can be delegated to it.
- Worker agents only need container-level capabilities (logs, exec, stats, attach).

### Multi-Node Docker Swarm Deployment

```
┌─────────────────────────────────────────────────────────────────────────┐
│                          Docker Swarm Cluster                           │
│                                                                         │
│  ┌─────────────────┐      ┌─────────────────┐      ┌─────────────────┐  │
│  │     Node 1      │      │     Node 2      │      │     Node 3      │  │
│  │ (Swarm Manager) │      │ (Swarm Worker)  │      │ (Swarm Worker)  │  │
│  │                 │      │                 │      │                 │  │
│  │  ┌───────────┐  │      │  ┌───────────┐  │      │  ┌───────────┐  │  │
│  │  │   Agent   │  │      │  │   Agent   │  │      │  │   Agent   │  │  │
│  │  └───────────┘  │      │  └───────────┘  │      │  └───────────┘  │  │
│  │        │        │      │        │        │      │        │        │  │
│  │   [Container]   │      │   [Container]   │      │   [Container]   │  │
│  │   [Container]   │      │   [Container]   │      │   [Container]   │  │
│  └─────────────────┘      └─────────────────┘      └─────────────────┘  │
│           │                        │                        │           │
└───────────┼────────────────────────┼────────────────────────┼───────────┘
            │                        │                        │
            └────────────────────────┼────────────────────────┘
                                     │
                        ┌─────────────────────────┐
                        │   GameServer.API Host   │
                        │  (Central Orchestrator) │
                        └─────────────────────────┘
                                     │
                        ┌─────────────────────────┐
                        │     GameServer.Web      │
                        │    (Blazor Frontend)    │
                        └─────────────────────────┘
```

## ?? CRITICAL RULES - NEVER VIOLATE THESE

### Rule 1: Never Connect Directly to Docker Daemon from Hubs

**? WRONG:**
```csharp
public class MyHub : Hub
{
    private readonly IDockerClient _dockerClient; // ? WRONG!
    
    public async Task DoSomething(string containerId)
    {
        // ? This only works if container is on the same node!
        await _dockerClient.Containers.DoSomethingAsync(containerId);
    }
}
```

**? CORRECT:**
```csharp
public class MyHub : Hub
{
    private readonly INodeAgentDiscovery _nodeAgentDiscovery; // ? CORRECT!
    private readonly IHttpClientFactory _httpClientFactory;
    
    public async Task DoSomething(string containerId)
    {
        // ? Find which node has the container
        var agent = await _nodeAgentDiscovery.GetAgentForContainerAsync(containerId);
        
        // ? Connect to that agent
        // ? Perform operation through agent
    }
}
```

### Rule 2: Always Use Node Agents for Container Operations

**Any operation on a container MUST go through Node Agents:**

- ? Logs ? Node Agent ? Container
- ? Terminal/Console ? Node Agent ? Container  
- ? Stats/Metrics ? Node Agent ? Container
- ? Exec commands ? Node Agent ? Container
- ? NEVER: Hub ? Docker Daemon ? Container (only works single-node!)

### Rule 3: Service Management vs Container Operations

**When to use Docker Client directly:**
- ? Creating/updating/deleting **services** (Swarm API)
- ? Listing services (Swarm manager has full view)
- ? Service-level operations (scaling, updates)

**When to use Node Agents:**
- ? Any operation on **containers** (logs, exec, stats, attach)
- ? Container-level monitoring
- ? Real-time streaming from containers

## Component Architecture

### 1. GameServer.API (Central REST API & Modular Host)

**Purpose:** Primary API entry point, SignalR hubs, OpenAPI/Scalar reference, and DI composition for modular libraries.

**Modular Structure:**
- **`GameServer.Contracts`** — Shared interfaces (`INodeAgentDiscovery`, `IAgentRegistry`, `IServiceOperations`), DTOs (`Dtos/V2/`), shared models, and `ServiceLabels` constants.
- **`GameServer.Catalog`** — Persistence and metadata: EF Core `GameServerV2DbContext`, entity models, migrations, repositories (`GameTypeRepository`, `GameServerRepository`, `MountTypeConfigRepository`), and `GameTypeSetupDetectionService`.
- **`GameServer.Orchestration`** — Node agent connectivity and discovery: `NodeAgentClient`, `NodeAgentDiscoveryService`, `AgentRegistryService`, `UdpAgentRegistryService`, `TerminalSessionManager`.
- **`GameServer.Deployment`** — Swarm deployment and lifecycle: `GameServerSpecBuilder`, `ServiceOperationsViaAgent`, `GameServerCommandService`, `GameServerValidationService`, `PortAllocator`.
- **`GameServer.Monitoring`** — Read-only monitoring and aggregation: `ServerResourceMonitor`, `ServerLogAggregator`, `ServerResourceAggregator`, `GameServerQueryService`.

**Standalone Host Microservices (Optional Promotion):**
- **`GameServer.Orchestration.Host`** — Standalone service hosting `/hubs/agentregistration` and agent discovery.
- **`GameServer.Monitoring.Host`** — Standalone service hosting `/hubs/resources`, `/hubs/serverlogs`, and `/hubs/attach`.

**SignalR route map (which host owns which endpoint):**

| Hub | GameServer.API route | Standalone host route |
|---|---|---|
| Agent registration | `/hubs/agentregistration` | `GameServer.Orchestration.Host` → `/hubs/agentregistration` |
| Interactive terminal (per-user exec) | `/hubs/terminal` | *(still owned by `GameServer.API`)* |
| Container attach (shared TTY) | `/hubs/attach` | `GameServer.Monitoring.Host` → `/hubs/attach` |
| Server logs (shared) | `/hubs/serverlogs` | `GameServer.Monitoring.Host` → `/hubs/serverlogs` |
| Resource monitoring (shared) | `/hubs/resources` | `GameServer.Monitoring.Host` → `/hubs/resources` |

All standalone-host routes match the `GameServer.API` route names exactly, so a SignalR client can switch between hosts by changing only its base URI.

> **Gateway model:** `GameServer.Web` always talks to `GameServer.API` only. When `ModularHosting:HostHubsInApi=false`, `GameServer.API` uses YARP to reverse-proxy `/hubs/agentregistration`, `/hubs/terminal` → `OrchestrationService:BaseUrl` and `/hubs/resources`, `/hubs/serverlogs`, `/hubs/attach` → `MonitoringService:BaseUrl` (WebSocket-aware). Standalone hosts are internal-only in this mode; the Web UI has no knowledge of them and its base URI never changes. When `HostHubsInApi=true` (default), the API maps the hubs in-process and no proxying happens.

**Key Services & Abstractions:**
- `IServiceOperations` - Abstraction for all Docker Swarm service operations
  - `ServiceOperationsViaAgent` - Delegates all service operations to a manager agent
- `AgentRegistryService` - Agent registration and container→agent mappings
- `NodeAgentDiscoveryService` - Agent discovery/health tracking via push-based registration and UDP announcements

**Persistence:**
- `GameServerV2DbContext` is the only persistence implementation and the single source of the V2 model.
- EF Core migrations automatically apply on startup.
- Supported providers: **PostgreSQL** (default in production stack), **SQLite** (default in local development), and **MySQL**.
- Seed data (such as default mount types) is declared with `HasData` in the model and delivered by migrations.
- The V2 schema is normalized around:
  - `GameType` owning catalog identity (key, display name, type)
  - `GameTypeRevision` owning the version-tagged deployable template, including its `ImageReference` and UI extensions (`uiExtensionsJson`)
  - `GameServer` storing only server-specific deployment intent via `GameTypeRevisionId`
- `GameServerPorts` and resolved Web Host state are derived and are not persisted in V2; `GameServerVolumes` are persisted as immutable per-server snapshots resolved from `GameTypeVolume` templates plus `MountTypeConfig` entries.

**✅ MODULAR ARCHITECTURE:**
- Primary Service runs **without any Docker daemon connection**
- All Docker operations (services, tasks, networks) are delegated to manager agents
- Container operations always go through agents (logs, exec, stats, attach)
- V2 persistence is the only active persistence layer

### 2. GameServer.Docker.Agent (Node Agents)

**Purpose:** Container-level operations on each Swarm node

**Deployment:** One agent per Swarm node

**Provides:**
- `GET /api/containers` - List containers on this node
- `GET /api/containers/{id}/logs` - Get container logs
- `GET /api/containers/{id}/stats` - Get container stats
- `SignalR Hub /hubs/nodeagent` - Real-time container operations
  - `StreamContainerLogs(containerId, ...)` - Stream logs
  - `StreamContainerStats(containerId, ...)` - Stream stats
  - `GetContainerStats(containerId)` - Snapshot stats

**Discovery:**
- Agents register with central API via labels
- `NodeAgentDiscoveryService` maintains agent list
- `GetAgentForContainerAsync(containerId)` finds the right agent

### 3. GameServer.Web (Blazor Frontend)

**Purpose:** User interface

**Components:**
- `Components/Server/` - Server management UI components
  - `ServerLogsViewer` - Connects to `{API}/hubs/serverlogs` (shared)
  - `ContainerTerminal` - Connects to `{API}/hubs/terminal` (exec shell, per-user)
  - `ContainerConsole` - Connects to `{API}/hubs/attach` (shared TTY attach)
  - `ResourceMonitor` - Connects to `{API}/hubs/resources` (shared)
- `Components/Pages/Servers/` - V2 server pages
  - `GameServerManagerV2` - `/gameservers-v2`
  - `GameServerDetailsV2` - `/gameservers-v2/{serverId}`
  - `GameServerEditorV2` - `/gameservers-v2/new` and `/gameservers-v2/{serverId}/edit`

### Persistence Architecture

The application uses a single V2 persistence layer.

#### V2 persistence
- `Data/V2/GameServerV2DbContext` — owns the model and seed data
- `Data/V2/SqliteGameServerV2DbContext` — SQLite migration set (`Data/V2/Migrations/SqliteMigrations`)
- `Data/V2/MySqlGameServerV2DbContext` — MySQL migration set (`Data/V2/Migrations/MySqlMigrations`)
- `Repositories/V2/IGameTypeRepository`
- `Repositories/V2/IGameServerRepository`
- provider-aware: **SQLite (default)** or MySQL via EF migrations; PostgreSQL is experimental
- PostgreSQL is backed by the dedicated `GameServer.DB.PostgreSql` project and `scripts/Deploy-V2PostgresDatabase.ps1`
- follows the normalized schema documented in `docs/reference/V2-Database-Diagram.md`
- see [Database Setup & Migrations](guides/DATABASE-INITIALIZATION.md) for configuration and how to add a migration

#### V2 schema ownership rules
- `GameType` owns catalog identity and metadata.
- `GameTypeRevision` owns the tagged deployable template, including the Docker image reference.
- `GameServer` stores only server-specific deployment intent and references `GameTypeRevisionId`.
- `GameServerSettings` stores desired per-server values.
- `GameServerPorts` and resolved Web Host state are derived and are not persisted in V2. `GameServerVolumes` are persisted as immutable snapshots resolved from `GameTypeVolume` templates and `MountTypeConfig` entries.
- Port availability validation is a backend service responsibility, not persisted schema data.

#### V2 compatibility rules
- V2 work must remain in `Models.V2`, `Repositories.V2`, and `Data.V2`.
- The V2 DbContext and design-time factory should follow the same registration and factory pattern used previously so automatic client generation is not disrupted.

## Implementation Patterns

### Pattern 1: Shared Streaming Aggregators

Real-time container data should be centralized in the primary service so multiple web clients can share the same underlying agent stream. The hub is a thin wrapper around a singleton aggregator.

**Shared streams (one underlying agent stream per resource, many clients):**
- **Logs** — `IServerLogAggregator` keyed by `serverId` → `/hubs/serverlogs`
- **Resource usage** — `IServerResourceAggregator` keyed by `serverId` → `/hubs/resources`
- **Container attach** — `IContainerAttachAggregator` keyed by `containerId` → `/hubs/attach`

**Per-user streams (one underlying agent stream per connection):**
- **Interactive exec shell** — `/hubs/terminal` via `TerminalSessionManager`

#### Example: Shared server logs

**File:** `src\GameServer.Docker\Hubs\ServerLogsHub.cs`

```csharp
public class ServerLogsHub : Hub
{
    private readonly IServerLogAggregator _logAggregator;

    public async IAsyncEnumerable<string> StreamServerLogs(string serverId, ...)
    {
        // The aggregator resolves the server, finds the agent/container,
        // opens a single shared agent stream, and fans it out to all subscribers.
        await foreach (var line in _logAggregator.StreamLogsAsync(serverId, ...))
        {
            yield return line;
        }
    }
}
```

#### Shared attach semantics

`ContainerAttachHub` at `/hubs/attach` streams the same container output to all subscribers:
- The first subscriber to call `SendInput` becomes the **input controller**.
- Late joiners receive an `InputControlledBy(connectionId)` frame so the UI can show a "view-only" indicator.
- When the controller disconnects, control is released; the next user to type wins.
- Viewers always see the same output frames, including input echoed by the container.

### Pattern 2: Container Operations via HTTP

**When SignalR streaming isn't needed:**

```csharp
// 1. Find agent
var agent = await _nodeAgentDiscovery.GetAgentForContainerAsync(containerId);

// 2. Call agent's REST API
var httpClient = _httpClientFactory.CreateClient();
httpClient.BaseAddress = new Uri(agent.InternalUrl);
var response = await httpClient.GetAsync($"/api/containers/{containerId}/stats");
```

### Pattern 3: Service Operations via IServiceOperations

**File:** `src\GameServer.Docker\Interfaces\IServiceOperations.cs`

All Swarm service operations (create, update, delete, list, inspect) run through `IServiceOperations`. The primary service no longer holds a direct Docker client; the implementation delegates to a manager agent.

```csharp
public class GameServerCommandService
{
    private readonly IServiceOperations _serviceOperations; // OK for service operations

    public async Task CreateServerAsync(SaveGameServerRequestDto server)
    {
        // Creating a Swarm SERVICE - goes through IServiceOperations
        var parameters = BuildServiceCreateParameters(server);
        await _serviceOperations.CreateServiceAsync(parameters);
    }

    public async Task<IReadOnlyList<GameServerListItemDto>> GetAllServersAsync()
    {
        var servers = await _gameServerRepository.GetAllAsync();
        return servers.Select(MapToListItem).ToList();
    }
}
```

## Common Mistakes to Avoid

### ? Mistake 1: Using IDockerClient in Hubs
```csharp
public class SomeHub : Hub
{
    private readonly IDockerClient _dockerClient; // ? WRONG for container ops!
}
```
**Why wrong:** Docker client only sees containers on its node, not across Swarm.

### ? Mistake 2: Passing ServerId Where ContainerId Expected
```csharp
await hubConnection.InvokeAsync("AttachToContainer", serverId); // ? Wrong!
```
**Fix:** Resolve container ID through the Node Agent that hosts the server's container:
```csharp
var agent = await _nodeAgentDiscovery.GetAgentForServerAsync(serverId);
var containerId = await ServerLogsHub.ResolveContainerIdAsync(agent, serverId);
await hubConnection.InvokeAsync("AttachToContainer", containerId); // ? Correct!
```

### ? Mistake 3: Assuming Container ID is Static
```csharp
// Cache container ID - ? BAD! Container ID changes on restart!
private static string _cachedContainerId;
```
**Fix:** Always get fresh container ID from server or query tasks.

### ? Mistake 4: Using Yield in Try-Catch
```csharp
try {
    yield return something; // ? Can't yield in try-catch!
} catch {
    // ...
}
```
**Fix:** Use try-finally only, or handle errors before yielding.

## File Organization

```
src/
├── GameServer.Contracts/          # Core DTOs, interfaces, models, options, helpers
├── GameServer.Catalog/            # EF Core DbContexts, migrations, repositories, setup detection
├── GameServer.Orchestration/      # Agent registry, discovery, NodeAgentClient, terminal sessions
├── GameServer.Orchestration.Host/ # Standalone Orchestration microservice host (Phase 2)
├── GameServer.Deployment/         # Spec builder, deployment, validation, command services, volume handlers
├── GameServer.Monitoring/         # Aggregators (logs, resources, attach), query services, resource monitors
├── GameServer.Monitoring.Host/    # Standalone Monitoring streaming host (Phase 2)
├── GameServer.DB.PostgreSql/      # PostgreSQL DAC and deployment scripts
├── GameServer.API/                # Central REST API & Modular Host (Controllers, Hubs, SignalR notifiers)
├── GameServer.API.Client/         # NSwag generated client library & typed services
├── GameServer.Docker.Agent/       # Linux Node Agent daemon (runs on each swarm/docker node)
├── GameServer.Windows.Agent/      # Windows Node Agent daemon
└── GameServer.Web/                # Blazor frontend web application
```

## Dependency Injection Patterns

### In GameServer.API Hubs (CORRECT):
```csharp
public ServerLogsHub(
    ILogger<ServerLogsHub> logger,
    IServerLogAggregator logAggregator) // ✅ For shared log streaming
{
}
```

### In GameServer.Deployment Services (CORRECT):
```csharp
public GameServerCommandService(
    IServiceOperations serviceOperations, // ✅ For Swarm service operations
    IGameServerRepository gameServerRepository)
{
}
```

### In GameServer.Docker.Agent (CORRECT):
```csharp
public ContainerService(
    IDockerClient dockerClient)  // ✅ OK - only sees local containers on this node
{
}
```

## Before Implementing ANY Feature

**Checklist:**

1. ? Am I working with **containers** or **services**?
   - Containers ? Use Node Agents
   - Services ? Use IDockerClient

2. ? Is this in a Hub?
   - Yes ? MUST use Node Agents for container operations

3. ? Do I need real-time streaming?
   - Yes ? Connect to Node Agent's SignalR hub
   - No ? Call Node Agent's REST API

4. ? Have I checked existing working examples?
   - `ContainerConsoleHub` - Correct Node Agent usage
   - `ResourceMonitoringHub` - Correct Node Agent usage
   - `ServerLogsHub` - NOW correct (was fixed)

5. ? Does the architecture make sense for Swarm?
   - Can my solution work when containers are on different nodes?
   - Am I querying the right API (Swarm manager vs Node agent)?

## Quick Reference: When to Use What

| Operation | Component | Method |
|-----------|-----------|--------|
| Create game server | GameServerCommandService | `IServiceOperations.CreateServiceAsync()` |
| List game servers | GameServerQueryService | `IGameServerRepository.GetAllAsync()` |
| Update server | GameServerCommandService | `IServiceOperations.UpdateServiceAsync()` |
| Delete server | GameServerCommandService | `IServiceOperations.RemoveServiceAsync()` |
| Get container logs | ServerLogsHub (`/hubs/serverlogs`) | Node Agent ? `StreamContainerLogs()` |
| Attach to console | ContainerAttachHub (`/hubs/attach`) | Node Agent ? WebSocket |
| Execute command | ContainerConsoleHub (`/hubs/terminal`) | Node Agent ? `/api/containers/{id}/exec` |
| Get stats | ResourceMonitoringHub (`/hubs/resources`) | Node Agent ? `StreamContainerStats()` |
| Agent registration | AgentRegistrationHub (`/hubs/agentregistration`) | SignalR bi-directional |
| List containers | N/A | Node Agent ? `/api/containers` |

## Testing Multi-Node Behavior

**Always test with multi-node Swarm:**

```bash
# Check which node has container
docker service ps <service-name>

# Verify container is on different node than API
docker node ls
docker ps  # Run on each node

# Test that logs/terminal work regardless of node
```

## Documentation Files

- **This file** - Architecture overview and patterns (READ FIRST!)
- `docs/MULTI-NODE-LOGS-FIX.md` - Multi-node log streaming explanation
- `docs/Container-Console-Client-Implementation.md` - Console client usage
- `docs/Agent-Fixes-Applied.md` - Node Agent implementation details

## When in Doubt

**Ask yourself:**
1. Would this work if the container is on a different Swarm node?
2. Am I using the same pattern as the working components?
3. Have I checked the architecture docs?

**If unsure:**
- Look at `ContainerConsoleHub` (reference implementation)
- Look at `ResourceMonitoringHub` (reference implementation)  
- Check `NodeAgentDiscoveryService` (how to find containers)

---

**Last Updated:** September 2026
**Maintainer:** Architecture team
**Review Required:** Before any Hub implementation or container operation changes
