# GameServer.GUI

Blazor Server application for deploying and managing game servers on Docker Swarm.

## Features

- **V2 GameType System** — Revision-based GameType catalog with publish lifecycle, portable import/export packages, and Docker image detection
- **V2 GameServer Management** — Deploy game servers from published revisions; port/volume/web host config derived from the revision
- **Multi-node Docker Swarm** — Node Agents handle container operations on each Swarm node
- **Real-time monitoring** — Log streaming, interactive terminal, and resource metrics via SignalR
- **Dual database support** — V2 path uses PostgreSQL (default); legacy V1 path uses SQLite

## Quick Start

See **[docs/QUICK-START.md](docs/QUICK-START.md)** for local development and Docker Swarm deployment instructions.

## Documentation

| Document | Description |
|----------|-------------|
| [docs/QUICK-START.md](docs/QUICK-START.md) | Local dev setup, Swarm deployment, V2 workflow |
| [docs/CURRENT-FEATURES.md](docs/CURRENT-FEATURES.md) | Full feature inventory, API endpoints, V2 pages |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Architecture patterns, critical rules, persistence layers |
| [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md) | Dev setup, coding standards, PR process |
| [docs/guides/V2-GameType-Assembly-Instructions.md](docs/guides/V2-GameType-Assembly-Instructions.md) | Step-by-step V2 GameType creation workflow |
| [docs/guides/V2-GameType-Editor-Components.md](docs/guides/V2-GameType-Editor-Components.md) | V2 editor component inventory and data flow |
| [docs/reference/V2-Database-Diagram.md](docs/reference/V2-Database-Diagram.md) | V2 schema ERD and table descriptions |
| [docs/samples/gametype-imports/](docs/samples/gametype-imports/) | Starter presets (Minecraft Java/Bedrock, Palworld) |

## Key Routes

| Route | Description |
|-------|-------------|
| `/gametypes-v2` | V2 GameType list (edit, delete, import) |
| `/gametypes-v2/new` | Create new V2 GameType |
| `/gametypes-v2/{key}` | Edit V2 GameType |
| `/gameservers-v2` | V2 GameServer list |
| `/gameservers-v2/new` | Create V2 game server |
| `/gameservers-v2/{serverId}` | V2 GameServer detail |
| `/gametypes` | V1 (legacy) GameType manager |
| `/servers` | V1 (legacy) server dashboard |

## API Base URLs

- **V2**: `/api/v2/gametypes`, `/api/v2/gameservers`
- **V1 (legacy)**: `/api/gametypes`, `/api/gameserver`
- **Swagger**: `http://localhost:5164/swagger`
