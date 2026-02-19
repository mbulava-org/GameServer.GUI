# Implementation Checklist - MUST Complete Before Coding

**Purpose:** Ensure all implementations follow established architecture patterns

## Pre-Implementation Phase

### Step 1: Understand the Request
- [ ] What is the user asking for?
- [ ] Is this a new feature, bug fix, or enhancement?
- [ ] Which component(s) are involved?

### Step 2: Check Architecture Documentation

**?? MANDATORY - Read these files in order:**

1. [ ] **`docs/ARCHITECTURE.md`** - Overall system architecture and patterns
2. [ ] Check if there's component-specific documentation:
   - [ ] `docs/MULTI-NODE-LOGS-FIX.md` - For log streaming
   - [ ] `docs/Container-Console-Client-Implementation.md` - For console/terminal
   - [ ] `docs/Agent-Fixes-Applied.md` - For Node Agent implementations

### Step 3: Identify the Pattern

**Is this operation on:**
- [ ] **Containers** (individual running instances)
  - ? Must use Node Agents
  - ? Find agent via `INodeAgentDiscovery.GetAgentForContainerAsync()`
  - ? Connect to agent's hub or REST API
  
- [ ] **Services** (Swarm service definitions)
  - ? Can use `IDockerClient.Swarm.*` directly
  - ? Swarm manager has full view

- [ ] **Both** (e.g., create service, then monitor containers)
  - ? Service creation: Use `IDockerClient`
  - ? Container monitoring: Use Node Agents

### Step 4: Find Reference Implementation

**Look for existing working examples:**

| Need to implement | Check this file | Key method |
|-------------------|-----------------|------------|
| Container logs streaming | `src\GameServer.Docker\Hubs\ServerLogsHub.cs` | `StreamServerLogs` |
| Console/Terminal attach | `src\GameServer.Docker\Hubs\ContainerConsoleHub.cs` | `AttachToContainer` |
| Resource monitoring | `src\GameServer.Docker\Hubs\ResourceMonitoringHub.cs` | `SubscribeToServer` |
| Service creation | `src\GameServer.Docker\Services\DockerServiceHelper.cs` | `CreateServerAsync` |
| Service listing | `src\GameServer.Docker\Services\DockerServiceHelper.cs` | `GetAllServersAsync` |

### Step 5: Verify Dependencies

**For Hub implementations, ensure you have:**
- [ ] `ILogger<THubType>` - Logging
- [ ] `IGameServerManager` - Server info (if needed)
- [ ] `INodeAgentDiscovery` - **REQUIRED** for container operations
- [ ] `IHttpClientFactory` - **REQUIRED** for agent HTTP calls

**For Service implementations:**
- [ ] `IDockerClient` - Swarm operations (services only)

### Step 6: Architecture Validation

**Answer these questions:**
- [ ] If container moves to different node, will this still work?
- [ ] Am I using the same pattern as reference implementations?
- [ ] Have I avoided anti-patterns listed in ARCHITECTURE.md?

## Anti-Pattern Detection

**? STOP if you find yourself doing any of these:**

- [ ] Injecting `IDockerClient` into a Hub for container operations
- [ ] Calling `_dockerClient.Containers.*` from a Hub
- [ ] Assuming container is on same node as API
- [ ] Caching container IDs without refresh mechanism
- [ ] Using try-catch blocks around `yield return` statements
- [ ] Creating direct WebSocket connections to containers (use agents!)

**? If you catch yourself, revise to use Node Agents**

## Implementation Phase

### Step 1: Dependency Injection

**In Hub (for container operations):**
```csharp
public MyHub(
    ILogger<MyHub> logger,
    INodeAgentDiscovery nodeAgentDiscovery,  // ? REQUIRED
    IHttpClientFactory httpClientFactory)    // ? REQUIRED
{
}
```

### Step 2: Find the Container

**Always start with:**
```csharp
// Get server to container ID mapping
var server = await _serverManager.GetServerById(serverId);
var containerId = server.ContainerId;

// Find which agent has this container
var agent = await _nodeAgentDiscovery.GetAgentForContainerAsync(containerId);
if (agent == null)
{
    _logger.LogWarning("No agent found for container {ContainerId}", containerId);
    // Handle error appropriately
    return;
}

_logger.LogInformation("Found container {ContainerId} on agent {AgentUrl}", 
    containerId, agent.InternalUrl);
```

### Step 3: Choose Communication Method

**For real-time streaming:**
```csharp
var hubUrl = $"{agent.InternalUrl}/hubs/nodeagent";
var connection = new HubConnectionBuilder()
    .WithUrl(hubUrl)
    .WithAutomaticReconnect()
    .Build();

await connection.StartAsync(cancellationToken);

await foreach (var item in connection.StreamAsync<T>("MethodName", containerId, ...))
{
    yield return item;
}
```

**For one-time operations:**
```csharp
var httpClient = _httpClientFactory.CreateClient();
httpClient.BaseAddress = new Uri(agent.InternalUrl);
var response = await httpClient.GetAsync($"/api/containers/{containerId}/stats");
```

## Post-Implementation Phase

### Testing Checklist

- [ ] Tested with container on **same node** as API
- [ ] Tested with container on **different node** than API
- [ ] Tested with container that **gets restarted** (new container ID)
- [ ] Tested with **multiple clients** connecting simultaneously
- [ ] Tested **error cases** (container not found, agent unavailable)

### Code Review Checklist

- [ ] No direct `IDockerClient.Containers.*` calls from Hubs
- [ ] Proper error handling and logging
- [ ] Follows existing patterns from reference implementations
- [ ] Documentation updated if needed
- [ ] Architecture patterns maintained

### Documentation Updates

**Update these if your change affects:**
- [ ] `docs/ARCHITECTURE.md` - If adding new pattern
- [ ] `docs/SESSION-SUMMARY.md` - If fixing architectural issue
- [ ] Component-specific docs - If changing behavior

## Common Implementation Scenarios

### Scenario 1: Adding New Container Operation

**Example:** Stream container network stats

1. ? Add method to Node Agent's hub: `StreamContainerNetworkStats()`
2. ? Create hub method in GameServer.Docker that:
   - Gets agent via `INodeAgentDiscovery`
   - Connects to agent's hub
   - Streams from agent method
3. ? Create UI component that connects to GameServer.Docker hub
4. ? **NEVER** connect directly to Docker daemon from GameServer.Docker hub

### Scenario 2: Adding Service-Level Operation

**Example:** Update service replicas

1. ? Add method to `DockerServiceHelper`
2. ? Inject `IDockerClient`
3. ? Call `_dockerClient.Swarm.UpdateServiceAsync()`
4. ? This is correct - services are managed centrally

### Scenario 3: Mixed Operation

**Example:** Create server then get its logs

1. ? Create service: `DockerServiceHelper` + `IDockerClient.Swarm.CreateServiceAsync()`
2. ? Wait for container to start
3. ? Get logs: Hub + Node Agents
4. ? **NEVER** try to get logs via `IDockerClient` from Hub

## Quick Decision Tree

```
Need to implement feature
    ?
    ?? Container operation? (logs, exec, stats, attach)
    ?  ?? YES ? Use Node Agents pattern
    ?      ?? Reference: ContainerConsoleHub, ServerLogsHub
    ?
    ?? Service operation? (create, update, delete, scale)
    ?  ?? YES ? Use DockerServiceHelper + IDockerClient.Swarm
    ?      ?? Reference: DockerServiceHelper
    ?
    ?? Both?
       ?? Service part ? DockerServiceHelper
       ?? Container part ? Node Agents
```

## Templates

### Template: New Hub for Container Operations

```csharp
using Microsoft.AspNetCore.SignalR;
using GameServer.Docker.Interfaces;
using Microsoft.AspNetCore.SignalR.Client;
using System.Runtime.CompilerServices;

namespace GameServer.Docker.Hubs
{
    public class MyNewHub : Hub
    {
        private readonly ILogger<MyNewHub> _logger;
        private readonly IGameServerManager _serverManager;
        private readonly INodeAgentDiscovery _nodeAgentDiscovery;
        private readonly IHttpClientFactory _httpClientFactory;

        public MyNewHub(
            ILogger<MyNewHub> logger,
            IGameServerManager serverManager,
            INodeAgentDiscovery nodeAgentDiscovery,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _serverManager = serverManager;
            _nodeAgentDiscovery = nodeAgentDiscovery;
            _httpClientFactory = httpClientFactory;
        }

        public async IAsyncEnumerable<T> StreamSomething(
            string serverId,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // 1. Get container ID
            var server = await _serverManager.GetServerById(serverId);
            var containerId = server?.ContainerId;
            
            if (string.IsNullOrEmpty(containerId))
            {
                _logger.LogWarning("No container ID for server {ServerId}", serverId);
                yield break;
            }

            // 2. Find agent
            var agent = await _nodeAgentDiscovery.GetAgentForContainerAsync(containerId);
            if (agent == null)
            {
                _logger.LogWarning("No agent found for container {ContainerId}", containerId);
                yield break;
            }

            _logger.LogInformation("Found container on agent {AgentUrl}", agent.InternalUrl);

            // 3. Connect and stream
            var hubUrl = $"{agent.InternalUrl}/hubs/nodeagent";
            var connection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .Build();

            await connection.StartAsync(cancellationToken);

            try
            {
                await foreach (var item in connection.StreamAsync<T>(
                    "AgentMethodName",
                    containerId,
                    cancellationToken))
                {
                    yield return item;
                }
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }
    }
}
```

## Final Verification

Before committing code, verify:
- [ ] I read `docs/ARCHITECTURE.md` completely
- [ ] I followed a reference implementation pattern
- [ ] I did NOT use anti-patterns
- [ ] I tested multi-node scenario
- [ ] Code review checklist complete

---

**Remember:** When in doubt, ask "Would this work if the container is on a different node?" If no, you're doing it wrong.
