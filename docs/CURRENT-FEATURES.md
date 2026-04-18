# Game Server Manager - Current Features & Implementation

**Last Updated:** 2024  
**Version:** v0.2.0-beta  
**Branch:** port-mapping

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

**Location:** `/servers` (dashboard), `/servers/{id}` (details)

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

The application currently has **two persistence layers** for game type and server configuration:

#### Legacy persistence (active for current API flows)
- SQLite-backed EF Core persistence
- `Data/GameServerDbContext`
- `Repositories/IGameTypeRepository`
- still used by the current controllers, extended metadata flows, and most existing UI behavior

#### V2 persistence (implemented, separate from legacy, PostgreSQL-default)
- `Data/V2/GameServerV2DbContext`
- `Repositories/V2/IGameTypeRepository`
- `Repositories/V2/IGameServerRepository`
- provider-aware configuration supporting SQLite, PostgreSQL, and MySQL
- PostgreSQL is the default and preferred V2 path and is backed by the dedicated `src/GameServer.DB.PostgreSql` project plus `scripts/Deploy-V2PostgresDatabase.ps1`
- normalized schema with:
  - `GameType` owning a fixed `ImageReference`
  - `GameTypeRevision` owning tag-based deployable templates
  - `GameServer` storing only server-specific deployment intent via `GameTypeRevisionId`
  - derived Web Host state resolved from revision Web Host definitions + server settings instead of being stored

#### Current V2 schema direction
- `GameServerPorts` and `GameServerVolumes` are not stored in V2 and are expected to be derived from the selected revision.
- Port availability validation is handled by backend services at deployment/update time, not persisted in V2 metadata.
- Setting-to-port relationships are modeled through unified port mapping rules rather than duplicated link fields.

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

#### V2 list actions
- The active `/gametypes-v2` list now includes row-level edit and delete actions for each GameType.
- The active `/gametypes-v2` list now also supports importing portable GameType JSON packages exported from the V2 editor.

#### V2 portable GameType packages
- V2 GameTypes can now be exported from the editor screen and imported from the V2 list page as portable JSON packages.
- Portable packages omit persisted integer ids for the GameType, revisions, ports, volumes, settings, metadata, port mappings, and web hosts.
- Nested revision relationships are preserved through JSON containment, child display order, and the exported `CurrentRevisionVersionTag` field.
- Import creates a new V2 GameType with nested revisions from the package and restores the current revision by version tag.

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
         - **Multiplier**: Target = Source × Multiplier
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

#### Port Definition (GameType Level)
- Define ports in `GameTypeDefinition.Ports`
- Mark default port with `IsDefaultPort = true`
- Example (Valheim):
  ```
  Port 2456 (udp) - Server Port
  Port 2457 (udp) - Connection Port [IsDefaultPort: true] ?
  Port 2458 (udp) - Steam List Port
  ```

#### Port Relationships
**Defined in Extended Metadata for port-type settings**

Example (Valheim SERVER_PORT):
- Links to Container Port: 2456 (udp)
- Relationships:
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

### ? Log Streaming (Fully Implemented)

**Backend Hub:** `ServerLogsHub.cs`
- Location: `{API}/hubs/serverlogs`
- Container lookup: Docker labels (`gameserver.docker.Id`)
- Streaming: Docker.DotNet direct container logs
- Features:
  - Follow mode
  - Tail lines configuration
  - Timestamps
  - Clean output (8-byte header removal)

**Frontend Component:** `ServerLogsViewer.razor`
- Connects to API base URI (not Navigation URL)
- Configuration: `GameServerDockerApi:BaseUri`
- Features:
  - Stream/Stop buttons
  - Refresh, Clear
  - Auto-scroll toggle
  - Max lines configuration
  - Filter by text and log level
  - Download logs
  - Connection status indicators

### ? Interactive Terminal (NEW!)

**Backend Hub:** Expected at `{API}/hubs/terminal`
- Exec-based shell sessions
- Methods:
  - `StartExecSession(containerId, shell)`
  - `SendInput(sessionId, data)`
  - `StopExecSession(sessionId)`

**Frontend Component:** `ContainerTerminal.razor`
- Full Xterm integration
- Matrix-style theme (green on black)
- Real-time bidirectional communication
- Auto-connect option
- Shell: `/bin/sh` (configurable)

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
  - legacy persistence: SQLite
  - V2 persistence: SQLite or MySQL based on configuration
- SignalR Hubs

**Infrastructure:**
- Docker Swarm
- SQLite database for the current legacy persistence path
- optional MySQL support for the V2 persistence path
- Volume Drivers (local, NFS)

### Persistence Architecture

#### Current state
- The legacy repository and model set remains the active path for most existing controllers and UI flows.
- The V2 database implementation exists in parallel and follows the latest normalized schema from `docs/DATABASE-REORGANIZATION-PROPOSAL.md`.
- The application host initializes both persistence paths so migration can happen incrementally.

#### V2 schema highlights
- `GameType` is the catalog root and owns the fixed Docker image reference.
- `GameTypeRevision` owns ports, volumes, setting definitions, setting metadata, setting port mappings, and Web Host definitions.
- `GameServer` stores only server-specific data such as selected revision, desired settings, service identity, and deployment status.
- Web Host output is deterministic from `GameTypeWebHosts` + `GameServerSettings` and is not persisted as a separate V2 table.

### SignalR Hubs

1. **ContainerConsoleHub** (`/hubs/console`)
   - TTY-attached console
   - Main process stdin/stdout

2. **ServerLogsHub** (`/hubs/serverlogs`)
   - Container log streaming
   - Read-only logs

3. **ResourceMonitoringHub** (`/hubs/resources`)
   - Real-time resource metrics

4. **ContainerTerminalHub** (`/hubs/terminal`)
   - Exec-based interactive shell
   - Read-write terminal

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

## API Endpoints

### GameServer.Docker API

Base URL: Configured in `appsettings.json` under `GameServerDockerApi:BaseUri`

#### Game Types
- `GET /api/gametypes` - List all game types
- `GET /api/gametypes/{key}` - Get game type by key
- `POST /api/gametypes` - Create game type
- `PUT /api/gametypes/{key}` - Update game type
- `DELETE /api/gametypes/{key}` - Delete game type

#### Extended Metadata
- `GET /api/gametypes/extended/{key}` - Get extended metadata
- `PUT /api/gametypes/extended/{key}` - Update extended metadata

#### Game Servers
- `GET /api/gameserver` - List all servers
- `GET /api/gameserver/{id}` - Get server by ID
- `POST /api/gameserver` - Create server
- `PUT /api/gameserver/{id}` - Update server
- `DELETE /api/gameserver/{id}` - Delete server
- `POST /api/gameserver/{id}/start` - Start server
- `POST /api/gameserver/{id}/stop` - Stop server

#### Resource Usage
- `GET /api/gameserver/{id}/usage` - Get resource usage

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
1. Volume configuration is immutable after creation
2. No multi-node swarm support yet (single-node Docker assumed)
3. Terminal hub (`/hubs/terminal`) needs to be implemented
4. File manager has limited features

### Planned Features
- [ ] Backup/restore functionality
- [ ] Scheduled tasks and automated restarts
- [ ] Plugin/mod management
- [ ] User authentication and permissions
- [ ] Multi-node swarm support
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
- [ ] Test log streaming
- [ ] Test terminal (when hub implemented)
- [ ] Verify resource monitoring

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
- Look for errors in browser console

### Default Port Not Showing
- Verify `IsDefaultPort = true` set in GameType
- Check port order matches between definition and server
- Verify GameTypeDefinition passed to components

### Port Relationships Not Working
- Verify metadata has `MapsToContainerPort = true`
- Check `LinkedContainerPort` matches port in Ports list
- Verify `PortRelationships` array is populated
- Check target ports exist in port definitions

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
