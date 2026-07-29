# Contributing to GameServer.Docker

Thank you for considering contributing to GameServer.Docker! This document provides guidelines and instructions for contributing.

## Table of Contents

- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [Coding Standards](#coding-standards)
- [Pull Request Process](#pull-request-process)
- [Testing Requirements](#testing-requirements)
- [Documentation](#documentation)

## Getting Started

### Prerequisites

- .NET 10 SDK or later
- Docker Desktop with Swarm mode enabled
- Visual Studio 2025 or VS Code
- Basic understanding of:
  - Blazor Server
  - Docker Swarm
  - SignalR
  - SQLite

### First Steps

1. **Read the Documentation**
   - `docs/ARCHITECTURE.md` - Understand system architecture
   - `docs/CURRENT-FEATURES.md` - Know what's implemented
   - `docs/QUICK-START.md` - Get the system running

2. **Review Existing Code**
   - Check `.github/copilot-instructions.md` for coding patterns
   - Review `docs/reference/CONSTANTS-AND-CONVENTIONS.md`

3. **Look for Issues**
   - Check GitHub Issues for open tasks
   - Look for issues labeled `good first issue` or `help wanted`

## Development Setup

### Clone and Build

```bash
# Clone the repository
git clone https://github.com/mbulava-org/GameServer.GUI.git
cd GameServer.GUI

# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run tests
dotnet test
```

### Database Setup

```bash
# Initialize SQLite database
cd src/GameServer.Docker
dotnet ef database update
```

### Running Locally

```bash
# Run the Web UI
cd src/GameServer.Web
dotnet run

# Access at https://localhost:7198
```

### Docker Swarm Setup

```bash
# Initialize Swarm (if not already done)
docker swarm init

# Deploy Node Agents (see docs/QUICK-START.md)
```

## Coding Standards

### Follow the Architecture

**CRITICAL RULES:**

1. **Never connect directly to Docker daemon from SignalR Hubs** for container operations
2. **Always use Node Agents** for container-level operations
3. **Use ServiceLabels constants** instead of hardcoded label strings
4. **Follow performance patterns** (parallel processing, batching, filtering)

See `docs/ARCHITECTURE.md` for detailed architectural patterns.

### Code Style

**C# Conventions:**
- Use file-scoped namespaces
- PascalCase for public members
- camelCase with `_` prefix for private fields
- Async methods end with `Async`
- Use var for obvious types

**Example:**
```csharp
namespace GameServer.Docker.Services;

public class GameServerManagerService
{
    private readonly ILogger<GameServerManagerService> _logger;
    private readonly DockerServiceHelper _dockerServiceHelper;
    
    public async Task<List<GameServer>> ListServersAsync()
    {
        // Implementation
    }
}
```

### Constants Usage

**Always use constants for Docker labels:**
```csharp
// ✅ Good
var labels = new Dictionary<string, string>
{
    [ServiceLabels.Managed] = ServiceLabels.ManagedValue,
    [ServiceLabels.ServerId] = server.ServerId
};

// ❌ Bad
var labels = new Dictionary<string, string>
{
    ["gameserver.docker.managed"] = "true",
    ["gameserver.docker.Id"] = server.ServerId
};
```

### Performance

**Use parallel processing:**
```csharp
// ✅ Good
var results = await Task.WhenAll(
    collection.Select(item => ProcessAsync(item)));

// ❌ Bad
foreach (var item in collection)
{
    await ProcessAsync(item);
}
```

**Use Docker filters:**
```csharp
// ✅ Good
var filters = new ServiceFilter { Label = new[] { $"{ServiceLabels.ServerId}={id}" } };
var services = await client.Swarm.ListServicesAsync(new ServicesListParameters { Filters = filters });

// ❌ Bad
var allServices = await client.Swarm.ListServicesAsync();
var service = allServices.FirstOrDefault(s => s.Spec.Labels["gameserver.docker.Id"] == id);
```

## Pull Request Process

### Before Submitting

1. **Create a feature branch**
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Write clear commit messages**
   ```
   Add parallel processing to ListGameServersAsync
   
   - Fetch services and tasks in parallel
   - Use Task.WhenAll for concurrent processing
   - Reduces API calls from N+1 to 2
   
   Performance improvement: 4-10x faster
   ```

3. **Update documentation**
   - Update relevant docs if behavior changes
   - Add XML doc comments to new public APIs
   - Update CURRENT-FEATURES.md if adding features

4. **Test your changes**
   - Run all tests: `dotnet test`
   - Test manually in browser
   - Verify no regressions

5. **Check build**
   ```bash
   dotnet build --no-incremental
   ```

### PR Template

```markdown
## Description
Brief description of changes

## Type of Change
- [ ] Bug fix
- [ ] New feature
- [ ] Breaking change
- [ ] Documentation update

## Testing
Describe testing performed

## Checklist
- [ ] Code follows style guidelines
- [ ] Documentation updated
- [ ] Tests added/updated
- [ ] All tests passing
- [ ] No new warnings
```

### Review Process

1. Automated checks must pass (build, tests)
2. At least one approval required
3. Address review feedback
4. Squash commits if requested
5. Maintainer will merge

## Testing Requirements

### Unit Tests

**Required for:**
- Business logic in Services
- Data access in Repositories
- Helper methods in utilities

**Example:**
```csharp
public class DockerServiceHelperTests
{
    [Fact]
    public async Task ListGameServersAsync_WithManagedServices_ReturnsOnlyManaged()
    {
        // Arrange
        var mockClient = CreateMockDockerClient();
        var helper = new DockerServiceHelper(mockClient, ...);
        
        // Act
        var result = await helper.ListGameServersAsync();
        
        // Assert
        Assert.All(result, server => Assert.NotNull(server.ServerId));
    }
}
```

### Integration Tests

**Test:**
- API endpoints
- Database operations
- SignalR hubs

### Manual Testing

**Always test:**
- UI changes in browser
- Multi-node scenarios if touching agents
- Error handling

## Documentation

### Required Documentation

**For New Features:**
- Update `CURRENT-FEATURES.md`
- Add to relevant guide in `docs/guides/`
- XML doc comments on public APIs

**For Bug Fixes:**
- Explain root cause in PR description
- Update docs if behavior changed

**For Performance Changes:**
- Add to `docs/architecture/PERFORMANCE-OPTIMIZATIONS.md`
- Include benchmarks/measurements

### Documentation Style

- Use Markdown
- Include code examples
- Add diagrams for complex features (Mermaid)
- Keep concise and focused

## Questions?

- **Slack/Discord:** [Link if available]
- **GitHub Discussions:** Ask questions in Discussions tab
- **Issues:** Create an issue for bugs or feature requests

## Code of Conduct

- Be respectful and professional
- Provide constructive feedback
- Help others learn
- Keep discussions focused on technical merits

## License

By contributing, you agree that your contributions will be licensed under the Apache-2.0 License.

---

**Thank you for contributing!** 🎉
