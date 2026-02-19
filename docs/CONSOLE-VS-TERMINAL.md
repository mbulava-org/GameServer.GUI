# Container Console vs Terminal - Architecture Clarification

## Two Different Use Cases

### 1. Container Console (TTY Attach)
**Component:** `ContainerConsole.razor`  
**Hub:** `/hubs/console` ? `ContainerConsoleHub.AttachToContainer()`  
**Purpose:** Attach to the **main process** of the container (read-only view)

**When to use:**
- View stdout/stderr of the main container process
- Monitor application logs in real-time
- **Requires:** Container started with TTY enabled (`-t` flag)
- **Limitation:** Read-only or limited interaction (depends on main process)

**Docker equivalent:**
```bash
docker attach <container-id>
```

**Architecture:**
```
ContainerConsole ? ContainerConsoleHub.AttachToContainer()
    ? Node Agent WebSocket /containers/{id}/attach/ws
    ? Docker API: attach to main process
```

### 2. Container Terminal (Interactive Exec)
**Component:** `ContainerTerminal.razor`  
**Hub:** `/hubs/terminal` ? `ContainerConsoleHub.StartExecSession()`  
**Purpose:** Start a **new shell process** (interactive shell)

**When to use:**
- Interactive command-line access
- Run commands inside the container
- Full shell environment (`/bin/sh`, `/bin/bash`, etc.)
- **Does NOT require** TTY on main process
- **Creates** a new process each time

**Docker equivalent:**
```bash
docker exec -it <container-id> /bin/sh
```

**Architecture:**
```
ContainerTerminal ? ContainerConsoleHub.StartExecSession(shell)
    ? Node Agent WebSocket /containers/{id}/exec/ws?shell=/bin/sh
    ? Docker API: exec create + attach to new process
```

## Hub Endpoints

Both use the **same hub** (`ContainerConsoleHub`) but mapped to different URLs for clarity:

```csharp
// In Program.cs
app.MapHub<ContainerConsoleHub>("/hubs/console");   // For TTY attach
app.MapHub<ContainerConsoleHub>("/hubs/terminal");  // For exec sessions
```

## ContainerConsoleHub Methods

### For Console (Attach)
```csharp
public async Task<bool> AttachToContainer(string containerId)
```
- Attaches to main process
- Creates WebSocket: `/containers/{id}/attach/ws`
- Forwards output to client
- Input can be sent if TTY supports it

### For Terminal (Exec)
```csharp
public async Task<bool> StartExecSession(string containerId, string shell = "/bin/sh")
```
- Starts new process with specified shell
- Creates WebSocket: `/containers/{id}/exec/ws?shell={shell}`
- Fully interactive bidirectional communication
- Process ends when client disconnects

### Common Methods
```csharp
public async Task SendInput(string input)  // Send keystrokes/commands
public async Task StopExecSession(string sessionId)  // End exec session
public async Task Disconnect()  // End any session
```

## SignalR Events

Both components receive the same events from `ContainerConsoleHub`:

| Event | Payload | Description |
|-------|---------|-------------|
| `Output` | `string` | Output from container (stdout/stderr) |
| `SessionStarted` | `string` (sessionId) | Session established |
| `Connected` | `string` (containerId) | Connection successful |
| `Error` | `string` (message) | Error occurred |
| `Disconnected` | `string` (reason) | Connection closed |

## UI Component Differences

### ContainerConsole
- **Title:** "Attached Console"
- **Description:** "View the main process output (requires TTY enabled)"
- **Auto-connect:** Usually FALSE (user must click Connect)
- **Note:** Shows warning if TTY not enabled on server

### ContainerTerminal
- **Title:** "Interactive Shell"
- **Description:** "Execute commands directly in the container using /bin/sh"
- **Shell parameter:** User can specify (default: /bin/sh)
- **Auto-connect:** Usually TRUE (connects automatically)
- **Always works:** Doesn't require TTY on main process

## Server Requirements

### For Console (Attach)
? **Requires TTY enabled** in service spec:
```csharp
serviceSpec.TaskTemplate.ContainerSpec.TTY = true;
```

If TTY not enabled, you'll see:
- No output from console
- Unable to attach
- Error: "the container is not running in TTY mode"

### For Terminal (Exec)
? **Always works** - no special requirements
- Creates new process, not dependent on main process TTY
- Works even if main process has no TTY

## Node Agent Support

Both require Node Agent WebSocket endpoints:

### Attach WebSocket
```
ws://agent:8080/containers/{containerId}/attach/ws
```

### Exec WebSocket
```
ws://agent:8080/containers/{containerId}/exec/ws?shell=/bin/sh
```

## When to Use Each

| Scenario | Use Console (Attach) | Use Terminal (Exec) |
|----------|---------------------|---------------------|
| View game server logs | ? YES | ? No |
| Run admin commands | ? Limited | ? YES |
| Debug application | ? YES | ? YES |
| Interactive shell | ? No | ? YES |
| View startup errors | ? YES | ? No |
| Install packages | ? No | ? YES |
| Edit config files | ? No | ? YES (with vi/nano) |

## Common Use Cases

### View Valheim Server Console
? Use **ContainerConsole** (attach to main process)
- Shows game server output
- Server commands go to main process
- Requires TTY enabled

### Run Commands in Minecraft Server
? Use **ContainerTerminal** (exec shell)
- Full shell access
- Can run `rcon-cli` or other tools
- Can edit server.properties
- Works regardless of TTY

### Debug Container Issues
? Use **ContainerTerminal** (exec shell)
- Can run `ps`, `netstat`, `ls`, etc.
- Full diagnostic access
- More powerful than attach

## Architecture Decision: Why Not Separate Hubs?

**Answer:** They share the same infrastructure:
- Both use Node Agents
- Both use WebSocket forwarding
- Both track sessions the same way
- Same cleanup logic

**Only differences:**
- WebSocket URL path (`/attach/ws` vs `/exec/ws`)
- Session type flag (`IsExecSession`)

By using ONE hub with TWO endpoints, we:
- ? Reduce code duplication
- ? Share session management
- ? Maintain clear API boundaries (`/console` vs `/terminal`)
- ? Simplify maintenance

## Summary

- **ContainerConsole** = Watch main process (attach)
- **ContainerTerminal** = Interactive shell (exec)
- **Same hub, different methods**
- **Both use Node Agents** (multi-node support)
- **Clear separation in UI** but shared backend

---

**Updated:** 2026-02-18  
**Related:** ARCHITECTURE.md, IMPLEMENTATION-CHECKLIST.md
