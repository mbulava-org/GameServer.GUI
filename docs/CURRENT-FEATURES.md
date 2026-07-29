# Game Server Manager - Current Features & Implementation

**Last Updated:** 2026  
**Version:** v0.3.0 (V2 GameServer System)

## ?? Executive Summary

Game Server Manager is a comprehensive Blazor Server application for deploying and managing game servers in Docker Swarm. This document reflects all currently implemented features as of the latest session.

---

## ?? Table of Contents

1. [Core Features](#core-features)
2. [Server Management](#server-management)
3. [GameType System](#gametype-system)
4. [Port Management](#port-management)
5. [Monitoring & Logs](#monitoring--logs)
6. [Architecture](#architecture)
7. [API Endpoints](#api-endpoints)

---

## Core Features

### ? Server Creation Wizard (5 Steps)

**Location:** `/servers/new`  
**Component:** `CreateServerWizard.razor`

1. **Step 1: Select Game Type**
   - Choose from pre-defined game types
   - Displays thumbnail, name, and description
   - Filters available game types from database

2. **Step 2: Basic Information**
   - Server name (required)
   - Server description (optional)
   - Validates uniqueness

3. **Step 3: Game Settings**
   - **Tabbed interface by category**
   - Auto-infers DataType for settings without metadata
   - Shows all settings from DefaultSettings
   - Required settings marked with red asterisk (*)
   - **Port settings with automatic port relationship updates**:
     - When port-type setting changes, related ports update automatically
     - Supports Offset, Fixed, and Multiplier relationships
     - UDP ports: All three values match (Setting = ContainerPort = PublishedPort)
     - TCP ports: Setting = ContainerPort, PublishedPort can differ

4. **Step 4: Technical Details**
   - **Port Mappings**:
     - Default port highlighted with green badge and star ?
     - Non-default ports read-only when default port exists
     - Shows "Auto-calculated" for relationship-driven ports
     - Container port displayed as reference
   - **Volumes** (future): Coming soon

5. **Step 5: Review & Create**
   - Summary of all configurations
   - **Network Information**:
     - Host IP (auto-detected)
     - Published Port (default port with badge)
     - Connection string using default port
   - **Settings Summary**:
     - Always shows required settings (even if empty with default value)
     - Groups by configuration
   - **Port Mappings**:
     - Default port with green badge and star ?
     - Shows protocol in badge
   - **Volumes**:
     - Empty sources show green "Create New" badge

### ? Server Management

**Location:** `/gameservers-v2` (dashboard), `/gameservers-v2/{serverId}` (details)

#### Dashboard Features
- List all game servers
- Quick actions: Start, Stop, Edit, Delete
- Status indicators (Running, Stopped, etc.)
- Filter and search capabilities
- Game type badges

#### Server Details Page

**Tabs:**

1. **Overview**
   - **Server Information**:
     - Server ID, Name, Game Type, Service Name
     - Description
   - **Network** (Updated!):
     - Host IP
     - Published Port (default port with "Default" badge)
     - Connection string (uses default port)
     - **Port Mappings list**:
       - Default port: Green badge with star ?
       - Non-default ports: Blue badges
       - Shows protocol
       - Format: `PublishedPort ? ContainerPort (protocol)`
   - **Resource Monitoring**:
     - REST API Monitor (polling)
     - Real-Time Monitor (SignalR)

2. **Logs** (Fully Implemented!)
   - **Real-time log streaming via SignalR**
   - Container lookup using Docker labels (`gameserver.docker.Id`)
   - Streams directly from Docker daemon using Docker.DotNet
   - Features:
     - Follow mode for live logs
     - Tail last N lines
     - Timestamps
     - Filter by text and log level
     - Download logs
     - Auto-scroll
     - Clean output (removes Docker 8-byte headers)
   - Connects to: `{API}/hubs/serverlogs`

3. **Terminal** (NEW!)
   - **Interactive shell session using exec**
   - Executes `/bin/sh` in the container
   - Full Xterm integration with:
     - Matrix-style green on black theme
     - Cursor blinking
     - 5000 lines scrollback
     - Color support (ANSI escape codes)
   - Real-time bidirectional communication
   - Auto-connect on tab open
   - Always available when server is running
   - Connects to: `{API}/hubs/terminal`

4. **Files**
   - Browse server files
   - Upload/download files
   - Edit configuration files
   - Manage world data and plugins

5. **TTY Console** (Conditional)
   - Only shown if TTY enabled in game type
   - Attached console (stdin/stdout of main process)
   - Different from Terminal (exec vs attach)
   - Connects to: `{API}/hubs/console`

#### Edit Server
- Modify server settings
- Update port mappings (preserves default port)
- Change environment variables
- Update volumes (immutable after creation - warning shown)

---

## GameType System

### ? GameType Management

**Location:** `/gametypes`

#### Features
- List all game types
- Create new game types
- Edit existing game types
- Delete game types
- Import/export definitions

### Database Persistence Status

The application uses a single V2 persistence layer for game type and server configuration:

#### V2 persistence
- `Data/V2/GameServerV2DbContext`
- `Repositories/V2/IGameTypeRepository`
- `Repositories/V2/IGameServerRepository`
- provider-aware configuration supporting SQLite (default), MySQL (supported), and PostgreSQL (planned)
- **SQLite is the current default; MySQL is supported.** PostgreSQL support exists in code but is not fully implemented and should be considered coming soon. The `src/GameServer.DB.PostgreSql` project and `scripts/Deploy-V2PostgresDatabase.ps1` are prepared for future completion.
- normalized schema with:
  - `GameType` owning a fixed `ImageReference`
  - `GameTypeRevision` owning tag-based deployable templates
  - `GameServer` storing only server-specific deployment intent via `GameTypeRevisionId`
  - derived Web Host state resolved from revision Web Host definitions + server settings instead of being stored

#### Current V2 schema direction
- `GameServerPorts` and resolved Web Host state are not stored in V2 and are expected to be derived from revision templates + server settings.
- `GameServerVolumes` are persisted as immutable per-server snapshots resolved from `GameTypeVolume` templates and keyed `MountTypeConfig` rows.
- Port availability validation is handled by backend services at deployment/update time, not persisted in V2 metadata.
- Setting-to-port relationships are modeled through unified port mapping rules rather than duplicated link fields.
- V2 revision volume usage categories now use `config`, `saves`, `backups`, `gamefiles`, and `logs`.
- V2 setting metadata now supports a `yesno` data type for literal `yes`/`no` values in addition to `boolean` for `true`/`false` values.

#### V2 editor Web Host rules
- The V2 Web Hosts tab now guides authors toward relative, lowercase path segments and supports runtime placeholders such as `{serverId}`, `{name}`, `{serviceName}`, and `{gameType}`.
- The editor exposes a `From Name` helper to build a path segment from the Web Host name.
- Port Variable choices come from revision settings that already have a numeric default port and use a compatible numeric data type (`number` or `port`).
- When a Port Variable is selected, the Static Port field becomes a read-only preview of that setting's current default port instead of storing a second conflicting value.

#### V2 editor draft workflow
- Creating a new V2 GameType now starts with an unsaved revision draft already selected so revision fields are editable immediately.
- Saving a brand-new V2 GameType now creates the parent record first, then persists the current revision draft in the same ordered save flow when the draft has content and passes validation.
- The V2 editor now uses a single top-level Save action instead of separate GameType and revision save buttons.
- If the parent GameType saves but the revision draft still fails validation, the editor keeps the draft in place instead of navigating away and dropping the unsaved aggregate edits.
- The first saved revision is automatically set as current when the GameType does not already have one.
- Cross-tab validation summaries refresh immediately when ports, settings, volumes, or Web Hosts are edited from their respective tabs.
- New V2 settings now default their category to `General`, or reuse the currently selected/last-used category when one already exists.
- The Detection tab can now scan Docker image metadata before the first save for a new V2 GameType, and comparison remains deferred until a saved GameType/revision exists.
- Applying detected volumes now maps the detected container path into the volume `Source` field and infers a readable description plus usage category from that path.

#### V2 list actions
- The active `/gametypes-v2` list now includes row-level edit and delete actions for each GameType.
- The active `/gametypes-v2` list now also supports importing portable GameType JSON packages exported from the V2 editor.

#### V2 portable GameType packages
- V2 GameTypes can now be exported from the editor screen and imported from the V2 list page as portable JSON packages.
- Portable packages omit persisted integer ids for the GameType, revisions, ports, volumes, settings, metadata, port mappings, and web hosts.
- Nested revision relationships are preserved through JSON containment, child display order, and the exported `CurrentRevisionVersionTag` field.
- Import creates a new V2 GameType with nested revisions from the package and restores the current revision by version tag.
- Sample portable imports now live under `docs/samples/gametype-imports/`, including starter presets for Palworld Dedicated Server, Minecraft Bedrock Server, and Minecraft Java Server based on the referenced upstream Docker image documentation.

### ? GameType Editor

**Location:** `/gametypes/{key}` or `/gametypes/new`

**Tabs:**

1. **Basic Information**
   - Key (immutable after creation)
   - Display Name
   - Description
   - Docker Image
   - Thumbnail URL
   - Documentation URL

2. **Ports**
   - Define port mappings
   - Set default port (? marked with IsDefaultPort flag)
   - Specify protocol (tcp/udp)
   - Port validation

3. **Volumes**
   - Define volume mounts
   - Source and target paths
   - Volume driver configuration

4. **Default Settings**
   - Key-value environment variables
   - **Expandable cards** for each setting
   - **Extended Metadata** (per setting):
     - Description, Category, Display Order
     - Data Type (string, number, boolean, enum, list, port)
     - Required, Cannot Be Empty
     - Placeholder, Validation Pattern/Message
     - Allowed Values, Value Mappings
     - **Port Mapping Configuration**:
       - Maps to Container Port ?
       - Linked Container Port (which port in Ports list)
       - Port Protocol (tcp/udp)
     - **Port Validation**:
       - Min Port, Max Port
       - Check Availability
       - Reserved Ports
       - Is User Editable
     - **Port Relationships** (NEW!):
       - **Auto-Detect button**: Scans existing ports and creates relationships automatically
       - **Manual Add**: Define custom relationships
       - **Relationship Types**:
         - **Offset**: Target = Source + Offset (e.g., Query Port = Game Port + 1)
         - **Fixed**: Target always has a fixed value (e.g., RCON always at 27020)
         - **Multiplier**: Target = Source � Multiplier
       - **Validation**: Checks if target port exists in port definitions
       - **Visual warnings**: Red border and alert if target port not found
       - Per relationship fields:
         - Target Container Port
         - Protocol (tcp/udp)
         - Offset/Fixed Value (based on type)
         - Description
         - Required checkbox

---

## Port Management

### ?? Complete Port Management System

#### Port Definition (GameTypeRevision Level)
- Define ports in `GameTypeRevision.Ports`
- Mark the advertised/default port with `IsAdvertised = true`
- Example (Valheim):
  ```
  Port 2456 (udp) - Server Port
  Port 2457 (udp) - Connection Port [IsAdvertised: true] ?
  Port 2458 (udp) - Steam List Port
  ```

#### Port Mappings
**Defined in setting metadata for port-type settings**

Example (Valheim `SERVER_PORT`):
- Primary direct mapping to Container Port: 2456 (udp)
- Related mappings:
  1. Offset +1 ? Port 2457 (Connection Port) - Required
  2. Offset +2 ? Port 2458 (Steam List Port) - Required

**Behavior:**
- User changes SERVER_PORT from 2456 to 30000
- System automatically updates:
  - Port 30000 (udp) - Server Port
  - Port 30001 (udp) - Connection Port (auto-calculated)
  - Port 30002 (udp) - Steam List Port (auto-calculated)

#### Port Protocols
- **UDP**: All three values must match
  - Setting value = ContainerPort = PublishedPort
  - Any change updates all three
- **TCP**: Setting controls ContainerPort, PublishedPort can differ
  - Setting value = ContainerPort
  - PublishedPort only updated if it was matching ContainerPort

#### Default Port System
- Default port tracked by **index** in port list (stable across value changes)
- Default port used for:
  - Connection strings
  - Primary port display
  - Port Mapping Editor highlighting
- Visual indicators:
  - Green badge with "Default" text
  - Star icon (?)

---

## Monitoring & Logs

### ? Real-Time Resource Monitoring

**Components:**
- `ResourceMonitorRest` - Polling-based monitoring
- `ResourceMonitor` - SignalR real-time monitoring

**Metrics:**
- CPU usage
- Memory usage
- Replica health
- Service status
- Task information

### ? Log Streaming (Fully Implemented — Shared)

**Backend Hub:** `ServerLogsHub.cs`
- Location: `{API}/hubs/serverlogs`
- Container lookup: Docker labels (`gameserver.docker.Id`)
- Streaming: Shared `IServerLogAggregator` keeps one agent log stream per server and fans lines out to all subscribers
- Features:
  - Follow mode
  - Tail lines configuration
  - Timestamps
  - Clean output (8-byte header removal)
  - Multiple users viewing the same server see identical output

**Frontend Component:** `ServerLogsViewer.razor` *(legacy V1 component removed; shared logs are consumed by V2 pages)*
- Connects to API base URI (not Navigation URL)
- Configuration: `GameServerDockerApi:BaseUri`

### ? Interactive Terminal (Fully Implemented — Per-User)

**Backend Hub:** `ContainerConsoleHub` mapped at `{API}/hubs/terminal`
- Exec-based shell sessions (`/bin/sh` by default, configurable)
- One underlying agent WebSocket per SignalR connection
- Methods:
  - `StartExecSession(containerId, shell = "/bin/sh")`
  - `SendInput(sessionId, input)`
  - `Disconnect()`
- Container operations are routed through the registered Node Agent; the hub never talks directly to the Docker daemon

**Frontend Component:** `ContainerTerminal.razor`
- Full Xterm integration
- Matrix-style theme (green on black)
- Real-time bidirectional communication
- Auto-connect option
- Shell: `/bin/sh` (configurable)

### ? Shared Container Attach (Implemented)

**Backend Hub:** `ContainerAttachHub` mapped at `{API}/hubs/attach`
- Shared `IContainerAttachAggregator` keeps one agent attach WebSocket per container ID and fans output frames out to all subscribers
- Input is accepted from one subscriber at a time:
  - The first user to send input becomes the controller
  - Late joiners receive an `InputControlledBy(connectionId)` frame and see a view-only indicator
  - When the controller disconnects, control is released
- Methods:
  - `SubscribeToContainer(serverId, containerId?, timestamps = false)` — returns `IAsyncEnumerable<string>` frames
  - `SendInput(containerId, input)` — only sent by the server when the caller is the controller
  - `DisconnectFromContainer(containerId)`

**Frontend Component:** `ContainerConsole.razor`
- Connects to `{API}/hubs/attach`
- Badges indicate Connected, Input Control, or View-only
- `DisableStdin` is toggled when control changes

---

## Architecture

### Technology Stack

**Frontend:**
- Blazor Server (.NET 10)
- Radzen Blazor Components
- SignalR (real-time updates)
- XtermBlazor (terminal emulation)

**Backend:**
- ASP.NET Core Web API
- Docker.DotNet (Docker API client)
- Entity Framework Core
  - V2 persistence: SQLite (default), MySQL (supported), or PostgreSQL (coming soon) based on configuration
- SignalR Hubs

**Infrastructure:**
- Docker Swarm
- V2 persistence: SQLite (default), MySQL (supported), PostgreSQL (coming soon)
- Volume Drivers (local, NFS)

### Persistence Architecture

#### Current state
- The V2 persistence layer is the only active persistence path.
- It follows the normalized schema documented in `docs/reference/V2-Database-Diagram.md`.

#### V2 schema highlights
- `GameType` is the catalog root and owns the fixed Docker image reference.
- `GameTypeRevision` owns ports, volumes, setting definitions, setting metadata, setting port mappings, and Web Host definitions.
- `GameServer` stores only server-specific data such as selected revision, desired settings, service identity, and deployment status.
- Web Host output is deterministic from `GameTypeWebHosts` + `GameServerSettings` and is not persisted as a separate V2 table.

### SignalR Hubs

1. **ContainerAttachHub** (`/hubs/attach`) — **shared multi-subscriber**
   - TTY-attached console (main process stdin/stdout)
   - Shared by all viewers of the same container
   - First-typist-wins input control
   - Implementation: `src/GameServer.Docker/Hubs/ContainerAttachHub.cs`

2. **ContainerConsoleHub** (`/hubs/terminal`) — **per-user exec**
   - Exec-based interactive shell (`/bin/sh`)
   - One agent WebSocket per SignalR connection
   - Implementation: `src/GameServer.Docker/Hubs/ContainerConsoleHub.cs`

3. **ServerLogsHub** (`/hubs/serverlogs`) — **shared multi-subscriber**
   - Container log streaming via `IServerLogAggregator`
   - Read-only logs; all viewers see the same lines

4. **ResourceMonitoringHub** (`/hubs/resources`) — **shared multi-subscriber**
   - Real-time resource metrics via `IServerResourceAggregator`

5. **AgentRegistrationHub** (`/hubs/agentregistration`)
   - Push registration, heartbeats, and container/agent mapping updates from Node Agents

### Docker Label System

All containers are tagged with labels for easy lookup:
```csharp
["gameserver.docker.managed"] = "true"
["gameserver.docker.Id"] = serverId
["gameserver.docker.name"] = serverName
["gameserver.docker.description"] = description
["gameserver.docker.gametype"] = gameType
```

Container lookup by label:
```csharp
await dockerClient.Containers.ListContainersAsync(new ContainersListParameters
{
    Filters = new Dictionary<string, IDictionary<string, bool>>
    {
        ["label"] = new Dictionary<string, bool>
        {
            ["gameserver.docker.Id=xyz"] = true
        }
    }
});
```

---

## V2 GameType System

### V2 GameType Manager

**Location:** `/gametypes-v2`  
**Component:** `GameTypeManagerV2.razor`

- List all V2 GameTypes with edit and delete actions per row
- Import portable GameType JSON packages (upload button)
- Navigate to create new or edit existing V2 GameTypes

### V2 GameType Editor

**Location:** `/gametypes-v2/new` or `/gametypes-v2/{key}`  
**Component:** `GameTypeDetailsV2.razor` (coordinator) + child components under `Components/Pages/GameTypes/Components/V2/`

**10-tab workflow:**

| Tab | Component | Purpose |
|-----|-----------|---------|
| Basic | `GameTypeBasicInfoV2Editor` | Key, name, type, thumbnail URL, documentation URL, active flag |
| Revisions | `GameTypeRevisionEditor` | Draft metadata, revision list, publish and set-current actions |
| Ports | `GameTypeRevisionPortsEditor` | Container port definitions, advertised port |
| Volumes | `GameTypeRevisionVolumesEditor` | Volume paths, usage categories (`config`, `saves`, `backups`, `gamefiles`, `logs`) |
| Settings | `GameTypeRevisionSettingsEditor` | Setting definitions with categories, data types, and port mapping rules |
| Web Hosts | `GameTypeRevisionWebHostsEditor` | Web endpoint rules with static port or port variable |
| Detection | `GameTypeRevisionDetectionEditor` | Scan Docker image metadata and compare against saved revision |
| Review | `GameTypeRevisionReviewEditor` | Draft summary, diff vs saved revision, cross-tab validation |
| Save | _(Save action)_ | Single top-level save: creates GameType + revision together |
| Publish | _(Publish action)_ | Publish revision and optionally set it as current |

**Draft workflow:**
- New GameTypes start with an unsaved draft revision already selected — all tabs are editable immediately.
- Single **Save** action persists both the parent GameType and the draft revision in one ordered flow.
- The first saved revision is automatically set as current.
- Cross-tab validation summaries refresh live as edits happen in any tab.
- New settings default their category to `General` (or the last-used category).
- Detection tab can scan Docker image metadata before the first save; comparison requires a saved revision.

**V2 Portable Packages:**
- Export a GameType as a self-contained JSON package (no integer IDs) via the editor screen.
- Import packages from the `/gametypes-v2` list page.
- Nested revision relationships, port mappings, volumes, settings, and web hosts are all preserved.
- Sample presets for Minecraft Bedrock, Minecraft Java, and Palworld Dedicated Server live under `docs/samples/gametype-imports/`.

### V2 GameServer Manager

**Location:** `/gameservers-v2`  
**Component:** `GameServerManagerV2.razor`

- List V2 servers with resolved port information and revision details
- Navigate to create new or view existing V2 servers
- `?includeDeleted=false` filter (soft delete support)

### V2 GameServer Detail

**Location:** `/gameservers-v2/{serverId}`  
**Component:** `GameServerDetailsV2.razor`

- Server identity: name, service name, status, lifecycle timestamps
- Selected revision info (version tag, image reference)
- Soft-delete flag (`IsDeleted`)

### V2 GameServer Editor

**Location:** `/gameservers-v2/new`  
**Component:** `GameServerEditorV2.razor`

- Select a published V2 GameType revision
- Provide per-server setting overrides via `GameServerSettingFieldV2`
- Port mappings and volumes are derived from the selected revision at deployment time — not configured per-server
- Validation via `POST /api/v2/gameservers/validate` before create

### V2 Data Model Summary

| Entity | Responsibility |
|--------|---------------|
| `GameType` | Catalog identity: key, name, type, thumbnail, docs, active flag, current revision pointer |
| `GameTypeRevision` | Deployable template: image reference, version tag, digest, TTY, ports, volumes, settings, web hosts |
| `GameServer` | Deployment intent: `GameTypeRevisionId`, name, service name, status, per-server setting overrides |
| `GameServerSettings` | Per-server environment variable overrides only |
| `GameTypeWebHost` | Web endpoint definitions (resolved at runtime from settings, not persisted per-server) |

**Key V2 differences from V1:**
- `ImageReference` lives on `GameTypeRevision`, not `GameType`
- `GameServerPorts` are not stored; derived from revision at deploy time
- `GameServerVolumes` are stored as immutable per-server snapshots resolved from revision volume templates + `MountTypeConfig`
- Port mapping rules use unified `MappingRole` + `RelationType` + `CalculationValue` model
- New `yesno` DataType for literal `yes`/`no` environment variable values
- Soft delete on `GameServer` via `IsDeleted` flag

---

## API Endpoints

### GameServer.Docker API

Base URL: Configured in `appsettings.json` under `GameServerDockerApi:BaseUri`

#### V2 Game Types
- `GET /api/v2/gametypes` - List V2 game types (`?includeInactive=false`)
- `GET /api/v2/gametypes/{key}` - Get V2 game type detail
- `POST /api/v2/gametypes` - Create V2 game type
- `PUT /api/v2/gametypes/{key}` - Update V2 game type
- `DELETE /api/v2/gametypes/{key}` - Delete V2 game type
- `GET /api/v2/gametypes/{key}/export` - Export portable JSON package
- `POST /api/v2/gametypes/import` - Import portable JSON package

#### V2 GameType Revisions
- `POST /api/v2/gametypes/{key}/revisions` - Add revision
- `PUT /api/v2/gametypes/{key}/revisions/{revisionId}` - Update revision
- `POST /api/v2/gametypes/{key}/revisions/{revisionId}/publish` - Publish revision
- `POST /api/v2/gametypes/{key}/revisions/{revisionId}/set-current` - Set as current revision

#### V2 GameType Detection
- `POST /api/v2/gametypes/detection/scan-tag` - Scan Docker image metadata (no saved key)
- `POST /api/v2/gametypes/{key}/detection/scan-tag` - Scan for saved key
- `POST /api/v2/gametypes/{key}/detection/compare` - Compare detected metadata vs saved revision

#### V2 Game Servers
- `GET /api/v2/gameservers` - List V2 game servers (`?includeDeleted=false`)
- `GET /api/v2/gameservers/{serverId}` - Get V2 game server detail
- `POST /api/v2/gameservers/validate` - Validate a server creation request
- `POST /api/v2/gameservers` - Create V2 game server
- `PUT /api/v2/gameservers/{serverId}` - Update V2 game server

---

## Configuration

### appsettings.json

```json
{
  "GameServerDockerApi": {
    "BaseUri": "http://192.168.10.50:5163/"
  },
  "ConnectionStrings": {
    "GameServerDb": "Data Source=gameserver.db"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

### Environment Variables (Container)

Configured per game type in DefaultSettings. Examples:

**Minecraft:**
- `EULA`, `SERVER_PORT`, `MAX_PLAYERS`, `DIFFICULTY`, etc.

**Valheim:**
- `SERVER_NAME`, `SERVER_PORT`, `WORLD_NAME`, `SERVER_PASS`, etc.

---

## Known Limitations & Future Work

### Current Limitations
1. `GameServerVolume` snapshots are immutable after creation
2. Create-server wizard Step 4 shows a read-only volume preview; per-volume source/driver selection is not configurable at creation time because values are resolved from `MountTypeConfig` templates
3. File manager supports browse, upload, download, and text editing; advanced folder operations are still limited

### Implemented but not battle-tested
- Multi-node Docker Swarm support with Node Agent registration is implemented (see [Quick Start](QUICK-START.md)). It requires at least one manager Node Agent with the `services`/`tasks`/`nodes`/`swarm` capabilities and an overlay network shared by Primary Service and agents. Manual test plans are available in `docs/testing/Manual-MultiNode-Swarm-Test-Plan.md`.

### Planned Features
- [ ] Backup/restore functionality
- [ ] Scheduled tasks and automated restarts
- [ ] Plugin/mod management
- [ ] User authentication and permissions
- [ ] Database backups
- [ ] Notification system

---

## Testing

### Manual Testing Checklist

**Server Creation:**
- [ ] Create server with default settings
- [ ] Create server with port relationships (Valheim)
- [ ] Verify default port selection in Technical Details
- [ ] Verify default port in Review step
- [ ] Check connection string uses default port

**Server Details:**
- [ ] Verify default port in Network section
- [ ] Check port mappings display with star
- [ ] Test log streaming (shared across multiple clients)
- [ ] Test terminal (/hubs/terminal, per-user exec)
- [ ] Test container attach (/hubs/attach, shared across multiple clients)
- [ ] Verify resource monitoring (shared across multiple clients)

**GameType Editor:**
- [ ] Create new game type with ports
- [ ] Set default port
- [ ] Add port relationships
- [ ] Use Auto-Detect for relationships
- [ ] Verify validation warnings
- [ ] Save and reload

---

## Troubleshooting

### Logs Not Streaming
- Check API base URI configuration
- Verify container ID lookup (check labels)
- Check SignalR hub is running at `{API}/hubs/serverlogs`
- If multiple viewers see different content, ensure they requested the same `tailLines` and filters
- Look for errors in browser console

### Container Attach Not Streaming
- Confirm the container is running and the agent exposes `/containers/{id}/attach/ws`
- Check that the client connects to `{API}/hubs/attach`, not the old `/hubs/console`
- Verify a controlling user exists before sending input (`SendInput` returns false for non-controllers)
- Look for `InputControlledBy` frames in the browser console

### Default Port Not Showing
- Verify `IsAdvertised = true` is set on one port in the revision's `Ports` list
- Check port order matches between the revision and the server
- Verify the `GameTypeRevisionDetailDto` is passed to components

### Port Mappings Not Working
- Verify the setting's metadata `DataType` is `"port"`
- Check the primary direct mapping targets an existing revision port
- Verify related offset/multiplier mappings derive from the primary mapping
- Check target ports exist in the revision's port definitions

---

## References

- [Docker.DotNet Documentation](https://github.com/dotnet/Docker.DotNet)
- [Radzen Blazor Components](https://blazor.radzen.com/)
- [XtermBlazor](https://github.com/WhitWaldo/XtermBlazor)
- [Docker Swarm Documentation](https://docs.docker.com/engine/swarm/)

---

**Document Maintainer:** System  
**Last Review:** Current Session  
**Status:** ? Up to Date
