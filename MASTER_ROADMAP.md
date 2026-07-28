# GameServer.Docker — Master Roadmap & Navigation Guide

> **Single source of truth** for project status, implemented features, planned work, and documentation links.  
> Last updated: 2026-05-20

---

## 🗺️ Quick Navigation

| I want to… | Go to |
|---|---|
| Get the project running locally | [Quick Start Guide](docs/QUICK-START.md) |
| Understand the system architecture | [Architecture Overview](docs/ARCHITECTURE.md) |
| Learn about node agents | [Agent Architecture](docs/architecture/Agent-Architecture.md) · [Agent Quick Start](docs/guides/Agent-QuickStart.md) |
| Add a new game type | [GameType Metadata Guide](docs/guides/GameType-Metadata-Complete-Guide.md) · [GameType Editor Guide](docs/guides/GameType-Editor-Complete-Functionality-Guide.md) |
| Configure port mapping | [Port Mapping Integration Guide](docs/guides/Port-Mapping-Integration-Guide.md) |
| Work on the database | [DB Initialization](docs/guides/DATABASE-INITIALIZATION.md) · [SQLite Schema](docs/reference/SQLite-GameType-Database-Schema.md) |
| Run the tests | [Testing Quick Reference](docs/TESTING-QUICK-REFERENCE.md) |
| See all API conventions | [Constants & Conventions](docs/reference/CONSTANTS-AND-CONVENTIONS.md) · [Quick Reference Card](docs/reference/QUICK-REFERENCE-CARD.md) |
| Browse API examples | [Port Examples JSON](docs/reference/Game-Server-Port-Examples.json) |
| Understand security model | [Agent Security](docs/architecture/Agent-Security.md) |

---

## 📁 Project Structure

```
GameServer.GUI/
├── src/
│   ├── GameServer.Docker/          # REST API & orchestration layer (port 5164 dev / 8080 docker)
│   ├── GameServer.Web/             # Blazor Server UI (port 5102 dev / 8080 docker)
│   ├── GameServer.Docker.Agent/    # Node agent (deploys to each Docker node)
│   ├── GameServer.Docker.Client/   # Shared HTTP client library
│   ├── GameServer.DB.PostgreSql/   # PostgreSQL DB provider (alternative to SQLite)
│   └── Services/                   # Shared service abstractions
├── docs/                           # All documentation (see index below)
├── tests/                          # Automated test suite
└── scripts/                        # Dev & ops helper scripts
```

---

## ✅ Implemented Features

### Core Infrastructure
| Feature | Status | Key Files / Notes |
|---|---|---|
| Multi-node Docker Swarm support | ✅ Done | Push-based registration via SignalR |
| Node Agent (push-based registration) | ✅ Done | `GameServer.Docker.Agent`, registers via `/hubs/agentregistration` |
| Agent heartbeats (30s interval) | ✅ Done | Real-time health tracking |
| Agent capability filtering | ✅ Done | Agents report capabilities; orchestrator routes accordingly |
| SQLite persistent storage | ✅ Done | Auto-created on first run; `--seed-database` flag |
| SQLite V2 support | ✅ Done | Default V2 provider |
| MySQL V2 support | ✅ Done | Configuration-driven |
| PostgreSQL V2 support | 🔲 Planned | `GameServer.DB.PostgreSql` project exists but not fully implemented |
| Background DB initialization | ✅ Done | Non-blocking startup |
| OpenAPI / Swagger / Scalar docs | ✅ Done | Available at `/swagger` |
| Serilog structured logging | ✅ Done | |

### Server Management
| Feature | Status | Key Files / Notes |
|---|---|---|
| Server creation wizard (5 steps) | ✅ Done | `CreateServerWizard.razor` · route `/servers/new` |
| Server list / dashboard | ✅ Done | `/servers` |
| Server detail page (tabbed) | ✅ Done | `/servers/{id}` |
| Start / Stop / Delete servers | ✅ Done | Quick actions on dashboard |
| Real-time status updates (SignalR) | ✅ Done | Live container stats |
| Live log streaming | ✅ Done | SignalR |
| Terminal / console access | ✅ Done | In-browser terminal tab |

### GameType System
| Feature | Status | Key Files / Notes |
|---|---|---|
| Built-in game templates | ✅ Done | Minecraft, Valheim, and others |
| GameType editor UI | ✅ Done | See [editor guide](docs/guides/GameType-Editor-Complete-Functionality-Guide.md) |
| Extended metadata / V2 schema | ✅ Done | [Metadata guide](docs/guides/GameType-Metadata-Complete-Guide.md) |
| Per-setting DataType inference | ✅ Done | Auto-inferred when metadata absent |
| Tabbed settings interface | ✅ Done | Grouped by category |
| Required settings validation | ✅ Done | Red asterisk (*) for required fields |
| V2 GameType assembly | ✅ Done | [Assembly instructions](docs/guides/V2-GameType-Assembly-Instructions.md) |

### Port Management
| Feature | Status | Key Files / Notes |
|---|---|---|
| Intelligent port mapping | ✅ Done | Offset, Fixed, and Multiplier relationships |
| Default port designation | ✅ Done | Green badge + ⭐ in wizard and detail UI |
| Auto-calculated related ports | ✅ Done | "Auto-calculated" label in wizard Step 4 |
| UDP port handling | ✅ Done | Setting = ContainerPort = PublishedPort |
| Port relationship validation | ✅ Done | Valheim and other multi-port games |
| Connection string auto-generation | ✅ Done | Uses default port |

### Networking & Web Hosts
| Feature | Status | Key Files / Notes |
|---|---|---|
| Reverse proxy configuration | ✅ Done | [Reverse proxy guide](docs/REVERSE-PROXY-CONFIGURATION.md) |
| Web host / virtual host management | ✅ Done | Edit-server integration |
| Network and load-balancer config | ✅ Done | [Network config guide](docs/NETWORK-AND-LOADBALANCER-CONFIG.md) |
| Overlay network (Swarm) | ✅ Done | [Why overlay?](docs/architecture/Agent-Why-Overlay-Network.md) |

### Settings & Configuration
| Feature | Status | Key Files / Notes |
|---|---|---|
| Settings tab (UI) | ✅ Done | |
| Configuration consolidation | ✅ Done | Single `appsettings.json` source of truth |
| File upload feature | ✅ Done | |
| Environment-specific overrides | ✅ Done | |

---

## 🚧 Planned / In-Progress Features

| Feature | Status | Notes |
|---|---|---|
| Volume management in wizard | 🔲 Planned | Step 4 "Volumes" tab shows "Coming soon" |
| User authentication & RBAC | 🔲 Planned | No auth layer yet |
| Multi-tenant / team support | 🔲 Planned | |
| Automatic backups | 🔲 Planned | |
| Server update / upgrade workflow | 🔲 Planned | |
| Metrics / performance dashboard | 🔲 Planned | Real-time stats exist; historical graphs planned |
| Plugin / mod management | 🔲 Planned | |
| Mobile-responsive improvements | 🔲 Planned | |
| Documentation cleanup & consolidation | 🔲 Planned | See [cleanup plan](docs/DOCUMENTATION-CLEANUP-PLAN.md) — 70+ obsolete files to remove |

---

## ⚠️ Known Issues & Limitations

| Issue | Severity | Reference |
|---|---|---|
| Volume management not yet implemented | Medium | Wizard Step 4 placeholder |
| No authentication / authorization | High | All endpoints are open; intended for private networks only |
| Agent discovery requires pre-configured `PrimaryServiceUrl` | Low | See [Agent Quick Start](docs/guides/Agent-QuickStart.md) |
| Docs folder contains 70+ obsolete files from development iterations | Low | [Cleanup plan](docs/DOCUMENTATION-CLEANUP-PLAN.md) exists; not yet executed |
| PostgreSQL provider exists but SQLite is the default | Low | Switch via connection string config |

---

## 📚 Documentation Index

### Root-Level Docs
| File | Purpose |
|---|---|
| [README.md](README.md) | Project intro (minimal — see `docs/`) |
| [docs/README.md](docs/README.md) | Full project README / getting started |
| [docs/QUICK-START.md](docs/QUICK-START.md) | Dev setup + first deployment |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | System architecture & mandatory patterns |
| [docs/CURRENT-FEATURES.md](docs/CURRENT-FEATURES.md) | Detailed feature reference |
| [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md) | Contribution guidelines |
| [docs/TESTING-QUICK-REFERENCE.md](docs/TESTING-QUICK-REFERENCE.md) | How to run tests |

### Architecture (`docs/architecture/`)
| File | Purpose |
|---|---|
| [Agent-Architecture.md](docs/architecture/Agent-Architecture.md) | Node agent design & registration flow |
| [Agent-README.md](docs/architecture/Agent-README.md) | Agent overview |
| [Agent-Security.md](docs/architecture/Agent-Security.md) | Security model for agent communication |
| [Agent-Why-Overlay-Network.md](docs/architecture/Agent-Why-Overlay-Network.md) | Rationale for Docker overlay networking |
| [PERFORMANCE-OPTIMIZATIONS.md](docs/architecture/PERFORMANCE-OPTIMIZATIONS.md) | Performance decisions |

### Guides (`docs/guides/`)
| File | Purpose |
|---|---|
| [Agent-QuickStart.md](docs/guides/Agent-QuickStart.md) | Deploy a node agent |
| [Agent-Registration-Flow.md](docs/guides/Agent-Registration-Flow.md) | Push-based agent registration and heartbeats |
| [DATABASE-INITIALIZATION.md](docs/guides/DATABASE-INITIALIZATION.md) | DB setup & seeding |
| [File-Manager.md](docs/guides/File-Manager.md) | Browse and edit server files |
| [GameType-Editor-Complete-Functionality-Guide.md](docs/guides/GameType-Editor-Complete-Functionality-Guide.md) | Using the GameType editor |
| [GameType-Metadata-Complete-Guide.md](docs/guides/GameType-Metadata-Complete-Guide.md) | Extended metadata system |
| [Port-Mapping-Integration-Guide.md](docs/guides/Port-Mapping-Integration-Guide.md) | Port relationships & configuration |
| [Terminal-And-Console.md](docs/guides/Terminal-And-Console.md) | Interactive terminal and TTY console usage |
| [V2-GameServer-Lifecycle.md](docs/guides/V2-GameServer-Lifecycle.md) | Create, edit, and view V2 game servers |
| [V2-GameType-Assembly-Instructions.md](docs/guides/V2-GameType-Assembly-Instructions.md) | Building V2 game type packages |
| [V2-GameType-Editor-Components.md](docs/guides/V2-GameType-Editor-Components.md) | V2 editor component reference |
| [V2-Ports-And-WebHosts.md](docs/guides/V2-Ports-And-WebHosts.md) | Configure revision ports, port mappings, and web hosts |

### Reference (`docs/reference/`)
| File | Purpose |
|---|---|
| [CONSTANTS-AND-CONVENTIONS.md](docs/reference/CONSTANTS-AND-CONVENTIONS.md) | Naming conventions & constants |
| [QUICK-REFERENCE-CARD.md](docs/reference/QUICK-REFERENCE-CARD.md) | At-a-glance command/config cheat sheet |
| [SQLite-GameType-Database-Schema.md](docs/reference/SQLite-GameType-Database-Schema.md) | Database schema reference |
| [V2-Database-Diagram.md](docs/reference/V2-Database-Diagram.md) | V2 DB entity diagram |
| [Game-Server-Port-Examples.json](docs/reference/Game-Server-Port-Examples.json) | Port mapping API examples |

---

## 🏃 Common Developer Tasks

### Start the application locally
```bash
# Clone and restore
git clone https://github.com/mbulava-org/GameServer.GUI.git
cd GameServer.GUI
dotnet restore

# Initialize Docker Swarm (once)
docker swarm init

# Run the API
cd src/GameServer.Docker && dotnet run

# Run the Web UI (separate terminal)
cd src/GameServer.Web && dotnet run
```

### Run the tests
```bash
dotnet test
# Or see docs/TESTING-QUICK-REFERENCE.md for targeted test runs
```

### Add a new game type
1. Follow [GameType Metadata Complete Guide](docs/guides/GameType-Metadata-Complete-Guide.md)
2. Define ports using [Port Mapping Integration Guide](docs/guides/Port-Mapping-Integration-Guide.md)
3. Use [V2 Assembly Instructions](docs/guides/V2-GameType-Assembly-Instructions.md) to package

### Deploy a node agent
Follow [Agent Quick Start](docs/guides/Agent-QuickStart.md). Set `AgentRegistration:PrimaryServiceUrl` in the agent's `appsettings.json`.

### Seed the database
```bash
cd src/GameServer.Docker
dotnet run -- --seed-database
```

---

## 🏗️ Architecture at a Glance

```
┌──────────────────────────────────┐
│       GameServer.Web             │
│     (Blazor Server UI)           │
│   Port: 5102 dev / 8080 prod     │
└───────────────┬──────────────────┘
                │ HTTP
┌───────────────▼──────────────────┐
│      GameServer.Docker           │
│   (REST API + Orchestration)     │
│   Port: 5164 dev / 8080 prod     │
│   SQLite DB + AgentRegistry      │
└──────┬──────────────┬────────────┘
       │ SignalR       │ SignalR
┌──────▼──────┐  ┌────▼────────┐
│  Agent #1   │  │  Agent #N   │
│ (Docker     │  │ (Docker     │
│  Socket)    │  │  Socket)    │
└─────────────┘  └─────────────┘
```

- **Agents** push-register via SignalR hub on startup and send heartbeats every 30s  
- **Primary service** maintains an in-memory `AgentRegistry` — O(1) container→agent lookups  
- **Blazor UI** consumes the REST API and receives real-time updates via SignalR

---

*This document is the master navigation guide. For deep dives, follow the links above.*
