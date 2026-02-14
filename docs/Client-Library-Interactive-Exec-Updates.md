# Client Library Updates Summary

## Overview

The Client Library has been updated to support **interactive command execution** via WebSocket, providing full bidirectional communication for running shells and TTY applications in containers.

---

## Changes Made

### 1. **IContainerConsoleClient Interface** ?
**File**: `src\GameServer.Docker.Client\Interfaces\IContainerConsoleClient.cs`

**Added**:
```csharp
Task ExecInteractiveAsync(
    string agentUrl, 
    string containerId, 
    string command, 
    string[]? args = null, 
    bool tty = true, 
    CancellationToken cancellationToken = default);
```

**Purpose**: Start an interactive exec session with full stdin/stdout/stderr support via WebSocket

---

### 2. **ContainerConsoleClient Implementation** ?
**File**: `src\GameServer.Docker.Client\Services\ContainerConsoleClient.cs`

#### **New Fields**:
```csharp
private ClientWebSocket? _activeWebSocket;
private readonly SemaphoreSlim _wsSendLock;
```

#### **Enhanced Methods**:

**`SendInputAsync`** - Now supports both modes:
- SignalR Hub mode (existing attach functionality)
- WebSocket mode (new interactive exec)
- Automatically detects which connection is active

**`ExecInteractiveAsync`** - NEW:
- Establishes WebSocket connection to Node Agent
- Starts bidirectional communication
- Handles connection lifecycle
- Raises `OutputReceived` events for real-time output
- Supports TTY mode for terminal applications

#### **New Helper Methods**:
- `BuildWebSocketUrl()` - Constructs WebSocket URL with query parameters
- `ReceiveFromWebSocketAsync()` - Receives output from WebSocket
- `SendToWebSocketAsync()` - Sends input to WebSocket (thread-safe)
- `MonitorInputEventsAsync()` - Keeps connection alive for input

#### **Enhanced Disposal**:
- Properly closes active WebSocket connections
- Disposes semaphore

---

### 3. **Documentation** ?
**File**: `docs\Interactive-Exec-Client-Usage.md`

Complete usage guide with:
- Basic examples
- Interactive shell example
- Advanced scenarios (vim, top, etc.)
- API comparison
- Best practices
- Error handling

---

## Feature Comparison

| Feature | Before | After |
|---------|--------|-------|
| Non-interactive exec | ? Via SignalR Hub | ? Unchanged |
| Interactive exec | ? Not supported | ? Via WebSocket |
| stdin Support | ? No | ? Yes (interactive mode) |
| Real-time output | ? For attach only | ? For attach and exec |
| TTY Support | ? For attach only | ? For attach and exec |

---

## Usage Examples

### Before (Non-Interactive Only)
```csharp
// Could only execute simple commands
var output = await client.ExecCommandAsync("abc123", "ls", new[] {"-la"});
Console.WriteLine(output); // Buffered output
```

### After (Interactive Support)
```csharp
// Non-interactive still works the same
var output = await client.ExecCommandAsync("abc123", "ls", new[] {"-la"});

// NEW: Interactive exec with stdin
client.OutputReceived += (s, o) => Console.Write(o);

await client.ExecInteractiveAsync(
    agentUrl: "http://agent:8080",
    containerId: "abc123",
    command: "bash",
    args: new[] { "-i" },
    tty: true
);

await client.SendInputAsync("ls -la\n");
await client.SendInputAsync("cat file.txt\n");
await client.SendInputAsync("exit\n");
```

---

## Architecture Changes

### Communication Flow

**Non-Interactive** (Unchanged):
```
Client ? SignalR Hub ? Node Agent REST ? Docker Exec
```

**Interactive** (NEW):
```
Client WebSocket ? Node Agent WebSocket ? Docker Exec Stream
```

### Direct Agent Connection

The new `ExecInteractiveAsync` bypasses the Primary Service and connects **directly to the Node Agent** via WebSocket. This provides:

? **Lower latency** (no SignalR relay)  
? **True streaming** (native WebSocket)  
? **Full TTY support** (bidirectional raw stream)  
? **Better for interactive workloads**

---

## Breaking Changes

**None!** All existing functionality is preserved:
- ? `ExecCommandAsync` works exactly as before
- ? `AttachToContainerAsync` unchanged
- ? All existing events unchanged
- ? Backward compatible

---

## New Dependencies

**Added**:
- `System.Net.WebSockets.ClientWebSocket` (part of .NET, no new package)
- Thread-safe WebSocket sending via `SemaphoreSlim`

---

## Benefits

### For Developers
1. **Unified API**: Same `ContainerConsoleClient` for both attach and exec
2. **Event-driven**: Consistent `OutputReceived` event pattern
3. **Flexible**: Choose non-interactive or interactive based on use case
4. **Type-safe**: Full C# API with IntelliSense support

### For Users
1. **Interactive shells**: Run bash, sh, PowerShell interactively
2. **TTY apps**: Use vim, nano, htop, top in containers
3. **Real-time feedback**: See output as it happens
4. **Full control**: Send commands dynamically during execution

### For Applications
1. **Admin consoles**: Build web-based container terminals
2. **Debugging tools**: Interactive troubleshooting
3. **Automation**: Mix programmatic control with interactive sessions
4. **Monitoring**: Stream real-time command output

---

## Testing Recommendations

### Unit Tests
```csharp
[Fact]
public async Task ExecInteractiveAsync_ConnectsToAgent()
{
    var client = new ContainerConsoleClient("...");
    
    var outputReceived = false;
    client.OutputReceived += (s, o) => outputReceived = true;
    
    await client.ExecInteractiveAsync(
        "http://agent:8080", 
        "container-id", 
        "echo", 
        new[] { "test" }
    );
    
    Assert.True(outputReceived);
}
```

### Integration Tests
```csharp
[Fact]
public async Task ExecInteractiveAsync_RunsBashSession()
{
    var client = new ContainerConsoleClient("...");
    var outputs = new List<string>();
    
    client.OutputReceived += (s, o) => outputs.Add(o);
    
    var execTask = client.ExecInteractiveAsync(
        "http://agent:8080",
        "test-container",
        "bash",
        tty: true
    );
    
    await client.SendInputAsync("echo 'Hello'\n");
    await Task.Delay(500);
    await client.SendInputAsync("exit\n");
    
    await execTask;
    
    Assert.Contains(outputs, o => o.Contains("Hello"));
}
```

---

## Migration Guide

### No Migration Required! ?

Existing code continues to work without changes:

```csharp
// This still works exactly as before
var client = new ContainerConsoleClient("https://service/hubs/console");
await client.ConnectAsync();
await client.AttachToContainerAsync("container-id");
await client.SendInputAsync("command\n");
```

### Opt-In to New Feature

To use interactive exec, simply call the new method:

```csharp
// Add this for interactive exec
await client.ExecInteractiveAsync(
    "http://agent:8080",
    "container-id",
    "bash",
    tty: true
);
```

---

## Future Enhancements

Potential improvements for future versions:

1. **Automatic Agent Discovery**
   - Remove need to specify `agentUrl`
   - Client queries Primary Service for Agent location

2. **Exit Code Capture**
   - Return exit code via event when exec completes
   - Add `ExecCompletedEvent(exitCode)`

3. **Binary Mode Support**
   - Support for non-text data
   - File transfer via exec

4. **Session Reconnection**
   - Resume interrupted exec sessions
   - Persist session state

5. **Multiplexing**
   - Multiple exec sessions over single WebSocket
   - Reduce connection overhead

---

## Summary

The Client Library now provides **complete interactive command execution** support:

? **New Method**: `ExecInteractiveAsync` for real-time interactive commands  
? **Enhanced**: `SendInputAsync` works with both SignalR and WebSocket  
? **Unified API**: Same client for attach and exec operations  
? **Backward Compatible**: All existing functionality preserved  
? **Well Documented**: Complete usage guide with examples  

Perfect for building admin tools, debugging interfaces, and interactive container management applications! ??
