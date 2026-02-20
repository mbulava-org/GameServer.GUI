# Copilot Instructions

## 📚 Before Making Changes

**ALWAYS read the relevant documentation first:**

1. **Architecture & Patterns**: Read `docs/ARCHITECTURE.md` to understand system architecture and mandatory patterns
   - Multi-node Docker Swarm architecture
   - When to use Node Agents vs Docker Client
   - Never connect directly to Docker daemon from Hubs for container operations

2. **Current Features**: Check `docs/CURRENT-FEATURES.md` to understand what's already implemented

3. **Constants & Conventions**: Use `ServiceLabels` constants from `GameServer.Docker.Constants` namespace
   - Never hardcode service label strings like `"gameserver.docker.managed"`
   - Use `ServiceLabels.Managed`, `ServiceLabels.ServerId`, etc.

4. **Performance Patterns**: Follow optimization patterns
   - Use parallel processing (`Task.WhenAll`) for collections
   - Use Docker label filters to narrow queries
   - Batch API calls when possible

5. **Quick References**: 
   - `docs/QUICK-REFERENCE-CARD.md` - Common operations
   - `docs/TESTING-QUICK-REFERENCE.md` - Testing guide

## General Guidelines
- Maintain a clear separation of concerns in your code
- Keep UI and logic together for better maintainability
- Follow the architectural patterns defined in ARCHITECTURE.md
- Use constants instead of magic strings

## Code Style
- Use specific formatting rules
- Follow naming conventions defined in documentation
- Use `ServiceLabels` constants for Docker labels
- Use descriptive variable names

## Project-Specific Rules

### Docker & Swarm
- **NEVER** connect directly to `IDockerClient` from SignalR Hubs for container operations
- **ALWAYS** use `INodeAgentDiscovery` to find the correct node agent for container operations
- Use `DockerServiceHelper` for Swarm **service** management (creating, updating services)
- Use Node Agents for **container** operations (logs, exec, stats, attach)

### Service Labels
- Use `ServiceLabels.Managed` instead of `"gameserver.docker.managed"`
- Use `ServiceLabels.ServerId` instead of `"gameserver.docker.Id"`
- Use `ServiceLabels.Name`, `ServiceLabels.Description`, `ServiceLabels.GameType`
- Use `ServiceLabels.ManagedValue` for the "true" value

### GameServer Settings
- Use `Server.Settings` to store list-like data as newline-separated strings under specific keys such as "OPS" or "WHITELIST"
- `GameServer.Lists` and `GameTypeDefinition.DefaultLists` have been removed
- `StepGameSettings` must not reference these lists and should use `Server.Settings` only

### UI Components
- Move component logic from code-behind into the `.razor` file when possible
- Keep UI and logic together for better maintainability
- The ports list is fixed in the UI: do not allow adding or removing port mappings
- When a `PortMapping`'s `PublishedPort` is 0, it should default to the `ContainerPort` value
- The memory limit is fixed in the UI: do not allow adding or removing memory mappings
- When a `MemoryMapping`'s `PublishedMemory` is 0, it should default to the `ContainerMemory` value

### Extended Metadata
- Use proper `DataType` values: `"string"`, `"number"`, `"boolean"`, `"enum"`, `"port"`
- For enums, provide `AllowedValues` array
- For enums with display labels, provide `ValueMappings` dictionary
- Use `RenderFragment<object>` for Radzen `Template` parameters (not `RenderFragment<string>`)

### Performance
- Use parallel processing for collections with `Task.WhenAll`
- Use Docker label filters to narrow queries (e.g., `ServiceFilter` with `Label` array)
- Batch API calls when fetching multiple resources
- Pre-fetch related data (like tasks) to avoid N+1 queries

### Database
- GameTypes are stored in SQLite database
- Extended metadata is JSON-serialized in `ExtendedMetadataJson` column
- Use `IGameTypeRepository` for database operations
- `GameTypeRegistry` is marked `[Obsolete]` - use database instead