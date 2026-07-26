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

### Multi-Node Docker Swarm Deployment (Legacy)

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
- `Repositories/` - Legacy SQLite-backed data access layer
- `Repositories/V2/` - New V2 persistence layer that coexists with the legacy layer
- `Data/` - Legacy EF Core DbContext and entities
- `Data/V2/` - V2 EF Core DbContext and entities

**Key Services:**
- `IServiceOperations` - **[NEW]** Abstraction for all Docker operations
  - `ServiceOperationsViaDirect` - Direct Docker client (legacy, requires Docker connection)
  - `ServiceOperationsViaAgent` - Delegates to manager agent (no Docker connection needed!)
- `DockerServiceHelper` - Server lifecycle management (uses `IServiceOperations`)
- `AgentRegistryService` - **[NEW]** Agent registration and container→agent mappings
- `NodeAgentDiscoveryService` - **[DEPRECATED]** Legacy Docker Swarm polling (will be removed)

**Persistence:**
- Legacy `GameServerDbContext` remains in place for the current API and automatic client generation path.
- `GameServerV2DbContext` is a separate new implementation in the `V2` namespace.
- V2 provider selection is configuration-driven and supports SQLite, PostgreSQL, and MySQL.
- PostgreSQL is the default and preferred V2 datastore and is modeled through the dedicated `GameServer.DB.PostgreSql` database project plus `pgpac` deployment tooling.
- The V2 schema is normalized around:
  - `GameType` owning a fixed `ImageReference`
  - `GameTypeRevision` owning version-tagged deployable templates
  - `GameServer` storing only server-specific deployment intent via `GameTypeRevisionId`
- Derived data such as `GameServerPorts`, `GameServerVolumes`, and resolved Web Host state are not persisted in V2.

**✅ PHASE 5 COMPLETE:**
- Primary Service can run **without any Docker connection** when `ServiceOperations:Mode=Agent`
- All Docker operations (services, tasks, networks) delegated to manager agent
- `IDockerClient` is optional and only used in Direct mode
- Container operations always go through agents (logs, exec, stats, attach)
- Legacy and V2 persistence layers can evolve independently while sharing the same application host

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
  - `ServerLogsViewer` - Connects to `{API}/hubs/serverlogs`
  - `ContainerTerminal` - Connects to `{API}/hubs/terminal` (exec shell)
  - `ContainerConsole` - Connects to `{API}/hubs/console` (TTY attach)
  - `ResourceMonitor` - Connects to `{API}/hubs/resources`
- `Components/Pages/Servers/` - V2 server pages
  - `GameServerManagerV2` - `/gameservers-v2`
  - `GameServerDetailsV2` - `/gameservers-v2/{serverId}`
  - `GameServerEditorV2` - `/gameservers-v2/new` and `/gameservers-v2/{serverId}/edit`

### Persistence Architecture

The application currently has two persistence layers that must coexist safely.

#### Legacy persistence
- `Data/GameServerDbContext`
- `Repositories/IGameTypeRepository`
- SQLite only
- still backs the current API surface and automatic client generation path

#### V2 persistence
- `Data/V2/GameServerV2DbContext`
- `Repositories/V2/IGameTypeRepository`
- `Repositories/V2/IGameServerRepository`
- provider-aware: **PostgreSQL (default)**, SQLite, or MySQL based on configuration
- PostgreSQL is backed by the dedicated `GameServer.DB.PostgreSql` project and `scripts/Deploy-V2PostgresDatabase.ps1`
- follows the normalized schema documented in `docs/reference/V2-Database-Diagram.md`

#### V2 schema ownership rules
- `GameType` owns the fixed Docker image reference and catalog metadata.
- `GameTypeRevision` owns the tagged deployable template.
- `GameServer` stores only server-specific deployment intent and references `GameTypeRevisionId`.
- `GameServerSettings` stores desired per-server values.
- `GameServerPorts`, `GameServerVolumes`, and resolved Web Host state are derived and are not persisted in V2.
- Port availability validation is a backend service responsibility, not persisted schema data.

#### V2 compatibility rules
- V2 work must remain in `Models.V2`, `Repositories.V2`, and `Data.V2`.
- The legacy persistence path must remain intact until controllers and services are explicitly migrated.
- The V2 DbContext and design-time factory should follow the same registration and factory pattern as the legacy DbContext so automatic client generation is not disrupted.

## Implementation Patterns

### Pattern 1: Streaming Container Logs (Example)

**File:** `src\GameServer.Docker\Hubs\ServerLogsHub.cs`

```csharp
public class ServerLogsHub : Hub
{
    private readonly INodeAgentDiscovery _nodeAgentDiscovery;
    private readonly IHttpClientFactory _httpClientFactory;
    
    public async IAsyncEnumerable<string> StreamServerLogs(string serverId, ...)
    {
        // 1. Get server info
        var server = await _serverManager.GetServerById(serverId);
        var containerId = server.ContainerId;
        
        // 2. Find which agent has this container
        var agent = await _nodeAgentDiscovery.GetAgentForContainerAsync(containerId);
        if (agent == null)
        {
            yield return "ERROR: Container not found on any node";
            yield break;
        }
        
        // 3. Connect to agent's SignalR hub
        var hubUrl = $"{agent.InternalUrl}/hubs/nodeagent";
        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .Build();
            
        await connection.StartAsync();
        
        // 4. Stream from agent
        await foreach (var line in connection.StreamAsync<string>(
            "StreamContainerLogs", containerId, ...))
        {
            yield return line;
        }
    }
}
```

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

### Pattern 3: Service Operations (Direct Docker)

**File:** `src\GameServer.Docker\Services\DockerServiceHelper.cs`

```csharp
public class DockerServiceHelper
{
    private readonly IDockerClient _dockerClient; // ? OK for service operations!
    
    public async Task CreateServerAsync(GameServer server)
    {
        // ? Creating a Swarm SERVICE - use Docker client directly
        var serviceSpec = BuildServiceSpec(server);
        await _dockerClient.Swarm.CreateServiceAsync(serviceSpec);
    }
    
    public async Task<List<GameServer>> GetAllServersAsync()
    {
        // ? Listing Swarm SERVICES - use Docker client directly
        var services = await _dockerClient.Swarm.ListServicesAsync();
        return services.Select(ConvertToGameServer).ToList();
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
**Fix:** Get containerId from server first:
```csharp
var server = await GetServerById(serverId);
await hubConnection.InvokeAsync("AttachToContainer", server.ContainerId); // ? Correct!
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
?   ??? Hubs/                       # SignalR (MUST use Node Agents)
?   ??? Services/                   # Business logic
?   ?   ??? DockerServiceHelper.cs      # Swarm service operations
?   ?   ??? GameServerManagerService.cs # Server lifecycle
?   ?   ??? NodeAgentDiscoveryService.cs # ? USE THIS!
?   ??? Repositories/               # Data persistence
?
??? GameServer.Docker.Agent/        # Node Agent (runs on each node)
?   ??? Controllers/                # Container operations REST API
?   ??? Hubs/                       # Container operations SignalR
?   ??? Services/                   # Container operations
?       ??? ContainerService.cs     # Direct Docker client (local only)
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
    IGameServerManager serverManager,
    INodeAgentDiscovery nodeAgentDiscovery,  // ? For container ops
    IHttpClientFactory httpClientFactory)    // ? For HTTP calls to agents
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
