using GameServer.Docker.Configurations;
using GameServer.Docker.Interfaces;
using GameServer.Docker.Models;
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
        private readonly ILogger<NodeAgentDiscoveryService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IServiceProvider _serviceProvider; // Use IServiceProvider to avoid circular dependency
        private readonly NodeAgentOptions _agentOptions;
        private readonly IAgentRegistry _agentRegistry;
        private readonly IUdpAgentRegistry _udpAgentRegistry;

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
            ILogger<NodeAgentDiscoveryService> logger,
            IHttpClientFactory httpClientFactory,
            IServiceProvider serviceProvider, // Inject IServiceProvider to avoid circular dependency
            NodeAgentOptions agentOptions,
            IAgentRegistry agentRegistry,
            IUdpAgentRegistry udpAgentRegistry)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _serviceProvider = serviceProvider;
            _agentOptions = agentOptions;
            _agentRegistry = agentRegistry;
            _udpAgentRegistry = udpAgentRegistry;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            #pragma warning disable CS0618 // Type or member is obsolete
            if (!_agentOptions.EnableBackgroundDiscovery)
            {
                _logger.LogWarning(
                    "⚠️ Background agent discovery is DISABLED. Using agent registration system only. " +
                    "If agents fail to register, container operations may fail.");
                return;
            }
            #pragma warning restore CS0618

            #pragma warning disable CS0618 // Type or member is obsolete
            _logger.LogWarning(
                "⚠️ DEPRECATION WARNING: Background agent discovery via Docker Swarm polling is deprecated. " +
                "This feature will be removed in a future version. " +
                "Please ensure agents are configured to register via AgentRegistration. " +
                "Set NodeAgentOptions:EnableBackgroundDiscovery=false to disable this legacy system.");
            #pragma warning restore CS0618

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

                var discoveredAgents = new List<NodeAgentEndpoint>();

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
            var udpAgents = _udpAgentRegistry.GetAllAgents();
            var discoveryAgents = _agents.Values.ToList();

            // Build combined list, preferring registry agents
            var agentsByNode = new Dictionary<string, NodeAgentEndpoint>();

            // Add discovery agents first
            foreach (var agent in discoveryAgents)
            {
                agentsByNode[agent.NodeId] = agent;
            }

            // Overlay UDP agents next
            foreach (var agent in udpAgents)
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
                "Returning {Total} agents ({Registry} from registry, {Udp} from UDP, {Discovery} from discovery, last discovery: {Seconds:F1}s ago)", 
                allAgents.Count,
                registryAgents.Count,
                udpAgents.Count,
                discoveryAgents.Count,
                timeSinceLastDiscovery.TotalSeconds);

            return Task.FromResult(allAgents);
        }

        private Task<List<NodeAgentEndpoint>> DiscoverAgentsInternalAsync(CancellationToken cancellationToken)
        {
            // Pull-based Docker Swarm discovery is no longer supported; all agents are discovered
            // via push-based registry registration or UDP announcements.
            return Task.FromResult(new List<NodeAgentEndpoint>());
        }

        public Task<NodeAgentEndpoint?> GetAgentForContainerAsync(string containerId)
        {
            _logger.LogDebug("Finding agent for container {ContainerId}", containerId);

            // Try registry first (new push-based registration system)
            var registryAgent = _agentRegistry.GetAgentForContainer(containerId);
            if (registryAgent != null)
            {
                _logger.LogDebug(
                    "✅ Found agent via REGISTRY (push-based) for container {ContainerId}: {AgentUrl} on node {NodeName}",
                    containerId.Substring(0, Math.Min(12, containerId.Length)),
                    registryAgent.InternalUrl,
                    registryAgent.NodeName);
                return Task.FromResult<NodeAgentEndpoint?>(registryAgent);
            }

            var udpAgent = _udpAgentRegistry.GetAgentForContainer(containerId);
            if (udpAgent != null)
            {
                _logger.LogDebug(
                    "✅ Found agent via UDP for container {ContainerId}: {AgentUrl} on node {NodeName}",
                    containerId.Substring(0, Math.Min(12, containerId.Length)),
                    udpAgent.InternalUrl,
                    udpAgent.NodeName);
                return Task.FromResult<NodeAgentEndpoint?>(udpAgent);
            }

            _logger.LogWarning(
                "⚠️ Agent not found in registry or UDP discovery for container {ContainerId}",
                containerId.Substring(0, Math.Min(12, containerId.Length)));
            return Task.FromResult<NodeAgentEndpoint?>(null);
        }

        public async Task<NodeAgentEndpoint?> GetAgentForServerAsync(string serverId)
        {
            _logger.LogDebug("Finding agent for server {ServerId}", serverId);

            // Strategy 1: If any registered agent already reports a container labelled with this server ID,
            // use the registry mapping.
            var containerId = await ResolveContainerIdForServerAsync(serverId).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(containerId))
            {
                _logger.LogDebug("Server {ServerId} resolved to container {ContainerId}, looking up agent", serverId, containerId);
                var agent = await GetAgentForContainerAsync(containerId).ConfigureAwait(false);
                if (agent != null)
                {
                    _logger.LogDebug("Found agent for server {ServerId} on node {NodeName}: {AgentUrl}",
                        serverId, agent.NodeName, agent.InternalUrl);
                    return agent;
                }
            }

            // Strategy 2: Broadcast a best-effort discovery to all agents to locate a container
            // whose labels include the server ID. This avoids requiring IDockerClient.
            var discoveredAgent = await DiscoverAgentForServerViaAgentsAsync(serverId).ConfigureAwait(false);
            if (discoveredAgent != null)
            {
                _logger.LogDebug("Found agent for server {ServerId} via agent probe on node {NodeName}: {AgentUrl}",
                    serverId, discoveredAgent.NodeName, discoveredAgent.InternalUrl);
            }
            else
            {
                _logger.LogWarning("No running container/agent found for server {ServerId}. Server may be stopped or not yet started", serverId);
            }

            return discoveredAgent;
        }

        private async Task<string?> ResolveContainerIdForServerAsync(string serverId)
        {
            // Registered agents send heartbeats with their container IDs. Check whether any of those
            // containers belongs to this server. We cannot infer that from the ID alone, so ask each
            // agent which containers it hosts and inspect the first-party labels via a lightweight probe.
            // In practice this is best-effort; the explicit probe in DiscoverAgentForServerViaAgentsAsync
            // performs the real resolution.
            await Task.CompletedTask;
            return null;
        }

        private async Task<NodeAgentEndpoint?> DiscoverAgentForServerViaAgentsAsync(string serverId)
        {
            var agents = (await DiscoverAgentsAsync().ConfigureAwait(false))
                .Where(a => a.IsHealthy)
                .ToList();

            if (agents.Count == 0)
            {
                _logger.LogWarning("No healthy agents available to locate server {ServerId}", serverId);
                return null;
            }

            // Ask each agent for its containers and pick the one that hosts a container for this server.
            // Containers deployed by this primary service carry a label with the server ID.
            var probeTasks = agents.Select(async agent =>
            {
                try
                {
                    var httpClient = GetOrCreateHttpClientForAgent(agent.InternalUrl);
                    var response = await httpClient.GetAsync($"/containers?label={Uri.EscapeDataString($"{GameServer.Docker.Constants.ServiceLabels.ServerId}={serverId}")}").ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        return null;
                    }

                    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                    {
                        return null;
                    }

                    return agent;
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "Agent probe failed for {AgentUrl} while locating server {ServerId}", agent.InternalUrl, serverId);
                    return null;
                }
            }).ToList();

            var results = await Task.WhenAll(probeTasks).ConfigureAwait(false);
            return results.FirstOrDefault(a => a != null);
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

        public async Task<List<string>?> GetServiceLogsAsync(string serviceId, int tailLines = 1000)
        {
            _logger.LogDebug("Fetching service logs for {ServiceId} (tail: {TailLines})", serviceId, tailLines);

            // Service logs must be retrieved from a manager node
            var managerAgent = await GetManagerAgentAsync();
            if (managerAgent == null)
            {
                _logger.LogWarning("Cannot fetch service logs: no manager agent available");
                return null;
            }

            try
            {
                var httpClient = GetOrCreateHttpClientForAgent(managerAgent.InternalUrl);
                var url = $"{managerAgent.InternalUrl}/api/services/{serviceId}/logs?tail={tailLines}";
                _logger.LogDebug("Fetching service logs from agent: GET {Url}", url);

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var response = await httpClient.GetAsync(url);
                stopwatch.Stop();

                _logger.LogTrace("Agent response: {StatusCode} in {Duration}ms", response.StatusCode, stopwatch.ElapsedMilliseconds);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                _logger.LogTrace("Received service logs JSON: {Length} bytes", json.Length);

                var doc = JsonDocument.Parse(json);

                // Parse the ServiceOperationResponse structure
                var success = doc.RootElement.GetProperty("success").GetBoolean();
                if (!success)
                {
                    var message = doc.RootElement.GetProperty("message").GetString();
                    _logger.LogWarning("Service logs request failed: {Message}", message);
                    return null;
                }

                var logsArray = doc.RootElement.GetProperty("data").GetProperty("logs");

                var logs = new List<string>();
                foreach (var logLine in logsArray.EnumerateArray())
                {
                    logs.Add(logLine.GetString() ?? string.Empty);
                }

                _logger.LogDebug("Successfully fetched {Count} log lines for service {ServiceId}", logs.Count, serviceId);
                return logs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching service logs from agent for service {ServiceId}", serviceId);
                return null;
            }
        }

        private async Task<NodeAgentEndpoint?> GetManagerAgentAsync()
        {
            var agents = await DiscoverAgentsAsync();

            // Find a manager node
            var managerAgent = agents.FirstOrDefault(a => a.IsManagerNode);

            if (managerAgent == null)
            {
                _logger.LogWarning("No manager agent available for service operations");
            }

            return managerAgent;
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
