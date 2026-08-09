# Reverse Proxy / Web Host Configuration

## Overview

The GameServer.GUI system now supports automatic reverse proxy configuration for game servers that expose web interfaces (like Dynmap for Minecraft, web admin panels, etc.). This feature:

- **Declaratively defines** what web interfaces exist at the GameType level
- **Conditionally enables** routes based on environment variable values
- **Dynamically resolves** container ports from settings
- **Auto-generates** Traefik labels for service discovery
- **Automatically attaches** services to load balancer networks

---

## Configuration

### appsettings.json

```json
{
  "NetworkOptions": {
    "NetworkName": null,
    "LoadBalancerNetwork": "traefik-public",
    "LoadBalancerProvider": "traefik"
  }
}
```

**Key Settings**:
- `LoadBalancerNetwork`: Network where load balancer is running (default: "traefik-public")
- `LoadBalancerProvider`: Load balancer type - "traefik", "nginx", "caddy", or "none" (default: "traefik")
- `NetworkName`: Optional game network (typically not needed)

**See**: `docs/NETWORK-AND-LOADBALANCER-CONFIG.md` for detailed configuration guide.

### Environment Variables (Optional UI Overrides)

```bash
# Domain for generated URLs in UI
LOADBALANCER_DOMAIN=games.example.com
```

---

## Architecture

### Components

1. **WebHostDefinition** (`GameTypeExtendedMetadata.cs`)
   - Declares a web interface that a game server exposes
   - Supports conditional enabling via `EnabledWhen` property
   - Supports dynamic ports via `ContainerPortVariable` property

2. **WebHostResolver** (`WebHostResolver.cs`)
   - Evaluates `EnabledWhen` conditions against server settings
   - Resolves dynamic ports from environment variables
   - Filters out disabled hosts

3. **DockerServiceHelper** (`DockerServiceHelper.cs`)
   - Uses WebHostResolver during service creation
   - Generates Traefik labels for discovered hosts
   - Attaches services to load balancer network

4. **UI Components**
   - `ExtendedMetadataEditor.razor` - GameType-level configuration
   - `WebHostDialog.razor` - Add/edit web host definitions
   - `StepReview.razor` - Preview generated URLs during server creation

## Data Model

### WebHostDefinition

```csharp
public class WebHostDefinition
{
    // Display name (e.g., "Dynmap", "Admin Panel")
    public string Name { get; set; }
    
    // Fixed container port (used when ContainerPortVariable is null)
    public uint ContainerPort { get; set; }
    
    // Description of this interface
    public string Description { get; set; }
    
    // Optional custom URL path segment (defaults to lowercase name)
    public string? PathSegment { get; set; }
    
    // Whether authentication is required
    public bool RequiresAuth { get; set; }
    
    // CONDITIONAL: Only enable if this condition is true
    // Format: "VAR=value" or "VAR!=value"
    // Example: "DYNMAP_ENABLED=true"
    public string? EnabledWhen { get; set; }
    
    // DYNAMIC PORT: Read port from this environment variable
    // If set, ContainerPort is ignored
    // Example: "WEBUI_PORT"
    public string? ContainerPortVariable { get; set; }
}
```

### ResolvedWebHost

After evaluation, a resolved host contains:

```csharp
public class ResolvedWebHost
{
    public string Name { get; set; }
    public uint ContainerPort { get; set; }    // Resolved from variable or fixed
    public string Description { get; set; }
    public string PathSegment { get; set; }    // Auto-generated if not specified
    public bool RequiresAuth { get; set; }
}
```

## Configuration Examples

### Example 1: Simple Fixed Port

```json
{
  "Name": "Admin Panel",
  "ContainerPort": 8080,
  "Description": "Web-based administration interface",
  "PathSegment": "admin",
  "RequiresAuth": false,
  "EnabledWhen": null,
  "ContainerPortVariable": null
}
```

**Result**: Always creates route to port 8080 at `/game-{serverId}/admin/`

### Example 2: Conditional Enabling

```json
{
  "Name": "Dynmap",
  "ContainerPort": 8123,
  "Description": "Real-time world map",
  "PathSegment": "map",
  "RequiresAuth": false,
  "EnabledWhen": "DYNMAP_ENABLED=true",
  "ContainerPortVariable": null
}
```

**Result**: Only creates route if server has setting `DYNMAP_ENABLED=true`

### Example 3: Dynamic Port

```json
{
  "Name": "Web UI",
  "ContainerPort": 8080,
  "Description": "Customizable web interface",
  "PathSegment": "ui",
  "RequiresAuth": false,
  "EnabledWhen": "WEB_ENABLED=true",
  "ContainerPortVariable": "WEBUI_PORT"
}
```

**Result**: 
- Only creates route if `WEB_ENABLED=true`
- Uses port from `WEBUI_PORT` setting (e.g., if `WEBUI_PORT=9090`, routes to container port 9090)

### Example 4: Multiple Hosts

For a Minecraft server with multiple web interfaces:

```json
{
  "WebHosts": [
    {
      "Name": "Dynmap",
      "ContainerPort": 8123,
      "Description": "Real-time world map",
      "EnabledWhen": "DYNMAP_ENABLED=true"
    },
    {
      "Name": "BlueMap",
      "ContainerPort": 8100,
      "Description": "3D world renderer",
      "EnabledWhen": "BLUEMAP_ENABLED=true"
    },
    {
      "Name": "Web Console",
      "ContainerPort": 8080,
      "Description": "Admin console",
      "RequiresAuth": true
    }
  ]
}
```

**Generated URLs**:
- `https://yourdomain.com/game-abc123/` → Dynmap (first host gets base path)
- `https://yourdomain.com/game-abc123/bluemap/` → BlueMap
- `https://yourdomain.com/game-abc123/web-console/` → Web Console

## Generated Traefik Labels

For each resolved web host, the system generates:

```yaml
traefik.enable: "true"
traefik.http.routers.{serviceName}.rule: "PathPrefix(`/game-{serverId}`)"
traefik.http.routers.{serviceName}.service: "{serviceName}"
traefik.http.services.{serviceName}.loadbalancer.server.port: "8123"
traefik.http.middlewares.{serviceName}-strip.stripprefix.prefixes: "/game-{serverId}"
traefik.http.routers.{serviceName}.middlewares: "{serviceName}-strip"
```

For additional hosts (2+):

```yaml
traefik.http.routers.{serviceName}-dynmap.rule: "PathPrefix(`/game-{serverId}/dynmap`)"
traefik.http.routers.{serviceName}-dynmap.service: "{serviceName}-dynmap"
traefik.http.services.{serviceName}-dynmap.loadbalancer.server.port: "8123"
traefik.http.middlewares.{serviceName}-dynmap-strip.stripprefix.prefixes: "/game-{serverId}/dynmap"
traefik.http.routers.{serviceName}-dynmap.middlewares: "{serviceName}-dynmap-strip"
```

## Network Configuration

Services with web hosts are automatically attached to:

1. **Game Network** (from `NetworkOptions:NetworkName` config)
2. **Load Balancer Network** (from `LOADBALANCER_NETWORK` environment variable, default: `traefik-public`)

This dual-network approach allows:
- Game servers to communicate with each other on the game network
- Traefik to discover and route to services on the load balancer network

## UI Workflow

### 1. Configure GameType (Administrator)

1. Navigate to **Game Types** page
2. Select a game type and click **Edit Extended Metadata**
3. Go to **Web Hosts** tab
4. Click **Add Web Host**
5. Fill in:
   - Name (e.g., "Dynmap")
   - Description
   - Port Configuration:
     - Fixed Port: Enter port number
     - From Variable: Enter environment variable name
   - Conditional Enabling (optional):
     - Enter condition like `DYNMAP_ENABLED=true`
   - Path segment (optional, auto-generated from name)
   - Authentication requirement
6. Click **Add**
7. Repeat for additional hosts
8. Click **Save All Changes**

### 2. Create Server (User)

1. Use the **Create Server** wizard
2. In **Step: Game Settings**, configure environment variables
3. If web hosts are defined with conditions, those conditions are evaluated
4. In **Step: Review**, see the generated web URLs
5. Create the server
6. Access web interfaces via the generated URLs

### 3. Runtime Behavior

- Service is created with Traefik labels
- Service connects to both game and load balancer networks
- Traefik discovers the service via labels
- Routes are created based on path prefix rules
- Web interfaces become accessible via the load balancer

## Condition Evaluation Logic

### Format

- `VARIABLE=value` - Must equal value
- `VARIABLE!=value` - Must NOT equal value

### Case Sensitivity

Comparisons are **case-insensitive**.

### Missing Variables

If a variable is not set in server settings, it's treated as an empty string.

### Examples

| Condition | Server Setting | Result |
|-----------|---------------|---------|
| `DYNMAP_ENABLED=true` | `DYNMAP_ENABLED=true` | ✅ Enabled |
| `DYNMAP_ENABLED=true` | `DYNMAP_ENABLED=false` | ❌ Disabled |
| `DYNMAP_ENABLED=true` | (not set) | ❌ Disabled |
| `WEB_MODE!=disabled` | `WEB_MODE=enabled` | ✅ Enabled |
| `WEB_MODE!=disabled` | `WEB_MODE=disabled` | ❌ Disabled |
| `WEB_MODE!=disabled` | (not set) | ✅ Enabled (empty != "disabled") |

## Port Resolution Logic

1. If `ContainerPortVariable` is null/empty → use `ContainerPort`
2. If `ContainerPortVariable` is set:
   - Look up variable in server settings
   - Parse as uint
   - Validate range (1-65535)
   - If invalid/missing → host is disabled

## Environment Variables

### LOADBALANCER_NETWORK

Specifies the Docker network where the load balancer (Traefik) is running.

**Default**: `traefik-public`

**Example**:
```bash
LOADBALANCER_NETWORK=ingress-network
```

## Future Enhancements

### Planned Features

1. **Authentication Integration**
   - Link `RequiresAuth` to actual auth middlewares
   - Support multiple auth providers (Basic, OAuth, OIDC)

2. **Custom Middlewares**
   - Rate limiting per host
   - CORS configuration
   - Custom headers

3. **Health Checks**
   - Auto-disable routes if health check fails
   - Retry logic

4. **SSL/TLS Configuration**
   - Per-host certificate configuration
   - Let's Encrypt integration

5. **Other Load Balancers**
   - Nginx support
   - Caddy support
   - Envoy support

### Extensibility Points

The label generation logic is isolated in `GenerateReverseProxyLabels()`, making it easy to:
- Support different load balancers
- Add new middleware types
- Customize routing strategies

## Testing

### Unit Tests

Test `WebHostResolver`:

```csharp
[Fact]
public void ResolveWebHosts_FiltersDisabledHosts()
{
    var definitions = new List<WebHostDefinition>
    {
        new() { Name = "A", ContainerPort = 8080, EnabledWhen = "ENABLED=true" },
        new() { Name = "B", ContainerPort = 8081, EnabledWhen = "ENABLED=false" }
    };
    
    var settings = new Dictionary<string, string>
    {
        ["ENABLED"] = "false"
    };
    
    var resolver = new WebHostResolver(logger);
    var resolved = resolver.ResolveWebHosts(definitions, settings);
    
    Assert.Single(resolved);
    Assert.Equal("B", resolved[0].Name);
}
```

### Integration Tests

1. Create a game type with web hosts
2. Create a server with required settings
3. Verify Traefik labels are generated
4. Verify service is attached to load balancer network
5. Verify routes are accessible

## Troubleshooting

### Routes not created

**Check**:
1. Are `EnabledWhen` conditions met?
2. Is `ContainerPortVariable` resolving correctly?
3. Is service attached to load balancer network?
4. Are Traefik labels present on service?

**Debug**:
```bash
# Check service labels
docker service inspect <service-name> --format '{{json .Spec.Labels}}' | jq

# Check networks
docker service inspect <service-name> --format '{{json .Spec.TaskTemplate.Networks}}'

# Check Traefik logs
docker service logs <traefik-service>
```

### Wrong port being used

**Check**:
1. If using `ContainerPortVariable`, verify setting value
2. Check resolved host logs in Primary Service logs

**Debug**:
```bash
# View Primary Service logs
docker service logs gameserver-docker | grep "Generated Traefik labels"
```

### Condition not evaluating correctly

**Check**:
1. Variable name matches exactly (including case)
2. Value comparison is correct
3. Use `!=` for negation, not `!`

## Configuration Reference

### appsettings.json

```json
{
  "NetworkOptions": {
    "NetworkName": "game-network"
  }
}
```

### Environment Variables

```bash
# Load balancer network name
LOADBALANCER_NETWORK=traefik-public
```

### Docker Compose (Traefik)

```yaml
services:
  traefik:
    image: traefik:v3.0
    networks:
      - traefik-public
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
    command:
      - --providers.docker.swarmMode=true
      - --providers.docker.exposedByDefault=false
      - --providers.docker.network=traefik-public
      
networks:
  traefik-public:
    external: true
```
