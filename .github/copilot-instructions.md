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
- Add test coverage for services during implementation, but defer GUI component tests until the very end because the pages will continue to iterate on look and feel.
- For new GameType GUI work, prefer creating automated GUI tests before manual testing and break large editing pages into smaller replaceable components for easier iteration.

## Code Style
- Use specific formatting rules
- Follow naming conventions defined in documentation
- Use `ServiceLabels` constants for Docker labels
- Use descriptive variable names

## Project-Specific Rules

### Database Schema
- You may directly adjust the proposed database schema in documentation, including field renames, removals, and repurposings; follow the latest edited schema rather than earlier drafts.
- For this project, new V2 DbContext work should follow the same pattern as the existing DbContext so automatic client generation is not disrupted.
- For this project, V2 repository initialization should follow the same pattern as the legacy repository's `InitializeDatabaseAsync` behavior unless intentionally diverging.
- For V2 database work, prefer adding PostgreSQL support through a dedicated PostgreSQL database project and use pgPacTool/postgresPacTools to deploy schema and database object changes instead of ad hoc schema deployment.

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

### GameServers in V2
- For this project, `GameServers` in V2 should avoid duplicating data owned by `GameType` and `GameTypeRevision` and should likely reference only `GameTypeRevisionId` rather than also storing game type or image fields.

### Docker Image Management
- For this project, Docker image info should be scanned when editing a GameType if a tag's SHA changes or a new tag is added
- GameServer should remain focused on deployment/update intent for the Docker service and should not store all image inspection data per server

### GameServer Ports and Volumes
- For this project, `GameServerPorts` and `GameServerVolumes` should not be stored in the database because they should be computable from `GameTypeRevision` directly.
- Port validation is a backend service responsibility and should not be stored in V2 database metadata. The backend should validate requested port/protocol combinations for availability across multiple GameServer instances before assigning or changing exposed game ports.
- Each port mapping should have a single calculation value column interpreted by `RelationType` and `MappingRole`, instead of separate `OffsetValue`, `FixedValue`, and `MultiplierValue` columns.
- Port DataType settings must require a primary direct port mapping to an existing GameType port, allow only one primary mapping per setting, restrict non-primary mappings to related offset/multiplier mappings, and port mapping descriptions should come from the GameType port description rather than separate mapping descriptions.
- For V2 setting port mappings, related offset or multiplier mappings should derive their target port from the primary direct mapping's port; the UI should not require a separate target port selector for those mappings, and referenced ports must already exist in the revision ports list.

### UI Components
- Move component logic from code-behind into the `.razor` file when possible
- Keep UI and logic together for better maintainability
- The ports list is fixed in the UI: do not allow adding or removing port mappings
- When a `PortMapping`'s `PublishedPort` is 0, it should default to the `ContainerPort` value
- The memory limit is fixed in the UI: do not allow adding or removing memory mappings
- When a `MemoryMapping`'s `PublishedMemory` is 0, it should default to the `ContainerMemory` value
- For GameType editing UX, prefer the existing settings editor master-detail pattern with a left-side settings list and a right-side details pane when redesigning similar pages such as the V2 editor.
- For the V2 GameType editor, creating a new revision draft must not require the Version Tag or any Ports because those fields live on different tabs; those requirements should apply at save/validation time, not draft creation time.
- Place `New Draft` and `Save Revision` by the active revision control; clicking `New Draft` should insert and select a dummy draft item in the revisions dropdown; cross-tab validation errors should render outside the tabs.

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
- For this project, the new persistence layer should use a `V2` namespace under `Models` and `Repositories` rather than prefixing every type and repository with `Versioned`. The V2 persistence layer is a separate new implementation that must coexist with the old models and repositories as distinct old and new data models/repositories.

### Web Hosts
- Web Hosts belong to `GameTypeRevision` but are enabled/disabled per `GameServer` instance, usually based on environment variables.
- Redirects and web hosts refer to the same domain concept.
- Load balancer configuration belongs in the Primary Service, and the current provider is Traefik running on a Swarm manager with label-driven updates.

### Editable Data Packages
- For editable data packages like GameTypes and Revisions, prefer concrete observable list types such as `List<T>` over `IEnumerable<T>` so nested collections can be observed and edited reliably in the UI.