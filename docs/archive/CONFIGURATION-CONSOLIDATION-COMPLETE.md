# Configuration Consolidation - Complete Plan

## Part 1: Agent Configuration (`GameServer.Docker.Agent`)

### New `AgentOptions` Class

```csharp
namespace GameServer.Docker.Agent.Configurations;

/// <summary>
/// Unified configuration for GameServer.Docker.Agent service.
/// </summary>
public class AgentOptions
{
    /// <summary>
    /// Node identification and metadata.
    /// </summary>
    public NodeSettings Node { get; set; } = new();

    /// <summary>
    /// Docker daemon connection settings.
    /// </summary>
    public DockerSettings Docker { get; set; } = new();

    /// <summary>
    /// Registration with Primary Service settings.
    /// </summary>
    public RegistrationSettings Registration { get; set; } = new();

    /// <summary>
    /// Container statistics streaming configuration.
    /// </summary>
    public StatsStreamSettings StatsStream { get; set; } = new();
}

public class NodeSettings
{
    /// <summary>
    /// Unique identifier for this node.
    /// Used for registration and logging.
    /// Default: Hostname
    /// </summary>
    public string Name { get; set; } = Environment.MachineName;

    /// <summary>
    /// Optional labels/tags for this node.
    /// </summary>
    public Dictionary<string, string> Labels { get; set; } = new();
}

public class DockerSettings
{
    /// <summary>
    /// Docker daemon URI.
    /// Default: "unix:///var/run/docker.sock" (Linux)
    /// Windows: "npipe://./pipe/docker_engine"
    /// </summary>
    public string Host { get; set; } = "unix:///var/run/docker.sock";

    /// <summary>
    /// Connection timeout in seconds.
    /// Default: 30
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; } = 30;
}

public class RegistrationSettings
{
    /// <summary>
    /// URL of the Primary Service to register with.
    /// Example: "http://gameserver-docker:8080"
    /// </summary>
    public string PrimaryServiceUrl { get; set; } = string.Empty;

    /// <summary>
    /// Interval in seconds between heartbeat messages.
    /// Default: 30 seconds
    /// </summary>
    public int HeartbeatIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Whether agent registration is enabled.
    /// Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Capabilities this agent supports.
    /// Default: ["logs", "exec", "stats", "attach", "services"]
    /// </summary>
    public List<string> Capabilities { get; set; } = new() 
    { 
        "logs", "exec", "stats", "attach", "services" 
    };

    /// <summary>
    /// Timeout in seconds for initial connection.
    /// Default: 30 seconds
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Automatic reconnect delays (seconds).
    /// Default: [0, 2, 10, 30]
    /// </summary>
    public List<int> ReconnectDelaySeconds { get; set; } = new() { 0, 2, 10, 30 };

    /// <summary>
    /// Maximum startup retry attempts.
    /// Default: 30
    /// </summary>
    public int MaxStartupRetries { get; set; } = 30;

    /// <summary>
    /// Base delay between startup retries (exponential backoff).
    /// Default: 5 seconds
    /// </summary>
    public int StartupRetryDelaySeconds { get; set; } = 5;
}

public class StatsStreamSettings
{
    /// <summary>
    /// Buffer size for stats streaming.
    /// Default: 10
    /// </summary>
    public int BufferSize { get; set; } = 10;

    /// <summary>
    /// Sampling interval in seconds.
    /// Default: 1
    /// </summary>
    public int SamplingIntervalSeconds { get; set; } = 1;
}
```

### Agent appsettings.json (New Format)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:8080"
      }
    }
  },
  "Agent": {
    "Node": {
      "Name": "node-01",
      "Labels": {
        "environment": "production",
        "region": "us-east-1"
      }
    },
    "Docker": {
      "Host": "unix:///var/run/docker.sock",
      "ConnectionTimeoutSeconds": 30
    },
    "Registration": {
      "PrimaryServiceUrl": "http://gameserver-docker:8080",
      "HeartbeatIntervalSeconds": 30,
      "Enabled": true,
      "Capabilities": ["logs", "exec", "stats", "attach", "services"],
      "ConnectionTimeoutSeconds": 30,
      "ReconnectDelaySeconds": [0, 2, 10, 30],
      "MaxStartupRetries": 30,
      "StartupRetryDelaySeconds": 5
    },
    "StatsStream": {
      "BufferSize": 10,
      "SamplingIntervalSeconds": 1
    }
  }
}
```

---

## Part 2: Main Service Configuration (`GameServer.Docker`)

### New `GameServerOptions` Class

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
    /// Game type registry data sources.
    /// </summary>
    public RegistrySettings Registry { get; set; } = new();

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

public class RegistrySettings
{
    /// <summary>
    /// Path to game types registry JSON file.
    /// Default: "gametypes.json"
    /// </summary>
    public string GameTypesFile { get; set; } = "gametypes.json";

    /// <summary>
    /// Path to extended metadata registry JSON file.
    /// Default: "extended-metadata.json"
    /// </summary>
    public string ExtendedMetadataFile { get; set; } = "extended-metadata.json";
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

### Main Service appsettings.json (New Format)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
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
    "Registry": {
      "GameTypesFile": "gametypes.json",
      "ExtendedMetadataFile": "extended-metadata.json"
    },
    "UI": {
      "LoadBalancerDomain": "games.example.com",
      "VanityDomain": "games.dev.bulavafamily.com"
    }
  }
}
```

---

## Environment Variables to Remove

### Agent
- ❌ `LOG_LEVEL` → Use `Logging:LogLevel:Default` in appsettings.json
- ❌ `NODE_NAME` → Use `Agent:Node:Name` in appsettings.json
- ❌ `DOCKER_HOST` → Use `Agent:Docker:Host` in appsettings.json

### Main Service (UI)
- ❌ `LOADBALANCER_DOMAIN` → Use `GameServer:UI:LoadBalancerDomain` in appsettings.json

### Benefits of Removing Environment Variables
1. ✅ **Consistency** - Same pattern everywhere
2. ✅ **Testability** - Easy to mock IOptions<T>
3. ✅ **Type Safety** - Compile-time checking
4. ✅ **IntelliSense** - Editor support
5. ✅ **Validation** - Can use DataAnnotations
6. ✅ **Documentation** - XML comments in code

---

## Migration Summary

### Files to Create
1. ✅ `src/GameServer.Docker.Agent/Configurations/AgentOptions.cs`
2. ✅ `src/GameServer.Docker/Configurations/GameServerOptions.cs`

### Files to Remove
1. ❌ `src/GameServer.Docker/Configurations/DockerConnection.cs`
2. ❌ `src/GameServer.Docker/Configurations/ServiceOperationsOptions.cs`
3. ❌ `src/GameServer.Docker/Configurations/PortAllocation.cs`
4. ❌ `src/GameServer.Docker/Configurations/VolumeDriverConfigOptions.cs`
5. ❌ `src/GameServer.Docker/Configurations/NetworkOptions.cs`
6. ❌ `src/GameServer.Docker/Configurations/NodeAgentOptions.cs`
7. ❌ `src/GameServer.Docker/Configurations/GameTypeRegistryData.cs`
8. ❌ `src/GameServer.Docker/Configurations/GameTypeExtendedMetadataRegistryData.cs`
9. ❌ `src/GameServer.Docker.Agent/Configurations/AgentRegistrationOptions.cs`
10. ❌ `src/GameServer.Docker/Services/ServiceOperationsViaDirect.cs`
11. ❌ `src/GameServer.Docker/Services/DockerClientFactory.cs`

### Code Changes

**Agent Program.cs**:
```csharp
// OLD
var logLevelEnv = Environment.GetEnvironmentVariable("LOG_LEVEL") ?? "Information";
var dockerUri = Environment.GetEnvironmentVariable("DOCKER_HOST") ?? "unix:///var/run/docker.sock";
builder.Services.Configure<AgentRegistrationOptions>(builder.Configuration.GetSection("AgentRegistration"));

// NEW
builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection("Agent"));

// Use options
var options = builder.Services.BuildServiceProvider().GetRequiredService<IOptions<AgentOptions>>().Value;
var dockerUri = options.Docker.Host;
```

**Main Service Program.cs**:
```csharp
// OLD
builder.Services.Configure<NetworkOptions>(builder.Configuration.GetSection("NetworkOptions"));
builder.Services.Configure<PortAllocation>(builder.Configuration.GetSection("PortAllocation"));
builder.Services.Configure<VolumeDriverConfigOptions>(builder.Configuration.GetSection("VolumeDriverConfigOptions"));
builder.Services.Configure<NodeAgentOptions>(builder.Configuration.GetSection("NodeAgentOptions"));

// NEW
builder.Services.Configure<GameServerOptions>(builder.Configuration.GetSection("GameServer"));
```

**ServerDetails.razor**:
```csharp
// OLD
private string GetLoadBalancerDomain()
{
    return Environment.GetEnvironmentVariable("LOADBALANCER_DOMAIN") ?? VanityDomain;
}

// NEW
[Inject] private IOptions<GameServerOptions> Options { get; set; } = default!;

private string GetLoadBalancerDomain()
{
    return Options.Value.UI.LoadBalancerDomain;
}
```

---

## Testing Strategy

### Unit Tests
Update all tests to use unified options:
```csharp
// OLD
var mockNetOptions = new Mock<IOptions<NetworkOptions>>();
var mockVolOptions = new Mock<IOptions<VolumeDriverConfigOptions>>();

// NEW
var mockOptions = new Mock<IOptions<GameServerOptions>>();
mockOptions.Setup(o => o.Value).Returns(new GameServerOptions
{
    Network = new NetworkSettings { LoadBalancerNetwork = "traefik-public" },
    Volumes = new VolumeSettings { LocalStoragePath = "/data" }
});
```

---

## Documentation Updates

### Update These Files
1. ✅ `docs/ARCHITECTURE.md` - Remove Direct mode, show unified config
2. ✅ `docs/CONFIGURATION.md` - Complete rewrite for new structure
3. ✅ `docs/AGENT-SETUP.md` - New configuration format
4. ✅ `docs/DEPLOYMENT.md` - Environment variable removal
5. ✅ `README.md` - Updated configuration examples

---

## Benefits

### Consistency
- ✅ Both services use same pattern
- ✅ Options pattern throughout
- ✅ No environment variable mixing

### Testability
- ✅ Easy to mock IOptions<T>
- ✅ No Environment.GetEnvironmentVariable() calls
- ✅ Type-safe configuration

### Maintainability
- ✅ Single source of truth per service
- ✅ Clear hierarchical structure
- ✅ IntelliSense support

### Discoverability
- ✅ All settings in one place
- ✅ XML documentation
- ✅ Validation attributes

---

## Rollout Timeline

### Phase 1: Create New (Week 1)
- ✅ Create `AgentOptions.cs`
- ✅ Create `GameServerOptions.cs`
- ✅ Support both old and new simultaneously

### Phase 2: Migrate Code (Week 2)
- ✅ Update all service constructors
- ✅ Remove env var usage
- ✅ Update tests

### Phase 3: Remove Old (Week 3)
- ✅ Delete old configuration classes
- ✅ Delete Direct mode services
- ✅ Update documentation

---

**Status**: 📝 **Ready for Implementation**
**Breaking Changes**: Yes (configuration format)
**Backward Compatible**: No (clean break recommended)
