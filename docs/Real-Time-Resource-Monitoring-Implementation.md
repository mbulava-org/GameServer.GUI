# Real-Time Resource Monitoring - Complete Implementation

## Overview

Implemented SignalR-based real-time resource monitoring for game servers, streaming CPU, memory, network, and disk usage metrics to connected clients.

---

## Architecture

```
Client ?SignalR? ResourceMonitoringHub ?Service? IGameServerResourceMonitor ?HTTP? Node Agents
```

### Flow
1. Client subscribes to server(s) via SignalR
2. Hub periodically polls `IGameServerResourceMonitor`
3. Resource data streamed to client in real-time
4. Client receives updates via events

---

## Components Created

### 1. Hub: `ResourceMonitoringHub`
**Path**: `src\GameServer.Docker\Hubs\ResourceMonitoringHub.cs`

**Features**:
- ? Single server monitoring
- ? Multiple server monitoring (batch updates)
- ? Configurable update intervals (1-60 seconds)
- ? One-time snapshots
- ? Dynamic interval updates
- ? Automatic session cleanup

**Methods**:
```csharp
Task SubscribeToServer(string serverId, int intervalSeconds = 5);
Task SubscribeToMultipleServers(string[] serverIds, int intervalSeconds = 5);
Task<ServerResourceUsage?> GetSnapshot(string serverId);
Task UpdateInterval(int intervalSeconds);
Task Unsubscribe();
```

**Events (Server ? Client)**:
```csharp
"ResourceUpdate" ? Single server update
"ResourceUpdateBatch" ? Multiple servers batch
"Subscribed" ? Subscription confirmed
"SubscribedMultiple" ? Multi-subscription confirmed
"Unsubscribed" ? Unsubscribed
"IntervalUpdated" ? Interval changed
"Error" ? Error occurred
```

---

### 2. Client Interface: `IResourceMonitoringClient`
**Path**: `src\GameServer.Docker.Client\Interfaces\IResourceMonitoringClient.cs`

**Features**:
- ? Event-driven API
- ? State tracking (IsConnected, MonitoredServerId, etc.)
- ? IAsyncDisposable for cleanup
- ? Strongly-typed methods

**Events (Client-Side)**:
```csharp
event EventHandler<ServerResourceUsage>? ResourceUpdateReceived;
event EventHandler<IEnumerable<ServerResourceUsage>>? ResourceUpdateBatchReceived;
event EventHandler<(string ServerId, int IntervalSeconds)>? Subscribed;
event EventHandler<(string[] ServerIds, int IntervalSeconds)>? SubscribedMultiple;
event EventHandler? Unsubscribed;
event EventHandler<int>? IntervalUpdated;
event EventHandler<string>? ErrorReceived;
```

**ServerResourceUsage Model**:
```csharp
public class ServerResourceUsage
{
    public string ServerId { get; set; }
    public string ServerName { get; set; }
    public string GameType { get; set; }
    public bool IsRunning { get; set; }
    public DateTime Timestamp { get; set; }
    
    // CPU
    public double? CpuUsagePercent { get; set; }
    
    // Memory
    public long? MemoryUsageBytes { get; set; }
    public long? MemoryLimitBytes { get; set; }
    public double? MemoryUsagePercent { get; set; }
    
    // Network
    public long? NetworkRxBytes { get; set; }
    public long? NetworkTxBytes { get; set; }
    
    // Disk
    public long? BlockReadBytes { get; set; }
    public long? BlockWriteBytes { get; set; }
    
    // Health
    public int? Replicas { get; set; }
    public int? HealthyReplicas { get; set; }
    
    // Location
    public string? ContainerId { get; set; }
    public string? NodeName { get; set; }
}
```

---

### 3. Client Implementation: `ResourceMonitoringClient`
**Path**: `src\GameServer.Docker.Client\Services\ResourceMonitoringClient.cs`

**Features**:
- ? Auto-reconnection (exponential backoff)
- ? Event forwarding (server events ? client events)
- ? Connection state management
- ? Optional logging
- ? Thread-safe operations

---

### 4. DI Extensions: `ServiceCollectionExtensions`
**Path**: `src\GameServer.Docker.Client\Extensions\ServiceCollectionExtensions.cs`

**Added Methods**:
```csharp
AddResourceMonitoringClient(services, hubUrl)
AddResourceMonitoringClient(services, hubUrl, configureConnection)
AddResourceMonitoringClientTransient(services, hubUrl)
```

---

## Usage Examples

### Example 1: Single Server Monitoring

```csharp
using GameServer.Docker.Client.Services;

await using var monitoring = new ResourceMonitoringClient("https://your-manager/hubs/resources");

// Handle updates
monitoring.ResourceUpdateReceived += (sender, usage) =>
{
    Console.WriteLine($"{usage.ServerName}:");
    Console.WriteLine($"  CPU: {usage.CpuUsagePercent:F1}%");
    Console.WriteLine($"  Memory: {usage.MemoryUsagePercent:F1}%");
    Console.WriteLine($"  Health: {usage.HealthyReplicas}/{usage.Replicas}");
};

// Connect and subscribe
await monitoring.ConnectAsync();
await monitoring.SubscribeToServerAsync("my-server-id", intervalSeconds: 5);

// Updates stream every 5 seconds...
await Task.Delay(TimeSpan.FromMinutes(5));
```

### Example 2: Dashboard with Multiple Servers

```csharp
public class ServerDashboard
{
    private readonly IResourceMonitoringClient _monitoring;
    private readonly Dictionary<string, ServerResourceUsage> _currentMetrics = new();
    
    public ServerDashboard(IResourceMonitoringClient monitoring)
    {
        _monitoring = monitoring;
        _monitoring.ResourceUpdateBatchReceived += OnBatchUpdate;
    }
    
    private void OnBatchUpdate(object? sender, IEnumerable<ServerResourceUsage> updates)
    {
        foreach (var update in updates)
        {
            _currentMetrics[update.ServerId] = update;
            
            // Check for alerts
            if (update.CpuUsagePercent > 90)
            {
                SendAlert($"High CPU on {update.ServerName}: {update.CpuUsagePercent:F1}%");
            }
            
            if (update.MemoryUsagePercent > 95)
            {
                SendAlert($"High memory on {update.ServerName}: {update.MemoryUsagePercent:F1}%");
            }
        }
        
        UpdateDashboardUI();
    }
    
    public async Task Start(string[] serverIds)
    {
        await _monitoring.ConnectAsync();
        await _monitoring.SubscribeToMultipleServersAsync(serverIds, intervalSeconds: 10);
    }
}
```

### Example 3: Dependency Injection

```csharp
// Startup.cs / Program.cs
builder.Services.AddResourceMonitoringClient("https://your-manager/hubs/resources");

// In a service
public class AlertingService : BackgroundService
{
    private readonly IResourceMonitoringClient _monitoring;
    private readonly ILogger<AlertingService> _logger;
    
    public AlertingService(
        IResourceMonitoringClient monitoring,
        ILogger<AlertingService> logger)
    {
        _monitoring = monitoring;
        _logger = logger;
        _monitoring.ResourceUpdateReceived += OnResourceUpdate;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _monitoring.ConnectAsync(stoppingToken);
        
        // Get all servers from API
        var serverIds = await GetAllServerIds();
        
        // Monitor all servers
        await _monitoring.SubscribeToMultipleServersAsync(serverIds, 10, stoppingToken);
        
        // Keep running
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
    
    private void OnResourceUpdate(object? sender, ServerResourceUsage usage)
    {
        if (usage.CpuUsagePercent > 90 || usage.MemoryUsagePercent > 95)
        {
            _logger.LogWarning(
                "High resource usage on {Server}: CPU {Cpu:F1}%, Memory {Memory:F1}%",
                usage.ServerName,
                usage.CpuUsagePercent,
                usage.MemoryUsagePercent);
        }
    }
}
```

### Example 4: Real-Time Chart Data

```csharp
public class MetricsCollector
{
    private readonly IResourceMonitoringClient _monitoring;
    private readonly List<(DateTime Time, double Cpu, double Memory)> _history = new();
    
    public MetricsCollector(IResourceMonitoringClient monitoring)
    {
        _monitoring = monitoring;
        _monitoring.ResourceUpdateReceived += OnUpdate;
    }
    
    private void OnUpdate(object? sender, ServerResourceUsage usage)
    {
        // Store for charting
        _history.Add((
            usage.Timestamp,
            usage.CpuUsagePercent ?? 0,
            usage.MemoryUsagePercent ?? 0
        ));
        
        // Keep last hour only
        var cutoff = DateTime.UtcNow.AddHours(-1);
        _history.RemoveAll(x => x.Time < cutoff);
        
        // Update chart
        UpdateChart();
    }
    
    public IEnumerable<ChartPoint> GetCpuChartData()
    {
        return _history.Select(h => new ChartPoint
        {
            X = h.Time,
            Y = h.Cpu
        });
    }
}
```

---

## Configuration

### Hub Endpoint
**Manager**: `https://your-manager/hubs/resources`

### Update Intervals
- **Minimum**: 1 second
- **Maximum**: 60 seconds
- **Default**: 5 seconds

### Reconnection Policy
- **Attempt 0**: Immediate
- **Attempt 1**: 2 seconds
- **Attempt 2**: 10 seconds
- **Attempt 3**: 30 seconds
- **After**: Stop retrying

---

## Performance Considerations

### Server Load
- Each subscribed client creates a background task
- Each task polls `IGameServerResourceMonitor` at the specified interval
- Recommended: Max 20-30 simultaneous clients per hub instance
- Consider SignalR scale-out (Redis/Azure SignalR) for more clients

### Network Bandwidth
- **Single server**: ~1-2 KB per update
- **10 servers**: ~10-20 KB per batch
- **5-second interval**: ~200-400 KB/min per client
- Consider longer intervals for many servers

### Client-Side
- Events fired on SignalR thread pool
- Handle updates quickly or queue for processing
- Don't block event handlers

---

## Comparison with REST Polling

| Aspect | SignalR Streaming | REST Polling |
|--------|------------------|--------------|
| **Latency** | Real-time (< 1s) | Polling interval |
| **Efficiency** | Push-based | Pull-based |
| **Server Load** | Constant | Spiky (per poll) |
| **Connection** | Persistent WebSocket | Request per poll |
| **Bandwidth** | Efficient (only updates) | Full payload each time |
| **Complexity** | Moderate (SignalR) | Simple (HTTP) |

**Recommendation**: Use SignalR for dashboards/monitoring UIs, REST for occasional checks.

---

## Testing

### Manual Testing

```bash
# Using SignalR test client (JavaScript)
const connection = new signalR.HubConnectionBuilder()
    .withUrl("https://localhost:5001/hubs/resources")
    .build();

connection.on("ResourceUpdate", (usage) => {
    console.log(usage);
});

await connection.start();
await connection.invoke("SubscribeToServer", "server-id", 5);
```

### Integration Testing

```csharp
[Fact]
public async Task SubscribeToServer_ValidServer_ReceivesUpdates()
{
    // Arrange
    var client = new ResourceMonitoringClient("https://localhost:5001/hubs/resources");
    var updateReceived = new TaskCompletionSource<ServerResourceUsage>();
    
    client.ResourceUpdateReceived += (s, usage) => updateReceived.TrySetResult(usage);
    
    // Act
    await client.ConnectAsync();
    await client.SubscribeToServerAsync("test-server-id", 1);
    
    // Assert
    var result = await updateReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
    Assert.NotNull(result);
    Assert.Equal("test-server-id", result.ServerId);
}
```

---

## Hub Configuration

### Program.cs

```csharp
// Add SignalR
builder.Services.AddSignalR();

// Map hub
app.MapHub<ResourceMonitoringHub>("/hubs/resources");
```

---

## API Summary

### Manager Endpoints

| Type | Endpoint | Purpose |
|------|----------|---------|
| SignalR Hub | `/hubs/resources` | Resource monitoring hub |
| SignalR Hub | `/hubs/console` | Container console hub |

### SignalR Methods (Client ? Server)

| Method | Parameters | Returns |
|--------|-----------|---------|
| `SubscribeToServer` | serverId, intervalSeconds | void |
| `SubscribeToMultipleServers` | serverIds[], intervalSeconds | void |
| `GetSnapshot` | serverId | ServerResourceUsage? |
| `UpdateInterval` | intervalSeconds | void |
| `Unsubscribe` | - | void |

### SignalR Events (Server ? Client)

| Event | Payload | Trigger |
|-------|---------|---------|
| `ResourceUpdate` | ServerResourceUsage | Single server update |
| `ResourceUpdateBatch` | ServerResourceUsage[] | Multiple servers batch |
| `Subscribed` | serverId, intervalSeconds | Subscription confirmed |
| `SubscribedMultiple` | serverIds[], intervalSeconds | Multi-subscription confirmed |
| `Unsubscribed` | - | Unsubscribed |
| `IntervalUpdated` | intervalSeconds | Interval changed |
| `Error` | string | Error occurred |

---

## Security Recommendations

### 1. Authentication
```csharp
[Authorize]
public class ResourceMonitoringHub : Hub
{
    // Require authentication
}
```

### 2. Authorization
```csharp
public async Task SubscribeToServer(string serverId, int intervalSeconds)
{
    // Check if user can access this server
    if (!await CanAccessServer(Context.User, serverId))
    {
        await Clients.Caller.SendAsync("Error", "Access denied");
        return;
    }
    // ... continue
}
```

### 3. Rate Limiting
```csharp
// Limit subscription count per user
private static ConcurrentDictionary<string, int> _userSubscriptions = new();

if (_userSubscriptions.GetOrAdd(Context.UserIdentifier, 0) >= 10)
{
    await Clients.Caller.SendAsync("Error", "Too many subscriptions");
    return;
}
```

---

## Build Status

? **Build Successful**

All components compile and integrate successfully.

---

## Files Created/Modified

### Created
1. ? `src\GameServer.Docker\Hubs\ResourceMonitoringHub.cs`
2. ? `src\GameServer.Docker.Client\Interfaces\IResourceMonitoringClient.cs`
3. ? `src\GameServer.Docker.Client\Services\ResourceMonitoringClient.cs`

### Modified
1. ? `src\GameServer.Docker\Program.cs` - Registered hub
2. ? `src\GameServer.Docker.Client\Extensions\ServiceCollectionExtensions.cs` - Added DI methods
3. ? `src\GameServer.Docker.Client\ReadMe.md` - Added documentation

---

## Summary

? **SignalR Hub**: Real-time resource streaming  
? **Client Interface**: Event-driven API  
? **Client Implementation**: Auto-reconnect, logging  
? **DI Extensions**: Easy registration  
? **Documentation**: Complete examples  
? **Build**: Successful  

The GameServer.Docker system now provides comprehensive real-time monitoring capabilities for all game servers! ??
