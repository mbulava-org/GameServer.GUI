# UDP Service Discovery - Implementation Plan

## ✅ What's Been Created

### 1. Architecture Document
**File**: `docs/UDP-SERVICE-DISCOVERY-ARCHITECTURE.md`
- Complete architectural overview
- Message flow diagrams
- Security considerations
- Configuration examples
- Monitoring and troubleshooting guides

### 2. Shared Models
**File**: `src/GameServer.Docker/Models/ServiceAnnouncementMessage.cs`
- Message format for UDP broadcasts
- Contains: ServiceId, Endpoint, ApiKey, Timestamp, Version, Capabilities

### 3. Configuration
**File**: `src/GameServer.Docker/Configurations/ServiceDiscoveryOptions.cs`
- Configuration options for both Primary and Agent
- Multicast address, port, intervals, timeouts

### 4. Primary Service Broadcaster
**File**: `src/GameServer.Docker/Services/PrimaryServiceAnnouncementService.cs`
- BackgroundService that broadcasts presence
- API key generation and rotation
- UDP multicast sender
- Configurable broadcast interval

## 🚧 Next Steps to Complete

### Phase 1: Agent Discovery Listener

#### 1. Create `AgentDiscoveryListenerService.cs` (Agent-side)
```csharp
// Location: src/GameServer.Docker.Agent/Services/AgentDiscoveryListenerService.cs

public class AgentDiscoveryListenerService : BackgroundService
{
    // Listen for UDP broadcasts
    // Parse ServiceAnnouncementMessage
    // Trigger connection to discovered Primary
    // Handle API key updates
}
```

#### 2. Create `PrimaryServiceRegistry.cs` (Agent-side)
```csharp
// Location: src/GameServer.Docker.Agent/Services/PrimaryServiceRegistry.cs

public class PrimaryServiceRegistry
{
    // Store discovered Primary services
    // Track last heartbeat
    // Remove stale entries
    // Provide active Primary list
}
```

#### 3. Update `AgentRegistrationService.cs` (Agent-side)
```csharp
// Use discovered endpoint instead of configured endpoint
// Send API key in SignalR connection headers
// Handle key rotation (reconnect with new key)
```

### Phase 2: API Key Validation

#### 4. Create `ApiKeyValidationMiddleware.cs` (Primary-side)
```csharp
// Validate API key in SignalR hub connections
// Check against current key from PrimaryServiceAnnouncementService
// Reject invalid/expired keys
```

#### 5. Update `AgentRegistrationHub.cs` (Primary-side)
```csharp
// Validate API key from connection headers
// Track which agents are using which keys
// Log authentication attempts
```

### Phase 3: Integration

#### 6. Update `Program.cs` (Primary)
```csharp
// Add ServiceDiscoveryOptions configuration
builder.Services.Configure<ServiceDiscoveryOptions>(
    builder.Configuration.GetSection("ServiceDiscovery"));

// Register announcement service
builder.Services.AddHostedService<PrimaryServiceAnnouncementService>();
```

#### 7. Update `Program.cs` (Agent)
```csharp
// Add ServiceDiscoveryOptions configuration
builder.Services.Configure<ServiceDiscoveryOptions>(
    builder.Configuration.GetSection("ServiceDiscovery"));

// Register discovery services
builder.Services.AddSingleton<PrimaryServiceRegistry>();
builder.Services.AddHostedService<AgentDiscoveryListenerService>();
```

#### 8. Update `appsettings.json` files
```json
// Primary: src/GameServer.Docker/appsettings.json
{
  "ServiceDiscovery": {
    "Enabled": true,
    "MulticastAddress": "239.1.1.1",
    "Port": 5000,
    "BroadcastIntervalSeconds": 5,
    "ApiKeyRotationMinutes": 5
  }
}

// Agent: src/GameServer.Docker.Agent/appsettings.json
{
  "ServiceDiscovery": {
    "Enabled": true,
    "MulticastAddress": "239.1.1.1",
    "Port": 5000,
    "PrimaryStaleTimeoutSeconds": 30,
    "FallbackEndpoint": "http://gameserver-docker_gameserver-docker:8080"
  }
}
```

### Phase 4: Testing

#### 9. Unit Tests
- Message serialization/deserialization
- API key generation and validation
- Registry add/remove/stale detection

#### 10. Integration Tests
- Primary broadcasts, Agent receives
- Agent auto-connects after discovery
- Key rotation end-to-end
- Failover scenarios

## 📋 Implementation Checklist

### Primary Service
- [x] ServiceAnnouncementMessage model
- [x] ServiceDiscoveryOptions configuration
- [x] PrimaryServiceAnnouncementService (broadcaster)
- [ ] API key validation middleware
- [ ] Update AgentRegistrationHub for key validation
- [ ] Update Program.cs to register services
- [ ] Update appsettings.json with discovery config

### Agent Service
- [ ] PrimaryServiceRegistry (stores discovered Primaries)
- [ ] AgentDiscoveryListenerService (UDP listener)
- [ ] Update AgentRegistrationService (use discovered endpoint)
- [ ] Update Program.cs to register services
- [ ] Update appsettings.json with discovery config

### Testing
- [ ] Unit tests for message serialization
- [ ] Unit tests for API key generation
- [ ] Integration test: Primary broadcast → Agent receive
- [ ] Integration test: Agent auto-connect
- [ ] Integration test: API key rotation
- [ ] Integration test: Multiple Primary instances

### Documentation
- [x] Architecture documentation
- [ ] Configuration guide
- [ ] Deployment guide
- [ ] Troubleshooting guide
- [ ] API documentation

## 🎯 Expected Timeline

### Immediate (Phase 1)
- **Time**: 2-3 hours
- **Goal**: Agent can discover and connect to Primary
- **Deliverables**: 
  - AgentDiscoveryListenerService
  - PrimaryServiceRegistry
  - Updated AgentRegistrationService

### Short-term (Phase 2)
- **Time**: 1-2 hours
- **Goal**: API key validation working
- **Deliverables**:
  - ApiKeyValidationMiddleware
  - Updated AgentRegistrationHub

### Medium-term (Phase 3)
- **Time**: 1 hour
- **Goal**: Everything integrated and configured
- **Deliverables**:
  - Updated Program.cs files
  - Updated appsettings.json files
  - Configuration documentation

### Long-term (Phase 4)
- **Time**: 2-3 hours
- **Goal**: Fully tested and production-ready
- **Deliverables**:
  - Comprehensive test suite
  - Deployment documentation
  - Monitoring setup

**Total estimated time**: 6-9 hours

## 🚀 Quick Start (When Complete)

### 1. Deploy Primary Service
```bash
docker service create \
  --name gameserver-docker \
  --network gameserver-net \
  --env ServiceDiscovery__Enabled=true \
  gameserver-docker:latest
```

### 2. Deploy Agent (No Configuration Needed!)
```bash
docker service create \
  --name gameserver-agent \
  --mode global \
  --network gameserver-net \
  --mount type=bind,source=/var/run/docker.sock,target=/var/run/docker.sock \
  --env ServiceDiscovery__Enabled=true \
  gameserver-agent:latest
```

### 3. Watch the Magic!
```bash
# Agent logs will show:
[18:30:00] AgentDiscoveryListenerService started, listening on 239.1.1.1:5000
[18:30:05] Discovered Primary Service: gameserver-primary-001 at http://...
[18:30:05] Connecting to Primary Service with API key...
[18:30:06] Successfully registered with Primary Service!

# Primary logs will show:
[18:30:00] Primary Service announcements starting...
[18:30:05] Broadcast sent: 456 bytes
[18:30:06] Agent newdev-docker-001 registered successfully
```

## 🔧 Configuration Examples

### Minimal (Zero Config)
```json
{
  "ServiceDiscovery": {
    "Enabled": true
  }
}
```
Everything else uses sensible defaults!

### Production (Custom Settings)
```json
{
  "ServiceDiscovery": {
    "Enabled": true,
    "MulticastAddress": "239.1.1.1",
    "Port": 5000,
    "BroadcastIntervalSeconds": 10,
    "ApiKeyRotationMinutes": 10,
    "PrimaryStaleTimeoutSeconds": 45
  }
}
```

### High Security (Frequent Key Rotation)
```json
{
  "ServiceDiscovery": {
    "Enabled": true,
    "ApiKeyRotationMinutes": 1,
    "PrimaryStaleTimeoutSeconds": 10
  }
}
```

## 💡 Next Steps

Would you like me to:

1. **Continue with Agent implementation** - Create the discovery listener and registry?
2. **Add API key validation** - Implement middleware and hub validation?
3. **Create tests** - Start with unit tests for existing components?
4. **Update configuration** - Add to appsettings.json and Program.cs?

Let me know which part you'd like me to tackle next!
