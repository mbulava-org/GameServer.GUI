# Terminal & Console Guide

Game Server Manager provides two container interaction surfaces:

| Feature | Hub Route | Type | When shown |
|---|---|---|---|
| **Terminal** | `{API}/hubs/terminal` | Exec-based interactive shell (`/bin/sh`) | Always available for running containers |
| **Console** | `{API}/hubs/console` | TTY attached to the main process | Only if TTY is enabled for the game type |

## Backend Implementation

Both surfaces share the same SignalR hub, `ContainerConsoleHub`, mapped to two different routes in `GameServer.Docker`:

```csharp
app.MapHub<Hubs.ContainerConsoleHub>("/hubs/console");   // TTY attach
app.MapHub<Hubs.ContainerConsoleHub>("/hubs/terminal");  // Exec shell
```

The hub delegates session management to `TerminalSessionManager` and routes container operations through the registered Node Agent. The hub never connects directly to the Docker daemon.

## Hub Methods

### Starting a session

```csharp
Task StartExecSession(string containerId, string shell = "/bin/sh")
```

Used by the terminal route. Establishes an exec session in the container and begins streaming output back to the caller.

### Sending input

```csharp
Task SendInput(string sessionId, string input)
```

Sends keystrokes/commands to the active session.

### Disconnecting

```csharp
Task Disconnect()
```

Closes the current session and cleans up resources. Sessions are also cleaned up automatically when the SignalR connection disconnects (`OnDisconnectedAsync`).

## Frontend Components

- `ContainerTerminal.razor` — connects to `/hubs/terminal`
- `ContainerConsole.razor` — connects to `/hubs/console`

Both use XtermBlazor and the Matrix-style green-on-black theme.

## Multi-Node Support

Because all container operations go through the Node Agent registration (`AgentRegistryService`) and `/hubs/agentregistration`, logs and terminal work regardless of which Swarm node is hosting the container. The Primary Service resolves the correct agent via the container-to-agent mapping.

## Common Issues

### Terminal shows "connection error"

- Verify `GameServerDockerApi:BaseUri` in the web configuration.
- Confirm the API is running and `GameServer.Docker/Program.cs` maps `/hubs/terminal`.
- Check that a Node Agent is registered and healthy (`/hubs/agentregistration` heartbeats).

### No output in terminal

- Make sure the container is running.
- Verify the game image contains `/bin/sh` or change the shell in `ContainerTerminal.razor`.

## Related Documentation

- [Architecture Overview](../ARCHITECTURE.md)
- [Current Features](../CURRENT-FEATURES.md)
