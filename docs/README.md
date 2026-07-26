# GameServer.Docker Documentation

Welcome to the **GameServer.Docker** documentation! This comprehensive guide will help you understand, deploy, and extend this powerful game server management system built with modern .NET and Docker Swarm.

## 🎮 What is GameServer.Docker?

GameServer.Docker is a **comprehensive web-based management platform** for deploying and managing game servers in Docker Swarm environments. Built with **.NET 10**, **Blazor**, and **SignalR**, it provides a modern, real-time interface for managing containerized game servers across multiple nodes.

### Key Features

- 🚀 **Multi-Node Docker Swarm Support** - Deploy across multiple worker nodes
- 🎨 **Modern Blazor UI** - Responsive, real-time web interface
- 📊 **Real-Time Monitoring** - Live container stats, logs, and terminal access
- 🔧 **Game Type System** - Extensible game server templates with metadata
- 🌐 **Port Management** - Intelligent port mapping with automatic relationships
- 🔐 **Agent-Based Architecture** - Secure node agents for distributed operations
- 📦 **Dual Persistence** - Legacy SQLite path plus a V2 layer supporting PostgreSQL (default), SQLite, or MySQL
- 🛠️ **RESTful API** - Full API access for automation

## 🏗️ Architecture

### Technology Stack

- **Backend:** .NET 10, ASP.NET Core
- **Frontend:** Blazor Server, Radzen UI Components
- **Real-Time:** SignalR for live updates
- **Container Orchestration:** Docker Swarm
- **Database:** SQLite for legacy data, PostgreSQL/SQLite/MySQL for V2 data via Entity Framework Core
- **API Documentation:** OpenAPI/Swagger, Scalar
- **Logging:** Serilog

### System Components

```
┌────────────────────────────────────────────────────────┐
│                 GameServer.Web                         │
│              (Blazor Server UI)                        │
│       Port: 5102 (dev) / 8080 (docker)                 │
└─────────────────┬──────────────────────────────────────┘
                  │
┌─────────────────▼──────────────────────────────────────┐
│              GameServer.Docker                         │
│          (REST API & Orchestration)                    │
│   Port: 5164 (dev) / 8080 (docker) | Swagger/API      │
└─────────────────┬──────────────────────────────────────┘
                  │
        ┌─────────┴──────────┬──────────────┐
        │                    │              │
┌───────▼─────────┐  ┌──────▼──────┐  ┌───▼─────────┐
│ Node Agent #1   │  │ Node Agent  │  │ Node Agent  │
│   (Worker 1)    │  │  (Worker 2) │  │  (Worker N) │
│   Port: 8080    │  │ Port: 8080  │  │ Port: 8080  │
└─────────────────┘  └─────────────┘  └─────────────┘
```

**See [ARCHITECTURE.md](ARCHITECTURE.md) for complete architecture details.**

## 📚 Quick Links

### 🚀 Getting Started
- **[Quick Start Guide](QUICK-START.md)** - Get up and running in 5 minutes
- **[Agent Quick Start](guides/Agent-QuickStart.md)** - ⭐ Agent-based architecture setup
- **[Architecture Overview](ARCHITECTURE.md)** - **READ THIS FIRST!** System design & patterns
- **[Current Features](CURRENT-FEATURES.md)** - Complete feature list

### 👨‍💻 For Developers
- **[Contributing Guide](CONTRIBUTING.md)** - How to contribute code
- **[Testing Guide](TESTING-QUICK-REFERENCE.md)** - Testing best practices
- **[Constants & Conventions](reference/CONSTANTS-AND-CONVENTIONS.md)** - Coding standards
- **[Quick Reference Card](reference/QUICK-REFERENCE-CARD.md)** - Common operations
- **[Performance Optimizations](architecture/PERFORMANCE-OPTIMIZATIONS.md)** - Performance patterns

### 📖 Feature Guides
- **[GameType Metadata Guide](guides/GameType-Metadata-Complete-Guide.md)** - Extended metadata system
- **[GameType Editor Guide](guides/GameType-Editor-Complete-Functionality-Guide.md)** - GameType editor UI
- **[Port Mapping Guide](guides/Port-Mapping-Integration-Guide.md)** - Port configuration
- **[Database Guide](guides/DATABASE-INITIALIZATION.md)** - Database setup and seeding
- **[V2 GameServer Lifecycle](guides/V2-GameServer-Lifecycle.md)** - Create, edit, and view V2 servers
- **[Terminal & Console](guides/Terminal-And-Console.md)** - Interactive terminal and TTY console
- **[File Manager](guides/File-Manager.md)** - Browse and edit server files
- **[Agent Registration Flow](guides/Agent-Registration-Flow.md)** - Push-based agent registration and heartbeats

## 📂 Projects in this Solution

### Main Applications

| Project | Description | Technology | Dev Port | Docker Port |
|---------|-------------|------------|----------|-------------|
| **GameServer.Web** | Blazor Server UI | .NET 10, Radzen, SignalR | 5102 / 7198 | 8080 / 8081 |
| **GameServer.Docker** | REST API & Orchestration | .NET 10, ASP.NET Core | 5164 / 7145 | 8080 / 8081 |
| **GameServer.Docker.Agent** | Node Agent Service | .NET 10, Docker.DotNet | 54879 / 54878 | 8080 |
| **GameServer.Docker.Client** | Shared Models & DTOs | .NET 10 Class Library | - | - |

### Test Projects

| Project | Description |
|---------|-------------|
| **GameServer.Docker.Tests** | Unit tests for Docker service |
| **GameServer.Web.Tests** | Unit tests for Web UI |
| **GameServer.Docker.Agent.Tests** | Unit tests for Agent service |
| **GameServer.Integration.Tests** | Integration tests |

## 📖 Documentation Structure

### Core Documentation

| Document | Description |
|----------|-------------|
| **[ARCHITECTURE.md](ARCHITECTURE.md)** | ⚠️ **REQUIRED READING** - System architecture, multi-node design, and mandatory patterns |
| **[CURRENT-FEATURES.md](CURRENT-FEATURES.md)** | Complete feature list with implementation details |
| **[CONTRIBUTING.md](CONTRIBUTING.md)** | Development guidelines and contribution workflow |
| **[QUICK-START.md](QUICK-START.md)** | 5-minute setup guide for local development |
| **[TESTING-QUICK-REFERENCE.md](TESTING-QUICK-REFERENCE.md)** | Testing patterns and practices |

### Guides (`guides/`)

Detailed guides for specific features and subsystems:

- **[Agent-QuickStart.md](guides/Agent-QuickStart.md)** - Deploy and configure Node Agents
- **[DATABASE-INITIALIZATION.md](guides/DATABASE-INITIALIZATION.md)** - Database setup
- **[GameType-Metadata-Complete-Guide.md](guides/GameType-Metadata-Complete-Guide.md)** - Extended metadata
- **[GameType-Editor-Complete-Functionality-Guide.md](guides/GameType-Editor-Complete-Functionality-Guide.md)** - GameType editor
- **[Port-Mapping-Integration-Guide.md](guides/Port-Mapping-Integration-Guide.md)** - Port relationships

### Reference (`reference/`)

Quick reference materials and API documentation:

- **[QUICK-REFERENCE-CARD.md](reference/QUICK-REFERENCE-CARD.md)** - Common operations
- **[CONSTANTS-AND-CONVENTIONS.md](reference/CONSTANTS-AND-CONVENTIONS.md)** - Coding standards
- **[SQLite-GameType-Database-Schema.md](reference/SQLite-GameType-Database-Schema.md)** - Database schema
- **[Game-Server-Port-Examples.json](reference/Game-Server-Port-Examples.json)** - Port examples

### Architecture (`architecture/`)

Deep dives into system architecture and design:

- **[Agent-Architecture.md](architecture/Agent-Architecture.md)** - Node Agent design
- **[Agent-Security.md](architecture/Agent-Security.md)** - Security considerations
- **[Agent-Why-Overlay-Network.md](architecture/Agent-Why-Overlay-Network.md)** - Network design
- **[PERFORMANCE-OPTIMIZATIONS.md](architecture/PERFORMANCE-OPTIMIZATIONS.md)** - Performance patterns

## 🎯 Common Tasks

### I want to...

| Task | Documentation |
|------|---------------|
| **Get started quickly** | [QUICK-START.md](QUICK-START.md) |
| **Understand the architecture** | [ARCHITECTURE.md](ARCHITECTURE.md) ⚠️ **Required** |
| **Add a new feature** | [CONTRIBUTING.md](CONTRIBUTING.md) + [ARCHITECTURE.md](ARCHITECTURE.md) |
| **Add a new game type** | [GameType Metadata Guide](guides/GameType-Metadata-Complete-Guide.md) |
| **Deploy to production** | [Agent Quick Start](guides/Agent-QuickStart.md) |
| **Fix a performance issue** | [Performance Optimizations](architecture/PERFORMANCE-OPTIMIZATIONS.md) |
| **Understand coding standards** | [Constants & Conventions](reference/CONSTANTS-AND-CONVENTIONS.md) |
| **Set up the database** | [Database Guide](guides/DATABASE-INITIALIZATION.md) |
| **Configure port mappings** | [Port Mapping Guide](guides/Port-Mapping-Integration-Guide.md) |
| **Write tests** | [Testing Guide](TESTING-QUICK-REFERENCE.md) |
| **Manage V2 servers** | [V2 GameServer Lifecycle](guides/V2-GameServer-Lifecycle.md) |
| **Use terminal/console** | [Terminal & Console](guides/Terminal-And-Console.md) |
| **Understand agent registration** | [Agent Registration Flow](guides/Agent-Registration-Flow.md) |

## 🏗️ Key Architectural Concepts

### Agent-Based Architecture (Current)

The system uses **push-based agent registration** where Node Agents connect to the Primary Service via SignalR:

✅ **Benefits:**
- Real-time agent health tracking via heartbeats (every 30s)
- O(1) container-to-agent lookups (no Docker API calls)
- Works with standalone Docker, not just Swarm
- Primary Service doesn't need Docker access

⚠️ **CRITICAL RULE:** Never connect directly to `IDockerClient` from SignalR Hubs for container operations. Always use Node Agents via `INodeAgentDiscovery`.

**See [ARCHITECTURE.md](ARCHITECTURE.md) for complete details.**

### Service Labels

All Docker service labels use **constants** from `GameServer.Docker.Constants.ServiceLabels`:

```csharp
// ✅ CORRECT
filters.Add("label", ServiceLabels.Managed);

// ❌ WRONG - Never hardcode!
filters.Add("label", "gameserver.docker.managed");
```

### Extended Metadata System

GameTypes support rich metadata for settings:
- **Data Types:** `string`, `number`, `boolean`, `enum`, `port`
- **Validation:** Min/Max values, allowed values
- **Port Relationships:** Offset, Fixed, Multiplier
- **UI Rendering:** Categories, descriptions, defaults

**See [GameType Metadata Guide](guides/GameType-Metadata-Complete-Guide.md) for details.**

### Performance Patterns

1. **Parallel Processing:** Use `Task.WhenAll` for collections
2. **Filtered Queries:** Use Docker label filters to narrow results
3. **Batch Operations:** Pre-fetch related data to avoid N+1 queries
4. **Caching:** Agent registry maintains in-memory container→agent mappings

**See [PERFORMANCE-OPTIMIZATIONS.md](architecture/PERFORMANCE-OPTIMIZATIONS.md) for details.**

## 🚀 Quick Start

### Prerequisites

- .NET 10 SDK
- Docker Desktop with Swarm mode enabled
- Visual Studio 2026 or VS Code

### Run Locally

```bash
# Clone the repository
git clone https://github.com/mbulava-org/GameServer.GUI.git
cd GameServer.GUI

# Initialize Docker Swarm (if not already)
docker swarm init

# Run the Web UI
cd src/GameServer.Web
dotnet run

# Run the API (in another terminal)
cd src/GameServer.Docker
dotnet run

# Access the application
# Web UI: http://localhost:5102 (or https://localhost:7198)
# API: http://localhost:5164/swagger (or https://localhost:7145/swagger)
```

**See [QUICK-START.md](QUICK-START.md) for complete setup instructions.**

## 🧪 Testing

The project uses **xUnit** for testing. All tests follow the AAA (Arrange-Act-Assert) pattern.

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet-coverage collect -f cobertura -o coverage.cobertura.xml dotnet test

# Run specific test project
dotnet test tests/GameServer.Docker.Tests
```

**See [TESTING-QUICK-REFERENCE.md](TESTING-QUICK-REFERENCE.md) for testing guidelines.**

## 📦 NuGet Packages

### Key Dependencies

- **Docker.DotNet** (3.125.15) - Docker API client
- **Radzen.Blazor** (9.0.4) - UI components
- **Serilog.AspNetCore** (10.0.0) - Structured logging
- **Microsoft.AspNetCore.SignalR.Client** (10.0.3) - Real-time communication
- **XtermBlazor** (2.3.0) - Terminal emulator for container attach

## 📝 Documentation Standards

All documentation follows these standards:

- ✅ **Markdown format** - Easy to read and version control
- ✅ **Code examples** - Show, don't just tell
- ✅ **Stay current** - Update docs when code changes
- ✅ **Link related docs** - Help readers find more info
- ✅ **Be concise** - Get to the point quickly

## 🔄 Recent Updates

### March 2026
- ✨ Added web redirect configuration support
- 🔒 Enhanced agent security and authentication
- 🐛 Fixed critical agent serialization bugs
- 📊 Improved configuration consolidation

### February 2026
- 📚 Major documentation reorganization
- 🗂️ Created guides/, reference/, architecture/ folders
- 📖 Added PERFORMANCE-OPTIMIZATIONS.md
- 📋 Added CONSTANTS-AND-CONVENTIONS.md
- 🤝 Added CONTRIBUTING.md
- 🧹 Cleaned up 80+ obsolete docs

## 🤝 Contributing

We welcome contributions! Please follow these steps:

1. Read [CONTRIBUTING.md](CONTRIBUTING.md)
2. Read [ARCHITECTURE.md](ARCHITECTURE.md) for design patterns
3. Check [CURRENT-FEATURES.md](CURRENT-FEATURES.md) to avoid duplicates
4. Create a feature branch
5. Write tests for new features
6. Update documentation
7. Submit a pull request

**When contributing:**
- ✅ Follow .NET 10 conventions
- ✅ Use constants from `ServiceLabels`
- ✅ Write tests (xUnit)
- ✅ Update relevant documentation
- ✅ Follow architecture patterns in ARCHITECTURE.md

## 📞 Support & Community

- **GitHub Issues:** Report bugs or request features
- **GitHub Discussions:** Ask questions and share ideas
- **Documentation:** Comprehensive guides in `/docs`

### Getting Help

1. **Check the docs first!** Most questions are answered in:
   - [ARCHITECTURE.md](ARCHITECTURE.md) - Architecture questions
   - [QUICK-REFERENCE-CARD.md](reference/QUICK-REFERENCE-CARD.md) - Common operations
   - [CURRENT-FEATURES.md](CURRENT-FEATURES.md) - Feature availability

2. **Search existing issues** - Your question might already be answered

3. **Create a new issue** - Provide details and context

## 📜 License

This project is licensed under the MIT License - see the LICENSE file for details.

---

## 🎯 Project Status

**Version:** 0.1.0 (Beta)  
**Target Framework:** .NET 10  
**Status:** Active Development  
**Last Updated:** March 2026

### Roadmap

- [ ] Kubernetes support
- [ ] Multi-cluster management
- [ ] Advanced scheduling
- [ ] Backup/restore functionality
- [ ] Metrics & alerting
- [ ] User authentication & RBAC

---

**Happy Gaming!** 🎮🚀

Built with ❤️ using .NET 10, Blazor, and Docker
