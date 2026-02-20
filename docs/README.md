# GameServer.Docker Documentation

Welcome to the GameServer.Docker documentation! This comprehensive guide will help you understand, deploy, and extend the game server management system.

## 📚 Quick Links

### Getting Started
- **[Quick Start Guide](QUICK-START.md)** - Get up and running in 5 minutes
- **[Architecture Overview](ARCHITECTURE.md)** - Understand the system design
- **[Current Features](CURRENT-FEATURES.md)** - See what's implemented

### For Developers
- **[Contributing Guide](CONTRIBUTING.md)** - How to contribute code
- **[Testing Guide](TESTING-QUICK-REFERENCE.md)** - Testing guidelines
- **[Constants & Conventions](reference/CONSTANTS-AND-CONVENTIONS.md)** - Coding standards

## 📖 Documentation Structure

### Core Documentation

**[ARCHITECTURE.md](ARCHITECTURE.md)**  
System architecture, multi-node design, and mandatory architectural patterns. **Read this before making any changes!**

**[CURRENT-FEATURES.md](CURRENT-FEATURES.md)**  
Comprehensive list of all implemented features and functionality.

**[QUICK-START.md](QUICK-START.md)**  
Step-by-step guide to get the system running locally.

**[TESTING-QUICK-REFERENCE.md](TESTING-QUICK-REFERENCE.md)**  
Quick reference for testing the application.

**[CONTRIBUTING.md](CONTRIBUTING.md)**  
Guidelines for contributing code, tests, and documentation.

**[DOCUMENTATION-CLEANUP-PLAN.md](DOCUMENTATION-CLEANUP-PLAN.md)**  
Historical: Documentation reorganization plan (Feb 2026).

### Guides (`guides/`)

Detailed guides for specific features and subsystems:

- **[Agent-QuickStart.md](guides/Agent-QuickStart.md)** - Deploy and configure Node Agents
- **[AGENT-REGISTRATION-MIGRATION.md](AGENT-REGISTRATION-MIGRATION.md)** - ⚠️ Migrate from pull-based discovery to push-based registration
- **[DATABASE-INITIALIZATION.md](guides/DATABASE-INITIALIZATION.md)** - Database setup and seeding
- **[GameType-Metadata-Complete-Guide.md](guides/GameType-Metadata-Complete-Guide.md)** - Extended metadata system
- **[GameType-Editor-Complete-Functionality-Guide.md](guides/GameType-Editor-Complete-Functionality-Guide.md)** - GameType editor UI
- **[Port-Mapping-Integration-Guide.md](guides/Port-Mapping-Integration-Guide.md)** - Port mapping and relationships

### Reference (`reference/`)

Quick reference materials and API documentation:

- **[QUICK-REFERENCE-CARD.md](reference/QUICK-REFERENCE-CARD.md)** - Common operations quick reference
- **[CONSTANTS-AND-CONVENTIONS.md](reference/CONSTANTS-AND-CONVENTIONS.md)** - Coding standards and constants
- **[SQLite-GameType-Database-Schema.md](reference/SQLite-GameType-Database-Schema.md)** - Database schema
- **[Game-Server-Port-Examples.json](reference/Game-Server-Port-Examples.json)** - Port configuration examples

### Architecture (`architecture/`)

Deep dives into system architecture and design:

- **[Agent-Architecture.md](architecture/Agent-Architecture.md)** - Node Agent design and implementation
- **[Agent-Security.md](architecture/Agent-Security.md)** - Security considerations for agents
- **[Agent-Why-Overlay-Network.md](architecture/Agent-Why-Overlay-Network.md)** - Network architecture rationale
- **[Agent-README.md](architecture/Agent-README.md)** - Agent service overview
- **[PERFORMANCE-OPTIMIZATIONS.md](architecture/PERFORMANCE-OPTIMIZATIONS.md)** - Performance patterns and optimizations

## 🎯 Find What You Need

### I want to...

**...get started quickly**
→ Read [QUICK-START.md](QUICK-START.md)

**...understand the architecture**
→ Read [ARCHITECTURE.md](ARCHITECTURE.md)

**...add a new feature**
→ Read [CONTRIBUTING.md](CONTRIBUTING.md) and [ARCHITECTURE.md](ARCHITECTURE.md)

**...add a new game type**
→ Read [guides/GameType-Metadata-Complete-Guide.md](guides/GameType-Metadata-Complete-Guide.md)

**...deploy to production**
→ Read [guides/Agent-QuickStart.md](guides/Agent-QuickStart.md) and [ARCHITECTURE.md](ARCHITECTURE.md)

**...fix a performance issue**
→ Read [architecture/PERFORMANCE-OPTIMIZATIONS.md](architecture/PERFORMANCE-OPTIMIZATIONS.md)

**...understand the constants**
→ Read [reference/CONSTANTS-AND-CONVENTIONS.md](reference/CONSTANTS-AND-CONVENTIONS.md)

**...set up the database**
→ Read [guides/DATABASE-INITIALIZATION.md](guides/DATABASE-INITIALIZATION.md)

**...understand port mapping**
→ Read [guides/Port-Mapping-Integration-Guide.md](guides/Port-Mapping-Integration-Guide.md)

## 🏗️ Key Concepts

### Multi-Node Architecture

GameServer.Docker uses a **multi-node Docker Swarm architecture** with Node Agents on each worker node. Never connect directly to the Docker daemon from SignalR Hubs - always use Node Agents for container operations.

### Service Labels

All Docker labels use constants from `GameServer.Docker.Constants.ServiceLabels`. Never hardcode label strings!

### Extended Metadata

GameTypes support rich metadata for settings including dropdowns, validation, port relationships, and more. See the metadata guide for details.

### Performance Patterns

- Use parallel processing with `Task.WhenAll`
- Use Docker label filters to narrow queries
- Batch API calls to avoid N+1 queries
- Pre-fetch related data

## 📝 Documentation Standards

All documentation follows these standards:

- **Markdown format** - Easy to read and version control
- **Code examples** - Show, don't just tell
- **Keep current** - Update docs when code changes
- **Link related docs** - Help readers find more info
- **Concise & focused** - Get to the point

## 🔄 Recent Changes

**February 2026 - Documentation Reorganization**
- Cleaned up 80+ obsolete docs
- Created new folder structure (guides/, reference/, architecture/)
- Added PERFORMANCE-OPTIMIZATIONS.md
- Added CONSTANTS-AND-CONVENTIONS.md
- Added CONTRIBUTING.md

## 🤝 Contributing to Documentation

Documentation improvements are always welcome! See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

**When updating docs:**
1. Keep them current with code changes
2. Add examples for new features
3. Update architecture docs for design changes
4. Link related documentation

## 📞 Getting Help

- **Issues:** Report bugs or request features
- **Discussions:** Ask questions in GitHub Discussions
- **Architecture Questions:** Read `ARCHITECTURE.md` first!

---

**Happy coding!** 🚀
