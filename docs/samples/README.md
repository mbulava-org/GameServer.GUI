# GameServer Samples and Stack Configurations

This directory contains starter deployment configurations, Docker stack specifications, and portable GameType import packages for the GameServer management system.

---

## 📁 Sample Configurations & Stacks

| File | Type | Target Environment | Description |
|------|------|--------------------|-------------|
| **[`docker-stack.yml`](docker-stack.yml)** | Docker Swarm | Production / Staging | Standard unified Swarm stack (PostgreSQL, `GameServer.API`, `GameServer.Web`, and global `GameServer.Docker.Agent`). |
| **[`docker-stack.modular.yml`](docker-stack.modular.yml)** | Docker Swarm | Distributed / Multi-Service | Expanded microservices Swarm stack with dedicated `GameServer.Orchestration.Host` and `GameServer.Monitoring.Host` services. |
| **[`docker-compose.local.yml`](docker-compose.local.yml)** | Docker Compose | Local Testing | Local compose file for quick evaluation and integration testing. |
| **[`../../test-sample.yml`](../../test-sample.yml)** | Docker Swarm | Test Environment | Comprehensive integration test Swarm stack configuration. |

---

## 🎮 Portable GameType Imports

Located in the [`gametype-imports/`](gametype-imports/) directory:

| Preset Package | Upstream Image | Key Features & Included Extensions |
|----------------|----------------|------------------------------------|
| **[`palworld-dedicated.portable.json`](gametype-imports/palworld-dedicated.portable.json)** | `thijsvanloef/palworld-server-docker` | Palworld game/query/RCON/REST API ports, persistent volume, and UI extensions (`PalworldApiTab`, `RconTab`). |
| **[`minecraft-java.portable.json`](gametype-imports/minecraft-java.portable.json)** | `itzg/minecraft-server` | Standard Minecraft Java port, `/data` volume, and core server configuration options. |
| **[`minecraft-bedrock.portable.json`](gametype-imports/minecraft-bedrock.portable.json)** | `itzg/minecraft-bedrock-server` | Bedrock UDP port, `/data` volume, and Bedrock environment configurations. |

---

## ⚙️ Service Configuration Reference

### 1. GameServer.API (`mbulava/gameserver-api:latest`)

| Environment Variable | Default / Example | Purpose |
|----------------------|-------------------|---------|
| `V2Database__Provider` | `PostgreSql` or `Sqlite` | Database provider (`PostgreSql`, `Sqlite`, `MySql`). |
| `V2Database__ConnectionStringName` | `GameServerV2PostgresDb` | Active connection string key name. |
| `ConnectionStrings__GameServerV2PostgresDb` | `Host=...;Database=...` | PostgreSQL connection string for EF Core. |
| `PortAllocation__StartPort` | `25565` | Start of the port range allocated to managed game servers. |
| `PortAllocation__EndPort` | `35565` | End of the port range allocated to managed game servers. |
| `PortAllocation__ReservedPortRanges` | _(empty)_ | Excluded ports/ranges (e.g., `8080,9000-9100`). |
| `ServiceOperations__Mode` | `Agent` | Mode for container/service operations (`Agent` or `Local`). |
| `NodeAgentOptions__ServiceName` | `gameserver_gameserver-agent` | Swarm service name of the node agent daemon. |
| `NodeAgentOptions__NetworkName` | `gameserver-network` | Overlay network name shared between API and agents. |
| `NodeAgentOptions__Port` | `8080` | Internal agent listening port. |
| `GameTypeExtensions__AllowedAssemblies` | `GameServer.Web` | Assemblies allowed for UI extension tab resolution. |

### 2. GameServer.Web (`mbulava/gameserver-web:latest`)

| Environment Variable | Default / Example | Purpose |
|----------------------|-------------------|---------|
| `GameServerDockerApi__BaseUri` | `http://gameserver-api:8080/` | Base URI of the backend `GameServer.API` service. |
| `ASPNETCORE_ENVIRONMENT` | `Production` | ASP.NET Core environment. |

### 3. GameServer.Docker.Agent (`mbulava/gameserver-docker-agent:latest`)

| Environment Variable | Default / Example | Purpose |
|----------------------|-------------------|---------|
| `NODE_NAME` | `{{.Node.Hostname}}` | Hostname of the current Swarm node. |
| `NODE_ID` | `{{.Node.ID}}` | Swarm node ID. |
| `AgentRegistration__PrimaryServiceUrl` | `http://gameserver-api:8080/` | URL of the central API/Orchestration service for SignalR registration. |
| `AgentRegistration__HeartbeatIntervalSeconds` | `30` | Interval in seconds between heartbeat pings. |
| `AgentRegistration__Enabled` | `true` | Enables automatic agent registration. |

### 4. Standalone Hosts (Optional Microservices)

- **`GameServer.Orchestration.Host` (`mbulava/gameserver-orchestration:latest`)**: Standalone host for `/hubs/agentregistration`, agent discovery, and terminal session routing.
- **`GameServer.Monitoring.Host` (`mbulava/gameserver-monitoring:latest`)**: Standalone host for `/hubs/resources`, `/hubs/serverlogs`, and `/hubs/attach`.

> **Route parity:** Standalone hosts and `GameServer.API` expose identical hub route names, so a SignalR client can switch hosts by changing only its base URI. See the SignalR route map in [`docs/ARCHITECTURE.md`](../ARCHITECTURE.md).
>
> **Single-endpoint contract:** `GameServer.Web` always connects only to `gameserver-api`. In [`docker-stack.modular.yml`](docker-stack.modular.yml), `gameserver-api` runs with `ModularHosting__HostHubsInApi=false` + `ModularHosting__UseHttpAgentRegistry=true`, so it uses YARP to reverse-proxy hub traffic to `gameserver-orchestration` and `gameserver-monitoring` (via `OrchestrationService__BaseUrl` / `MonitoringService__BaseUrl`). The Web UI never learns the standalone hosts exist. Agents still register directly against `gameserver-orchestration` (see `AgentRegistration__PrimaryServiceUrl`).
