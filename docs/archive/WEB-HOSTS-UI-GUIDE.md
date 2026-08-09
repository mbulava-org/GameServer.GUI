# Web Hosts UI Integration - Quick Guide

## Overview

The Web Hosts feature is now fully integrated into the GameServer.GUI UI with three main interfaces:

### 1. **GameType Configuration** (Administrator)
**Location**: `Game Types` → Select Type → `Extended Metadata` → **`Web Hosts`** tab

**Purpose**: Define web interfaces at the GameType level

**Features**:
- ✅ Add/Edit/Delete web host definitions
- ✅ Configure conditional enabling (`DYNMAP_ENABLED=true`)
- ✅ Set dynamic ports from environment variables
- ✅ Reorder hosts (affects URL priority)
- ✅ Quick-select from available settings

**Component**: `WebHostsEditor.razor`

---

### 2. **Server Editor** (User - Real-time Preview)
**Location**: `Servers` → Select Server → `Edit` → **`Settings`** tab (bottom section)

**Purpose**: Preview which web hosts will be active based on current settings

**Features**:
- ✅ Real-time status preview (Active/Disabled)
- ✅ Shows resolved ports (if using dynamic variables)
- ✅ Displays conditions and whether they're met
- ✅ Helpful hints on how to enable disabled hosts
- ✅ Count badge showing active hosts

**Component**: `WebHostsPreview.razor`

---

### 3. **Server Details** (User - Access Interface)
**Location**: `Servers` → Select Server → **`Web Access`** tab

**Purpose**: View and access web interfaces for a specific server

**Features**:
- ✅ Shows which hosts are currently enabled (based on conditions)
- ✅ Displays actual resolved ports (if dynamic)
- ✅ Provides clickable URLs with "Open" and "Copy" buttons
- ✅ Shows status indicators (Active/Disabled)
- ✅ Explains why a host is disabled (condition not met)

**Component**: `WebHostsDisplay.razor`

---

## User Workflow

### Administrator Setup (One Time)

1. **Navigate to Game Types**
   ```
   Game Types → Minecraft → Extended Metadata → Web Hosts
   ```

2. **Add Web Host**
   - Click `Add Web Host`
   - Fill in:
     - Name: "Dynmap"
     - Description: "Real-time world map"
     - Port: Fixed (8123) OR Variable (DYNMAP_PORT)
     - Enabled When: `DYNMAP_ENABLED=true` (optional)
     - Path Segment: "map" (optional, defaults to "dynmap")

3. **Save Metadata**
   - Click `Save All Changes`
   - Web hosts are now configured for all servers of this type

### User Access

1. **Create/Edit Server**
   ```
   Servers → Create New OR Edit Existing
   ```

2. **Configure Settings**
   - In the Settings tab, configure environment variables
   - See **Web Access Preview** panel at the bottom
   - Real-time feedback: ✅ Active or ❌ Disabled

3. **Preview Shows**:
   - Which hosts will be accessible
   - Current port (fixed or from variable)
   - Why a host is disabled (if applicable)
   - Hints on how to enable disabled hosts

4. **After Saving**:
   ```
   Servers → [Server Name] → Web Access tab
   ```

5. **Access Web Interface**:
   - Click `Open` button → Opens in new tab
   - Click `Copy` button → Copies URL to clipboard

---

## UI Components Reference

### WebHostsEditor (GameType Configuration)

**Location**: `src\GameServer.Web\Components\Pages\GameTypes\WebHostsEditor.razor`

**Parameters**:
- `WebHosts` (List<WebHostDefinition>) - List to edit
- `WebHostsChanged` (EventCallback) - Called when list changes
- `AvailableSettings` (List<string>) - Settings to show as quick-pick

**Features**:
- Card-based list view
- Move up/down buttons (affects routing priority)
- Edit/Delete buttons per host
- Preview of generated URL path
- Badge indicators (Primary, Port type, Auth required)

---

### WebHostDialog (Add/Edit Dialog)

**Location**: `src\GameServer.Web\Components\Pages\GameTypes\WebHostDialog.razor`

**Parameters**:
- `WebHost` (WebHostDefinition) - Host to edit
- `IsNew` (bool) - Create or edit mode
- `AvailableSettings` (List<string>) - For quick-pick

**Sections**:
1. **Basic Info**: Name, Description
2. **Port Configuration**: 
   - Radio: Fixed Port vs Environment Variable
   - Quick-pick badges for available settings
3. **Conditional Enabling**: 
   - Condition input (VAR=value or VAR!=value)
   - Quick-pick badges for common conditions
4. **Advanced Options**: 
   - Custom URL path segment
   - Authentication requirement
5. **Preview**: Shows generated URL and port source

---

### WebHostsPreview (Settings Editor)

**Location**: `src\GameServer.Web\Components\Server\WebHostsPreview.razor`

**Parameters**:
- `WebHosts` (ICollection<WebHostDefinition>) - Configured hosts
- `ServerSettings` (IDictionary<string,string>) - Current server settings

**Purpose**: Real-time feedback during server configuration

**Features**:
- Live evaluation of conditions as user types
- Shows count of active hosts in badge
- Visual indicators for each host (✅/❌)
- Port resolution preview (shows actual vs configured)
- Helpful hints for enabling disabled hosts

**Display Elements**:
- Status icon (check = enabled, cancel = disabled)
- Host name + status badge
- Description (if provided)
- Port info:
  - Fixed: "Port: 8123"
  - Dynamic (set): "Port: 9090 (from WEBUI_PORT)"
  - Dynamic (not set): ⚠️ "Port variable WEBUI_PORT not set"
- Condition display with status
- Enable hints: "To enable: Set DYNMAP_ENABLED to 'true'"

**Example Display**:
```
┌─────────────────────────────────────────────┐
│ 🌐 Web Access Preview        [2 of 3 Active]│
│                                              │
│ ✅ Dynmap                   [Will be accessible]
│    Real-time world map                      │
│    📡 Port: 8123                            │
│                                              │
│ ❌ BlueMap                  [Disabled]      │
│    3D renderer                              │
│    📡 Port: 8100                            │
│    ❌ Condition: BLUEMAP_ENABLED=true → Not met
│    💡 To enable: Set BLUEMAP_ENABLED to 'true'
│                                              │
│ ✅ Admin Panel              [Will be accessible]
│    Web admin                                │
│    📡 Port: 9090 (from WEBUI_PORT)         │
│    ✅ Condition: WEB_ENABLED=true          │
└─────────────────────────────────────────────┘
```

---

### WebHostsDisplay (Server View)

**Location**: `src\GameServer.Web\Components\Server\WebHostsDisplay.razor`

**Parameters**:
- `WebHosts` (ICollection<WebHostDefinition>) - Configured hosts
- `ServerSettings` (IDictionary<string,string>) - Server's env vars
- `ServerId` (string) - For URL generation
- `LoadBalancerDomain` (string) - Base domain (default: "yourdomain.com")

**Features**:
- Read-only card-based display
- Status indicators (Active/Disabled)
- Condition evaluation display
- Port resolution (shows actual vs configured)
- Clickable URLs with Open/Copy actions
- Helpful alerts for disabled hosts

**Status Logic**:
- ✅ **Green** = Condition met OR no condition
- ❌ **Gray** = Condition not met
- ⚠️ **Warning** = Port variable not set

---

## Example Configurations

### Example 1: Minecraft with Dynmap

**GameType Configuration**:
```json
{
  "Name": "Dynmap",
  "ContainerPort": 8123,
  "Description": "Real-time world map",
  "EnabledWhen": "DYNMAP_ENABLED=true",
  "PathSegment": "map"
}
```

**Server Settings**:
```json
{
  "DYNMAP_ENABLED": "true"
}
```

**Result**:
- Status: ✅ Active (condition met)
- URL: `https://yourdomain.com/game-abc123/map/`
- Port: 8123 (fixed)

---

### Example 2: Dynamic Web UI Port

**GameType Configuration**:
```json
{
  "Name": "Web Console",
  "ContainerPortVariable": "WEBUI_PORT",
  "Description": "Admin web interface",
  "EnabledWhen": "WEB_ENABLED=true"
}
```

**Server Settings**:
```json
{
  "WEB_ENABLED": "true",
  "WEBUI_PORT": "9090"
}
```

**Result**:
- Status: ✅ Active
- URL: `https://yourdomain.com/game-abc123/`
- Port: 9090 (from $WEBUI_PORT)

---

### Example 3: Multiple Hosts

**GameType Configuration**:
```json
[
  {
    "Name": "Dynmap",
    "ContainerPort": 8123,
    "Description": "World map"
  },
  {
    "Name": "BlueMap",
    "ContainerPort": 8100,
    "Description": "3D renderer",
    "EnabledWhen": "BLUEMAP_ENABLED=true"
  },
  {
    "Name": "Admin Panel",
    "ContainerPort": 8080,
    "RequiresAuth": true
  }
]
```

**Generated URLs**:
- Dynmap: `https://yourdomain.com/game-abc123/` (primary)
- BlueMap: `https://yourdomain.com/game-abc123/bluemap/` (if enabled)
- Admin: `https://yourdomain.com/game-abc123/admin-panel/`

---

## UI States & Messages

### Active Host Card
```
🌐 Dynmap              [Active]
Real-time world map

[Port: 8123]

🔗 https://yourdomain.com/game-abc123/map/
   [Open] [Copy]
```

### Disabled Host Card
```
🌐❌ BlueMap           [Disabled]
3D world renderer

[Port: 8100]

❌ Condition: BLUEMAP_ENABLED=true (not met)

⚠️ To enable this interface: Set the required environment 
   variable to meet the condition BLUEMAP_ENABLED=true
```

### Port Not Resolved
```
🌐❌ Web UI            [Disabled]
Admin interface

⚠️ [Port: ${WEBUI_PORT} (not set)]

❌ Not accessible (conditions not met)
```

---

## Configuration Tips

### 1. **First Host = Base Path**
The first web host in the list gets the base path `/game-{serverId}/`. All others get subpaths.

**Order matters!** Use the move up/down buttons to prioritize.

### 2. **Use Conditions Wisely**
- Make common interfaces **always enabled** (no condition)
- Use conditions for optional features: `FEATURE_ENABLED=true`
- Use negation for exclusions: `MODE!=disabled`

### 3. **Dynamic Ports**
Best for servers that allow port customization:
```
Environment Variable: WEBUI_PORT
User sets: WEBUI_PORT=9090
System routes to: container:9090
```

### 4. **Path Segments**
- Leave empty for auto-generation
- Use short, URL-safe names: "map", "admin", "stats"
- Avoid spaces and special characters

---

## Environment Variables

### LOADBALANCER_DOMAIN
Sets the base domain for generated URLs.

**Default**: Server's `VanityDomain` property

**Example**:
```bash
LOADBALANCER_DOMAIN=games.example.com
```

**Generated URL**:
```
https://games.example.com/game-abc123/dynmap/
```

---

## Troubleshooting

### "Web Access" tab not showing
**Cause**: No web hosts defined for this game type

**Fix**: 
1. Go to Game Types → Select Type → Extended Metadata
2. Add web hosts in the "Web Hosts" tab
3. Save metadata

### Host shows as "Disabled"
**Check**:
1. Is there an `EnabledWhen` condition?
2. Does the server have that setting?
3. Does the value match the condition?

**Example**:
- Condition: `DYNMAP_ENABLED=true`
- Server setting: `DYNMAP_ENABLED=false` → ❌ Disabled
- Fix: Change setting to `true` or remove condition

### Port shows as "(not set)"
**Cause**: `ContainerPortVariable` refers to a non-existent setting

**Fix**:
1. Edit server settings
2. Add the environment variable (e.g., `WEBUI_PORT=8080`)
3. Restart/update server

### URL doesn't work
**Check**:
1. Is the load balancer running?
2. Is the service attached to the load balancer network?
3. Does Traefik have the correct labels?
4. Is `LOADBALANCER_DOMAIN` correct?

**Debug**:
```bash
# Check service labels
docker service inspect <service-name> --format '{{json .Spec.Labels}}' | jq

# Check networks
docker service inspect <service-name> --format '{{json .Spec.TaskTemplate.Networks}}'
```

---

## Future Enhancements

### Planned Features
- [ ] Real-time availability checking (ping endpoint)
- [ ] Custom middleware configuration per host
- [ ] Authentication integration
- [ ] SSL/TLS certificate management
- [ ] Health check visualization
- [ ] Traffic metrics per host

### Extensibility Points
- Add new load balancer providers (Nginx, Caddy)
- Custom URL pattern templates
- Integration with external auth providers
- Rate limiting configuration

---

## Quick Reference: UI Locations

| Task | Location | Tab/Section |
|------|----------|-------------|
| Configure hosts | Game Types → [Type] → Extended Metadata | **Web Hosts** tab |
| Add host | Web Hosts tab | Click "Add Web Host" |
| Edit host | Web Hosts tab | Click edit icon on card |
| Reorder hosts | Web Hosts tab | Use arrow up/down buttons |
| **Preview during edit** | **Servers → Edit → Settings** | **Bottom section** |
| **See enable hints** | **Settings tab preview** | **Below host cards** |
| View server hosts | Servers → [Server] | **Web Access** tab |
| Access interface | Web Access tab | Click "Open" button |
| Copy URL | Web Access tab | Click copy icon |
| Check status | Web Access tab | See badge (Active/Disabled) |

---

## Component Dependencies

```
ExtendedMetadataEditor.razor
└── WebHostsEditor.razor
    └── WebHostDialog.razor (Dialog)

EditServer.razor (Settings tab)
└── WebHostsPreview.razor (Real-time preview)

ServerDetails.razor (Web Access tab)
└── WebHostsDisplay.razor (Read-only access)
```

## Data Flow

```
1. Admin configures GameType.ExtendedMetadata.WebHosts
   ↓
2. Save to database (GameTypeExtendedMetadata table)
   ↓
3. User creates/edits server with environment variables
   ├── WebHostsPreview shows real-time status
   │   ├── Evaluates conditions as user types
   │   ├── Shows enable/disable status
   │   └── Provides helpful hints
   ↓
4. User saves server settings
   ↓
5. DockerServiceHelper.BuildGameServerServiceSpec()
   ├── WebHostResolver.ResolveWebHosts()
   │   ├── Evaluates EnabledWhen conditions
   │   └── Resolves dynamic ports
   └── GenerateReverseProxyLabels()
       └── Creates Traefik labels
   ↓
6. Service created with labels + network attachment
   ↓
7. Traefik discovers service via labels
   ↓
8. User views ServerDetails → Web Access tab
   └── WebHostsDisplay shows active hosts with URLs
```
