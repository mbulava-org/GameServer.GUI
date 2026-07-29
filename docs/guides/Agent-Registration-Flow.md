# Agent Registration Flow

Game Server Manager uses a push-based agent registration model. Each Docker node runs an instance of `GameServer.Docker.Agent`, which connects to the Primary Service and reports its state.

## Components

| Component | Project | Responsibility |
|---|---|---|
| `AgentRegistryService` | `GameServer.Docker` | In-memory registry of connected agents, their capabilities, and container-to-agent mappings |
| `AgentRegistrationHub` | `GameServer.Docker` | SignalR hub at `/hubs/agentregistration` that receives registrations and heartbeats |
| `AgentRegistrationService` | `GameServer.Docker.Agent` | Background service that connects to the primary service and pushes registration/heartbeats |
| `NodeAgentHub` | `GameServer.Docker.Agent` | Agent-side SignalR hub at `/hubs/nodeagent` used for streaming container data |

## Registration Flow

1. The agent starts and reads `AgentRegistration:PrimaryServiceUrl` from configuration (defaulting to the Primary Service URL).
2. `AgentRegistrationService` opens a SignalR connection to `{PrimaryServiceUrl}/hubs/agentregistration`.
3. The agent sends its metadata:
   - Node name
   - Capabilities (e.g., `docker`, `logs`, `exec`)
   - Internal URL used for direct API calls
4. Every 30 seconds the agent sends a heartbeat.
5. The Primary Service marks agents unhealthy if a heartbeat is missed.
6. The Primary Service stores a container-to-agent mapping so logs, terminal, and stats requests can be routed to the correct node.

## Why This Matters

- The Primary Service does not need access to the Docker socket.
- Container operations (logs, exec, stats) always go through the node that hosts the container.
- The system works across multi-node Docker Swarm deployments.

## Configuration

### Agent (`appsettings.json`)

```json
{
  "AgentRegistration": {
	"PrimaryServiceUrl": "http://primary-service:8080/"
  }
}
```

### Primary Service

No explicit configuration is required. The hub is registered automatically:

```csharp
app.MapHub<Hubs.AgentRegistrationHub>("/hubs/agentregistration");
```

## Troubleshooting

### Agent never shows as registered

- Verify `AgentRegistration:PrimaryServiceUrl` points to the Primary Service.
- Check that the Primary Service is running and reachable on the configured URL.
- Confirm SignalR can connect (WebSockets/long polling allowed).
- Look for registration/heartbeat errors in the agent logs.

### Logs or terminal fail for a running container

- Confirm the container's host node has a running, registered agent.
- Check the Primary Service logs for "container-to-agent" lookup failures.
- Restart the agent on the affected node if its heartbeat has expired.

## Related Documentation

- [Architecture Overview](../ARCHITECTURE.md)
- [Terminal & Console Guide](Terminal-And-Console.md)
- [Current Features](../CURRENT-FEATURES.md)
