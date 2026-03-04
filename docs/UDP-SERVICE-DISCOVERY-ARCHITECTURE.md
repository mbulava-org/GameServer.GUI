# UDP-Based Service Discovery Architecture

## Overview

Implements **zero-configuration service discovery** using UDP multicast broadcasts. The Primary Service auto-detects its IP address on the shared Docker overlay network and broadcasts it. Agents automatically discover and connect without needing any endpoint configuration.

## Key Features

### 🎯 True Zero Configuration
- **Primary**: Auto-detects its container IP on the shared network (`NetworkOptions.NetworkName`)
- **Agent**: Listens for broadcasts and auto-connects using received IP
- **No DNS**: Uses direct IP communication on Docker overlay network
- **No manual configuration**: Everything discovered automatically

### 🔐 Security Built-in
- API keys generated and rotated automatically
- Keys distributed via broadcasts
- Short-lived credentials (5-minute rotation)

### 🌐 Multi-Master Ready
- Multiple Primary instances supported
- Agents connect to all discovered Primaries
- Automatic failover

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│              Docker Overlay Network (10.0.1.0/24)            │
│                                                               │
│  ┌──────────────────┐         UDP Multicast          ┌──────┴──────┐
│  │  Primary Service │────────► 239.1.1.1:5000 ◄──────│   Agent 1   │
│  │  IP: 10.0.1.5    │                                 │ IP: 10.0.1.8│
│  │  (Broadcaster)   │                                 │  (Listener) │
│  └──────────────────┘                                 └─────────────┘
│          │                                                    │
│          │ Auto-detects: 10.0.1.5                           │ Receives:
│          │ Broadcasts:                                       │ - IP: 10.0.1.5
│          │ - IP Address: 10.0.1.5                           │ - Port: 8080
│          │ - Port: 8080                                     │ - API Key
│          │ - API Key: abc123                                │
│          │ - Service ID                                     │
│          │                                                   │ Connects:
│          │                                                   │ http://10.0.1.5:8080
│          │◄──────────────────────────────────────────────────┤
│          │          /hubs/agentregistration                  │
│  ┌───────▼──────────┐                              ┌────────▼────────┐
│  │   Agent 2        │                              │    Agent 3      │
│  │   IP: 10.0.1.9   │                              │   IP: 10.0.1.10 │
│  └──────────────────┘                              └─────────────────┘
│                                                                       │
└───────────────────────────────────────────────────────────────────────┘
```

## How It Works

### 1. Primary Service Startup
```
1. Primary container starts on Docker overlay network
2. PrimaryServiceAnnouncementService starts
3. Auto-detects container IP on the shared network (e.g., 10.0.1.5)
4. Generates unique service ID
5. Generates initial API key
6. Begins broadcasting: {"endpoint": "http://10.0.1.5:8080", "apiKey": "...", ...}
```

### 2. IP Address Detection
```csharp
// Primary auto-detects its IP on the Docker overlay network
foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
{
    // Find interface with private IP (10.x, 172.16-31.x, 192.168.x)
    // This is the Docker overlay network
    if (isPrivateIP && isOperational)
    {
        return ipAddress; // e.g., "10.0.1.5"
    }
}
```

### 3. Broadcast Message
```json
{
  "serviceId": "gameserver-primary-newdev-001-123456789",
  "endpoint": "http://10.0.1.5:8080",
  "apiKey": "Qx7K...base64...",
  "timestamp": "2026-03-04T18:30:00Z",
  "version": "0.0.4.220",
  "capabilities": ["service-management", "agent-registration"]
}
```

### 4. Agent Discovery
```
1. Agent starts, listens on UDP 239.1.1.1:5000
2. Receives broadcast from Primary
3. Parses endpoint: http://10.0.1.5:8080
4. Stores API key
5. Connects to Primary using IP address
6. No DNS resolution needed!
```

## Network Configuration

### Docker Swarm
```yaml
services:
  gameserver-docker:
    networks:
      - gameserver-net
    # No need to publish broadcast port externally
    
  gameserver-agent:
    networks:
      - gameserver-net
    # Will receive broadcasts on internal network

networks:
  gameserver-net:
    driver: overlay
    # Overlay networks support multicast
```

### UDP Multicast Group
- **Address**: `239.1.1.1` (IPv4 multicast)
- **Port**: `5000`
- **TTL**: `1` (same subnet only for security)

## Security

### API Key Management
```csharp
public class ApiKeyManager
{
    private string _currentKey;
    private DateTime _keyGeneratedAt;
    private readonly TimeSpan _rotationInterval = TimeSpan.FromMinutes(5);
    
    public string GetCurrentKey()
    {
        if (DateTime.UtcNow - _keyGeneratedAt > _rotationInterval)
        {
            RotateKey();
        }
        return _currentKey;
    }
    
    private void RotateKey()
    {
        _currentKey = GenerateSecureKey();
        _keyGeneratedAt = DateTime.UtcNow;
    }
    
    private string GenerateSecureKey()
    {
        // Generate cryptographically secure random key
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}
```

### Message Signing (Optional Enhancement)
```csharp
public class SignedAnnouncementMessage : AnnouncementMessage
{
    public string Signature { get; set; }
    
    public bool Verify(string publicKey)
    {
        // Verify message signature using RSA or ECDSA
        // Prevents spoofing
    }
}
```

## Configuration

### Primary Service (`appsettings.json`)
```json
{
  "ServiceDiscovery": {
    "Enabled": true
  },
  "NetworkOptions": {
    "NetworkName": "gameserver-net"
  }
}
```

**That's it!** Everything else is auto-detected:
- ✅ Container IP address (auto-detected from network interface)
- ✅ Service ID (auto-generated from hostname + timestamp)
- ✅ API key (auto-generated and rotated)
- ✅ Port (hardcoded to 8080, standard for the service)

### Agent (`appsettings.json`)
```json
{
  "ServiceDiscovery": {
    "Enabled": true
  }
}
```

**Even simpler!** Just enable discovery and it works.

### Optional Advanced Settings

If you need to customize behavior:

```json
{
  "ServiceDiscovery": {
    "Enabled": true,
    "MulticastAddress": "239.1.1.1",
    "Port": 5000,
    "BroadcastIntervalSeconds": 5,
    "ApiKeyRotationMinutes": 5,
    "PrimaryStaleTimeoutSeconds": 30,
    "MulticastTtl": 1
  }
}
```

But **defaults are perfect for Docker Swarm overlay networks!**

## Advantages

### 1. Zero Configuration
- Agents don't need to know Primary endpoint
- Works in dynamic environments (Swarm, Kubernetes)
- Survives Primary restarts/migrations

### 2. High Availability
- Supports multiple Primary instances
- Automatic failover
- Load distribution

### 3. Security
- Automatic API key rotation
- Keys never stored in configuration
- Short-lived credentials

### 4. Network Resilience
- Continues working if one Primary fails
- Auto-reconnects when Primary comes back
- Handles network partitions gracefully

## Implementation Priority

### Phase 1: Basic Discovery (MVP)
1. ✅ `PrimaryServiceAnnouncementService` - Basic UDP broadcast
2. ✅ `AgentDiscoveryListenerService` - Basic UDP listening
3. ✅ `AnnouncementMessage` - Simple message format
4. ✅ Agent auto-connection on discovery

### Phase 2: Security (Next)
1. ✅ `ApiKeyManager` - Key generation and rotation
2. ✅ API key validation in SignalR hub
3. ✅ Key rotation handling in agents

### Phase 3: Multi-Master (Advanced)
1. ⏳ Multiple Primary support
2. ⏳ Load balancing between Primaries
3. ⏳ Distributed consensus (if needed)

### Phase 4: Enhanced Security (Future)
1. ⏳ Message signing
2. ⏳ Encryption
3. ⏳ Certificate-based authentication

## Monitoring

### Primary Metrics
- Broadcasts sent per minute
- Active listening agents
- Failed broadcast attempts
- API key rotations

### Agent Metrics
- Broadcasts received per minute
- Active Primary connections
- Connection failures
- Stale Primary removals

## Troubleshooting

### Agent Not Discovering Primary

**Symptoms**: Agent logs show no broadcasts received

**Checks**:
1. Verify UDP port 5000 is open
2. Check multicast is enabled on network
3. Verify both services on same Docker network
4. Check firewall rules

### Multiple Primaries Not Working

**Symptoms**: Agent only connects to one Primary

**Checks**:
1. Verify different `ServiceId` for each Primary
2. Check agent's `PrimaryServiceRegistry` has multiple entries
3. Verify SignalR connections to both

### Key Rotation Failures

**Symptoms**: Agent disconnects during key rotation

**Checks**:
1. Verify agent receives new key before old expires
2. Check key rotation interval is reasonable
3. Verify agent updates stored key correctly

## Performance Considerations

### Network Overhead
- ~100 bytes per broadcast message
- Broadcast every 5 seconds
- ~20 bytes/second per Primary
- Negligible for Docker overlay network

### CPU Usage
- UDP broadcast: minimal (<1% CPU)
- UDP listening: minimal (<1% CPU)
- JSON serialization: ~0.1ms per message

### Memory Usage
- Message buffer: ~1KB
- Primary registry: ~1KB per Primary
- Total: <10KB overhead

## Migration Strategy

### From Current System
```
1. Deploy new code with discovery enabled
2. Keep existing configuration as fallback
3. Monitor discovery success rate
4. Remove hardcoded endpoints when stable
```

### Backward Compatibility
```csharp
// Agent tries discovery first, falls back to config
if (discoveredPrimary != null)
{
    await ConnectToPrimary(discoveredPrimary);
}
else if (!string.IsNullOrEmpty(_config.FallbackEndpoint))
{
    await ConnectToPrimary(_config.FallbackEndpoint);
}
```

## Testing

### Unit Tests
- Message serialization/deserialization
- API key generation and validation
- Primary registry add/remove/stale detection

### Integration Tests
- Primary broadcasts, Agent receives
- Key rotation end-to-end
- Multiple Primary discovery
- Failover scenarios

### Load Tests
- 100+ agents listening
- Multiple Primaries broadcasting
- Rapid Primary restarts

## Future Enhancements

### 1. Service Mesh Integration
- Integrate with Envoy/Istio
- Use service mesh discovery instead of UDP

### 2. DNS-SD (DNS Service Discovery)
- Use DNS-SD as alternative to UDP
- Better for Kubernetes environments

### 3. Consul/Etcd Integration
- Use external service registry
- Better for multi-cluster deployments

### 4. gRPC Health Checks
- Replace UDP broadcasts with gRPC health checks
- More efficient, better error handling

## References

- [UDP Multicast in .NET](https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.udpclient)
- [Docker Swarm Networking](https://docs.docker.com/network/overlay/)
- [Service Discovery Patterns](https://microservices.io/patterns/server-side-discovery.html)
- [API Key Best Practices](https://cloud.google.com/docs/authentication/api-keys)
