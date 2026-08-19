# GameServer.Docker - Architecture Overview & Mandatory Patterns

**?? READ THIS FIRST before implementing any new feature or fixing bugs**

## System Architecture

### Agent Registration (Current - Recommended)

**As of 2025, the system uses push-based agent registration:**

```
┌─────────────────────────────────┐
│   Primary Service               │
│   (GameServer.Docker)           │
│                                 │
│   ┌──────────────────────┐     │
│   │  AgentRegistry       │     │
│   │  (In-Memory)         │     │
│   │  - Agent metadata    │     │
│   │  - Container→Agent   │     │
│   │    mappings          │     │
│   └──────────────────────┘     │
└──────────▲──────────────────────┘
           │ SignalR Registration
           │ + Heartbeats (every 30s)
           │
    ┌──────┴───────┬──────────────┬─────────────┐
    │              │              │             │
┌───▼────┐    ┌───▼────┐    ┌───▼────┐   ┌───▼────┐
│ Agent  │    │ Agent  │    │ Agent  │   │ Agent  │
│ Node 1 │    │ Node 2 │    │ Node 3 │   │ Node N │
│        │    │        │    │        │   │        │
│ Docker │    │ Docker │    │ Docker │   │ Docker │
│ Socket │    │ Socket │    │ Socket │   │ Socket │
└────────┘    └────────┘    └────────┘   └────────┘
```

**Benefits:**
- ✅ No Docker Swarm queries needed from Primary Service
- ✅ Real-time agent health tracking via heartbeats
- ✅ O(1) container-to-agent lookups (dictionary, not API calls)
- ✅ Agents can run outside Docker Swarm (standalone Docker, K8s, etc.)
- ✅ Primary Service can run without Docker access

**Configuration:**
- **Agent**: `appsettings.json` → `AgentRegistration:PrimaryServiceUrl`
- **Primary**: Automatic - agents connect to `/hubs/agentregistration`

**Multi-node requirements:**
- Agents and Primary Service must share the same overlay network.
- At least one registered agent must report `IsManagerNode = true` and include the `services`, `tasks`, `nodes`, and `swarm` capabilities so service create/update/delete can be delegated to it.
- Worker agents only need container-level capabilities (logs, exec, stats, attach).

### Multi-Node Docker Swarm Deployment

```
???????????????????????????????????????????????????????????????
?                     Docker Swarm Cluster                      ?
?                                                               ?
?  ???????????????      ???????????????      ??????????????? ?
?  ?   Node 1    ?      ?   Node 2    ?      ?   Node 3    ? ?
?  ?  (Manager)  ?      ?  (Worker)   ?      ?  (Worker)   ? ?
?  ?             ?      ?             ?      ?             ? ?
?  ?  ?????????  ?      ?  ?????????  ?      ?  ?????????  ? ?
?  ?  ? Agent ?  ?      ?  ? Agent ?  ?      ?  ? Agent ?  ? ?
?  ?  ?????????  ?      ?  ?????????  ?      ?  ?????????  ? ?
?  ?      ?      ?      ?      ?      ?      ?      ?      ? ?
?  ?  [Container] ?      ?  [Container] ?      ?  [Container] ? ?
?  ?  [Container] ?      ?  [Container] ?      ?  [Container] ? ?
?  ????????????????      ????????????????      ???????????????? ?
?         ?                     ?                     ?         ?
?????????????????????????????????????????????????????????????????
          ?                     ?                     ?
          ?????????????????????????????????????????????
                                ?
                    ??????????????????????????
                    ?  GameServer.Docker API ?
                    ?   (Central Orchestrator)?
                    ??????????????????????????
                                ?
                    ??????????????????????????
                    ?   GameServer.Web       ?
                    ?   (Blazor Frontend)     ?
                    ??????????????????????????
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

### 1. GameServer.Docker (Central API)

**Purpose:** Orchestration, service management, data persistence

**Components:**
- `Controllers/` - REST API endpoints for CRUD operations
- `Hubs/` - SignalR hubs for real-time features
  - **MUST use Node Agents for container operations**
- `Services/` - Business logic, service orchestration
- `Repositories/V2/` - V2 persistence layer
- `Data/V2/` - V2 EF Core DbContext and entities

**Key Services:**
- `IServiceOperations` - Abstraction for all Docker Swarm service operations
  - `ServiceOperationsViaAgent` - Delegates all service operations to a manager agent
- `AgentRegistryService` - Agent registration and container→agent mappings
- `NodeAgentDiscoveryService` - Agent discovery/health tracking via push-based registration and UDP announcements

**Persistence:**
- `GameServerV2DbContext` is the only persistence implementation and the single source of the V2 model.
- Each relational provider has its own `DbContext` subclass (`SqliteGameServerV2DbContext`, `MySqlGameServerV2DbContext`) so EF Core keeps a separate, provider-correct migration set for each.
- **Schema management is owned entirely by EF Core migrations.** There is no hand-rolled schema creation or repair at runtime; pending migrations are applied on startup and the operation is idempotent.
- **SQLite is the default provider.** It requires no external server and is the best-tested local option.
- **MySQL is supported** and selected via configuration.
- **PostgreSQL is experimental.** Its schema is deployed out-of-band by the `GameServer.DB.PostgreSql` project and `pgpac` tooling rather than by EF migrations; startup verifies the schema exists and fails fast with deployment guidance if it does not.
- Seed data (such as the built-in mount types) is declared with `HasData` in the model and delivered by the migrations.
- The V2 schema is normalized around:
  - `GameType` owning catalog identity (key, display name, type)
  - `GameTypeRevision` owning the version-tagged deployable template, including its `ImageReference`
  - `GameServer` storing only server-specific deployment intent via `GameTypeRevisionId`
- `GameServerPorts` and resolved Web Host state are not persisted in V2; `GameServerVolumes` are persisted as immutable per-server snapshots resolved from `GameTypeVolume` templates plus `MountTypeConfig` entries.

**✅ PHASE 5 COMPLETE:**
- Primary Service runs **without any Docker daemon connection**
- All Docker operations (services, tasks, networks) are delegated to manager agents
- Container operations always go through agents (logs, exec, stats, attach)
- V2 persistence is the only active persistence layer

### 2. GameServer.Docker.Agent (Docker Node Agents)

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
- Agents register with central API via `/hubs/agentregistration`
- `AgentRegistryService` maintains agent list and container→agent mappings
- `GetAgentForContainerAsync(containerId)` finds the right agent

### 3. GameServer.Windows.Agent (Windows Host Agents)

**Purpose:** SteamCMD CLI operations and native Windows game server process lifecycle

**Deployment:** Runs as a Windows Service or Console application on dedicated Windows hosts

**Provides:**
- **SteamCMD Management**:
  - Auto-downloads and extracts `steamcmd.exe` from Valve CDN if missing
  - `POST /api/steamcmd/install` - Install or update game server App IDs
  - `POST /api/steamcmd/workshop/download` - Download Steam Workshop items
  - `GET /api/steamcmd/apps/{appId}/status` - Status and executable file inspection
- **Native Process Supervision & Win32 Job Objects**:
  - `POST /api/servers/start` - Launch server within a Win32 Job Object (`JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`)
  - `POST /api/servers/{id}/stop` - Graceful shutdown (Ctrl-C / stdin / RCON) with timeout and process-tree kill fallback
  - `POST /api/servers/{id}/restart` - Restart server process
  - `POST /api/servers/{id}/command` - Send standard input commands or Source RCON commands
- **Real-Time Streaming (`/hubs/windowsagent` and `/hubs/nodeagent`)**:
  - `StreamServerLogs(serverId, ...)` - Live stream from circular log ring buffer
  - `StreamServerStats(serverId, ...)` - Process CPU and RAM telemetry
  - `StreamHostStats(...)` - Host memory, CPU, and disk storage metrics
- **Host Diagnostics & File Management**:
  - `GET /api/ports/check` - Inspect active TCP/UDP listeners using `IPGlobalProperties`
  - `GET /api/files` & `POST /api/files/backups/{serverId}` - File browsing, config editing, and zip backup archives

**Registration:**
- Pushes registration to Primary Service at `/hubs/agentregistration` with `HostType = "windows"`
- Sends periodic heartbeats with active server IDs

### 4. GameServer.Web (Blazor Frontend)

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
??? GameServer.Docker/              # Central API
?   ??? Controllers/                # REST endpoints (service CRUD)
?   ??? Hubs/                       # SignalR (MUST use aggregators / Node Agents)
?   ??? Services/                   # Business logic
?   ?   ??? V2/                       # V2 persistence-bound services
?   ?   ??? NodeAgentDiscoveryService.cs # Agent discovery / container→agent lookup
?   ??? Repositories/V2/            # V2 data persistence
?
??? GameServer.Docker.Agent/        # Node Agent (runs on each Swarm node)
?   ??? Controllers/                # Container operations REST API
?   ??? Hubs/                       # Container operations SignalR
?   ??? Services/                   # Container operations
?       ??? ContainerService.cs     # Direct Docker client (local only)
?
??? GameServer.Windows.Agent/       # Windows Host Agent (runs on Windows hosts)
?   ??? Controllers/                # SteamCMD, Process, Files, Ports REST API
?   ??? Hubs/                       # WindowsAgentHub SignalR (/hubs/windowsagent)
?   ??? Native/                     # Win32 Job Objects & WindowsProcessHelper
?   ??? Services/                   # SteamCmdService, GameProcessManager, RconClient
?
??? GameServer.Docker.Client/       # Client library
?   ??? Services/
?       ??? ContainerConsoleClient.cs    # Console operations
?       ??? ResourceMonitoringClient.cs  # Resource monitoring
?
??? GameServer.Web/                 # Blazor frontend
    ??? Components/
        ??? Server/                 # Server UI components
```

## Dependency Injection Patterns

### In GameServer.Docker Hubs (CORRECT):
```csharp
public SomeHub(
    ILogger<SomeHub> logger,
    IServerLogAggregator logAggregator,       // ? For shared log streaming
    IServerResourceAggregator resourceAggregator) // ? For shared resource streaming
{
}
```

### In GameServer.Docker Services (CORRECT):
```csharp
public DockerServiceHelper(
    IServiceOperations serviceOperations)  // ✅ For Swarm service operations
{
}
```

### In GameServer.Docker.Agent (CORRECT):
```csharp
public ContainerService(
    IDockerClient dockerClient)  // ? OK - only sees local containers
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
| Create game server | DockerServiceHelper | `IDockerClient.Swarm.CreateServiceAsync()` |
| List game servers | DockerServiceHelper | `IDockerClient.Swarm.ListServicesAsync()` |
| Update server | DockerServiceHelper | `IDockerClient.Swarm.UpdateServiceAsync()` |
| Delete server | DockerServiceHelper | `IDockerClient.Swarm.RemoveServiceAsync()` |
| Get container logs | ServerLogsHub (`/hubs/serverlogs`) | Node Agent ? `StreamContainerLogs()` |
| Attach to console | ContainerConsoleHub (`/hubs/console`) | Node Agent ? WebSocket |
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
- `docs/guides/Windows-Agent-Setup-And-Communication.md` - Windows Agent setup, SteamCMD lifecycle, and Primary API communication
- `docs/guides/Agent-Registration-Flow.md` - Push-based agent registration flow
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

**Last Updated:** 2026-02-18
**Maintainer:** Architecture team
**Review Required:** Before any Hub implementation or container operation changes
