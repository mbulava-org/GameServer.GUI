# Interactive Exec Client Usage Guide

## Overview

The `ContainerConsoleClient` now supports **interactive command execution** via direct WebSocket connections to Node Agents. This provides real-time bidirectional communication for running commands that need stdin (shells, interactive programs, etc.).

## Two Ways to Execute Commands

### 1. Non-Interactive Exec (via Primary Service SignalR Hub)

**Use for**: Simple commands where you just need the output

```csharp
using GameServer.Docker.Client.Services;

var client = new ContainerConsoleClient("https://primary-service/hubs/console");
await client.ConnectAsync();

// Execute and get output
var output = await client.ExecCommandAsync(
    containerId: "abc123",
    command: "ls",
    args: new[] { "-la", "/app" }
);

Console.WriteLine(output);
```

**Characteristics**:
- ? Request/Response pattern
- ? Simple output retrieval
- ? Exit code included
- ? No stdin interaction
- ? Output is buffered

---

### 2. Interactive Exec (via WebSocket to Node Agent) ? NEW

**Use for**: Interactive shells, real-time commands, TTY applications

```csharp
using GameServer.Docker.Client.Services;

var client = new ContainerConsoleClient("https://primary-service/hubs/console");

// Set up event handlers for real-time output
client.OutputReceived += (sender, output) =>
{
    Console.Write(output); // Print output as it arrives
};

client.Connected += (sender, containerId) =>
{
    Console.WriteLine($"Connected to container: {containerId}");
};

client.Disconnected += (sender, reason) =>
{
    Console.WriteLine($"Disconnected: {reason}");
};

// Start interactive bash session
var execTask = client.ExecInteractiveAsync(
    agentUrl: "http://node-agent-1:8080",
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

await client.SendInputAsync("exit\n"); // Exit shell

await execTask; // Wait for session to complete
```

**Characteristics**:
- ? Real-time streaming output
- ? Interactive stdin support
- ? TTY support for terminal apps
- ? Bidirectional communication
- ? Full shell experience

---

## Complete Interactive Shell Example

```csharp
using GameServer.Docker.Client.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

public class InteractiveShellExample
{
    public static async Task Main()
    {
        var client = new ContainerConsoleClient("https://primary-service/hubs/console");
        
        // Setup output handler
        client.OutputReceived += (sender, output) =>
        {
            Console.Write(output);
        };

        client.ErrorReceived += (sender, error) =>
        {
            Console.Error.WriteLine($"Error: {error}");
        };

        using var cts = new CancellationTokenSource();
        
        // Start bash in background task
        var execTask = Task.Run(async () =>
        {
            await client.ExecInteractiveAsync(
                agentUrl: "http://agent:8080",
                containerId: "my-container-id",
                command: "bash",
                args: new[] { "-i" },
                tty: true,
                cancellationToken: cts.Token
            );
        });

        // Wait for connection
        await Task.Delay(1000);

        // Interactive loop - read from console, send to container
        Console.WriteLine("Interactive shell ready. Type 'exit' to quit.");
        
        while (!cts.Token.IsCancellationRequested)
        {
            var input = Console.ReadLine();
            if (string.IsNullOrEmpty(input))
                continue;

            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                await client.SendInputAsync("exit\n");
                break;
            }

            await client.SendInputAsync(input + "\n");
        }

        await execTask;
        await client.DisposeAsync();
    }
}
```

---

## Advanced Usage: Running Interactive Programs

### Example: Using vim in a container

```csharp
client.OutputReceived += (sender, output) =>
{
    // Vim sends terminal control codes
    Console.Write(output);
};

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

### Example: Running top for monitoring

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

client.OutputReceived += (sender, output) =>
{
    Console.Clear();
    Console.Write(output); // Display top output
};

await client.ExecInteractiveAsync(
    agentUrl: "http://agent:8080",
    containerId: "abc123",
    command: "top",
    args: new[] { "-b", "-n", "1" }, // Batch mode, 1 iteration
    tty: false,
    cancellationToken: cts.Token
);
```

---

## API Comparison

| Feature | `ExecCommandAsync` | `ExecInteractiveAsync` |
|---------|-------------------|----------------------|
| Communication | SignalR (Primary Service) | WebSocket (Direct to Agent) |
| Input | ? No stdin | ? Full stdin via `SendInputAsync` |
| Output | Buffered, returned as string | ? Streamed via `OutputReceived` event |
| TTY | ? No | ? Optional |
| Use Case | Simple commands | Interactive shells, TTY apps |
| Exit Code | ? Returned | Logged by Agent |

---

## Architecture Flow

### Non-Interactive Exec
```
Client 
  ? SignalR Hub (Primary Service)
    ? ContainerConsoleHub method
      ? Node Agent HTTP API
        ? Docker Exec
```

### Interactive Exec
```
Client WebSocket
  ? Node Agent WebSocket Endpoint
    ? Docker Exec Stream (MultiplexedStream)
      ? Container Process
```

---

## Error Handling

```csharp
try
{
    await client.ExecInteractiveAsync(
        agentUrl: "http://agent:8080",
        containerId: "abc123",
        command: "bash",
        tty: true
    );
}
catch (WebSocketException ex)
{
    Console.WriteLine($"WebSocket error: {ex.Message}");
}
catch (OperationCanceledException)
{
    Console.WriteLine("Operation cancelled");
}
catch (Exception ex)
{
    Console.WriteLine($"Unexpected error: {ex.Message}");
}
```

---

## Best Practices

### 1. Always Dispose
```csharp
await using var client = new ContainerConsoleClient("...");
// Use client
// Automatically disposed
```

### 2. Handle Events Before Starting
```csharp
client.OutputReceived += HandleOutput;
client.ErrorReceived += HandleError;
client.Disconnected += HandleDisconnect;

// Then start exec
await client.ExecInteractiveAsync(...);
```

### 3. Use TTY for Terminal Apps
```csharp
// TTY required for: vim, nano, htop, less, etc.
await client.ExecInteractiveAsync(
    ...,
    command: "vim",
    tty: true  // IMPORTANT
);
```

### 4. Always Add Newline to Commands
```csharp
// Shell commands need newline to execute
await client.SendInputAsync("ls -la\n");  // ? Correct
await client.SendInputAsync("ls -la");    // ? Won't execute
```

### 5. Use Cancellation Tokens
```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

await client.ExecInteractiveAsync(
    ...,
    cancellationToken: cts.Token
);
```

---

## Limitations

1. **Agent URL Required**: You need to know the specific Node Agent URL hosting the container
2. **WebSocket Support**: Network must support WebSocket connections
3. **Binary Data**: Currently only supports text mode (UTF-8)
4. **No Exit Code in Response**: Exit code is logged by Agent but not returned to client

---

## Future Enhancements

- [ ] Support for binary data (non-TTY raw mode)
- [ ] Expose exit code via event
- [ ] Automatic Agent discovery (remove need for `agentUrl` parameter)
- [ ] Reconnection support for interrupted sessions
- [ ] Multiplexed streams (multiple execs over single WebSocket)

---

## Summary

The new `ExecInteractiveAsync` method brings full interactive command execution to the Client Library:

? **Real-time bidirectional communication**  
? **TTY support for terminal applications**  
? **Direct WebSocket streaming to Node Agents**  
? **Same familiar API as container attach**  
? **Event-driven output handling**  

Perfect for building admin consoles, debugging tools, or any application requiring interactive container access! ??
