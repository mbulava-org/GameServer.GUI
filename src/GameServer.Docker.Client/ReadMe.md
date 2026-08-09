# GameServer.Docker.Client

A comprehensive .NET client library for managing game servers running in Docker Swarm. This library provides strongly-typed REST API clients for server management, file operations, game type registry, port allocation, and **real-time streaming features** via SignalR.

## Features

### REST API Clients
- ? **Server Management** - Deploy, start, stop, restart, and list game servers
- ? **File Management** - Upload, download, delete files and create directories
- ? **Resource Monitoring** - Get server resource usage (CPU, memory, network, disk)
- ? **Log Access** - Retrieve service logs with optional tail parameter
- ? **Game Type Registry** - Manage game server templates and configurations
- ? **Extended Metadata** - Advanced game type configuration with validation, enums, and dynamic port mapping
- ? **Port Management** - Allocate and release network ports for servers
- ? **Dashboard** - Get overview of all servers with status information

### Real-Time Features (SignalR) ? NEW
- ? **Live Resource Monitoring** - Real-time CPU, memory, network, and disk usage streaming
- ? **Interactive Console** - Bidirectional shell access to containers
- ? **Interactive Command Execution** - Run commands with stdin/stdout/stderr via WebSocket
- ? **TTY Support** - Full terminal emulation for vim, nano, htop, etc.
- ? **Command Execution** - Execute commands and receive output in real-time

### Built-In Features
- ?? **Type-Safe** - Strongly-typed clients auto-generated from OpenAPI specification using NSwag
- ?? **Dependency Injection** - First-class DI support for ASP.NET Core
- ? **Async/Await** - Modern async patterns throughout
- ?? **HttpClient Integration** - Uses standard HttpClient for HTTP communication
- ? **True Streaming** - End-to-end SignalR streaming with zero polling

## Installation

```bash
dotnet add package GameServer.Docker.Client
```

> **Note:** This package is currently in development and may not yet be published to NuGet.

## Recent Architectural Improvements

### v0.1.0 (Latest) - Real-Time Streaming & Interactive Exec ??

#### End-to-End SignalR Streaming Architecture
The system now uses **true streaming** from Docker containers to external clients with **zero HTTP polling**:

**Architecture Flow:**
```
Docker (IProgress callbacks)
  ? Native streaming
Node Agent SignalR Hub
  ? SignalR streaming
Primary Service
  ? SignalR streaming
External Clients
```

**Benefits:**
- ? **Sub-second latency** for resource monitoring
- ? **Zero polling** anywhere in the stack
- ? **Efficient** - Persistent WebSocket connections
- ? **Scalable** - Automatic reconnection with backoff
- ? **Real-time** - True push-based streaming

#### Interactive Command Execution via WebSocket
New `ExecInteractiveAsync` method provides full interactive shell access:

**Features:**
- ? Direct WebSocket connection to Node Agents
- ? Full stdin/stdout/stderr bidirectional communication
- ? TTY support for terminal applications (vim, nano, htop, etc.)
- ? Same API pattern as container attach
- ? Event-driven output handling

**Use Cases:**
- Interactive debugging shells
- Running terminal applications
- Real-time command feedback
- Admin consoles and monitoring tools

**Example:**
```csharp
client.OutputReceived += (s, o) => Console.Write(o);

await client.ExecInteractiveAsync(
    agentUrl: "http://agent:8080",
    containerId: "abc123",
    command: "bash",
    args: new[] { "-i" },
    tty: true
);

await client.SendInputAsync("ls -la\n");
await client.SendInputAsync("cat server.log\n");
await client.SendInputAsync("exit\n");
```

### v0.0.1 (Beta) - Performance & Scalability Enhancements

#### Multi-File Storage for Extended Metadata
The extended metadata system now stores each game type in its own individual file for better performance and maintainability:

**Storage Structure:**
```
/data/game-types-extended/
  ?? minecraft.json
  ?? valheim.json
  ?? hytale.json
```

**Benefits:**
- ? **Faster Updates** - Only affected game type file is written
- ? **Better Concurrency** - Per-file locking reduces contention
- ? **Easier Maintenance** - Inspect/edit individual game types
- ? **Scalable** - No single file size limits

**API Impact:** None - All API endpoints work identically.

#### Optimized Node Agent Communication
The backend now uses dedicated HttpClient instances per node agent for optimal connection pooling in multi-node Docker Swarm environments:

**Architecture:**
- One HttpClient per node agent endpoint
- Concurrent requests don't block each other
- Better isolation between node operations
- Thread-safe connection management

**Benefits:**
- ? **Better Performance** - Optimized connection pooling per node
- ? **Higher Throughput** - Parallel operations across nodes
- ? **Improved Reliability** - Node isolation prevents cascading failures
- ? **Lower Latency** - Reduced connection overhead

**API Impact:** None - Improved performance is transparent to clients.

#### Smart CI/CD Pipeline
The build pipeline now intelligently detects API changes and only publishes client packages when necessary:

**Features:**
- Detects changes in Controllers, Models, DTOs, and Interfaces
- Provides GitHub Actions warnings when API contracts change
- Manual override option via `workflow_dispatch`
- Skips unnecessary publishes for non-API changes

**Benefits:**
- ? **Reduced Noise** - Only meaningful client updates
- ? **Faster Builds** - Skips client publish when not needed
- ? **Clear Communication** - Warnings highlight breaking changes

**For Client Users:**
- New client versions only published when API actually changes
- Check GitHub Actions warnings for API compatibility notes

## Quick Start

### Game Server API - Server Management

The primary API for managing game servers, including deployment, lifecycle operations, file management, and resource monitoring.


```csharp
using GameServer.Docker.Client;

var httpClient = new HttpClient 
{ 
    BaseAddress = new Uri("https://your-api.com") 
}; 
var gameServerApi = new GameServerApi(httpClient);

// List all servers 
var servers = await gameServerApi.ListAsync(); 
foreach (var server in servers) 
{ 
    Console.WriteLine($"Server: {server.Name} ({server.ServerId})"); 
}

// Get specific server 
var server = await gameServerApi.GetAsync("server-id"); 
Console.WriteLine($"Server is {(server.IsRunning ? "running" : "stopped")}");

// Deploy a new server 
var newServer = new GameServer 
{ 
    ServerId = "minecraft-1", 
    Name = "My Minecraft Server", 
    GameType = "minecraft", 
    Settings = new Dictionary<string, string> 
    { 
        ["EULA"] = "TRUE", 
        ["VERSION"] = "1.20.1", 
        ["MEMORY"] = "4G" 
    } 
}; 
await gameServerApi.DeployAsync(newServer);

// Start, stop operations
await gameServerApi.StartServerAsync("server-id");
await gameServerApi.StopServerAsync("server-id");
// Note: RestartServer endpoint is not currently available
```

### File Management Operations

```csharp
// List files in a directory
var files = await gameServerApi.GetFilesAsync("server-id", "/data", "/"); 
foreach (var file in files)
{
    Console.WriteLine($"{file.Name} - {file.Size} bytes - {file.LastModified}");
}

// Download a file
var fileResponse = await gameServerApi.DownloadFileAsync("server-id", "/data", "/world/level.dat"); 
using var fileStream = File.Create("level.dat");
await fileResponse.Stream.CopyToAsync(fileStream);

// Upload a file
using var uploadStream = File.OpenRead("server.properties");
var fileParameter = new FileParameter(uploadStream, "server.properties");
await gameServerApi.UploadFileAsync("server-id", "/data", "/config/server.properties", fileParameter); 

// Delete a file or directory
await gameServerApi.DeleteFileAsync("server-id", "/data", "/temp/old-file.txt", recursive: false); 

// Create a directory
await gameServerApi.CreateDirectoryAsync("server-id", "/data", "/backups");
```

### Resource Usage Monitoring

```csharp
// Get current resource usage (service-level metrics)
var resourceUsage = await gameServerApi.GetResourceUsageAsync("server-id"); 
Console.WriteLine($"Service Status: {resourceUsage.ServiceStatus}");
Console.WriteLine($"Replicas: {resourceUsage.RunningReplicas}/{resourceUsage.DesiredReplicas}");
Console.WriteLine($"Replica Health: {resourceUsage.ReplicaHealthPercent:F2}%");
Console.WriteLine($"Is Healthy: {resourceUsage.IsHealthy}");
Console.WriteLine($"Failed Tasks: {resourceUsage.FailedTasks}");
if (resourceUsage.ServiceMemoryLimitPerReplica.HasValue)
{
    Console.WriteLine($"Memory Limit: {resourceUsage.ServiceMemoryLimitPerReplica.Value / 1024 / 1024} MB per replica");
}
if (resourceUsage.ServiceCpuLimitPerReplica.HasValue)
{
    Console.WriteLine($"CPU Limit: {resourceUsage.ServiceCpuLimitPerReplica.Value / 1_000_000_000.0} CPUs per replica");
}
```

### Service Logs

```csharp
// Get service logs with optional tail parameter
var logs = await gameServerApi.GetServiceLogsAsync("server-id", tail: 100); 
foreach (var logLine in logs) 
{ 
    Console.WriteLine(logLine); 
}
```

### Dashboard API - Overview Statistics

```csharp
var dashboardApi = new DashboardApi(httpClient);

// Get dashboard overview with all servers
var servers = await dashboardApi.GetServersAsync(); 
Console.WriteLine($"Total Servers: {servers.Count}");

foreach (var server in servers) 
{ 
    Console.WriteLine($"Server: {server.Name} ({server.ServerId})");
    Console.WriteLine($"  Status: {server.Status}");
    Console.WriteLine($"  Game Type: {server.GameType}");
    Console.WriteLine($"  Running: {server.IsRunning}");
    Console.WriteLine($"  Ports: {server.Ports}");
}
```

### Game Type API - Manage Server Templates

```csharp
var gameTypeApi = new GameTypeApi(httpClient);

// List available game types 
var gameTypes = await gameTypeApi.GetAllAsync(); 
foreach (var gameType in gameTypes) 
{ 
    Console.WriteLine($"Game Type: {gameType.Key}"); 
    Console.WriteLine($"  Name: {gameType.DisplayName}"); 
    Console.WriteLine($"  Image: {gameType.Image}"); 
    Console.WriteLine($"  Description: {gameType.Description}");
}

// Get specific game type 
var minecraft = await gameTypeApi.GetAsync("minecraft"); 
Console.WriteLine($"Minecraft default settings:"); 
foreach (var setting in minecraft.DefaultSettings) 
{ 
    Console.WriteLine($"  {setting.Key} = {setting.Value}"); 
}

// Create custom game type 
var customGameType = new GameTypeDefinition 
{ 
    Key = "my-custom-game", 
    DisplayName = "My Custom Game Server", 
    Description = "Custom game server configuration", 
    Image = "my-org/custom-game-server:latest", 
    Ports = new List<PortDefinition> 
    { 
        new PortDefinition { Port = 25565, Protocol = "tcp" }, 
        new PortDefinition { Port = 25565, Protocol = "udp" }
    }, 
    DefaultSettings = new Dictionary<string, string> 
    { 
        ["MAX_PLAYERS"] = "20", 
        ["SERVER_NAME"] = "My Server" 
    } 
}; 
await gameTypeApi.CreateAsync(customGameType);

// Update a game type
await gameTypeApi.UpdateAsync("my-custom-game", customGameType);

// Delete a game type
await gameTypeApi.DeleteAsync("my-custom-game");
```

### Game Type Extended Metadata API - Advanced Configuration

The Extended Metadata API provides advanced configuration options for game types, including TTY settings, rich setting metadata, validation rules, dropdowns, and dynamic port mapping.

```csharp
var extendedMetadataApi = new GameTypeExtendedMetadataApi(httpClient);

// List all extended metadata
var allMetadata = await extendedMetadataApi.GetAllAsync();
foreach (var metadata in allMetadata)
{
    Console.WriteLine($"Game Type: {metadata.GameTypeKey}");
    Console.WriteLine($"  TTY Enabled: {metadata.EnableTTY}");
    Console.WriteLine($"  Settings Count: {metadata.SettingsMetadata.Count}");
}

// Get extended metadata for specific game type
var minecraftMetadata = await extendedMetadataApi.GetAsync("minecraft");
Console.WriteLine($"Minecraft Settings:");
foreach (var setting in minecraftMetadata.SettingsMetadata)
{
    Console.WriteLine($"  {setting.Key}:");
    Console.WriteLine($"    Description: {setting.Value.Description}");
    Console.WriteLine($"    Required: {setting.Value.IsRequired}");
    Console.WriteLine($"    Data Type: {setting.Value.DataType}");
    
    if (setting.Value.AllowedValues?.Any() == true)
    {
        Console.WriteLine($"    Allowed Values: {string.Join(", ", setting.Value.AllowedValues)}");
    }
}

// Create extended metadata with validation and enums
var customMetadata = new GameTypeExtendedMetadata
{
    GameTypeKey = "my-custom-game",
    EnableTTY = true,
    SettingsMetadata = new Dictionary<string, SettingMetadata>
    {
        ["SERVER_PORT"] = new SettingMetadata
        {
            Key = "SERVER_PORT",
            Description = "Server port number (changes which port the server listens on)",
            DataType = "port",
            MapsToContainerPort = true,
            LinkedContainerPort = 7777,
            PortProtocol = "tcp",
            Category = "Network",
            DisplayOrder = 1,
            Placeholder = "7777"
        },
        ["DIFFICULTY"] = new SettingMetadata
        {
            Key = "DIFFICULTY",
            Description = "Game difficulty level",
            DataType = "enum",
            IsRequired = true,
            AllowedValues = new List<string> { "easy", "normal", "hard" },
            ValueMappings = new Dictionary<string, string>
            {
                ["easy"] = "Easy - Reduced enemy damage",
                ["normal"] = "Normal - Standard difficulty",
                ["hard"] = "Hard - Increased challenge"
            },
            Category = "Gameplay",
            DisplayOrder = 2
        },
        ["MAX_PLAYERS"] = new SettingMetadata
        {
            Key = "MAX_PLAYERS",
            Description = "Maximum number of players",
            DataType = "number",
            IsRequired = true,
            CannotBeEmpty = true,
            ValidationPattern = "^([1-9]|[1-9][0-9]|100)$",
            ValidationMessage = "Must be between 1 and 100",
            Category = "Server",
            DisplayOrder = 3
        }
    }
};
await extendedMetadataApi.SaveAsync(customMetadata);

// Update individual setting metadata
var portSetting = new SettingMetadata
{
    Key = "SERVER_PORT",
    Description = "Updated description",
    DataType = "port",
    MapsToContainerPort = true,
    LinkedContainerPort = 7777,
    PortProtocol = "tcp"
};
await extendedMetadataApi.UpdateSettingMetadataAsync("my-custom-game", "SERVER_PORT", portSetting);

// Get specific setting metadata
var difficultySetting = await extendedMetadataApi.GetSettingMetadataAsync("minecraft", "DIFFICULTY");
Console.WriteLine($"Difficulty Setting:");
Console.WriteLine($"  Type: {difficultySetting.DataType}");
Console.WriteLine($"  Options: {string.Join(", ", difficultySetting.AllowedValues)}");
foreach (var mapping in difficultySetting.ValueMappings)
{
    Console.WriteLine($"  {mapping.Key}: {mapping.Value}");
}

// Delete setting metadata
await extendedMetadataApi.DeleteSettingMetadataAsync("my-custom-game", "OLD_SETTING");

// Delete entire extended metadata
await extendedMetadataApi.DeleteAsync("my-custom-game");
```

#### Extended Metadata Features

**TTY Configuration:**
- Enable pseudo-terminal for interactive console access
- Useful for servers that require interactive input

**Setting Metadata:**
- `IsRequired` - Mark settings as mandatory
- `CannotBeEmpty` - Prevent empty values
- `DataType` - Specify type: "string", "number", "boolean", "enum", "list", "port"
- `ValidationPattern` - Regex validation with custom error messages
- `Category` - Group settings for UI organization
- `DisplayOrder` - Control display order in UI

**Enum Support (Dropdowns):**
- `AllowedValues` - List of valid options
- `ValueMappings` - User-friendly descriptions for each value
- Perfect for difficulty levels, game modes, server types, etc.

**Dynamic Port Mapping:**
- `MapsToContainerPort` - Setting controls a port
- `LinkedContainerPort` - Which port to update
- Example: SERVER_PORT setting changes from 25565 to 25566, updates the port mapping

**Value Mappings:**
- Map technical values to descriptions
- Example: `"0": "Disabled - Feature turned off"`, `"1": "Enabled - Feature active"`
- Great for numeric codes or abbreviated values

#### Building Dynamic Forms

Use extended metadata to build dynamic, validated forms:

```csharp
var metadata = await extendedMetadataApi.GetAsync("minecraft");

// Organize by category
var categorized = metadata.SettingsMetadata.Values
    .GroupBy(s => s.Category ?? "General")
    .OrderBy(g => g.Key);

foreach (var category in categorized)
{
    Console.WriteLine($"\n{category.Key}:");
    
    foreach (var setting in category.OrderBy(s => s.DisplayOrder))
    {
        Console.Write($"  {setting.Key}");
        if (setting.IsRequired) Console.Write(" *");
        Console.WriteLine($": {setting.Description}");
        
        if (setting.DataType == "enum" && setting.AllowedValues?.Any() == true)
        {
            Console.WriteLine($"    Options:");
            foreach (var value in setting.AllowedValues)
            {
                var description = setting.ValueMappings?.GetValueOrDefault(value) ?? value;
                Console.WriteLine($"      - {value}: {description}");
            }
        }
        
        if (!string.IsNullOrEmpty(setting.Placeholder))
        {
            Console.WriteLine($"    Default: {setting.Placeholder}");
        }
    }
}
```

### Container Console Client - Real-Time Interactive Access

The `IContainerConsoleClient` provides real-time, bidirectional communication with container consoles via SignalR, including **interactive command execution with full stdin support**.

#### Features
- ? **Container Attach** - Attach to running container's main process (via Primary Service)
- ? **Interactive Exec** - Execute commands with full stdin/stdout/stderr (direct to Agent)
- ? **TTY Support** - Full terminal emulation for interactive applications
- ? **Event-Driven** - Real-time output via events
- ? **Unified API** - Same client for both attach and exec operations

#### Basic Container Attach (via SignalR Hub)

```csharp
using GameServer.Docker.Client.Interfaces;
using GameServer.Docker.Client.Services;

// Create client
var consoleClient = new ContainerConsoleClient("https://your-manager/hubs/console");

// Register event handlers
consoleClient.OutputReceived += (sender, output) =>
{
    Console.Write(output); // Display container output
};

consoleClient.ErrorReceived += (sender, error) =>
{
    Console.WriteLine($"Error: {error}");
};

consoleClient.Connected += (sender, containerId) =>
{
    Console.WriteLine($"Connected to {containerId}");
};

consoleClient.Disconnected += (sender, reason) =>
{
    Console.WriteLine($"Disconnected: {reason}");
};

// Connect to hub
await consoleClient.ConnectAsync();

// Attach to container's main process
bool success = await consoleClient.AttachToContainerAsync("my-container-id");
if (success)
{
    // Send commands to container stdin
    await consoleClient.SendInputAsync("ls -la\n");
    await consoleClient.SendInputAsync("pwd\n");
    
    // Disconnect
    await consoleClient.DisconnectFromContainerAsync();
}


// Cleanup
await consoleClient.DisposeAsync();
```

#### Interactive Command Execution (via WebSocket to Agent) ? NEW

The new `ExecInteractiveAsync` method provides **direct WebSocket connection to Node Agents** for interactive command execution with full stdin/stdout/stderr support.

**Use Cases:**
- Interactive shells (bash, sh, PowerShell)
- Terminal applications (vim, nano, htop, top)
- Real-time debugging and troubleshooting
- Admin consoles with TTY support

**Example - Interactive Bash Shell:**

```csharp
var client = new ContainerConsoleClient("https://your-manager/hubs/console");

// Setup event handlers
client.OutputReceived += (s, output) => Console.Write(output);
client.Connected += (s, id) => Console.WriteLine($"Shell started in {id}");
client.Disconnected += (s, reason) => Console.WriteLine($"\nShell ended: {reason}");

// Start interactive bash session (direct to Agent via WebSocket)
var execTask = client.ExecInteractiveAsync(
    agentUrl: "http://node-agent-1:8080",  // Direct to Agent
    containerId: "abc123",
    command: "bash",
    args: new[] { "-i" },  // Interactive mode
    tty: true              // Enable TTY for proper terminal
);

// Send commands interactively
await client.SendInputAsync("ls -la\n");
await Task.Delay(500);

await client.SendInputAsync("cd /app\n");
await Task.Delay(500);

await client.SendInputAsync("cat server.log\n");
await Task.Delay(500);

await client.SendInputAsync("exit\n");  // Exit shell

await execTask;  // Wait for session to complete
```

**Example - Running vim:**

```csharp
client.OutputReceived += (s, output) => Console.Write(output);

await client.ExecInteractiveAsync(
    agentUrl: "http://agent:8080",
    containerId: "abc123",
    command: "vim",
    args: new[] { "/app/config.json" },
    tty: true  // REQUIRED for vim
);

// Send vim commands
await client.SendInputAsync("i");           // Insert mode
await client.SendInputAsync("Hello World"); // Type text
await client.SendInputAsync("\x1B");        // ESC key
await client.SendInputAsync(":wq\n");       // Save and quit
```

**Example - Non-Interactive Command (via SignalR Hub):**

For simple commands where you don't need interactivity, use the original method:

```csharp
// Connect to hub first
await client.ConnectAsync();

// Execute command and get output (non-interactive, via Primary Service)
var output = await client.ExecCommandAsync(
    containerId: "abc123",
    command: "cat",
    args: new[] { "/etc/hostname" }
);

Console.WriteLine($"Hostname: {output}");
```

**Comparison:**

| Feature | `AttachToContainerAsync` | `ExecInteractiveAsync` | `ExecCommandAsync` |
|---------|-------------------------|------------------------|-------------------|
| Connection | SignalR Hub (Primary) | WebSocket (Direct to Agent) | SignalR Hub (Primary) |
| Process | Main container process | New exec process | New exec process |
| Stdin | ? Yes | ? Yes | ? No |
| Interactive | ? Yes | ? Yes | ? No (buffered) |
| TTY | ? Yes | ? Optional | ? No |
| Output | ? Real-time events | ? Real-time events | ? Returned as string |
| Use Case | Long-running console | Interactive shells/tools | Simple commands |

#### Interactive Console Example

```csharp
using GameServer.Docker.Client.Services;

await using var console = new ContainerConsoleClient("https://your-manager/hubs/console");

// Handle output
console.OutputReceived += (s, output) => Console.Write(output);
console.ErrorReceived += (s, error) => Console.WriteLine($"\nError: {error}");

// Connect
await console.ConnectAsync();
await console.AttachToContainerAsync("minecraft-server-1");

// Interactive loop
Console.WriteLine("Connected! Type 'exit' to quit.");
while (true)
{
    var input = Console.ReadLine();
    if (input == "exit") break;
    
    await console.SendInputAsync(input + "\n");
}
```

#### Dependency Injection Setup

```csharp
using GameServer.Docker.Client.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Register console client
builder.Services.AddContainerConsoleClient("https://your-manager/hubs/console");

// Or with custom configuration
builder.Services.AddContainerConsoleClient(
    "https://your-manager/hubs/console",
    hubBuilder =>
    {
        hubBuilder.WithAutomaticReconnect();
        // Add authentication, etc.
    });

var app = builder.Build();

// Use in controllers/services
public class ConsoleService
{
    private readonly IContainerConsoleClient _consoleClient;
    
    public ConsoleService(IContainerConsoleClient consoleClient)
    {
        _consoleClient = consoleClient;
    }
    
    public async Task<string> RunCommandAsync(string containerId, string command)
    {
        await _consoleClient.ConnectAsync();
        var result = await _consoleClient.ExecCommandAsync(containerId, "sh", new[] { "-c", command });
        return result;
    }
}
```

#### Advanced Features

**Auto-Reconnection:**
```csharp
var console = new ContainerConsoleClient("https://your-manager/hubs/console");
// Automatically reconnects using exponential backoff: 0s, 2s, 10s, 30s
```

**Multiple Containers:**
```csharp
// Disconnect from current container and attach to another
await console.DisconnectFromContainerAsync();
await console.AttachToContainerAsync("another-container-id");
```

**Check Connection State:**
```csharp
if (console.IsConnected)
{
    Console.WriteLine($"Connected. Attached to: {console.AttachedContainerId}");
}
```

### Resource Monitoring Client - Real-Time Stats Streaming ? NEW

The `IResourceMonitoringClient` provides **real-time streaming** of server resource usage (CPU, memory, network, disk) with true push-based updates.

#### Features
- ? **Real-Time Streaming** - Live resource usage updates (no polling!)
- ? **Single Server Monitoring** - Monitor one server with configurable interval
- ? **Multi-Server Monitoring** - Monitor multiple servers simultaneously
- ? **Event-Driven** - Receive updates via events as they arrive
- ? **Automatic Reconnection** - Resilient to connection issues
- ? **Low Latency** - Sub-second updates via SignalR streaming

#### Basic Usage - Single Server Monitoring

```csharp
using GameServer.Docker.Client.Services;

var client = new ResourceMonitoringClient("https://your-manager/hubs/resources");

// Setup event handlers
client.ResourceUpdateReceived += (sender, usage) =>
{
    Console.WriteLine($"Server: {usage.ServerId}");
    Console.WriteLine($"  CPU: {usage.RealTimeStats?.CpuUsagePercent:F2}%");
    Console.WriteLine($"  Memory: {usage.RealTimeStats?.MemoryUsagePercent:F2}%");
    Console.WriteLine($"  Network RX: {usage.RealTimeStats?.NetworkRxBytes / 1024 / 1024:F2} MB");
    Console.WriteLine($"  Network TX: {usage.RealTimeStats?.NetworkTxBytes / 1024 / 1024:F2} MB");
    Console.WriteLine($"  Replicas: {usage.RunningReplicas}/{usage.DesiredReplicas}");
};

client.ErrorReceived += (sender, error) =>
{
    Console.WriteLine($"Error: {error}");
};

// Connect to hub
await client.ConnectAsync();

// Subscribe to server updates (every 5 seconds)
await client.SubscribeToServerAsync("my-server-id", intervalSeconds: 5);

// Let it run for a while
await Task.Delay(TimeSpan.FromMinutes(5));

// Unsubscribe
await client.UnsubscribeAsync();
await client.DisposeAsync();
```

#### Multi-Server Monitoring

```csharp
var client = new ResourceMonitoringClient("https://your-manager/hubs/resources");

// Handle batch updates
client.ResourceUpdateBatchReceived += (sender, updates) =>
{
    Console.WriteLine($"\n--- Batch Update ({updates.Count()} servers) ---");
    foreach (var usage in updates)
    {
        Console.WriteLine($"{usage.ServerId}: CPU {usage.RealTimeStats?.CpuUsagePercent:F2}%, " +
                         $"Mem {usage.RealTimeStats?.MemoryUsagePercent:F2}%");
    }
};

await client.ConnectAsync();

// Monitor multiple servers (update every 10 seconds)
await client.SubscribeToMultipleServersAsync(
    serverIds: new[] { "server-1", "server-2", "server-3" },
    intervalSeconds: 10
);

// Updates arrive as batches via ResourceUpdateBatchReceived event
```

#### Get Single Snapshot (Non-Streaming)

```csharp
await client.ConnectAsync();

// Get one-time snapshot without subscribing to stream
var snapshot = await client.GetSnapshotAsync("my-server-id");

Console.WriteLine($"Server: {snapshot.ServerId}");
Console.WriteLine($"Status: {snapshot.ServiceStatus}");
Console.WriteLine($"Health: {snapshot.IsHealthy}");
Console.WriteLine($"Running Replicas: {snapshot.RunningReplicas}/{snapshot.DesiredReplicas}");

if (snapshot.RealTimeStats != null)
{
    Console.WriteLine($"CPU: {snapshot.RealTimeStats.CpuUsagePercent:F2}%");
    Console.WriteLine($"Memory: {snapshot.RealTimeStats.MemoryUsagePercent:F2}%");
}
```

#### Update Interval Dynamically

```csharp
// Initially subscribe with 10 second interval
await client.SubscribeToServerAsync("server-id", intervalSeconds: 10);

// Later, speed up to 2 second interval
await client.UpdateIntervalAsync(2);

// Handle interval update confirmation
client.IntervalUpdated += (sender, intervalSeconds) =>
{
    Console.WriteLine($"Interval updated to {intervalSeconds} seconds");
};
```

#### Subscription Events

```csharp
client.Subscribed += (sender, data) =>
{
    Console.WriteLine($"Subscribed to {data.ServerId} with {data.IntervalSeconds}s interval");
};

client.SubscribedMultiple += (sender, data) =>
{
    Console.WriteLine($"Subscribed to {data.ServerIds.Length} servers with {data.IntervalSeconds}s interval");
};

client.Unsubscribed += (sender, args) =>
{
    Console.WriteLine("Unsubscribed from monitoring");
};

client.IntervalUpdated += (sender, intervalSeconds) =>
{
    Console.WriteLine($"Interval changed to {intervalSeconds}s");
};
```

#### Dependency Injection Setup

```csharp
using GameServer.Docker.Client.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Register resource monitoring client
builder.Services.AddResourceMonitoringClient("https://your-manager/hubs/resources");

// Or with custom configuration
builder.Services.AddResourceMonitoringClient(
    "https://your-manager/hubs/resources",
    hubBuilder =>
    {
        hubBuilder.WithAutomaticReconnect();
        // Add authentication, etc.
    });

var app = builder.Build();

// Use in controllers/services
public class MonitoringService
{
    private readonly IResourceMonitoringClient _monitoringClient;
    
    public MonitoringService(IResourceMonitoringClient monitoringClient)
    {
        _monitoringClient = monitoringClient;
        
        // Setup event handlers
        _monitoringClient.ResourceUpdateReceived += OnResourceUpdate;
    }
    
    private void OnResourceUpdate(object? sender, ServerResourceUsage usage)
    {
        // Process real-time resource updates
        if (usage.RealTimeStats?.CpuUsagePercent > 80)
        {
            Console.WriteLine($"WARNING: {usage.ServerId} CPU usage is {usage.RealTimeStats.CpuUsagePercent:F2}%");
        }
    }
    
    public async Task StartMonitoringAsync(string serverId)
    {
        await _monitoringClient.ConnectAsync();
        await _monitoringClient.SubscribeToServerAsync(serverId, intervalSeconds: 5);
    }
}
```

#### Resource Usage Data Model

The `ServerResourceUsage` model includes:

**Service-Level Data (from Docker Swarm):**
- `ServerId`, `ServiceId` - Identifiers
- `ServiceStatus` - Current service state
- `DesiredReplicas`, `RunningReplicas` - Replica counts
- `IsHealthy`, `ReplicaHealthPercent` - Health metrics
- `ServiceCpuLimitPerReplica`, `ServiceMemoryLimitPerReplica` - Resource limits
- `ServiceCpuReservationPerReplica`, `ServiceMemoryReservationPerReplica` - Reservations
- `TaskIds`, `ContainerIds` - Running task/container identifiers

**Real-Time Stats (from Node Agent streaming):**
- `RealTimeStats.CpuUsagePercent` - Current CPU usage %
- `RealTimeStats.MemoryUsagePercent` - Current memory usage %
- `RealTimeStats.MemoryUsageBytes`, `MemoryLimitBytes` - Memory in bytes
- `RealTimeStats.NetworkRxBytes`, `NetworkTxBytes` - Network I/O
- `RealTimeStats.BlockReadBytes`, `BlockWriteBytes` - Disk I/O
- `RealTimeStats.Pids` - Number of processes

**Check Connection State:**

### Resource Monitoring Client - Real-Time Performance Metrics

The `IResourceMonitoringClient` streams real-time resource usage data (CPU, memory, network, disk) from game servers.

#### Basic Usage

```csharp
using GameServer.Docker.Client.Interfaces;
using GameServer.Docker.Client.Services;

// Create client
var monitoringClient = new ResourceMonitoringClient("https://your-manager/hubs/resources");

// Register event handler
monitoringClient.ResourceUpdateReceived += (sender, usage) =>
{
    Console.WriteLine($"Server: {usage.ServerName}");
    Console.WriteLine($"  CPU: {usage.CpuUsagePercent:F1}%");
    Console.WriteLine($"  Memory: {usage.MemoryUsagePercent:F1}% ({usage.MemoryUsageBytes / 1024 / 1024} MB)");
    Console.WriteLine($"  Network RX: {usage.NetworkRxBytes / 1024 / 1024} MB");
    Console.WriteLine($"  Network TX: {usage.NetworkTxBytes / 1024 / 1024} MB");
    Console.WriteLine($"  Disk Read: {usage.BlockReadBytes / 1024 / 1024} MB");
    Console.WriteLine($"  Disk Write: {usage.BlockWriteBytes / 1024 / 1024} MB");
    Console.WriteLine($"  Replicas: {usage.HealthyReplicas}/{usage.Replicas}");
};

// Connect to hub
await monitoringClient.ConnectAsync();

// Subscribe to a single server (updates every 5 seconds)
await monitoringClient.SubscribeToServerAsync("my-server-id", intervalSeconds: 5);

// Updates will stream in real-time...

// Cleanup
await monitoringClient.DisposeAsync();
```

#### Monitoring Multiple Servers

```csharp
await using var monitoring = new ResourceMonitoringClient("https://your-manager/hubs/resources");

// Handle batch updates
monitoring.ResourceUpdateBatchReceived += (sender, updates) =>
{
    foreach (var usage in updates)
    {
        Console.WriteLine($"{usage.ServerName}: CPU {usage.CpuUsagePercent:F1}%, Memory {usage.MemoryUsagePercent:F1}%");
    }
};

await monitoring.ConnectAsync();

// Monitor multiple servers at once
string[] serverIds = { "server-1", "server-2", "server-3" };
await monitoring.SubscribeToMultipleServersAsync(serverIds, intervalSeconds: 10);
```

#### Get Single Snapshot (No Subscription)

```csharp
await using var monitoring = new ResourceMonitoringClient("https://your-manager/hubs/resources");
await monitoring.ConnectAsync();

// Get one-time snapshot
var snapshot = await monitoring.GetSnapshotAsync("my-server-id");
if (snapshot != null)
{
    Console.WriteLine($"Current CPU: {snapshot.CpuUsagePercent:F1}%");
    Console.WriteLine($"Current Memory: {snapshot.MemoryUsagePercent:F1}%");
}
```

#### Dependency Injection Setup

```csharp
using GameServer.Docker.Client.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Register resource monitoring client
builder.Services.AddResourceMonitoringClient("https://your-manager/hubs/resources");

var app = builder.Build();

// Use in controllers/services
public class MonitoringService
{
    private readonly IResourceMonitoringClient _monitoringClient;
    
    public MonitoringService(IResourceMonitoringClient monitoringClient)
    {
        _monitoringClient = monitoringClient;
    }
    
    public async Task StartMonitoring(string serverId)
    {
        _monitoringClient.ResourceUpdateReceived += OnResourceUpdate;
        await _monitoringClient.ConnectAsync();
        await _monitoringClient.SubscribeToServerAsync(serverId, intervalSeconds: 5);
    }
    
    private void OnResourceUpdate(object? sender, ServerResourceUsage usage)
    {
        // Handle resource update (store in database, send alert, etc.)
        if (usage.CpuUsagePercent > 90)
        {
            // Send alert: High CPU usage!
        }
    }
}
```

#### Advanced Features

**Dynamic Interval Updates:**
```csharp
// Start with 10-second intervals
await monitoring.SubscribeToServerAsync("server-id", 10);

// Later, switch to 2-second intervals for more detail
await monitoring.UpdateIntervalAsync(2);
```

**Check Monitoring State:**
```csharp
if (monitoring.IsConnected)
{
    Console.WriteLine($"Monitoring: {monitoring.MonitoredServerId ?? "Multiple servers"}");
    Console.WriteLine($"Interval: {monitoring.CurrentIntervalSeconds}s");
}
```

**Building a Dashboard:**
```csharp
public class ResourceDashboard
{
    private readonly IResourceMonitoringClient _monitoring;
    private readonly Dictionary<string, ServerResourceUsage> _latestData = new();
    
    public ResourceDashboard(IResourceMonitoringClient monitoring)
    {
        _monitoring = monitoring;
        _monitoring.ResourceUpdateBatchReceived += OnBatchUpdate;
    }
    
    private void OnBatchUpdate(object? sender, IEnumerable<ServerResourceUsage> updates)
    {
        foreach (var update in updates)
        {
            _latestData[update.ServerId] = update;
        }
        
        // Refresh UI with latest data
        RefreshDashboard();
    }
    
    public async Task MonitorAllServers()
    {
        // Get all server IDs from API
        var servers = await GetAllServerIds();
        
        await _monitoring.ConnectAsync();
        await _monitoring.SubscribeToMultipleServersAsync(servers, intervalSeconds: 5);
    }
}
```

### Port API - Network Port Management

```csharp
var portApi = new PortApi(httpClient);

// Allocate a port (auto-assign from available range)
var allocatedPort = await portApi.AllocateAsync("tcp", preferredPort: null); 
Console.WriteLine($"Allocated port: {allocatedPort}");

// Allocate with preferred port (will use it if available)
var preferredAllocatedPort = await portApi.AllocateAsync("tcp", preferredPort: 25565); 
Console.WriteLine($"Allocated port: {preferredAllocatedPort}");

// Release a port 
await portApi.ReleaseAsync(25565, "tcp");
```

## API Client Interfaces

The library provides the following API interfaces (all auto-generated from OpenAPI spec):

### IGameServerApi
- `DeployAsync(GameServer server)` - Deploy a new game server
- `GetAsync(string id)` - Get server details by ID
- `ListAsync()` - List all servers
- `GetFilesAsync(string id, string volumeTarget, string path)` - List files
- `DeleteFileAsync(string id, string volumeTarget, string filePath, bool? recursive)` - Delete file/directory
- `DownloadFileAsync(string id, string volumeTarget, string filePath)` - Download file
- `UploadFileAsync(string id, string volumeTarget, string filePath, FileParameter file)` - Upload file
- `CreateDirectoryAsync(string id, string volumeTarget, string directoryPath)` - Create directory
- `GetConsoleInfoAsync(string id)` - Get console connection info
- `GetLogsInfoAsync(string id)` - Get logs connection info
- `GetResourceUsageAsync(string id)` - Get current resource usage
- `GetResourceMonitoringInfoAsync(string id)` - Get resource monitoring connection info
- `GetServiceLogsAsync(string id, int? tail)` - Get service logs
- `StartServerAsync(string id)` - Start server
- `StopServerAsync(string id)` - Stop server

### IDashboardApi
- `GetServersAsync()` - Get all servers with status information

### IGameTypeApi
- `GetAllAsync()` - List all game type definitions
- `GetAsync(string key)` - Get a specific game type
- `CreateAsync(GameTypeDefinition definition)` - Create new game type
- `UpdateAsync(string key, GameTypeDefinition definition)` - Update game type
- `DeleteAsync(string key)` - Delete game type

### IGameTypeExtendedMetadataApi
- `GetAllAsync()` - List all extended metadata entries
- `GetAsync(string gameTypeKey)` - Get extended metadata for a specific game type
- `SaveAsync(GameTypeExtendedMetadata metadata)` - Create or update extended metadata
- `DeleteAsync(string gameTypeKey)` - Delete extended metadata
- `GetSettingMetadataAsync(string gameTypeKey, string settingKey)` - Get metadata for a specific setting
- `UpdateSettingMetadataAsync(string gameTypeKey, string settingKey, SettingMetadata metadata)` - Update individual setting metadata
- `DeleteSettingMetadataAsync(string gameTypeKey, string settingKey)` - Delete setting metadata

### IPortApi
- `AllocateAsync(string protocol, int? preferredPort)` - Allocate a port
- `ReleaseAsync(int port, string protocol)` - Release an allocated port

### IContainerConsoleClient (SignalR)
- `ConnectAsync(CancellationToken)` - Connect to SignalR hub
- `AttachToContainerAsync(string containerId, CancellationToken)` - Attach to container console
- `SendInputAsync(string input, CancellationToken)` - Send input to container stdin
- `ExecCommandAsync(string containerId, string command, string[] args, CancellationToken)` - Execute command
- `DisconnectFromContainerAsync(CancellationToken)` - Disconnect from container
- `StopAsync(CancellationToken)` - Stop SignalR connection
- **Events**: `OutputReceived`, `ErrorReceived`, `Connected`, `Disconnected`, `CommandOutputReceived`

### IResourceMonitoringClient (SignalR)
- `ConnectAsync(CancellationToken)` - Connect to SignalR hub
- `SubscribeToServerAsync(string serverId, int intervalSeconds, CancellationToken)` - Subscribe to single server updates
- `SubscribeToMultipleServersAsync(string[] serverIds, int intervalSeconds, CancellationToken)` - Subscribe to multiple servers
- `GetSnapshotAsync(string serverId, CancellationToken)` - Get one-time resource snapshot
- `UpdateIntervalAsync(int intervalSeconds, CancellationToken)` - Update monitoring interval
- `UnsubscribeAsync(CancellationToken)` - Unsubscribe from monitoring
- `StopAsync(CancellationToken)` - Stop SignalR connection
- **Events**: `ResourceUpdateReceived`, `ResourceUpdateBatchReceived`, `Subscribed`, `SubscribedMultiple`, `Unsubscribed`, `IntervalUpdated`, `ErrorReceived`

## Data Models

### GameServer
Represents a game server instance:
- `ServerId` - Unique server identifier
- `Name` - Display name
- `Description` - Server description
- `GameType` - Game type key (e.g., "minecraft", "valheim")
- `Status` - Current status
- `IsRunning` - Whether server is running
- `Settings` - Dictionary of environment variables/settings
- `ServiceName` - Docker Swarm service name (populated after deployment)
- `Volumes` - Volume definitions
- `Ports` - Port mappings (list of PortMapping objects)

### GameServerDashboardItem
Dashboard view of a server:
- `ServerId` - Unique server identifier
- `Name` - Display name
- `Description` - Server description
- `GameType` - Game type key
- `Ports` - Formatted string of ports (e.g., "25565/tcp, 25566/udp")
- `IsRunning` - Whether server is running
- `Status` - Current status
- `ServiceName` - Docker Swarm service name

### GameTypeDefinition
Template for a game server type:
- `Key` - Unique identifier
- `DisplayName` - Human-readable name
- `Description` - Description
- `Image` - Docker image
- `ThumbnailUrl` - Optional thumbnail image URL
- `DocumentationUrl` - Optional documentation URL
- `Ports` - Default port definitions
- `Volumes` - Volume definitions
- `DefaultSettings` - Default environment variables

### GameTypeExtendedMetadata
Advanced configuration for a game type:
- `GameTypeKey` - Game type key (must match GameTypeDefinition.Key)
- `EnableTTY` - Enable pseudo-terminal for interactive console access
- `SettingsMetadata` - Dictionary of setting metadata (key: setting name)
- `CustomProperties` - Additional custom properties for extensibility

### SettingMetadata
Metadata for individual game server settings:
- `Key` - Setting/environment variable name
- `Description` - User-friendly description
- `IsRequired` - Whether the setting is mandatory
- `CannotBeEmpty` - Whether the setting can be blank
- `DataType` - Data type: "string", "number", "boolean", "enum", "list", "port"
- `MapsToContainerPort` - Whether this setting controls a port mapping
- `LinkedContainerPort` - The original container port this setting controls
- `PortProtocol` - Protocol for port mapping (default: "tcp")
- `ListDelimiter` - Delimiter for list types (default: ",")
- `AllowedValues` - List of valid values for enum types
- `ValueMappings` - Dictionary mapping values to descriptions
- `DisplayOrder` - UI display order hint
- `Category` - Category/group name for UI organization
- `Placeholder` - Placeholder text for UI
- `ValidationPattern` - Regex pattern for validation
- `ValidationMessage` - Error message when validation fails

### ServerResourceUsage
Docker Swarm service-level resource information:
- `ServerId` / `ServiceId` - Service identifiers
- `Timestamp` - When metrics were captured
- `ServiceCreatedAt` / `ServiceUpdatedAt` / `ServiceVersion` - Service lifecycle info
- `DesiredReplicas` / `RunningReplicas` / `FailedTasks` / `PendingTasks` / `TaskCount` - Replica/task information
- `TaskIds` / `ContainerIds` - Task and container references
- `ServiceCpuLimitPerReplica` / `ServiceCpuLimitTotal` / `ServiceCpuReservationPerReplica` / `ServiceCpuReservationTotal` - CPU specifications (NanoCPUs)
- `ServiceMemoryLimitPerReplica` / `ServiceMemoryLimitTotal` / `ServiceMemoryReservationPerReplica` / `ServiceMemoryReservationTotal` - Memory specifications (Bytes)
- `UpdateState` / `UpdateStartedAt` / `UpdateCompletedAt` - Service update status
- `ReplicaHealthPercent` - Computed health percentage
- `IsHealthy` - Whether all replicas are running
- `ServiceStatus` - Service status (Stopped, Running, Starting, etc.)

Note: This provides service-level specifications, not real-time container stats

### PortDefinition
Port configuration template:
- `Port` - Port number
- `Protocol` - Protocol (tcp/udp)
- `IsDefaultPort` - Whether this is the primary connection port (used by UI to identify the main port users should connect to)

### PortMapping
Actual port mapping on a deployed server:
- `PublishedPort` - External/host port
- `ContainerPort` - Internal container port
- `Protocol` - Protocol (tcp/udp)

### VolumeDefinition
Volume mount configuration:
- `Source` - Volume source path
- `Target` - Container mount point

### FileItem
File/directory information:
- `Name` - File/directory name
- `Path` - Full path
- `IsDirectory` - Whether it's a directory
- `Size` - File size in bytes
- `Permissions` - File permissions string
- `LastModified` - Last modification timestamp

## Dependency Injection

### ASP.NET Core Setup

```csharp
using GameServer.Docker.Client;

var builder = WebApplication.CreateBuilder(args);

// Configure API base URL 
var apiBaseUrl = builder.Configuration["GameServer:ApiUrl"] ?? "https://localhost:5001";

// Register REST API clients 
builder.Services.AddHttpClient<IGameServerApi, GameServerApi>(client => 
{ 
    client.BaseAddress = new Uri(apiBaseUrl); 
});

builder.Services.AddHttpClient<IDashboardApi, DashboardApi>(client => 
{ 
    client.BaseAddress = new Uri(apiBaseUrl); 
});

builder.Services.AddHttpClient<IGameTypeApi, GameTypeApi>(client => 
{ 
    client.BaseAddress = new Uri(apiBaseUrl); 
});

builder.Services.AddHttpClient<IGameTypeExtendedMetadataApi, GameTypeExtendedMetadataApi>(client => 
{ 
    client.BaseAddress = new Uri(apiBaseUrl); 
});

builder.Services.AddHttpClient<IPortApi, PortApi>(client => 
{ 
    client.BaseAddress = new Uri(apiBaseUrl); 
});

var app = builder.Build();
```

### Configuration (appsettings.json)

```json
{ 
    "GameServer": { 
        "ApiUrl": "https://your-gameserver-api.com" 
    } 
}
```

### Use in Controllers or Services

```csharp
using GameServer.Docker.Client;

public class GameServerService
{
    private readonly IGameServerApi _gameServerApi;
    private readonly IDashboardApi _dashboardApi;
    private readonly IGameTypeExtendedMetadataApi _extendedMetadataApi;
    
    public GameServerService(
        IGameServerApi gameServerApi, 
        IDashboardApi dashboardApi,
        IGameTypeExtendedMetadataApi extendedMetadataApi)
    {
        _gameServerApi = gameServerApi;
        _dashboardApi = dashboardApi;
        _extendedMetadataApi = extendedMetadataApi;
    }
    
    public async Task<ICollection<GameServer>> GetAllServersAsync()
    {
        return await _gameServerApi.ListAsync();
    }
    
    public async Task<GameServer> GetServerAsync(string id)
    {
        return await _gameServerApi.GetAsync(id);
    }
    
    public async Task DeployServerAsync(GameServer server)
    {
        // Validate settings against extended metadata before deployment
        var metadata = await _extendedMetadataApi.GetAsync(server.GameType);
        if (metadata != null)
        {
            foreach (var settingMeta in metadata.SettingsMetadata.Values)
            {
                if (settingMeta.IsRequired && !server.Settings.ContainsKey(settingMeta.Key))
                {
                    throw new InvalidOperationException($"Required setting '{settingMeta.Key}' is missing.");
                }
            }
        }
        
        await _gameServerApi.DeployAsync(server);
    }
}
```

## Code Generation

The REST API clients are auto-generated using [NSwag](https://github.com/RicoSuter/NSwag) from the GameServer.Docker API's OpenAPI specification. The generation happens during build via MSBuild target.

To regenerate the clients manually:
```bash
cd src/GameServer.Docker.Client
dotnet build
```

The generated client code is in `GameServer.Docker.Client.v1.g.cs`.

### NSwag Configuration

The client generation is configured in `nswag.json`:
- Source: `../GameServer.Docker/GameServer.Docker.csproj`
- Target Framework: `net10.0`
- Output: `GameServer.Docker.Client.v1.g.cs`
- Namespace: `GameServer.Docker.Client`
- JSON Library: Newtonsoft.Json

## Requirements

- .NET 10.0 or later
- GameServer.Docker API endpoint

## Dependencies

- `Microsoft.AspNetCore.SignalR.Client` (10.0.2) - For future SignalR features
- `Newtonsoft.Json` (13.0.4) - JSON serialization
- `NSwag.MSBuild` (14.6.3) - Code generation

## Project Structure

```
GameServer.Docker.Client/
??? GameServer.Docker.Client.csproj  # Project file
??? nswag.json                        # NSwag configuration
??? GameServer.Docker.Client.v1.g.cs  # Generated API clients
??? ReadMe.md                         # This file
??? Examples/                         # Example code
?   ??? SignalRClientExamples.md     # SignalR examples (future)
??? Interfaces/                       # Client interfaces (future)
??? Services/                         # Client implementations (future)
```

## Future Features

The following features are planned for future releases:

### SignalR Real-Time Features
- ?? **Console Client** - Interactive shell access to game server containers
- ?? **Resource Monitoring Client** - Real-time CPU, memory, network, and disk metrics
- ?? **Logs Client** - Live log streaming from Docker Swarm services

These features will provide:
- Real-time bidirectional communication
- Auto-reconnection logic
- Event-based updates
- Strongly-typed SignalR hub proxies

## Changelog

### v0.0.1-beta (Current)

#### ?? Performance Improvements
- **Node Agent Communication**: Implemented per-node HttpClient instances for optimal connection pooling in Docker Swarm environments
  - Better performance in multi-node clusters
  - Improved throughput for concurrent operations
  - Enhanced reliability through node isolation

#### ?? Extended Metadata Enhancements
- **Multi-File Storage**: Each game type now stored in individual files
  - `minecraft.json`, `valheim.json`, etc. instead of single monolithic file
  - Faster updates (only affected file written)
  - Better concurrency with per-file locking
  - Easier maintenance and inspection
  - No single-file size limits

#### ?? Build & CI/CD
- **Smart API Change Detection**: CI pipeline now detects API contract changes
  - Automatically publishes client only when API changes
  - GitHub Actions warnings for breaking changes
  - Manual override option via workflow dispatch
  - Reduces noise from non-API updates

#### ?? Code Quality
- Removed migration logic (no legacy data to migrate)
- Removed hardcoded built-in game types
- Simplified service initialization
- 34% code reduction in extended metadata service (~111 lines removed)

#### ?? Documentation
- Enhanced README with architectural improvements
- Added performance optimization notes
- Updated examples with best practices
- Added changelog section

### Previous Versions

Development history available on [GitHub](https://github.com/mbulava-org/GameServer.Docker).

## Contributing

Contributions are welcome! Please open an issue or submit a pull request on [GitHub](https://github.com/mbulava-org/GameServer.Docker).

## License

[Add your license here]

## Support

For issues and questions, please visit the [GitHub repository](https://github.com/mbulava-org/GameServer.Docker).
