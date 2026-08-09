# Configuration Consolidation & Direct Mode Removal

## Executive Summary

This refactoring consolidates scattered configuration classes into cohesive `GameServerOptions` and `AgentOptions` classes, removes deprecated "Direct Mode" Docker connectivity, and eliminates environment variable usage in favor of the Options pattern for consistency and testability.

---

## Current State Problems

### 1. **Fragmented Configuration**
Multiple small configuration classes scattered across both services:

**GameServer.Docker (Main Service)**:
- `DockerConnection` (7 lines) - **DEPRECATED**
- `ServiceOperationsOptions` (23 lines) - **DEPRECATED**  
- `PortAllocation` (8 lines)
- `VolumeDriverConfigOptions` (27 lines)
- `NetworkOptions` (29 lines)
- `NodeAgentOptions` (25 lines)
- `GameTypeRegistryData` (?)
- `GameTypeExtendedMetadataRegistryData` (?)

**GameServer.Docker.Agent**:
- `AgentRegistrationOptions` (70 lines)
- `ContainerStatsStreamOptions` (?)

### 2. **Environment Variable Overuse**
Configuration mixed between Options pattern and environment variables:

**Agent**:
- ❌ `LOG_LEVEL` - Log level (should be in Logging config)
- ❌ `NODE_NAME` - Node identifier  
- ❌ `DOCKER_HOST` - Docker socket path

**Main Service (UI)**:
- ❌ `LOADBALANCER_DOMAIN` - Domain for generated URLs

### 3. **Deprecated Direct Mode**
- `ServiceOperationsViaDirect.cs` - Direct Docker connection (obsolete)
- `DockerConnection` configuration - Only used by Direct mode
- `ServiceOperationsOptions.Mode = "Direct"` - Backward compatibility flag
- `DockerClientFactory` - Creates direct Docker clients (obsolete)

### 4. **Configuration Inconsistency**
- Agent uses env vars, main service uses Options
- No standard pattern across codebase
- Hard to test code that reads environment directly
- Documentation unclear on what to configure where

---

## Proposed Solution

### Create Unified `GameServerOptions`

```csharp
namespace GameServer.Docker.Configurations;

/// <summary>
/// Unified configuration for GameServer.Docker service.
/// </summary>
public class GameServerOptions
{
    /// <summary>
    /// Agent-based architecture settings.
    /// </summary>
    public AgentSettings Agent { get; set; } = new();

    /// <summary>
    /// Docker networking configuration.
    /// </summary>
    public NetworkSettings Network { get; set; } = new();

    /// <summary>
    /// Port allocation settings for game servers.
    /// </summary>
    public PortAllocationSettings Ports { get; set; } = new();

    /// <summary>
    /// Volume and storage configuration.
    /// </summary>
    public VolumeSettings Volumes { get; set; } = new();

    /// <summary>
    /// UI/Web-specific settings.
    /// </summary>
    public UISettings UI { get; set; } = new();
}

public class AgentSettings
{
    /// <summary>
    /// Discovery mode: "SignalR" (agents connect in) or "Static" (hardcoded list).
    /// Default: "SignalR"
    /// </summary>
    public string DiscoveryMode { get; set; } = "SignalR";

    /// <summary>
    /// Static agent configuration (when DiscoveryMode = "Static").
    /// </summary>
    public List<StaticAgentConfig> StaticAgents { get; set; } = new();

    /// <summary>
    /// SignalR registration endpoint (when DiscoveryMode = "SignalR").
    /// Default: "https://localhost:7269"
    /// </summary>
    public string RegistrationEndpoint { get; set; } = "https://localhost:7269";
}

public class StaticAgentConfig
{
    public string Name { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
}

public class NetworkSettings
{
    /// <summary>
    /// Optional network for service-to-service communication.
    /// Default: null (not used)
    /// </summary>
    public string? GameNetwork { get; set; }

    /// <summary>
    /// Network where the load balancer is running.
    /// Services with web hosts attach to this network.
    /// Default: "traefik-public"
    /// </summary>
    public string LoadBalancerNetwork { get; set; } = "traefik-public";

    /// <summary>
    /// Load balancer provider: "traefik", "nginx", "caddy", "none".
    /// Default: "traefik"
    /// </summary>
    public string LoadBalancerProvider { get; set; } = "traefik";
}

public class PortAllocationSettings
{
    /// <summary>
    /// Starting port for automatic allocation.
    /// Default: 25565 (Minecraft default)
    /// </summary>
    public uint StartPort { get; set; } = 25565;

    /// <summary>
    /// Ending port for automatic allocation.
    /// Default: 35565
    /// </summary>
    public uint EndPort { get; set; } = 35565;
}

public class VolumeSettings
{
    /// <summary>
    /// Volume driver name.
    /// Default: "local"
    /// </summary>
    public string Driver { get; set; } = "local";

    /// <summary>
    /// Volume driver options (for NFS, CIFS, etc.).
    /// </summary>
    public VolumeDriverOptions? DriverOptions { get; set; }

    /// <summary>
    /// Root storage path in Docker volumes.
    /// Default: "" (driver default)
    /// </summary>
    public string RootStoragePath { get; set; } = "";

    /// <summary>
    /// Subdirectory format for each server.
    /// Variables: {gameTypeKey}, {serverId}, {Source}
    /// Default: "{gameTypeKey}/{serverId}/{Source}"
    /// </summary>
    public string SubPathFormat { get; set; } = "{gameTypeKey}/{serverId}/{Source}";

    /// <summary>
    /// Local mount path for accessing storage.
    /// Used for file management operations.
    /// Default: "/data"
    /// </summary>
    public string LocalStoragePath { get; set; } = "/data";
}

public class VolumeDriverOptions
{
    public string Type { get; set; } = "nfs";
    public string Device { get; set; } = ":/exported/path";
    public string Options { get; set; } = "addr=host.docker.internal,rw";
}

public class UISettings
{
    /// <summary>
    /// Base domain for load balancer URLs in UI.
    /// Used by WebHostsDisplay and WebHostsPreview components.
    /// Example: "games.example.com"
    /// Default: "localhost"
    /// </summary>
    public string LoadBalancerDomain { get; set; } = "localhost";

    /// <summary>
    /// Vanity domain for connection strings.
    /// Default: "games.dev.bulavafamily.com"
    /// </summary>
    public string VanityDomain { get; set; } = "games.dev.bulavafamily.com";
}
```

---

## Files to Remove

### 1. Configuration Classes
- ❌ `src/GameServer.Docker/Configurations/DockerConnection.cs`
- ❌ `src/GameServer.Docker/Configurations/ServiceOperationsOptions.cs`
- ❌ `src/GameServer.Docker/Configurations/PortAllocation.cs` → Merged
- ❌ `src/GameServer.Docker/Configurations/VolumeDriverConfigOptions.cs` → Merged
- ❌ `src/GameServer.Docker/Configurations/NetworkOptions.cs` → Merged
- ❌ `src/GameServer.Docker/Configurations/NodeAgentOptions.cs` → Merged
- ❌ `src/GameServer.Docker/Configurations/GameTypeRegistryData.cs` → Merged
- ❌ `src/GameServer.Docker/Configurations/GameTypeExtendedMetadataRegistryData.cs` → Merged

### 2. Service Classes
- ❌ `src/GameServer.Docker/Services/ServiceOperationsViaDirect.cs`
- ❌ `src/GameServer.Docker/Services/DockerClientFactory.cs` (if only used by Direct mode)

### 3. Test Files
- ⚠️ Update tests that reference Direct mode
- ❌ Remove tests specific to Direct mode

---

## Migration Steps

### Step 1: Create New Configuration
1. Create `src/GameServer.Docker/Configurations/GameServerOptions.cs`
2. Implement all nested classes
3. Add XML documentation

### Step 2: Update Program.cs
```csharp
// OLD
builder.Services.Configure<DockerConnection>(builder.Configuration.GetSection("DockerConnection"));
builder.Services.Configure<ServiceOperationsOptions>(builder.Configuration.GetSection("ServiceOperationsOptions"));
builder.Services.Configure<PortAllocation>(builder.Configuration.GetSection("PortAllocation"));
builder.Services.Configure<VolumeDriverConfigOptions>(builder.Configuration.GetSection("VolumeDriverConfigOptions"));
builder.Services.Configure<NetworkOptions>(builder.Configuration.GetSection("NetworkOptions"));
builder.Services.Configure<NodeAgentOptions>(builder.Configuration.GetSection("NodeAgentOptions"));

// NEW
builder.Services.Configure<GameServerOptions>(builder.Configuration.GetSection("GameServer"));
```

### Step 3: Update appsettings.json
```json
{
  "GameServer": {
    "Agent": {
      "DiscoveryMode": "SignalR",
      "RegistrationEndpoint": "https://localhost:7269"
    },
    "Network": {
      "GameNetwork": null,
      "LoadBalancerNetwork": "traefik-public",
      "LoadBalancerProvider": "traefik"
    },
    "Ports": {
      "StartPort": 25565,
      "EndPort": 35565
    },
    "Volumes": {
      "Driver": "local",
      "RootStoragePath": "",
      "SubPathFormat": "{gameTypeKey}/{serverId}/{Source}",
      "LocalStoragePath": "/data"
    },
    "UI": {
      "LoadBalancerDomain": "games.example.com",
      "VanityDomain": "games.dev.bulavafamily.com"
    }
  }
}
```

### Step 4: Update Service Registrations
```csharp
// Remove conditional Direct/Agent registration
services.AddScoped<IServiceOperations, ServiceOperationsViaAgent>();
```

### Step 5: Update All References
Search and replace in codebase:
- `IOptions<DockerConnection>` → `IOptions<GameServerOptions>`
- `IOptions<ServiceOperationsOptions>` → Remove
- `IOptions<PortAllocation>` → `IOptions<GameServerOptions>`
- `IOptions<VolumeDriverConfigOptions>` → `IOptions<GameServerOptions>`
- `IOptions<NetworkOptions>` → `IOptions<GameServerOptions>`
- `IOptions<NodeAgentOptions>` → `IOptions<GameServerOptions>`

Access pattern:
```csharp
// OLD
private readonly IOptions<NetworkOptions> _netOptions;
var network = _netOptions.Value.LoadBalancerNetwork;

// NEW
private readonly IOptions<GameServerOptions> _options;
var network = _options.Value.Network.LoadBalancerNetwork;
```

### Step 6: Remove Files
Delete deprecated files listed above.

### Step 7: Update Tests
- Update constructor mocks to use `GameServerOptions`
- Remove Direct mode test paths
- Simplify Agent-only tests

### Step 8: Update Documentation
- Remove all references to Direct mode
- Update configuration examples
- Update architecture diagrams

---

## Breaking Changes

### Configuration File Format
**Users must update appsettings.json** from:
```json
{
  "NetworkOptions": { ... },
  "PortAllocation": { ... },
  "VolumeDriverConfigOptions": { ... }
}
```

To:
```json
{
  "GameServer": {
    "Network": { ... },
    "Ports": { ... },
    "Volumes": { ... }
  }
}
```

### Code Changes
Any custom code referencing old configuration classes must update:
```csharp
// OLD
IOptions<NetworkOptions> netOptions

// NEW  
IOptions<GameServerOptions> options
// Access: options.Value.Network
```

---

## Benefits

### 1. **Clarity**
- Single configuration entry point
- Hierarchical structure reflects relationships
- Self-documenting with XML comments

### 2. **Simplicity**
- One appsettings.json section
- One options class to inject
- No mode switching logic

### 3. **Maintainability**
- Fewer files to manage
- Clear ownership of settings
- Easier to extend

### 4. **Performance**
- Remove Direct mode overhead
- Single Agent path
- Simpler dependency injection

### 5. **Architecture**
- Commit fully to Agent-based design
- Remove legacy code
- Cleaner codebase

---

## Testing Strategy

### 1. Create Migration Tests
Test old → new configuration mapping:
```csharp
[Fact]
public void MigrateOldConfiguration_ShouldMapToNew()
{
    // Arrange: Old format
    var oldConfig = new
    {
        NetworkOptions = new { LoadBalancerNetwork = "traefik-public" },
        PortAllocation = new { StartPort = 25565 }
    };

    // Act: Map to new
    var newConfig = MapToGameServerOptions(oldConfig);

    // Assert
    Assert.Equal("traefik-public", newConfig.Network.LoadBalancerNetwork);
    Assert.Equal(25565u, newConfig.Ports.StartPort);
}
```

### 2. Integration Tests
- Verify services start with new configuration
- Test all Agent operations still work
- Verify backward compatibility period

### 3. Documentation Tests
- Sample appsettings.json validates
- Examples in docs compile
- Migration guide is complete

---

## Rollout Plan

### Phase 1: Add New (Non-Breaking)
1. Create `GameServerOptions.cs`
2. Support **both** old and new configurations
3. Add deprecation warnings to old configs
4. Release version X.Y.0

### Phase 2: Deprecation Period (1-2 releases)
1. Log warnings when old configs are used
2. Provide migration tool/script
3. Update all documentation
4. Release version X.Y+1.0

### Phase 3: Remove Old (Breaking)
1. Remove old configuration classes
2. Remove Direct mode services
3. Update major version (X+1.0.0)
4. Remove compatibility layer

---

## File Count Reduction

**Before**: 8 configuration files + 2 deprecated services = 10 files
**After**: 1 configuration file = 1 file

**Net Reduction**: 9 files 📉

---

## Example Updated Service

```csharp
public class DockerServiceHelper
{
    private readonly IOptions<GameServerOptions> _options;

    public DockerServiceHelper(IOptions<GameServerOptions> options, ...)
    {
        _options = options;
    }

    private async Task<IList<NetworkAttachmentConfig>> CreateNetworkConfig(...)
    {
        var opts = _options.Value;
        
        // Access game network
        if (opts.Network.GameNetwork != null)
        {
            networks.Add(new NetworkAttachmentConfig
            {
                Target = opts.Network.GameNetwork
            });
        }

        // Access load balancer network
        if (!string.IsNullOrWhiteSpace(opts.Network.LoadBalancerNetwork))
        {
            networks.Add(new NetworkAttachmentConfig
            {
                Target = opts.Network.LoadBalancerNetwork
            });
        }

        return networks;
    }
}
```

---

## Documentation Updates

### Update Files:
1. ✅ `docs/ARCHITECTURE.md` - Remove Direct mode references
2. ✅ `docs/CONFIGURATION.md` - New unified structure
3. ✅ `docs/MIGRATION-GUIDE.md` - Old → New mapping
4. ✅ `README.md` - Updated configuration examples
5. ✅ `appsettings.json.example` - New format

---

## Decision Log

**Decision**: Remove Direct Mode
**Rationale**:
- Agent architecture is mature and tested
- Direct mode creates maintenance burden
- Security: Agent mode is more secure (no Docker socket exposure)
- Scalability: Agent mode scales better
- Complexity: Supporting both modes adds unnecessary complexity

**Decision**: Consolidate Configuration
**Rationale**:
- 8 small classes → 1 cohesive class
- Easier discovery of available settings
- Better IntelliSense experience
- Clearer relationships between settings

---

## Success Criteria

✅ All services use `GameServerOptions`
✅ No references to old configuration classes
✅ No Direct mode code paths
✅ All tests passing with new configuration
✅ Documentation updated
✅ Migration guide complete
✅ Example appsettings.json provided

---

## Next Steps

1. **Review This Document** - Team approval
2. **Create GameServerOptions.cs** - Implement unified class
3. **Update Program.cs** - Add both old and new support
4. **Run Tests** - Ensure backward compatibility
5. **Update Documentation** - Configuration guide
6. **Release with Deprecation Warnings** - Version X.Y.0
7. **Wait 1-2 Releases** - Let users migrate
8. **Remove Old Code** - Clean break (Version X+1.0.0)

---

**Status**: 📝 **Proposed** - Awaiting approval
