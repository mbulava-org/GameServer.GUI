# Multi-Node Container Logs Fix

## Root Cause Analysis

### The Problem
ServerLogsHub was connecting **directly to a single Docker daemon** using `_dockerClient`:
```csharp
logStream = await _dockerClient.Containers.GetContainerLogsAsync(containerId, ...);
```

**In Docker Swarm with multiple nodes:**
- GameServer.Docker connects to Docker daemon at `http://192.168.10.50:6666/`
- Container might be running on a **different node**
- Docker API returns: `DockerContainerNotFoundException: No such container`

### Why ResourceMonitor Works
ResourceMonitor uses **Node Agents** to find containers:
```csharp
// 1. Find which agent has the container
var agent = await _nodeAgentDiscovery.GetAgentForContainerAsync(containerId);

// 2. Connect to that agent
// 3. Get stats from the correct node
```

### The Architecture
```
???????????????????????
? GameServer.Docker   ? ? Central orchestrator
? (Node A)            ?
???????????????????????
           ?
     ????????????????????????
     ?           ?          ?
??????????? ??????????? ???????????
? Agent 1 ? ? Agent 2 ? ? Agent 3 ?
? (Node A)? ? (Node B)? ? (Node C)?
??????????? ??????????? ???????????
     ?          ?           ?
  [Container] [Container] [Container]
```

**Current Behavior:**
- ServerLogsHub queries Docker on Node A only
- Container might be on Node B or C ? NOT FOUND!

**Correct Behavior:**
- Ask Node Agents to find the container
- Connect to the correct agent
- Stream logs from that agent

## The Solution

### Step 1: Add Node Agent Discovery to ServerLogsHub

```csharp
public class ServerLogsHub : Hub
{
    private readonly INodeAgentDiscovery _nodeAgentDiscovery;
    private readonly IHttpClientFactory _httpClientFactory;
    
    // Remove or make optional: private readonly IDockerClient _dockerClient;
    
    public ServerLogsHub(
        ILogger<ServerLogsHub> logger,
        IGameServerManager serverManager,
        INodeAgentDiscovery nodeAgentDiscovery,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _serverManager = serverManager;
        _nodeAgentDiscovery = nodeAgentDiscovery;
        _httpClientFactory = httpClientFactory;
    }
}
```

### Step 2: Use Node Agent to Find Container

```csharp
public async IAsyncEnumerable<string> StreamServerLogs(...)
{
    // ... get server and containerId ...
    
    // Find which node agent has this container
    var agent = await _nodeAgentDiscovery.GetAgentForContainerAsync(containerId);
    if (agent == null)
    {
        _logger.LogWarning("No agent found for container {ContainerId}", containerId);
        yield return $"ERROR: Container {containerId} not found on any node";
        yield break;
    }
    
    _logger.LogInformation("Found container {ContainerId} on agent {AgentUrl}", 
        containerId, agent.InternalUrl);
    
    // Connect to agent's SignalR hub and stream logs
    await foreach (var logLine in StreamLogsFromAgentAsync(
        agent.InternalUrl, containerId, follow, tailLines, timestamps, cancellationToken))
    {
        yield return logLine;
    }
}
```

### Step 3: Stream Logs from Agent

```csharp
private async IAsyncEnumerable<string> StreamLogsFromAgentAsync(
    string agentUrl,
    string containerId,
    bool follow,
    int tailLines,
    bool timestamps,
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    var hubUrl = $"{agentUrl}/hubs/nodeagent";
    _logger.LogDebug("Connecting to Node Agent hub at {HubUrl}", hubUrl);
    
    var connection = new HubConnectionBuilder()
        .WithUrl(hubUrl, options =>
        {
            options.HttpMessageHandlerFactory = _ => 
                _httpClientFactory.CreateHandler();
        })
        .WithAutomaticReconnect()
        .Build();
    
    try
    {
        await connection.StartAsync(cancellationToken);
        _logger.LogInformation("Connected to Node Agent, streaming logs for container {ContainerId}", 
            containerId);
        
        // Call the agent's StreamContainerLogs method
        await foreach (var logLine in connection.StreamAsync<string>(
            "StreamContainerLogs",
            containerId,
            follow,
            tailLines,
            timestamps,
            cancellationToken))
        {
            yield return logLine;
        }
    }
    finally
    {
        await connection.DisposeAsync();
        _logger.LogDebug("Disconnected from Node Agent");
    }
}
```

## Benefits

? **Works with multi-node Swarm** - automatically finds the right node  
? **No stale container IDs** - agent always has current containers  
? **Consistent with architecture** - matches how ResourceMonitor and ContainerConsole work  
? **Better error messages** - knows which node was checked  
? **Scalable** - works with any number of nodes  

## Testing

1. Deploy containers across multiple nodes
2. Query logs from GameServer.Docker
3. Verify it connects to the correct agent
4. Check logs show: "Found container on agent {url}"

## Alternative: Query Swarm Manager

If you don't want to relay through agents, you could:
1. Configure `_dockerClient` to connect to **Swarm Manager**
2. Use **Service logs** instead of container logs
3. Swarm manager can see all containers

But this doesn't work for:
- Real-time log streaming (service logs are snapshots)
- Terminal/console access (needs direct container connection)
- Container stats (needs node-level access)

**Recommendation**: Use Node Agents as designed. They exist specifically to solve this problem!
