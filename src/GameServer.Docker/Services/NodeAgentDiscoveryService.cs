using Docker.DotNet;
using Docker.DotNet.Models;
using GameServer.Docker.Configurations;
using GameServer.Docker.Interfaces;
using GameServer.Docker.Models;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace GameServer.Docker.Services
{
    /// <summary>
    /// Background service that continuously discovers and monitors Node Agents in the Docker Swarm cluster.
    /// Maintains a thread-safe list of healthy agents with automatic eviction of unhealthy agents.
    /// NOTE: This service will be deprecated in favor of AgentRegistry which uses push-based registration.
    /// </summary>
    public class NodeAgentDiscoveryService : BackgroundService, INodeAgentDiscovery
    {
        private readonly IDockerClient _client;
        private readonly ILogger<NodeAgentDiscoveryService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IGameServerManager _serverManager;
        private readonly NodeAgentOptions _agentOptions;
        private readonly IAgentRegistry _agentRegistry;

        // Thread-safe agent storage - key is NodeId
        private readonly ConcurrentDictionary<string, NodeAgentEndpoint> _agents = new();
        private readonly SemaphoreSlim _discoveryLock = new(1, 1);
        private DateTime _lastDiscoveryTime = DateTime.MinValue;

        // Per-node HttpClient cache for better connection pooling and isolation
        // Key is the agent's InternalUrl (base address)
        private readonly ConcurrentDictionary<string, HttpClient> _httpClients = new();

        // SignalR connections to Node Agents - key is NodeId
        private readonly ConcurrentDictionary<string, HubConnection> _agentConnections = new();

        public NodeAgentDiscoveryService(
            IDockerClient client,
            ILogger<NodeAgentDiscoveryService> logger,
            IHttpClientFactory httpClientFactory,
            IGameServerManager serverManager,
            IOptions<NodeAgentOptions> agentOptions,
            IAgentRegistry agentRegistry)
        {
            _client = client;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _serverManager = serverManager;
            _agentOptions = agentOptions.Value;
            _agentRegistry = agentRegistry;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Node Agent Discovery background service starting (refresh interval: {Interval}s)", 
                _agentOptions.BackgroundRefreshIntervalSeconds);

            // Do initial discovery immediately
            await PerformDiscoveryAsync(stoppingToken);

            // Then run on interval
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_agentOptions.BackgroundRefreshIntervalSeconds));

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await PerformDiscoveryAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Node Agent Discovery background service is stopping");
            }
        }

        private async Task PerformDiscoveryAsync(CancellationToken cancellationToken)
        {
            // Use semaphore to prevent concurrent discoveries
            if (!await _discoveryLock.WaitAsync(0, cancellationToken))
            {
                _logger.LogDebug("Discovery already in progress, skipping this cycle");
                return;
            }

            try
            {
                _logger.LogDebug("Starting background agent discovery cycle");

                var discoveredAgents = await DiscoverAgentsInternalAsync(cancellationToken);
                
                // Update the thread-safe agent dictionary
                var currentNodeIds = new HashSet<string>(discoveredAgents.Select(a => a.NodeId));
                var previousNodeIds = new HashSet<string>(_agents.Keys);

                // Add or update discovered agents
                foreach (var agent in discoveredAgents)
                {
                    if (_agents.TryGetValue(agent.NodeId, out var existing))
                    {
                        // Update existing agent
                        if (!existing.IsHealthy && agent.IsHealthy)
                        {
                            _logger.LogTrace("Agent on node {NodeName} ({NodeId}) is now healthy", 
                                agent.NodeName, agent.NodeId);
                        }
                        else if (existing.IsHealthy && !agent.IsHealthy)
                        {
                            _logger.LogWarning("Agent on node {NodeName} ({NodeId}) is now unhealthy", 
                                agent.NodeName, agent.NodeId);
                        }
                        
                        _agents[agent.NodeId] = agent;
                    }
                    else
                    {
                        // New agent discovered
                        _agents[agent.NodeId] = agent;
                        _logger.LogTrace("New agent discovered on node {NodeName} ({NodeId}): {Url} [Healthy: {Healthy}]",
                            agent.NodeName, agent.NodeId, agent.InternalUrl, agent.IsHealthy);
                    }
                }

                // Remove agents that are no longer present
                var removedNodes = previousNodeIds.Except(currentNodeIds);
                foreach (var nodeId in removedNodes)
                {
                    if (_agents.TryRemove(nodeId, out var removed))
                    {
                        _logger.LogWarning("Agent removed from node {NodeName} ({NodeId}) - no longer present in Swarm",
                            removed.NodeName, nodeId);
                    }
                }

                _lastDiscoveryTime = DateTime.UtcNow;
                
                var healthyCount = _agents.Values.Count(a => a.IsHealthy);
                _logger.LogDebug("Discovery cycle complete: {Total} agents ({Healthy} healthy, {Unhealthy} unhealthy)",
                    _agents.Count, healthyCount, _agents.Count - healthyCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during background agent discovery");
            }
            finally
            {
                _discoveryLock.Release();
            }
        }

        public Task<List<NodeAgentEndpoint>> DiscoverAgentsAsync()
        {
            // PHASE 2: Merge agents from both systems
            // Priority: Registry agents (connected via registration) take precedence
            var registryAgents = _agentRegistry.GetAllAgents();
            var discoveryAgents = _agents.Values.ToList();

            // Build combined list, preferring registry agents
            var agentsByNode = new Dictionary<string, NodeAgentEndpoint>();

            // Add discovery agents first
            foreach (var agent in discoveryAgents)
            {
                agentsByNode[agent.NodeId] = agent;
            }

            // Overlay registry agents (these take precedence)
            foreach (var agent in registryAgents)
            {
                agentsByNode[agent.NodeId] = agent;
            }

            var allAgents = agentsByNode.Values.ToList();

            var timeSinceLastDiscovery = DateTime.UtcNow - _lastDiscoveryTime;
            _logger.LogDebug(
                "Returning {Total} agents ({Registry} from registry, {Discovery} from discovery, last discovery: {Seconds:F1}s ago)", 
                allAgents.Count,
                registryAgents.Count,
                discoveryAgents.Count,
                timeSinceLastDiscovery.TotalSeconds);

            return Task.FromResult(allAgents);
        }

        private async Task<List<NodeAgentEndpoint>> DiscoverAgentsInternalAsync(CancellationToken cancellationToken)
        {
            _logger.LogTrace("Discovering node agents in swarm (service: {ServiceName}, network: {NetworkName})", 
                _agentOptions.ServiceName, _agentOptions.NetworkName);

            try
            {
                // Find agent service tasks
                // Note: We only filter by desired-state, not actual state, because services
                // may remain in "starting" state for extended periods due to health checks
                var tasks = await _client.Tasks.ListAsync(new TasksListParameters
                {
                    Filters = new Dictionary<string, IDictionary<string, bool>>
                    {
                        ["service"] = new Dictionary<string, bool> { [_agentOptions.ServiceName] = true },
                        ["desired-state"] = new Dictionary<string, bool> { ["running"] = true }
                    }
                }, cancellationToken);

                var agents = new List<NodeAgentEndpoint>();

                _logger.LogTrace("Found {TaskCount} tasks for service '{ServiceName}'", tasks.Count, _agentOptions.ServiceName);

                // Filter tasks by configured active states (running, starting, ready, etc.)
                var activeStates = _agentOptions.ActiveTaskStates
                    .Select(s => s.ToLowerInvariant())
                    .ToHashSet();

                var activeTasks = tasks.Where(t => 
                    t.Status?.State != null && 
                    activeStates.Contains(t.Status.State.ToString().ToLowerInvariant())).ToList();

                _logger.LogTrace("{ActiveCount} of {TotalCount} tasks are in active states", activeTasks.Count, tasks.Count);

                foreach (var task in activeTasks)
                {
                    var nodeId = task.NodeID ?? string.Empty;
                    var containerId = task.Status?.ContainerStatus?.ContainerID ?? string.Empty;
                    var taskId = task.ID ?? string.Empty;

                    _logger.LogTrace("Processing task {TaskId} on node {NodeId}, container {ContainerId}, state {State}",
                        taskId, nodeId, containerId, task.Status?.State);

                    if (string.IsNullOrEmpty(nodeId) || string.IsNullOrEmpty(containerId))
                    {
                        _logger.LogTrace("Skipping task {TaskId}: missing node ID or container ID", taskId);
                        continue;
                    }

                    // Get node information for hostname
                    var node = await _client.Swarm.InspectNodeAsync(nodeId, cancellationToken);
                    var nodeName = node.Description?.Hostname ?? nodeId;

                    // Get the overlay network IP from task's NetworkAttachments
                    // Use the configured network name to find the correct attachment
                    if (task.NetworksAttachments == null || !task.NetworksAttachments.Any())
                    {
                        _logger.LogTrace("Task {TaskId} on node {NodeName} ({NodeId}) has no network attachments",
                            taskId, nodeName, nodeId);
                        continue;
                    }

                    _logger.LogTrace("Task {TaskId} has {Count} network attachment(s): {Networks}",
                        taskId,
                        task.NetworksAttachments.Count,
                        string.Join(", ", task.NetworksAttachments.Select(na => na.Network?.Spec?.Name ?? "unknown")));

                    var networkAttachment = task.NetworksAttachments
                        .FirstOrDefault(na => na.Network?.Spec?.Name == _agentOptions.NetworkName);
                    
                    var agentIp = networkAttachment?.Addresses?.FirstOrDefault()?.Split('/')[0];
                    
                    if (string.IsNullOrEmpty(agentIp))
                    {
                        _logger.LogTrace("Could not determine overlay network IP for agent on node {NodeName} ({NodeId}) on network {NetworkName}. Available networks: {Networks}", 
                            nodeName, nodeId, _agentOptions.NetworkName,
                            string.Join(", ", task.NetworksAttachments?.Select(na => na.Network?.Spec?.Name ?? "unknown") ?? Array.Empty<string>()));
                        continue;
                    }

                    _logger.LogTrace("Found agent IP {AgentIp} for task {TaskId} on node {NodeName}",
                        agentIp, taskId, nodeName);

                    // Agent is accessible via overlay network IP
                    // Format: http://{overlay-ip}:{agent-port}
                    var internalUrl = $"http://{agentIp}:{_agentOptions.Port}";

                    var agent = new NodeAgentEndpoint
                    {
                        NodeId = nodeId,
                        NodeName = nodeName,
                        TaskId = task.ID ?? string.Empty,
                        ContainerId = containerId,
                        InternalUrl = internalUrl,
                        DiscoveredAt = DateTime.UtcNow
                    };

                    // Health check
                    _logger.LogTrace("Performing health check for agent at {Url}", internalUrl);
                    var healthCheckStart = DateTime.UtcNow;
                    agent.IsHealthy = await CheckAgentHealthAsync(agent);
                    var healthCheckDuration = (DateTime.UtcNow - healthCheckStart).TotalMilliseconds;

                    agents.Add(agent);
                    _logger.LogTrace("Discovered agent on node {NodeName} ({NodeId}): {Url} [State: {State}, Healthy: {Healthy}, HealthCheck: {Duration}ms]",
                        nodeName, nodeId, internalUrl, task.Status?.State, agent.IsHealthy, healthCheckDuration);
                }

                return agents;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error discovering node agents");
                return new List<NodeAgentEndpoint>();
            }
        }

        public async Task<NodeAgentEndpoint?> GetAgentForContainerAsync(string containerId)
        {
            _logger.LogDebug("Finding agent for container {ContainerId}", containerId);

            // PHASE 2: Try registry first (new push-based registration system)
            var registryAgent = _agentRegistry.GetAgentForContainer(containerId);
            if (registryAgent != null)
            {
                _logger.LogDebug(
                    "✅ Found agent via REGISTRY (push-based) for container {ContainerId}: {AgentUrl} on node {NodeName}",
                    containerId.Substring(0, Math.Min(12, containerId.Length)),
                    registryAgent.InternalUrl,
                    registryAgent.NodeName);
                return registryAgent;
            }

            _logger.LogDebug(
                "⚠️ Agent not found in registry for container {ContainerId}, falling back to Docker Swarm query (pull-based discovery)",
                containerId.Substring(0, Math.Min(12, containerId.Length)));

            // FALLBACK: Use legacy Docker Swarm query method
            // First, find which node the container is on
            _logger.LogTrace("Querying Docker for all running tasks to locate container {ContainerId}", containerId);
            var tasks = await _client.Tasks.ListAsync(new TasksListParameters
            {
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    ["desired-state"] = new Dictionary<string, bool> { ["running"] = true }
                }
            });

            _logger.LogTrace("Found {TaskCount} running tasks total", tasks.Count);

            // Accept active states (running, starting, ready) since containers may be starting
            var activeStates = _agentOptions.ActiveTaskStates
                .Select(s => s.ToLowerInvariant())
                .ToHashSet();

            var task = tasks.FirstOrDefault(t =>
                t.Status?.State != null &&
                activeStates.Contains(t.Status.State.ToString().ToLowerInvariant()) &&
                t.Status?.ContainerStatus?.ContainerID == containerId);

            if (task == null)
            {
                _logger.LogWarning("No active task found for container {ContainerId}. Container may have stopped or not yet started", containerId);
                return null;
            }

            var nodeId = task.NodeID;
            if (string.IsNullOrEmpty(nodeId))
            {
                _logger.LogWarning("Task {TaskId} for container {ContainerId} has no node ID", task.ID, containerId);
                return null;
            }

            _logger.LogDebug("Container {ContainerId} is running on node {NodeId} (task state: {State})",
                containerId, nodeId, task.Status?.State);

            // Find agent on that node from our cached list
            if (_agents.TryGetValue(nodeId, out var agent) && agent.IsHealthy)
            {
                _logger.LogDebug(
                    "✅ Found agent via DISCOVERY (pull-based) for container {ContainerId}: {AgentUrl} on node {NodeName}",
                    containerId,
                    agent.InternalUrl,
                    agent.NodeName);
                return agent;
            }

            // Try to find any agent on that node (even if unhealthy)
            if (_agents.TryGetValue(nodeId, out var unhealthyAgent))
            {
                _logger.LogWarning("Agent found on node {NodeId} but is unhealthy: {AgentUrl}", nodeId, unhealthyAgent.InternalUrl);
            }
            else
            {
                _logger.LogWarning("No agent found on node {NodeId} at all. Available nodes: {Nodes}",
                    nodeId, string.Join(", ", _agents.Values.Select(a => $"{a.NodeName}({a.NodeId})")));
            }

            return null;
        }

        public async Task<NodeAgentEndpoint?> GetAgentForServerAsync(string serverId)
        {
            _logger.LogDebug("Finding agent for server {ServerId}", serverId);

            // Get running container ID for this server
            _logger.LogTrace("Looking up container ID for server {ServerId}", serverId);
            var containerId = await _serverManager.GetRunningContainerIdAsync(serverId);
            
            if (string.IsNullOrEmpty(containerId))
            {
                _logger.LogWarning("No running container found for server {ServerId}. Server may be stopped or not yet started", serverId);
                return null;
            }

            _logger.LogDebug("Server {ServerId} has container {ContainerId}, looking up agent", serverId, containerId);
            var agent = await GetAgentForContainerAsync(containerId);
            
            if (agent != null)
            {
                _logger.LogDebug("Found agent for server {ServerId} on node {NodeName}: {AgentUrl}",
                    serverId, agent.NodeName, agent.InternalUrl);
            }
            
            return agent;
        }

        public async Task<ContainerStats?> GetContainerStatsAsync(string containerId)
        {
            _logger.LogDebug("Fetching container stats for {ContainerId}", containerId);
            
            var agent = await GetAgentForContainerAsync(containerId);
            if (agent == null)
            {
                _logger.LogWarning("Cannot fetch stats for container {ContainerId}: no agent available", containerId);
                return null;
            }

            try
            {
                var httpClient = GetOrCreateHttpClientForAgent(agent.InternalUrl);
                var url = $"{agent.InternalUrl}/containers/{containerId}/stats";
                _logger.LogDebug("Fetching stats from agent: GET {Url}", url);

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var response = await httpClient.GetAsync(url);
                stopwatch.Stop();
                
                _logger.LogTrace("Agent response: {StatusCode} in {Duration}ms", response.StatusCode, stopwatch.ElapsedMilliseconds);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                _logger.LogTrace("Received stats JSON: {Length} bytes", json.Length);
                
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var stats = new ContainerStats
                {
                    ContainerId = containerId,
                    Timestamp = root.GetProperty("timestamp").GetDateTime(),
                    
                    // CPU
                    CpuUsagePercent = root.GetProperty("cpu").GetProperty("usagePercent").GetDouble(),
                    CpuTotalUsage = root.GetProperty("cpu").GetProperty("totalUsage").GetUInt64(),
                    CpuSystemUsage = root.GetProperty("cpu").GetProperty("systemUsage").GetUInt64(),
                    OnlineCpus = root.GetProperty("cpu").GetProperty("onlineCpus").GetUInt32(),
                    
                    // Memory
                    MemoryUsageBytes = root.GetProperty("memory").GetProperty("usageBytes").GetUInt64(),
                    MemoryLimitBytes = root.GetProperty("memory").GetProperty("limitBytes").GetUInt64(),
                    MemoryUsagePercent = root.GetProperty("memory").GetProperty("usagePercent").GetDouble(),
                    MemoryMaxUsageBytes = root.GetProperty("memory").GetProperty("maxUsageBytes").GetUInt64(),
                    
                    // Network
                    NetworkRxBytes = root.GetProperty("network").GetProperty("rxBytes").GetInt64(),
                    NetworkTxBytes = root.GetProperty("network").GetProperty("txBytes").GetInt64(),
                    
                    // Block I/O
                    BlockReadBytes = root.GetProperty("blockIo").GetProperty("readBytes").GetInt64(),
                    BlockWriteBytes = root.GetProperty("blockIo").GetProperty("writeBytes").GetInt64(),
                    
                    // Processes
                    Pids = root.GetProperty("pids").GetUInt64()
                };

                _logger.LogDebug("Successfully fetched stats for container {ContainerId}: CPU {Cpu:F2}%, Memory {Memory:F2}%",
                    containerId, stats.CpuUsagePercent, stats.MemoryUsagePercent);

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching stats from agent for container {ContainerId}", containerId);
                return null;
            }
        }

        public async IAsyncEnumerable<ContainerStats> StreamContainerStatsAsync(
            string containerId, 
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Starting SignalR stats stream for container {ContainerId}", containerId);

            // Find the agent for this container
            var agent = await GetAgentForContainerAsync(containerId);
            if (agent == null)
            {
                _logger.LogWarning("Cannot stream stats for container {ContainerId}: no agent available", containerId);
                yield break;
            }

            // Get or create SignalR connection to this agent
            var hubConnection = await GetOrCreateAgentConnectionAsync(agent, cancellationToken);
            if (hubConnection == null)
            {
                _logger.LogError("Cannot establish SignalR connection to agent for container {ContainerId}", containerId);
                yield break;
            }

            _logger.LogInformation("Streaming stats from Agent {AgentUrl} for container {ContainerId} via SignalR",
                agent.InternalUrl, containerId);

            // Stream stats from the agent's SignalR hub
            await foreach (var statsData in hubConnection.StreamAsync<object>(
                "StreamContainerStats", 
                containerId, 
                cancellationToken))
            {
                ContainerStats? stats = null;
                
                try
                {
                    // Parse the stats object (coming as JSON from SignalR)
                    var json = JsonSerializer.Serialize(statsData);
                    var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    stats = new ContainerStats
                    {
                        ContainerId = containerId,
                        Timestamp = root.GetProperty("timestamp").GetDateTime(),
                        
                        // CPU
                        CpuUsagePercent = root.GetProperty("cpu").GetProperty("usagePercent").GetDouble(),
                        CpuTotalUsage = root.GetProperty("cpu").GetProperty("totalUsage").GetUInt64(),
                        CpuSystemUsage = root.GetProperty("cpu").GetProperty("systemUsage").GetUInt64(),
                        OnlineCpus = root.GetProperty("cpu").GetProperty("onlineCpus").GetUInt32(),
                        
                        // Memory
                        MemoryUsageBytes = root.GetProperty("memory").GetProperty("usageBytes").GetUInt64(),
                        MemoryLimitBytes = root.GetProperty("memory").GetProperty("limitBytes").GetUInt64(),
                        MemoryUsagePercent = root.GetProperty("memory").GetProperty("usagePercent").GetDouble(),
                        MemoryMaxUsageBytes = root.GetProperty("memory").GetProperty("maxUsageBytes").GetUInt64(),
                        
                        // Network
                        NetworkRxBytes = root.GetProperty("network").GetProperty("rxBytes").GetInt64(),
                        NetworkTxBytes = root.GetProperty("network").GetProperty("txBytes").GetInt64(),
                        
                        // Block I/O
                        BlockReadBytes = root.GetProperty("blockIo").GetProperty("readBytes").GetInt64(),
                        BlockWriteBytes = root.GetProperty("blockIo").GetProperty("writeBytes").GetInt64(),
                        
                        // Processes
                        Pids = root.GetProperty("pids").GetUInt64()
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing stats from SignalR stream for container {ContainerId}", containerId);
                }

                if (stats != null)
                {
                    yield return stats;
                }
            }

            _logger.LogInformation("Stats stream ended for container {ContainerId}", containerId);
        }

        /// <summary>
        /// Get or create a SignalR connection to the specified agent
        /// </summary>
        private async Task<HubConnection?> GetOrCreateAgentConnectionAsync(NodeAgentEndpoint agent, CancellationToken cancellationToken)
        {
            // Check if we already have a connection for this agent
            if (_agentConnections.TryGetValue(agent.NodeId, out var existingConnection))
            {
                // Check if the connection is still healthy
                if (existingConnection.State == HubConnectionState.Connected)
                {
                    return existingConnection;
                }
                
                // Connection is not healthy, remove it and create a new one
                _logger.LogWarning("Existing SignalR connection to agent {NodeId} is in state {State}, recreating",
                    agent.NodeId, existingConnection.State);
                
                _agentConnections.TryRemove(agent.NodeId, out _);
                
                try
                {
                    await existingConnection.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing old connection to agent {NodeId}", agent.NodeId);
                }
            }

            // Create new connection
            try
            {
                var hubUrl = $"{agent.InternalUrl}/hubs/nodeagent";
                _logger.LogInformation("Creating SignalR connection to Node Agent at {HubUrl}", hubUrl);

                var connection = new HubConnectionBuilder()
                    .WithUrl(hubUrl)
                    .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
                    .Build();

                // Set up reconnection logging
                connection.Reconnecting += error =>
                {
                    _logger.LogWarning(error, "SignalR connection to agent {NodeId} reconnecting", agent.NodeId);
                    return Task.CompletedTask;
                };

                connection.Reconnected += connectionId =>
                {
                    _logger.LogInformation("SignalR connection to agent {NodeId} reconnected (ConnectionId: {ConnectionId})",
                        agent.NodeId, connectionId);
                    return Task.CompletedTask;
                };

                connection.Closed += error =>
                {
                    _logger.LogWarning(error, "SignalR connection to agent {NodeId} closed", agent.NodeId);
                    _agentConnections.TryRemove(agent.NodeId, out _);
                    return Task.CompletedTask;
                };

                // Connect to the agent
                await connection.StartAsync(cancellationToken);
                
                _logger.LogInformation("Successfully connected to Node Agent {NodeId} at {HubUrl}",
                    agent.NodeId, hubUrl);

                // Store the connection
                _agentConnections[agent.NodeId] = connection;

                return connection;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create SignalR connection to agent {NodeId} at {Url}",
                    agent.NodeId, agent.InternalUrl);
                return null;
            }
        }

        public async Task<List<string>?> GetContainerLogsAsync(string containerId, int tailLines = 100)
        {
            _logger.LogDebug("Fetching container logs for {ContainerId} (tail: {TailLines})", containerId, tailLines);
            
            var agent = await GetAgentForContainerAsync(containerId);
            if (agent == null)
            {
                _logger.LogWarning("Cannot fetch logs for container {ContainerId}: no agent available", containerId);
                return null;
            }

            try
            {
                var httpClient = GetOrCreateHttpClientForAgent(agent.InternalUrl);
                var url = $"{agent.InternalUrl}/containers/{containerId}/logs?tail={tailLines}";
                _logger.LogDebug("Fetching logs from agent: GET {Url}", url);

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var response = await httpClient.GetAsync(url);
                stopwatch.Stop();
                
                _logger.LogTrace("Agent response: {StatusCode} in {Duration}ms", response.StatusCode, stopwatch.ElapsedMilliseconds);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                _logger.LogTrace("Received logs JSON: {Length} bytes", json.Length);
                
                var doc = JsonDocument.Parse(json);
                var logsArray = doc.RootElement.GetProperty("logs");

                var logs = new List<string>();
                foreach (var logLine in logsArray.EnumerateArray())
                {
                    logs.Add(logLine.GetString() ?? string.Empty);
                }

                _logger.LogDebug("Successfully fetched {Count} log lines for container {ContainerId}", logs.Count, containerId);
                return logs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching logs from agent for container {ContainerId}", containerId);
                return null;
            }
        }

        private async Task<bool> CheckAgentHealthAsync(NodeAgentEndpoint agent)
        {
            try
            {
                var httpClient = GetOrCreateHttpClientForAgent(agent.InternalUrl);
                var url = $"{agent.InternalUrl}/health";
                _logger.LogTrace("Health check: GET {Url}", url);
                
                var response = await httpClient.GetAsync(url);
                var isHealthy = response.IsSuccessStatusCode;
                
                if (!isHealthy)
                {
                    _logger.LogTrace("Agent health check failed for {Url}: {StatusCode}",
                        url, response.StatusCode);
                }
                
                return isHealthy;
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Agent health check exception for {Url}: {Message}",
                    agent.InternalUrl, ex.Message);
                return false;
            }
        }
        
        /// <summary>
        /// Gets or creates an HttpClient for the specified agent base URL.
        /// Each node agent gets its own HttpClient for optimal connection pooling.
        /// </summary>
        private HttpClient GetOrCreateHttpClientForAgent(string baseUrl)
        {
            return _httpClients.GetOrAdd(baseUrl, url =>
            {
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.BaseAddress = new Uri(url);
                httpClient.Timeout = TimeSpan.FromSeconds(_agentOptions.TimeoutSeconds);
                
                _logger.LogDebug("Created dedicated HttpClient for node agent: {BaseUrl}", url);
                return httpClient;
            });
        }

        /// <summary>
        /// Cleanup resources when the service stops
        /// </summary>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Node Agent Discovery service stopping, cleaning up SignalR connections");

            // Dispose all SignalR connections
            foreach (var kvp in _agentConnections)
            {
                try
                {
                    _logger.LogDebug("Closing SignalR connection to agent {NodeId}", kvp.Key);
                    await kvp.Value.StopAsync(cancellationToken);
                    await kvp.Value.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error closing connection to agent {NodeId}", kvp.Key);
                }
            }

            _agentConnections.Clear();
            await base.StopAsync(cancellationToken);
        }
    }
}
