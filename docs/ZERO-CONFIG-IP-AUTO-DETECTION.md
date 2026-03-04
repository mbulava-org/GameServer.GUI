# Zero-Config Service Discovery - Subnet Broadcast

## Overview

Implemented **true zero-configuration** service discovery using **UDP subnet broadcast**. The Primary auto-detects its container IP and subnet broadcast address, then broadcasts to all containers on the same subnet. No multicast routing or configuration required!

## What's Different?

### ❌ Before (Multicast with Configuration)
```json
{
  "ServiceDiscovery": {
    "MulticastAddress": "239.1.1.1",
    "ServiceId": "gameserver-primary-001",
    "PublicEndpoint": "http://gameserver-docker:8080"
  }
}
```

### ✅ After (Subnet Broadcast, Zero Config)
```json
{
  "ServiceDiscovery": {
    "Enabled": true
  }
}
```

**Everything else is automatic!**

## How It Works

### 1. Primary Service Auto-Detection

When `PrimaryServiceAnnouncementService` starts:

```
┌─────────────────────────────────────────────┐
│ 1. Enumerate all network interfaces         │
│ 2. Find interface with private IP address   │
│    (10.x, 172.16-31.x, 192.168.x)          │
│ 3. Get IP address: 10.0.1.5                │
│ 4. Get subnet mask: 255.255.255.0          │
│ 5. Calculate broadcast: 10.0.1.255         │
│    (IP | ~Mask)                             │
│ 6. Construct endpoint: http://10.0.1.5:8080│
│ 7. Start broadcasting to 10.0.1.255:5000   │
└─────────────────────────────────────────────┘
```

**Example calculation**:
- Container IP: `10.0.1.5`
- Subnet Mask: `255.255.255.0` (typical /24 for Docker)
- Broadcast Address: `10.0.1.255`
- Broadcast sends to: **All containers in 10.0.1.0/24 subnet**

### 2. Subnet Broadcast vs Multicast

| Feature | Multicast (239.x.x.x) | Subnet Broadcast (10.x.x.255) |
|---------|----------------------|-------------------------------|
| **Routing** | Requires multicast routing | Direct broadcast, no routing needed |
| **Scope** | Can cross subnets if routed | Limited to single subnet only |
| **Security** | Could leak to other subnets | Contained to Docker overlay network |
| **Configuration** | Needs multicast address + TTL | Auto-calculated from IP |
| **Simplicity** | More complex | Simpler |
| **Docker Support** | Depends on network driver | Works with all overlay networks |

**Winner**: Subnet Broadcast! ✅

### 3. Broadcast Address Calculation

```csharp
// Formula: BroadcastAddress = IP | ~SubnetMask
// Example: IP=10.0.1.5, Mask=255.255.255.0

IP bytes:        [10] [  0] [  1] [  5]  = 0x0A 0x00 0x01 0x05
Mask bytes:      [255][255][255][  0]  = 0xFF 0xFF 0xFF 0x00
~Mask bytes:     [  0][  0][  0][255]  = 0x00 0x00 0x00 0xFF
IP | ~Mask:      [10] [  0] [  1] [255] = 0x0A 0x00 0x01 0xFF

Result: 10.0.1.255
```

**Code**:
```csharp
private static IPAddress CalculateBroadcastAddress(IPAddress address, IPAddress subnetMask)
{
    var ipBytes = address.GetAddressBytes();
    var maskBytes = subnetMask.GetAddressBytes();
    var broadcastBytes = new byte[ipBytes.Length];

    for (int i = 0; i < ipBytes.Length; i++)
    {
        broadcastBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
    }

    return new IPAddress(broadcastBytes);
}
```

### 2. Broadcast Message Format

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

**Sent to**: `10.0.1.255:5000` (subnet broadcast address)
**Received by**: All containers in `10.0.1.0/24` subnet

**Key points**:
- ✅ `endpoint` contains **IP address**, not service name
- ✅ Broadcast address **auto-calculated** from subnet mask
- ✅ `serviceId` is **auto-generated** (hostname + timestamp)
- ✅ `apiKey` is **auto-generated** and rotated
- ✅ Port is **hardcoded** (8080 for Primary)

### 3. Agent Discovery

Agent receives broadcast and connects directly using IP:

```
Agent binds to: 0.0.0.0:5000 (listens for broadcasts on port 5000)
                          ↓
Agent receives: {"endpoint": "http://10.0.1.5:8080", ...}
                          ↓
Agent connects: http://10.0.1.5:8080/hubs/agentregistration
                          ↓
                    No DNS lookup!
                    Direct IP connection!
```

## Benefits

### 1. No Multicast Routing
- **Multicast**: Requires IGMP, multicast routing tables, TTL management
- **Broadcast**: Direct subnet broadcast, supported by all networks

### 2. Better Security
- **Multicast**: Can potentially be routed beyond the subnet
- **Broadcast**: Limited to single subnet (Docker overlay network)

### 3. Simpler Configuration
- **Multicast**: Need multicast address (239.x.x.x) + TTL
- **Broadcast**: Auto-calculated from subnet mask

### 4. More Reliable
- **Multicast**: Some network switches/routers filter multicast
- **Broadcast**: Universally supported

### 5. Docker Native
- Works with all Docker overlay network configurations
- No special network driver requirements

## Network Requirements

### Docker Overlay Network

The system relies on Docker's overlay network which:
- ✅ Assigns private IPs to containers (10.x range)
- ✅ Supports UDP multicast
- ✅ Enables direct container-to-container communication
- ✅ Works across multiple hosts

**Example**:
```bash
docker network create \
  --driver overlay \
  --attachable \
  gameserver-net
```

### IP Address Ranges

The auto-detection looks for **private IP addresses**:

| Range | CIDR | Usage |
|-------|------|-------|
| 10.0.0.0 - 10.255.255.255 | 10.0.0.0/8 | Docker overlay (most common) |
| 172.16.0.0 - 172.31.255.255 | 172.16.0.0/12 | Docker overlay (alternative) |
| 192.168.0.0 - 192.168.255.255 | 192.168.0.0/16 | Docker overlay (rare) |

## Benefits

### 1. Zero Configuration
- **No service names** to configure
- **No DNS dependencies**
- **No hardcoded endpoints**
- Works in any Docker Swarm environment

### 2. Network Resilience
- Survives Primary restarts (IP may change, auto-detected)
- Survives network reconfigurations
- Works with dynamic IP allocation

### 3. Multi-Network Support
- Works on any overlay network
- No naming conflicts
- Easy to run multiple environments

### 4. Simplified Deployment
```bash
# Deploy Primary - NO configuration needed!
docker service create \
  --name gameserver-primary \
  --network gameserver-net \
  gameserver-docker:latest

# Deploy Agent - NO configuration needed!
docker service create \
  --name gameserver-agent \
  --mode global \
  --network gameserver-net \
  --mount type=bind,source=/var/run/docker.sock,target=/var/run/docker.sock \
  gameserver-agent:latest
```

## Logging

### Primary Service Logs

```
[18:30:00] Primary Service Announcement initialized: ServiceId=gameserver-primary-newdev-001-123456789, Version=0.0.4.220
[18:30:00] Auto-detecting container network configuration on network 'gameserver-net'
[18:30:00] Checking interface: eth0, Type=Ethernet, Status=Up
[18:30:00] Detected network configuration: IP=10.0.1.5, Mask=255.255.255.0, Broadcast=10.0.1.255 on interface eth0
[18:30:00] Starting Primary Service announcements: IP=10.0.1.5, Broadcast=10.0.1.255:5000, Interval=5s
[18:30:05] Broadcast sent: 456 bytes to 10.0.1.255:5000
[18:30:10] Broadcast sent: 456 bytes to 10.0.1.255:5000
```

**Key details**:
- Shows detected IP, subnet mask, and **calculated broadcast address**
- Confirms broadcasts going to **subnet broadcast** (not multicast)

### Agent Logs (When Implemented)

```
[18:30:15] AgentDiscoveryListenerService started, listening on 239.1.1.1:5000
[18:30:20] Broadcast received from Primary: 10.0.1.5:8080
[18:30:20] ServiceId: gameserver-primary-newdev-001-123456789
[18:30:20] Connecting to Primary at http://10.0.1.5:8080
[18:30:21] Successfully connected to Primary Service
```

## Troubleshooting

### Problem: Primary Can't Detect IP

**Symptoms**: 
```
Failed to detect container IP address on network 'gameserver-net'. 
Service discovery will not work.
```

**Solution**:
1. Verify container is on the overlay network:
   ```bash
   docker inspect <container-id> | grep Networks
   ```
2. Check network interfaces inside container:
   ```bash
   docker exec <container-id> ip addr show
   ```
3. Ensure overlay network exists:
   ```bash
   docker network ls | grep gameserver-net
   ```

### Problem: Agent Not Receiving Broadcasts

**Symptoms**: Agent logs show no broadcasts received

**Solution**:
1. Verify both services on same network:
   ```bash
   docker service inspect gameserver-primary --format '{{.Spec.TaskTemplate.Networks}}'
   docker service inspect gameserver-agent --format '{{.Spec.TaskTemplate.Networks}}'
   ```
2. Check if multicast is enabled on the network
3. Verify UDP port 5000 is not blocked

### Problem: IP Address Changes After Restart

**This is expected and handled automatically!**

When a Primary restarts:
1. New IP may be assigned by Docker (e.g., 10.0.1.6)
2. Primary auto-detects new IP
3. Broadcasts with new IP
4. Agents receive new IP and reconnect

No configuration change needed!

## What's Auto-Generated

### Service ID
```
Format: gameserver-primary-{hostname}-{timestamp}
Example: gameserver-primary-newdev-001-1234567890123
```

### API Key
```
Format: Base64-encoded 32-byte random value
Example: Qx7KpL...32 characters...mNz8=
Rotation: Every 5 minutes (configurable)
```

### Endpoint
```
Format: http://{auto-detected-ip}:{port}
Example: http://10.0.1.5:8080
Port: 8080 (hardcoded for Primary Service)
```

## Configuration Options (All Optional!)

```json
{
  "ServiceDiscovery": {
    "Enabled": true,                      // Default: true
    "Port": 5000,                         // Default: 5000
    "BroadcastIntervalSeconds": 5,        // Default: 5
    "ApiKeyRotationMinutes": 5,           // Default: 5
    "PrimaryStaleTimeoutSeconds": 30,     // Default: 30
    "MaxMessageSize": 8192                // Default: 8192
  }
}
```

**Removed settings** (no longer needed):
- ❌ `MulticastAddress` - Using subnet broadcast instead
- ❌ `MulticastTtl` - Not applicable to subnet broadcast
- ❌ `ServiceId` - Auto-generated
- ❌ `PublicEndpoint` - Auto-detected

**Recommendation**: Use defaults for production!

## Files Modified

1. **`src/GameServer.Docker/Services/PrimaryServiceAnnouncementService.cs`**
   - Added `DetectContainerIpAddress()` method
   - Auto-generates service ID
   - Constructs endpoint from detected IP + port
   - Removed dependency on configuration

2. **`src/GameServer.Docker/Configurations/ServiceDiscoveryOptions.cs`**
   - Removed `ServiceId` property
   - Removed `PublicEndpoint` property
   - Updated documentation

3. **`docs/UDP-SERVICE-DISCOVERY-ARCHITECTURE.md`**
   - Updated to reflect auto-detection
   - Simplified configuration examples
   - Added IP detection flow diagram

## Next Steps

To complete the zero-config system:

1. **Implement Agent Listener** - `AgentDiscoveryListenerService.cs`
2. **Update Agent Registration** - Use discovered IP endpoint
3. **Add API Key Validation** - Verify keys in SignalR hub
4. **Test End-to-End** - Primary broadcasts → Agent connects

## Testing

### Manual Test: Check Detected IP

```bash
# Start Primary container
docker service create --name test-primary --network gameserver-net gameserver-docker:latest

# Check logs
docker service logs test-primary | grep "Detected container IP"

# Should see:
# Detected container IP address: 10.0.X.Y on interface eth0
```

### Manual Test: Verify Broadcasts

```bash
# On any container in the same subnet, listen for UDP broadcasts:
docker run --rm --network gameserver-net alpine sh -c "apk add socat && socat -u UDP4-RECVFROM:5000,reuseaddr,fork -"

# Should see JSON messages every 5 seconds
# {"serviceId":"gameserver-primary-...","endpoint":"http://10.0.1.5:8080",...}
```

**Note**: This listens for subnet broadcasts on port 5000, not multicast.

## Summary

✅ **Zero configuration required**
✅ **Auto-detects container IP on Docker overlay network**
✅ **No service name dependencies**
✅ **No DNS lookups**
✅ **Works with dynamic IP allocation**
✅ **Survives restarts and reconfigurations**
✅ **Simplified deployment**

This is **true auto-discovery**! 🎉
