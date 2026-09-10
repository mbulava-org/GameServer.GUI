# GameServer.Windows.Agent

`GameServer.Windows.Agent` is a dedicated, lightweight ASP.NET Core Web API and Windows Service designed to run natively on Windows host machines. It interfaces with the **SteamCMD CLI** and provides complete process lifecycle management, real-time log streaming, interactive console commands, and host diagnostics for Windows-hosted dedicated game servers.

This agent is kept completely decoupled from the Docker Swarm node agents (`GameServer.Docker.Agent`), enabling the system to orchestrate and monitor games running directly on physical or virtual Windows servers alongside containerized servers.

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Key Features](#key-features)
3. [Project Structure](#project-structure)
4. [Communication with Primary API](#communication-with-primary-api)
5. [API Reference](#api-reference)
   - [SteamCMD Management](#steamcmd-management)
   - [Game Server Process Management](#game-server-process-management)
   - [File & Backup Management](#file--backup-management)
   - [Port & Resource Diagnostics](#port--resource-diagnostics)
   - [SignalR Streaming Hub](#signalr-streaming-hub)
6. [Configuration](#configuration)
7. [Installation & Hosting](#installation--hosting)
   - [Running as an Interactive Console App](#running-as-an-interactive-console-app)
   - [Installing as a Windows Service](#installing-as-a-windows-service)
8. [Troubleshooting](#troubleshooting)

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                            GameServer.GUI / Central                         │
│   ┌────────────────────────────────┐    ┌───────────────────────────────┐   │
│   │ GameServer.Web (Blazor UI)     │    │ GameServer.Docker / Primary   │   │
│   └───────────────▲────────────────┘    └───────────────▲───────────────┘   │
└───────────────────┼─────────────────────────────────────┼───────────────────┘
                    │                                     │ SignalR Push Registration
                    │ SignalR Streams & REST Calls        │ & Periodic Heartbeats
                    │ (Logs, Stats, Stdin Commands)       │ (/hubs/agentregistration)
┌───────────────────▼─────────────────────────────────────▼───────────────────┐
│                    GameServer.Windows.Agent (Windows Host)                  │
│                                                                             │
│  ┌───────────────────────┐  ┌───────────────────────┐  ┌─────────────────┐  │
│  │   REST Controllers    │  │   SignalR Hubs        │  │ Agent Registry  │  │
│  │   - SteamCmdController│  │   - WindowsAgentHub   │  │   Client        │  │
│  │   - ServersController │  │     (/hubs/windows-   │  │                 │  │
│  │   - FilesController   │  │      agent)           │  │                 │  │
│  │   - PortsController   │  │   - Real-time logs &  │  │                 │  │
│  │   - HealthController  │  │     CPU/RAM streams   │  │                 │  │
│  └───────────┬───────────┘  └───────────┬───────────┘  └────────┬────────┘  │
│              │                          │                       │           │
│  ┌───────────▼──────────────────────────▼───────────────────────▼────────┐  │
│  │                       Core Agent Services                             │  │
│  │  ┌───────────────────────────┐     ┌───────────────────────────────┐  │  │
│  │  │      SteamCmdService      │     │      GameProcessManager       │  │  │
│  │  │  - Auto-Download & Setup  │     │  - Win32 Job Objects          │  │  │
│  │  │  - App Install / Update   │     │  - Process Tree Supervision   │  │  │
│  │  │  - Output / Progress Parse│     │  - Graceful Stop & AutoRestart│  │  │
│  │  │  - Workshop Mod Downloads │     │  - Circular Log Ring Buffer   │  │  │
│  │  └─────────────┬─────────────┘     └───────────────┬───────────────┘  │  │
│  │                │                                   │                  │  │
│  │  ┌─────────────▼─────────────┐     ┌───────────────▼───────────────┐  │  │
│  │  │   WindowsResourceMonitor  │     │       WindowsPortService      │  │  │
│  │  │   - GlobalMemoryStatusEx  │     │   - IPGlobalProperties        │  │  │
│  │  │   - CPU & Disk Telemetry  │     │   - TCP/UDP Listener Query    │  │  │
│  │  └───────────────────────────┘     └───────────────────────────────┘  │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
│  ┌────────────────────────────────┐     ┌────────────────────────────────┐  │
│  │   steamcmd.exe (CLI Tool)      │     │  Native Windows Game Processes │  │
│  │   - C:\GameServers\_steamcmd\  │     │  - PalServer.exe               │  │
│  │   - Downloads / updates games  │     │  - ConanSandboxServer.exe      │  │
│  │                                │     │  - ShooterGameServer.exe       │  │
│  └────────────────────────────────┘     └────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Key Features

1. **SteamCMD CLI Integration**:
   - **Zero-Setup Bootstrapping**: If `steamcmd.exe` is missing from the host, the agent automatically downloads and extracts `steamcmd.zip` from Valve's official CDN.
   - **Install & Update Engine**: Executes `+force_install_dir`, `+login`, `+app_update`, `validate`, and beta branch switches.
   - **Real-Time Progress Tracking**: Regex parser intercepts stdout to emit download percentages, validation progress, and error codes over SignalR/REST.
   - **Workshop Mods**: Downloads Steam Workshop items via `+workshop_download_item`.

2. **Native Process Supervision & Win32 Job Objects**:
   - **Win32 Job Objects**: Wraps every spawned server and all child processes in a Job Object with `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`. When a server is stopped, all child executables (e.g. shipping binaries launched by batch files) are cleanly terminated without leaving orphaned processes.
   - **Graceful Shutdown**: Sends console `Ctrl+C` events and standard `quit` commands via stdin, falling back to process tree termination if the game process fails to exit before the timeout.
   - **Crash Detection & Auto-Restart**: Intercepts unexpected process exits, suppresses Windows Error Reporting dialogs (`SetErrorMode`), and applies exponential backoff auto-restart policies.

3. **Real-Time Logging & Interactive Console**:
   - **Circular Log Ring Buffer**: Retains recent log lines (configurable, e.g. 2,000 lines) per server in memory.
   - **SignalR Streaming**: Streams live stdout/stderr lines via `IAsyncEnumerable<string>`.
   - **Console Input & RCON**: Supports sending stdin commands or native Source RCON packets directly to running game servers.

4. **Host Telemetry & Diagnostics**:
   - **Port Inspection**: Uses `IPGlobalProperties` to query active TCP and UDP listeners on the Windows host before starting game servers to prevent port collisions.
   - **Resource Telemetry**: Queries physical RAM (`GlobalMemoryStatusEx`), process memory, CPU utilization %, and storage drive usage.
   - **File & Backup Manager**: Provides directory tree browsing, configuration editing (`.ini`, `.json`, `.cfg`), and automated zip backup creation and restoration.

5. **Windows Service Hosting**:
   - Built with `Microsoft.Extensions.Hosting.WindowsServices` to run as a standard Windows Service (`sc.exe create ...`) or interactively in a console.

---

## Project Structure

```
src/GameServer.Windows.Agent/
├── Configurations/
│   └── WindowsAgentOptions.cs       # Strongly-typed configuration options
├── Controllers/
│   ├── FilesController.cs           # Directory browsing, config editing, backups
│   ├── HealthController.cs          # Agent health & system overview
│   ├── PortsController.cs           # Active TCP/UDP port availability
│   ├── ServersController.cs         # Process start, stop, restart, stats, logs
│   └── SteamCmdController.cs        # SteamCMD install, update, and status
├── Hubs/
│   └── WindowsAgentHub.cs           # SignalR streaming for logs and stats
├── Interfaces/
│   ├── IFileManagerService.cs
│   ├── IGameProcessManager.cs
│   ├── ISteamCmdService.cs
│   ├── IWindowsPortService.cs
│   └── IWindowsResourceMonitor.cs
├── Models/
│   ├── GameProcessModels.cs         # Process status, requests, and metrics DTOs
│   └── SteamCmdModels.cs            # SteamCMD requests, progress events, DTOs
├── Native/
│   ├── JobObject.cs                 # Win32 Job Object P/Invoke wrapper
│   └── WindowsProcessHelper.cs      # SetErrorMode, SendCtrlC, taskkill helpers
├── Services/
│   ├── AgentRegistrationService.cs  # SignalR registration client to Primary API
│   ├── FileManagerService.cs        # File system operations & zip archives
│   ├── GameProcessManager.cs        # Core process lifecycle & supervision
│   ├── ProcessLogRingBuffer.cs      # Thread-safe circular buffer & fan-out
│   ├── RconClient.cs                # Source RCON protocol implementation
│   ├── SteamCmdOutputParser.cs      # Regex parser for SteamCMD stdout
│   ├── SteamCmdService.cs           # SteamCMD runner & downloader
│   ├── WindowsPortService.cs        # Network port listener checks
│   └── WindowsResourceMonitor.cs    # Host & process CPU/RAM telemetry
├── appsettings.json                 # Base configuration
├── appsettings.Development.json     # Development overrides
└── Program.cs                       # Application entry point & service wiring
```

---

## Communication with Primary API

The Windows Agent uses a **push-based registration and heartbeat pattern** to integrate with the Primary API (`GameServer.Docker`):

1. **Registration Handshake**:
   - On startup, the background `AgentRegistrationService` opens a SignalR connection to the Primary Service at:
     ```
     {PrimaryServiceUrl}/hubs/agentregistration
     ```
   - The agent invokes `RegisterAgent` with its identity payload:
     ```json
     {
       "nodeId": "win-gamestation-01",
       "nodeName": "GAMESTATION-01",
       "internalUrl": "http://192.168.1.150:5180",
       "capabilities": [
         "steamcmd",
         "windows-process",
         "process-exec",
         "stats",
         "logs",
         "files",
         "ports"
       ],
       "registeredAt": "2026-08-18T05:00:00Z",
       "isManagerNode": false,
       "hostType": "windows"
     }
     ```

2. **Periodic Heartbeats**:
   - Every 30 seconds (configurable), the agent sends a heartbeat containing all currently running server IDs:
     ```json
     {
       "nodeId": "win-gamestation-01",
       "containerIds": ["server-palworld-01", "server-valheim-01"],
       "health": "healthy",
       "timestamp": "2026-08-18T05:00:30Z"
     }
     ```

3. **Direct Operations & Real-Time Proxying**:
   - The Primary Service / Blazor UI routes user actions to the agent's `internalUrl`:
     - **Logs & Stats**: Connected via SignalR hub at `http://{AgentIP}:5180/hubs/windowsagent`.
     - **Process Commands & SteamCMD**: Executed via HTTP REST calls to `http://{AgentIP}:5180/api/...`.

---

## API Reference

### SteamCMD Management

| Method | Route | Description |
|---|---|---|
| `GET` | `/api/steamcmd/installed` | Returns `true` if `steamcmd.exe` exists on the host. |
| `POST` | `/api/steamcmd/ensure-installed` | Downloads and unpacks SteamCMD if missing. |
| `POST` | `/api/steamcmd/install` | Starts an install or update job for a Steam App ID. |
| `POST` | `/api/steamcmd/workshop/download` | Downloads a Steam Workshop item. |
| `GET` | `/api/steamcmd/apps/{appId}/status` | Inspects installation directory, file sizes, and `.exe` binaries. |

#### Example: Install a Dedicated Server
```http
POST /api/steamcmd/install
Content-Type: application/json

{
  "appId": 2394010,
  "installDirectory": "C:\\GameServers\\instances\\palworld-01",
  "validate": true,
  "anonymousLogin": true
}
```

---

### Game Server Process Management

| Method | Route | Description |
|---|---|---|
| `GET` | `/api/servers` | List all managed game servers and their statuses. |
| `GET` | `/api/servers/{id}` | Get process details, PID, CPU/RAM, and uptime. |
| `POST` | `/api/servers/start` | Launch a game server inside a Win32 Job Object. |
| `POST` | `/api/servers/{id}/stop` | Gracefully stop or force-kill a game server. |
| `POST` | `/api/servers/{id}/restart` | Restart a game server. |
| `GET` | `/api/servers/{id}/logs` | Retrieve recent log lines from the circular buffer. |
| `GET` | `/api/servers/{id}/stats` | Get current CPU % and memory working set metrics. |
| `POST` | `/api/servers/{id}/command` | Send standard input or Source RCON command. |

#### Example 1: Start a Palworld Server
```http
POST /api/servers/start
Content-Type: application/json

{
  "serverId": "palworld-01",
  "name": "My Palworld Server",
  "executablePath": "PalServer.exe",
  "arguments": "-port=8211 -players=16 -log -useperfthreads",
  "workingDirectory": "C:\\GameServers\\instances\\palworld-01",
  "autoRestart": true
}
```

#### Example 2: Start a Conan Exiles Dedicated Server
```http
POST /api/servers/start
Content-Type: application/json

{
  "serverId": "conan-01",
  "name": "My Conan Exiles Server",
  "executablePath": "ConanSandboxServer.exe",
  "arguments": "-log -Port=7777 -QueryPort=27015 -MaxPlayers=40 -MULTIHOME=0.0.0.0",
  "workingDirectory": "C:\\GameServers\\instances\\conan-01",
  "autoRestart": true,
  "rconPort": 25575
}
```

---

### File & Backup Management

| Method | Route | Description |
|---|---|---|
| `GET` | `/api/files?directoryPath=...` | List directories and files with sizes and modification times. |
| `GET` | `/api/files/content?filePath=...` | Read text content of a config file. |
| `POST` | `/api/files/content?filePath=...` | Write/overwrite text content of a config file. |
| `POST` | `/api/files/backups/{serverId}` | Create a compressed `.zip` backup of a server's save data. |
| `GET` | `/api/files/backups/{serverId}` | List existing backup archives. |
| `POST` | `/api/files/backups/{serverId}/restore/{backupId}` | Extract a backup archive to restore save data. |

---

### Port & Resource Diagnostics

| Method | Route | Description |
|---|---|---|
| `GET` | `/api/ports/check?port=8211&protocol=udp` | Check if a port is currently available on the Windows host. |
| `POST` | `/api/ports/check-batch` | Check multiple TCP/UDP ports in a single request. |
| `GET` | `/api/health` | System health check, OS details, memory, and drive free space. |

---

### SignalR Streaming Hub

**Hub Endpoint:** `/hubs/windowsagent` (also aliased at `/hubs/nodeagent`)

#### Streaming Methods:
- `StreamServerLogs(string serverId, bool includeHistory, int tailLines)`: Real-time asynchronous stream (`IAsyncEnumerable<string>`) of process stdout/stderr lines.
- `StreamServerStats(string serverId, int intervalSeconds)`: Real-time asynchronous stream of process CPU %, RAM Working Set, Private Bytes, and Thread counts.
- `StreamHostStats(int intervalSeconds)`: Stream of host-wide memory usage and storage capacity.

---

## Configuration

Settings are configured via `appsettings.json` or environment variables:

```json
{
  "WindowsAgent": {
    "AgentPort": "5180",
    "SteamCmd": {
      "SteamCmdDirectory": "C:\\GameServers\\_steamcmd",
      "ExecutableName": "steamcmd.exe",
      "AutoDownloadIfMissing": true,
      "DownloadUrl": "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip",
      "DefaultTimeoutMinutes": 30
    },
    "Storage": {
      "BaseInstancesDirectory": "C:\\GameServers\\instances",
      "BackupsDirectory": "C:\\GameServers\\backups"
    },
    "ProcessSupervision": {
      "GracefulStopTimeoutSeconds": 30,
      "LogBufferSizeLines": 2000,
      "EnableCrashRestart": true,
      "MaxRestartRetries": 5,
      "RestartBackoffSeconds": 10
    },
    "AgentRegistration": {
      "Enabled": true,
      "PrimaryServiceUrl": "http://localhost:5164",
      "HeartbeatIntervalSeconds": 30,
      "ConnectionTimeoutSeconds": 15,
      "MaxStartupRetries": 30,
      "StartupRetryDelaySeconds": 5,
      "ReconnectDelaySeconds": [ 2, 5, 10, 30, 60 ]
    }
  }
}
```

### Environment Variable Overrides
Any setting can be overridden with standard .NET environment variable syntax:
- `WindowsAgent__AgentPort=5180`
- `WindowsAgent__AgentRegistration__PrimaryServiceUrl=http://192.168.1.10:5164`
- `WindowsAgent__SteamCmd__SteamCmdDirectory=D:\SteamCMD`
- `WindowsAgent__Storage__BaseInstancesDirectory=D:\GameServers`

---

## Installation & Hosting

### Running as an Interactive Console App

For local development or testing:

```powershell
cd src/GameServer.Windows.Agent
dotnet run --urls "http://0.0.0.0:5180"
```

Explore the interactive Scalar API documentation in your browser:
```
http://localhost:5180/scalar/v1
```

---

### Installing as a Windows Service

1. **Publish the Project**:
   ```powershell
   dotnet publish src/GameServer.Windows.Agent/GameServer.Windows.Agent.csproj -c Release -r win-x64 --self-contained false -o C:\Services\GameServer.Windows.Agent
   ```

2. **Register the Windows Service**:
   Open an Administrative PowerShell prompt and run:
   ```powershell
   New-Service -Name "GameServerWindowsAgent" `
               -BinaryPathName "C:\Services\GameServer.Windows.Agent\GameServer.Windows.Agent.exe --urls http://0.0.0.0:5180" `
               -DisplayName "GameServer Windows Agent" `
               -Description "Manages SteamCMD installations and native Windows game server processes." `
               -StartupType Automatic
   ```

3. **Configure Firewall Rule**:
   Allow incoming connections to the agent port (default `5180`):
   ```powershell
   New-NetFirewallRule -DisplayName "GameServer Windows Agent (HTTP 5180)" `
                       -Direction Inbound `
                       -LocalPort 5180 `
                       -Protocol TCP `
                       -Action Allow
   ```

4. **Start the Service**:
   ```powershell
   Start-Service -Name "GameServerWindowsAgent"
   ```

---

## Troubleshooting

### 1. SteamCMD Download or Execution Fails
- **Symptom**: `FileNotFoundException: SteamCMD executable was not found`.
- **Resolution**:
  - Verify `AutoDownloadIfMissing` is `true` in `appsettings.json`.
  - Check that the machine has Internet access to `https://steamcdn-a.akamaihd.net`.
  - Ensure the agent has write permissions to create and extract files in the `SteamCmdDirectory` folder.

### 2. Primary Service Registration Fails
- **Symptom**: `Failed to connect to Primary Service (attempt X/30)`.
- **Resolution**:
  - Verify `AgentRegistration:PrimaryServiceUrl` points to the reachable address of `GameServer.Docker` (e.g. `http://192.168.1.10:5164`).
  - Verify firewall settings allow outbound TCP connections to the Primary Service port.
  - Verify the Primary Service is running and listening on `/hubs/agentregistration`.

### 3. Server Process Leaves Lingering Child Processes
- **Symptom**: Executables like `PalServer-Win64-Shipping.exe` keep running after stopping the server.
- **Resolution**:
  - The agent's built-in **Win32 Job Object** automatically handles child process termination. Ensure the agent process is running with sufficient privileges to assign processes to Job Objects.
