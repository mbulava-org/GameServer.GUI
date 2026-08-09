# Network and Load Balancer Configuration Guide

## Overview

The GameServer.Docker service now supports flexible network attachment and multi-provider load balancer integration. Services are intelligently attached to networks based on their functionality requirements.

---

## Configuration Options

### NetworkOptions in appsettings.json

```json
{
  "NetworkOptions": {
    "NetworkName": "gameserver-private",
    "LoadBalancerNetwork": "traefik-public",
    "LoadBalancerProvider": "traefik"
  }
}
```

### Configuration Properties

#### **NetworkName** (optional)
- **Purpose**: Network for agent-to-primary service communication
- **Default**: `null` (not attached)
- **Note**: Individual game services typically don't need this network as they expose their needed ports directly
- **Future Use**: May be used for service-to-service communication

**Example**:
```json
"NetworkName": "gameserver-private"
```

#### **LoadBalancerNetwork** (optional)
- **Purpose**: Network where the load balancer is running
- **Default**: `"traefik-public"`
- **Behavior**: Services are ONLY attached if they have web hosts configured
- **Auto-Attachment**: Enabled automatically when `WebHosts` are defined in GameType metadata

**Example**:
```json
"LoadBalancerNetwork": "traefik-public"
```

#### **LoadBalancerProvider** (required)
- **Purpose**: Specifies which load balancer to generate labels for
- **Default**: `"traefik"`
- **Supported Values**:
  - `"traefik"` - Traefik v2/v3 (full support)
  - `"nginx"` - Nginx (basic labels, requires additional config)
  - `"caddy"` - Caddy v2 (basic labels)
  - `"none"` - Disable label generation

**Example**:
```json
"LoadBalancerProvider": "traefik"
```

---

## Network Attachment Logic

### Conditional Attachment

Services are attached to networks **only when needed**:

| Network | Attached When | Purpose |
|---------|---------------|---------|
| `NetworkName` | Always (if configured) | Future service-to-service communication |
| `LoadBalancerNetwork` | WebHosts configured | Reverse proxy discovery |

### Example Scenarios

#### Scenario 1: Service WITHOUT Web Hosts
```json
{
  "NetworkOptions": {
    "NetworkName": "gameserver-private",
    "LoadBalancerNetwork": "traefik-public"
  }
}
```

**Result**: 
- ✅ Attached to `gameserver-private` (if needed)
- ❌ **NOT** attached to `traefik-public` (no web hosts)
- Ports exposed directly

#### Scenario 2: Service WITH Web Hosts
```json
{
  "NetworkOptions": {
    "LoadBalancerNetwork": "traefik-public",
    "LoadBalancerProvider": "traefik"
  }
}
```

**GameType has WebHosts defined**

**Result**:
- ✅ Attached to `traefik-public`
- ✅ Traefik labels generated
- Web interfaces accessible via reverse proxy

#### Scenario 3: Network Name Not Configured
```json
{
  "NetworkOptions": {
    "LoadBalancerNetwork": "ingress-net",
    "LoadBalancerProvider": "traefik"
  }
}
```

**Result**:
- ❌ **NOT** attached to any game network
- ✅ Attached to `ingress-net` (if web hosts)
- Services expose ports directly

---

## Load Balancer Providers

### Traefik (Recommended)

**Full Support** with automatic service discovery.

#### Configuration
```json
{
  "NetworkOptions": {
    "LoadBalancerNetwork": "traefik-public",
    "LoadBalancerProvider": "traefik"
  }
}
```

#### Generated Labels Example
```yaml
traefik.enable: "true"
traefik.http.routers.minecraft-abc123.rule: "PathPrefix(`/game-abc123`)"
traefik.http.routers.minecraft-abc123.service: "minecraft-abc123"
traefik.http.services.minecraft-abc123.loadbalancer.server.port: "8123"
traefik.http.middlewares.minecraft-abc123-strip.stripprefix.prefixes: "/game-abc123"
traefik.http.routers.minecraft-abc123.middlewares: "minecraft-abc123-strip"
```

#### Traefik Docker Compose Example
```yaml
services:
  traefik:
    image: traefik:v3.0
    command:
      - --providers.docker.swarmMode=true
      - --providers.docker.exposedByDefault=false
      - --providers.docker.network=traefik-public
      - --entrypoints.web.address=:80
      - --entrypoints.websecure.address=:443
    ports:
      - "80:80"
      - "443:443"
    networks:
      - traefik-public
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
    deploy:
      placement:
        constraints:
          - node.role == manager

networks:
  traefik-public:
    external: true
```

---

### Nginx

**Basic Support** - Labels generated for documentation, additional configuration required.

#### Configuration
```json
{
  "NetworkOptions": {
    "LoadBalancerNetwork": "nginx-net",
    "LoadBalancerProvider": "nginx"
  }
}
```

#### Generated Labels
```yaml
nginx.enable: "true"
nginx.minecraft-abc123.path: "/game-abc123"
nginx.minecraft-abc123.port: "8123"
```

**Note**: Nginx requires additional configuration files. Labels are for reference only.

---

### Caddy

**Basic Support** - Labels generated, Caddy Docker plugin required.

#### Configuration
```json
{
  "NetworkOptions": {
    "LoadBalancerNetwork": "caddy-net",
    "LoadBalancerProvider": "caddy"
  }
}
```

#### Generated Labels
```yaml
caddy: "true"
caddy.minecraft-abc123.path: "/game-abc123"
caddy.minecraft-abc123.reverse_proxy: "{{upstreams 8123}}"
```

---

### None

Disable all label generation (manual configuration).

#### Configuration
```json
{
  "NetworkOptions": {
    "LoadBalancerProvider": "none"
  }
}
```

**Result**: No labels, no network attachment, manual setup required.

---

## Environment Variables

### LOADBALANCER_DOMAIN
Base domain for generated URLs in UI.

```bash
LOADBALANCER_DOMAIN=games.example.com
```

**Used by**: `WebHostsDisplay` and `WebHostsPreview` components

---

## Complete appsettings.json Example

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "NetworkOptions": {
    "NetworkName": null,
    "LoadBalancerNetwork": "traefik-public",
    "LoadBalancerProvider": "traefik"
  },
  "VolumeDriverConfigOptions": {
    "LocalStoragePath": "/mnt/gameservers",
    "SubPathFormat": "{gameTypeKey}/{serverId}/{Source}"
  },
  "PortAllocation": {
    "StartPort": 25565,
    "EndPort": 35565
  }
}
```

---

## Network Setup

### Create Load Balancer Network

```bash
# Traefik
docker network create --driver=overlay traefik-public

# Nginx
docker network create --driver=overlay nginx-net

# Caddy
docker network create --driver=overlay caddy-net
```

### Verify Network
```bash
docker network ls
docker network inspect traefik-public
```

---

## Troubleshooting

### Services Not Accessible via Load Balancer

**Check**:
1. Is `LoadBalancerNetwork` configured?
2. Does GameType have web hosts defined?
3. Is service attached to the network?

**Debug**:
```bash
# Check service networks
docker service inspect <service-name> --format '{{json .Spec.TaskTemplate.Networks}}' | jq

# Check labels
docker service inspect <service-name> --format '{{json .Spec.Labels}}' | jq
```

### Wrong Provider Labels

**Check**:
```json
{
  "NetworkOptions": {
    "LoadBalancerProvider": "traefik"  // ← Verify this matches your setup
  }
}
```

### Network Not Found

**Error**: `network not found: traefik-public`

**Fix**:
```bash
docker network create --driver=overlay traefik-public
```

---

## Migration Guide

### From Old Config (Environment Variable)

**Old**:
```bash
LOADBALANCER_NETWORK=traefik-public
```

**New**:
```json
{
  "NetworkOptions": {
    "LoadBalancerNetwork": "traefik-public",
    "LoadBalancerProvider": "traefik"
  }
}
```

### Benefits of New Config
- ✅ Centralized configuration
- ✅ Multi-provider support
- ✅ Conditional network attachment
- ✅ Better logging and diagnostics
- ✅ Type-safe configuration

---

## Best Practices

1. **Use Traefik** for automatic service discovery
2. **Set NetworkName to null** unless you need service-to-service communication
3. **Always configure LoadBalancerNetwork** if using web hosts
4. **Match LoadBalancerProvider** to your actual load balancer
5. **Use overlay networks** for Swarm mode
6. **Monitor logs** for network attachment messages

---

## Logging

### Network Attachment Messages

**Service WITHOUT web hosts**:
```
[INFO] Service will be created without network attachments (ports will be exposed directly).
```

**Service WITH web hosts** (LoadBalancerNetwork configured):
```
[INFO] Attaching service to load balancer network: traefik-public (for 2 web hosts)
```

**Missing LoadBalancerNetwork**:
```
[WARN] Service has web hosts configured but no LoadBalancerNetwork is set. 
       Web interfaces will not be accessible via reverse proxy.
```

---

## Summary

- **NetworkName**: Optional, for future service-to-service communication
- **LoadBalancerNetwork**: Required for web host access, auto-attached when needed
- **LoadBalancerProvider**: Configurable, defaults to Traefik
- **Conditional Logic**: Networks attached only when functionality requires them
- **Multi-Provider**: Support for Traefik, Nginx, Caddy, or none
