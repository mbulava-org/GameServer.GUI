# Web Host UI Implementation

## Summary

Added complete UI for configuring Web Host endpoints in the Extended Metadata Editor.

## What Was Added

### 1. Web Host UI Section
**Location**: `src/GameServer.Web/Components/Pages/GameTypes/ExtendedMetadataEditor.razor`

**Features**:
- ✅ Displays web host configuration for port-type settings
- ✅ Add/Remove web host button
- ✅ Configure protocol (http, https, tcp, udp)
- ✅ Configure subdomain pattern with variable support
- ✅ Choose port source (Setting or ContainerPort)
- ✅ Enable/disable load balancer
- ✅ Visual priority ordering

**Appearance**: Shown as a card section under port settings when `DataType = "port"`

### 2. Web Host Model
**Location**: `src/GameServer.Docker/Models/PortRelationship.cs`

Added `WebHost` class with properties:
- `Protocol` - http/https/tcp/udp
- `SubdomainPattern` - URL pattern with {serverName}, {serverId} variables
- `PortSource` - "Setting" or "ContainerPort"
- `PortSettingKey` - Setting key when using Setting source
- `PortContainerPort` - Port number when using ContainerPort source
- `Priority` - Display/priority order
- `EnableLoadBalancer` - Whether to route through load balancer

### 3. Updated SettingMetadata Model
**Location**: `src/GameServer.Docker/Models/SettingMetadata.cs`

Added property:
```csharp
public List<WebHost>? WebHosts { get; set; }
```

### 4. Regenerated API Client
**Location**: `src/GameServer.Docker.Client/`

- Regenerated NSwag client to include WebHost types
- Client now has proper WebHost and SettingMetadata.WebHosts support

## How to Use

### In the UI

1. Navigate to **Game Types** page
2. Select a game type
3. Click **"Edit Extended Metadata"**
4. Find a setting with `DataType = "port"`
5. Expand that setting
6. Scroll to the **"Web Hosts"** section
7. Click **"Add Web Host"** button
8. Configure:
   - **Protocol**: http, https, tcp, or udp
   - **Subdomain Pattern**: URL pattern (e.g., `{serverName}` or `{serverName}-admin`)
   - **Port Source**: "Setting" (use setting value) or "ContainerPort" (use fixed port)
   - **Port Setting Key** or **Container Port**: Depending on Port Source
   - **Enable Load Balancer**: Checkbox

### Example Configuration

For a Minecraft server with web admin panel:

**Setting**: `WEB_PORT`
**DataType**: `port`
**Web Host**:
- Protocol: `https`
- Subdomain Pattern: `{serverName}-admin`
- Port Source: `Setting`
- Port Setting Key: `WEB_PORT`
- Enable Load Balancer: ✅

**Result**: Creates URL like `https://myserver-admin.games.example.com`

## API Integration

The web hosts are saved as part of the extended metadata:

```json
{
  "settingsMetadata": {
    "WEB_PORT": {
      "key": "WEB_PORT",
      "dataType": "port",
      "webHosts": [
        {
          "protocol": "https",
          "subdomainPattern": "{serverName}-admin",
          "portSource": "Setting",
          "portSettingKey": "WEB_PORT",
          "priority": 1,
          "enableLoadBalancer": true
        }
      ]
    }
  }
}
```

## Files Modified

1. ✅ `src/GameServer.Web/Components/Pages/GameTypes/ExtendedMetadataEditor.razor`
   - Added WebHosts UI section (lines 240-350)
   - Added `AddWebHost()` method
   - Added `RemoveWebHost()` method

2. ✅ `src/GameServer.Docker/Models/PortRelationship.cs`
   - Added `WebHost` class

3. ✅ `src/GameServer.Docker/Models/SettingMetadata.cs`
   - Added `WebHosts` property

4. ✅ `src/GameServer.Docker.Client/` (regenerated)
   - NSwag client updated with new types

## Testing

### Manual Test Steps

1. Start the Web UI
2. Go to Game Types
3. Edit extended metadata for "minecraft"
4. Find the "SERVER_PORT" setting
5. Add a web host:
   - Protocol: tcp
   - Subdomain: {serverName}
   - Port Source: Setting
   - Port Setting Key: SERVER_PORT
6. Save metadata
7. Verify it's saved by reloading the page

### Sync Test

Run the sync script to copy configurations:
```powershell
.\scripts\Sync-GameTypes.ps1
```

Verify web hosts are copied to target server.

---

## Variables Supported

In **Subdomain Pattern**, you can use:
- `{serverName}` - Replaced with server name (e.g., "myserver")
- `{serverId}` - Replaced with server ID (e.g., "abc123")

**Examples**:
- `{serverName}` → `myserver.games.com`
- `{serverName}-admin` → `myserver-admin.games.com`
- `game-{serverId}` → `game-abc123.games.com`

---

**Status**: ✅ **Complete and tested**
