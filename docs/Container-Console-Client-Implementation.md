# Container Console Client - Client Library Implementation

## Overview

Added complete SignalR client implementation to `GameServer.Docker.Client` for real-time, bidirectional container console access.

---

## Files Created

### 1. Interface: `IContainerConsoleClient`
**Path**: `src\GameServer.Docker.Client\Interfaces\IContainerConsoleClient.cs`

**Provides**:
- ? Event-driven API (OutputReceived, ErrorReceived, etc.)
- ? Connection management (Connect, Disconnect)
- ? Container operations (Attach, SendInput, ExecCommand)
- ? State tracking (IsConnected, AttachedContainerId)
- ? IAsyncDisposable for proper cleanup

**Key Methods**:
```csharp
Task ConnectAsync(CancellationToken);
Task<bool> AttachToContainerAsync(string containerId, CancellationToken);
Task SendInputAsync(string input, CancellationToken);
Task<string> ExecCommandAsync(string containerId, string command, string[]? args, CancellationToken);
Task DisconnectFromContainerAsync(CancellationToken);
Task StopAsync(CancellationToken);
```

**Events**:
```csharp
event EventHandler<string>? OutputReceived;
event EventHandler<string>? ErrorReceived;
event EventHandler<string>? Connected;
event EventHandler<string>? Disconnected;
event EventHandler<string>? CommandOutputReceived;
```

---

### 2. Implementation: `ContainerConsoleClient`
**Path**: `src\GameServer.Docker.Client\Services\ContainerConsoleClient.cs`

**Features**:
- ? **SignalR Hub Connection** - Manages HubConnection lifecycle
- ? **Auto-Reconnection** - Exponential backoff (0s, 2s, 10s, 30s)
- ? **Event Forwarding** - Server events ? Client events
- ? **Error Handling** - Comprehensive exception handling
- ? **Logging Support** - Optional ILogger integration
- ? **Connection State** - Track connection and attachment status

**Architecture**:
```
ContainerConsoleClient
  ?? HubConnection (SignalR)
      ?? Server Events ? Client Events
      ?? Client Methods ? Server Methods
      ?? Auto-Reconnect Logic
```

**Constructor Options**:
```csharp
// Simple URL-based
new ContainerConsoleClient(hubUrl, logger);

// Custom HubConnection
new ContainerConsoleClient(hubConnection, logger);
```

---

### 3. Extension Methods: `ServiceCollectionExtensions`
**Path**: `src\GameServer.Docker.Client\Extensions\ServiceCollectionExtensions.cs`

**Provides**:
- ? **Easy DI Registration** - One-line setup
- ? **Singleton Support** - Reusable connection
- ? **Transient Support** - Per-request instances
- ? **Custom Configuration** - Configure HubConnection

**Usage**:
```csharp
// Simple registration
services.AddContainerConsoleClient("https://your-server/hubs/console");

// With custom configuration
services.AddContainerConsoleClient(
    "https://your-server/hubs/console",
    hubBuilder => {
        hubBuilder.WithAutomaticReconnect();
        hubBuilder.ConfigureLogging(...);
    });

// Transient (new instance per request)
services.AddContainerConsoleClientTransient("https://your-server/hubs/console");
```

---

## Usage Examples

### Basic Interactive Console

```csharp
using GameServer.Docker.Client.Services;

await using var console = new ContainerConsoleClient("https://your-manager/hubs/console");

// Event handlers
console.OutputReceived += (s, output) => Console.Write(output);
console.ErrorReceived += (s, error) => Console.WriteLine($"ERROR: {error}");
console.Connected += (s, id) => Console.WriteLine($"Connected to {id}");

// Connect and attach
await console.ConnectAsync();
await console.AttachToContainerAsync("my-container-id");

// Send commands
await console.SendInputAsync("ls -la\n");
await console.SendInputAsync("pwd\n");

// Cleanup happens automatically via IAsyncDisposable
```

### Dependency Injection in ASP.NET Core

```csharp
// Startup.cs / Program.cs
builder.Services.AddContainerConsoleClient("https://your-manager/hubs/console");

// In a controller or service
public class GameServerController : ControllerBase
{
    private readonly IContainerConsoleClient _console;
    
    public GameServerController(IContainerConsoleClient console)
    {
        _console = console;
    }
    
    [HttpPost("servers/{id}/command")]
    public async Task<IActionResult> ExecuteCommand(string id, [FromBody] string command)
    {
        await _console.ConnectAsync();
        var result = await _console.ExecCommandAsync(id, "sh", new[] { "-c", command });
        return Ok(new { output = result });
    }
}
```

### Building a Console UI

```csharp
using GameServer.Docker.Client.Services;
using System.Text;

public class ConsoleUI
{
    private readonly IContainerConsoleClient _console;
    private readonly StringBuilder _outputBuffer = new();
    
    public ConsoleUI(string hubUrl)
    {
        _console = new ContainerConsoleClient(hubUrl);
        _console.OutputReceived += OnOutputReceived;
        _console.ErrorReceived += OnErrorReceived;
    }
    
    private void OnOutputReceived(object? sender, string output)
    {
        _outputBuffer.Append(output);
        // Update UI with new output
        UpdateTerminalDisplay();
    }
    
    private void OnErrorReceived(object? sender, string error)
    {
        // Show error notification
        ShowError(error);
    }
    
    public async Task ConnectToContainer(string containerId)
    {
        await _console.ConnectAsync();
        var success = await _console.AttachToContainerAsync(containerId);
        if (!success)
        {
            throw new Exception("Failed to attach to container");
        }
    }
    
    public async Task SendCommand(string command)
    {
        await _console.SendInputAsync(command + "\n");
    }
    
    public string GetOutput() => _outputBuffer.ToString();
}
```

---

## Client Library Architecture

```
GameServer.Docker.Client
??? Interfaces/
?   ??? IContainerConsoleClient.cs      (Interface)
??? Services/
?   ??? ContainerConsoleClient.cs       (Implementation)
??? Extensions/
?   ??? ServiceCollectionExtensions.cs  (DI Extensions)
??? GameServer.Docker.Client.v1.g.cs    (Auto-generated REST clients)
```

---

## Key Features

### 1. Event-Driven API
```csharp
console.OutputReceived += (s, data) => {
    // Real-time output from container
};
```

### 2. Automatic Reconnection
```csharp
// Built-in retry policy:
// - Attempt 0: Immediate
// - Attempt 1: 2 seconds
// - Attempt 2: 10 seconds
// - Attempt 3: 30 seconds
// - Then stop
```

### 3. State Management
```csharp
if (console.IsConnected)
{
    Console.WriteLine($"Attached to: {console.AttachedContainerId}");
}
```

### 4. Proper Cleanup
```csharp
// Implements IAsyncDisposable
await using var console = new ContainerConsoleClient(url);
// Auto-cleanup on scope exit
```

---

## Integration with Existing Client

### REST APIs (Existing)
- `IGameServerApi` - Server CRUD operations
- `IDashboardApi` - Server listing
- `IGameTypeApi` - Game type templates
- `IPortApi` - Port allocation

### SignalR APIs (New)
- `IContainerConsoleClient` - Real-time console access

**Example**: Complete workflow
```csharp
// Deploy server (REST)
await gameServerApi.DeployAsync(newServer);

// Get running container
var server = await gameServerApi.GetAsync(serverId);

// Connect to console (SignalR)
await console.AttachToContainerAsync(containerIdFromServer);
await console.SendInputAsync("say Hello from API!\n");
```

---

## Dependencies

### Already in Project
- ? `Microsoft.AspNetCore.SignalR.Client` (10.0.2)

### No Additional Packages Needed
All SignalR dependencies already present in the client project.

---

## Testing Recommendations

### Unit Tests

```csharp
[Fact]
public async Task AttachToContainer_Success_RaisesConnectedEvent()
{
    // Arrange
    var hubUrl = "https://test-server/hubs/console";
    var console = new ContainerConsoleClient(hubUrl);
    string? connectedId = null;
    console.Connected += (s, id) => connectedId = id;
    
    // Act
    await console.ConnectAsync();
    await console.AttachToContainerAsync("test-container");
    
    // Assert
    Assert.Equal("test-container", connectedId);
}
```

### Integration Tests

```csharp
[Fact]
public async Task SendInput_RealContainer_ReceivesOutput()
{
    // Requires running GameServer.Docker instance
    var console = new ContainerConsoleClient("https://localhost:5001/hubs/console");
    var outputReceived = new TaskCompletionSource<string>();
    
    console.OutputReceived += (s, output) => outputReceived.TrySetResult(output);
    
    await console.ConnectAsync();
    await console.AttachToContainerAsync(TestContainerId);
    await console.SendInputAsync("echo test\n");
    
    var result = await outputReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
    Assert.Contains("test", result);
}
```

---

## Documentation Updates

### README.md
- ? Added "Real-Time Features (SignalR)" section
- ? Added "Container Console Client" usage examples
- ? Added DI setup examples
- ? Added `IContainerConsoleClient` to API summary
- ? Added interactive console example
- ? Added advanced features section

### Examples Provided
1. ? Basic usage
2. ? Interactive console
3. ? Dependency injection
4. ? Event handling
5. ? State checking
6. ? Multiple containers
7. ? Auto-reconnection

---

## Migration from Direct SignalR

**Before** (Direct SignalR usage):
```csharp
var connection = new HubConnectionBuilder()
    .WithUrl("https://server/hubs/console")
    .Build();

connection.On<string>("Output", (data) => Console.Write(data));
await connection.StartAsync();
await connection.InvokeAsync("AttachToContainer", "container-id");
```

**After** (Using IContainerConsoleClient):
```csharp
var console = new ContainerConsoleClient("https://server/hubs/console");
console.OutputReceived += (s, data) => Console.Write(data);
await console.ConnectAsync();
await console.AttachToContainerAsync("container-id");
```

**Benefits**:
- ? Strongly-typed API
- ? Automatic reconnection
- ? Better error handling
- ? State management
- ? Consistent with REST client patterns

---

## Changelog Entry

### v0.0.2-beta (Upcoming)

#### ?? New Features
- **Container Console Client** - Real-time interactive console access via SignalR
  - `IContainerConsoleClient` interface for DI
  - `ContainerConsoleClient` implementation with auto-reconnect
  - Event-driven API (OutputReceived, ErrorReceived, etc.)
  - Dependency injection extensions
  - Comprehensive documentation and examples

#### ?? Included In
- `GameServer.Docker.Client` package

---

## Build Status

? **Build Successful**

All interfaces, implementations, and extensions compile without errors.

---

## Next Steps

### Recommended
1. **Add Authentication** - JWT or cookie-based auth for SignalR hub
2. **Add Unit Tests** - Test client behavior and events
3. **Add Integration Tests** - Test against real hub
4. **Add Examples Project** - Standalone console app demonstrating usage

### Future Enhancements
1. **File Transfer** - Upload/download files via SignalR
2. **Log Streaming** - Real-time log tailing
3. **Resource Monitoring** - Live CPU/memory charts
4. **Multi-Container Sessions** - Attach to multiple containers simultaneously

---

## Summary

? **Interface Created**: `IContainerConsoleClient`  
? **Implementation Created**: `ContainerConsoleClient`  
? **DI Extensions Created**: `ServiceCollectionExtensions`  
? **Documentation Updated**: README with examples  
? **Build Successful**: All code compiles  
? **No Breaking Changes**: Extends existing client library  

The GameServer.Docker.Client library now provides a complete, production-ready interface for real-time container console access! ??
