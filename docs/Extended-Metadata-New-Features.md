# Extended Metadata - New Features Guide

## Overview of Updates

The extended metadata system has been enhanced with the following new capabilities:

1. **Removed AttachStdin** - Simplified, only TTY is needed
2. **Fixed Port Mapping** - Settings now update existing ports, not create new ones
3. **Enum Support** - Dropdown lists for settings with specific allowed values
4. **Value Mappings** - Semantic descriptions for numeric or coded values
5. **Default Port Marking** - Identify the primary connection port for users

---

## 1. Enum Support (Dropdown Lists)

Use `DataType = "enum"` with `AllowedValues` to create dropdown selectors.

### Example: Server Type Selection

```json
{
  "key": "TYPE",
  "description": "Server type",
  "dataType": "enum",
  "allowedValues": ["VANILLA", "PAPER", "SPIGOT", "FORGE", "FABRIC"],
  "placeholder": "VANILLA"
}
```

**UI Behavior:** Renders as a dropdown with only these options available.

### Example: Difficulty Setting

```json
{
  "key": "DIFFICULTY",
  "description": "Game difficulty",
  "dataType": "enum",
  "allowedValues": ["peaceful", "easy", "normal", "hard"],
  "placeholder": "easy"
}
```

---

## 2. Value Mappings (Semantic Descriptions)

Use `ValueMappings` to provide user-friendly descriptions for setting values.

### Example: Difficulty with Descriptions

```json
{
  "key": "DIFFICULTY",
  "description": "Game difficulty",
  "dataType": "enum",
  "allowedValues": ["peaceful", "easy", "normal", "hard"],
  "valueMappings": {
    "peaceful": "Peaceful - No hostile mobs",
    "easy": "Easy - Reduced damage",
    "normal": "Normal - Standard difficulty",
    "hard": "Hard - Increased damage and challenges"
  }
}
```

**UI Behavior:** Display the descriptive text alongside or instead of the raw value.

### Example: Game Mode with Descriptions

```json
{
  "key": "MODE",
  "description": "Default game mode for players",
  "dataType": "enum",
  "allowedValues": ["survival", "creative", "adventure", "spectator"],
  "valueMappings": {
    "survival": "Survival - Gather resources and survive",
    "creative": "Creative - Unlimited resources and flight",
    "adventure": "Adventure - Limited interactions for custom maps",
    "spectator": "Spectator - Fly through blocks and observe"
  }
}
```

### Example: Numeric Values with Meaning

```json
{
  "key": "LOG_LEVEL",
  "description": "Logging verbosity level",
  "dataType": "enum",
  "allowedValues": ["0", "1", "2", "3"],
  "valueMappings": {
    "0": "Silent - No logging",
    "1": "Errors Only - Critical issues only",
    "2": "Warnings - Errors and warnings",
    "3": "Verbose - All information"
  }
}
```

---

## 3. Fixed Port Mapping

Settings can now control existing port definitions by linking to them.

### How It Works

1. **GameTypeDefinition** defines base ports (e.g., port 25565)
2. **SettingMetadata** links to that port via `LinkedContainerPort`
3. When the setting value changes, the port mapping is updated

### Example: Minecraft Server Port

**GameTypeDefinition:**
```csharp
Ports = new()
{
    new PortDefinition(25565, "tcp", true) // Default port
}
```

**SettingMetadata:**
```json
{
  "key": "SERVER_PORT",
  "description": "Server port number (default: 25565)",
  "dataType": "port",
  "mapsToContainerPort": true,
  "linkedContainerPort": 25565,
  "portProtocol": "tcp"
}
```

**Result:**
- User sets `SERVER_PORT` to `25566`
- The port mapping updates from `25565:25565` to `25566:25566`
- Container listens on the new port

### Code Usage

```csharp
// Get updated ports with setting values applied
var ports = await _metadataApplier.ApplyDynamicPortMappings(server, definition);

// ports[0].Port is now 25566 if SERVER_PORT setting is "25566"
```

---

## 4. Default Port Marking

Mark the primary port that users should connect to.

### PortDefinition Update

```csharp
public class PortDefinition
{
    public uint Port { get; set; }
    public string Protocol { get; set; } = "tcp";
    public bool IsDefaultPort { get; set; } = false; // NEW
}
```

### Example: Minecraft

```csharp
Ports = new()
{
    new PortDefinition(25565, "tcp", true),  // Primary connection port
    new PortDefinition(25575, "tcp", false)  // RCON port (secondary)
}
```

### Getting the Default Port

```csharp
var ports = await _metadataApplier.ApplyDynamicPortMappings(server, definition);
var defaultPort = _metadataApplier.GetDefaultPort(ports);

// Display to user: "Connect to: <server-ip>:<defaultPort.Port>"
```

---

## 5. Complete Example: Minecraft Setup

### GameTypeDefinition

```csharp
new GameTypeDefinition
{
    Key = "minecraft",
    DisplayName = "Minecraft",
    Image = "itzg/minecraft-server",
    Ports = new()
    {
        new PortDefinition(25565, "tcp", true)  // Default, but controllable
    },
    DefaultSettings = new()
    {
        ["EULA"] = "false",
        ["TYPE"] = "VANILLA",
        ["VERSION"] = "LATEST",
        ["DIFFICULTY"] = "easy",
        ["MODE"] = "survival",
        ["SERVER_PORT"] = "25565"
    }
}
```

### Extended Metadata

```json
{
  "gameTypeKey": "minecraft",
  "enableTTY": true,
  "settingsMetadata": {
    "EULA": {
      "key": "EULA",
      "description": "Accept Minecraft EULA",
      "isRequired": true,
      "dataType": "enum",
      "allowedValues": ["true", "false"]
    },
    "TYPE": {
      "key": "TYPE",
      "description": "Server type",
      "dataType": "enum",
      "allowedValues": ["VANILLA", "PAPER", "SPIGOT", "FORGE", "FABRIC"]
    },
    "DIFFICULTY": {
      "key": "DIFFICULTY",
      "dataType": "enum",
      "allowedValues": ["peaceful", "easy", "normal", "hard"],
      "valueMappings": {
        "peaceful": "Peaceful - No hostile mobs",
        "easy": "Easy - Reduced damage",
        "normal": "Normal - Standard difficulty",
        "hard": "Hard - Increased damage"
      }
    },
    "MODE": {
      "key": "MODE",
      "dataType": "enum",
      "allowedValues": ["survival", "creative", "adventure", "spectator"],
      "valueMappings": {
        "survival": "Survival Mode",
        "creative": "Creative Mode",
        "adventure": "Adventure Mode",
        "spectator": "Spectator Mode"
      }
    },
    "SERVER_PORT": {
      "key": "SERVER_PORT",
      "description": "Server port (changes which port the server listens on)",
      "dataType": "port",
      "mapsToContainerPort": true,
      "linkedContainerPort": 25565,
      "portProtocol": "tcp"
    }
  }
}
```

### User Creates Server

```json
{
  "name": "My Server",
  "gameType": "minecraft",
  "settings": {
    "EULA": "true",
    "TYPE": "PAPER",
    "DIFFICULTY": "hard",
    "MODE": "survival",
    "SERVER_PORT": "25566"
  }
}
```

### What Happens

1. **Validation**: Checks all enum values are in AllowedValues ?
2. **Port Update**: Port 25565 ? 25566 based on SERVER_PORT setting ?
3. **TTY**: Container created with TTY enabled ?
4. **Container Created**: Exposes port 25566, runs Paper server on Hard difficulty ?

---

## UI Integration Examples

### Rendering Enum Fields

```typescript
function renderSettingField(meta: SettingMetadata, value: string, onChange: (v: string) => void) {
  if (meta.dataType === 'enum' && meta.allowedValues) {
    return (
      <select value={value} onChange={e => onChange(e.target.value)}>
        <option value="">-- Select --</option>
        {meta.allowedValues.map(val => (
          <option key={val} value={val}>
            {meta.valueMappings?.[val] || val}
          </option>
        ))}
      </select>
    );
  }
  
  // ... other types
}
```

### Displaying Default Port

```typescript
function ServerConnectionInfo({ server, ports }: Props) {
  const defaultPort = ports.find(p => p.isDefaultPort) || ports[0];
  
  return (
    <div>
      <h3>Connection Details</h3>
      <p>
        <strong>Address:</strong> {server.hostname}:{defaultPort.port}
      </p>
      <p>
        <strong>Protocol:</strong> {defaultPort.protocol}
      </p>
    </div>
  );
}
```

### Showing Value Mappings as Tooltips

```typescript
function DifficultySelector({ meta, value, onChange }: Props) {
  return (
    <div>
      <label>{meta.key}</label>
      <select value={value} onChange={e => onChange(e.target.value)}>
        {meta.allowedValues?.map(val => (
          <option 
            key={val} 
            value={val}
            title={meta.valueMappings?.[val]} // Tooltip on hover
          >
            {val}
          </option>
        ))}
      </select>
      {meta.valueMappings?.[value] && (
        <p className="help-text">{meta.valueMappings[value]}</p>
      )}
    </div>
  );
}
```

---

## API Examples

### Create Extended Metadata with All Features

```bash
curl -X POST http://localhost:5000/api/gametypes/extended \
  -H "Content-Type: application/json" \
  -d '{
    "gameTypeKey": "valheim",
    "enableTTY": true,
    "settingsMetadata": {
      "SERVER_PORT": {
        "key": "SERVER_PORT",
        "description": "Game server port",
        "dataType": "port",
        "mapsToContainerPort": true,
        "linkedContainerPort": 2456,
        "portProtocol": "udp",
        "placeholder": "2456"
      },
      "SERVER_PUBLIC": {
        "key": "SERVER_PUBLIC",
        "description": "List server publicly",
        "dataType": "enum",
        "allowedValues": ["0", "1"],
        "valueMappings": {
          "0": "Private - Not listed",
          "1": "Public - Listed in server browser"
        }
      },
      "WORLD_PRESET": {
        "key": "WORLD_PRESET",
        "description": "World difficulty preset",
        "dataType": "enum",
        "allowedValues": ["normal", "casual", "easy", "hard", "hardcore", "immersive", "hammer"],
        "valueMappings": {
          "normal": "Normal - Standard difficulty",
          "casual": "Casual - Easier combat and building",
          "easy": "Easy - Lower enemy difficulty",
          "hard": "Hard - Tougher enemies",
          "hardcore": "Hardcore - Death penalties enabled",
          "immersive": "Immersive - Realistic experience",
          "hammer": "Hammer - Creative mode"
        },
        "category": "Gameplay",
        "displayOrder": 1
      }
    }
  }'
```

---

## Migration Guide

### Update Existing Metadata

If you have existing metadata with the old `MapsToContainerPort` logic:

**Before:**
```json
{
  "key": "SERVER_PORT",
  "mapsToContainerPort": true,
  "portProtocol": "tcp"
}
```

**After:**
```json
{
  "key": "SERVER_PORT",
  "mapsToContainerPort": true,
  "linkedContainerPort": 25565,  // ADD THIS - links to existing port
  "portProtocol": "tcp"
}
```

### Add Enum Values

Convert freeform string fields to enums:

**Before:**
```json
{
  "key": "DIFFICULTY",
  "dataType": "string",
  "placeholder": "easy"
}
```

**After:**
```json
{
  "key": "DIFFICULTY",
  "dataType": "enum",
  "allowedValues": ["peaceful", "easy", "normal", "hard"],
  "valueMappings": {
    "peaceful": "Peaceful - No mobs",
    "easy": "Easy",
    "normal": "Normal",
    "hard": "Hard"
  },
  "placeholder": "easy"
}
```

---

## Best Practices

### 1. Always Provide Value Mappings for Enums

```json
{
  "dataType": "enum",
  "allowedValues": ["0", "1", "2"],
  "valueMappings": {
    "0": "Disabled",
    "1": "Enabled",
    "2": "Auto"
  }
}
```

### 2. Mark the Primary Port

```csharp
Ports = new()
{
    new PortDefinition(25565, "tcp", true),  // Primary - users connect here
    new PortDefinition(25575, "tcp", false), // RCON - admin only
    new PortDefinition(8080, "tcp", false)   // Web UI - optional
}
```

### 3. Link Port Settings Correctly

```json
{
  "key": "SERVER_PORT",
  "mapsToContainerPort": true,
  "linkedContainerPort": 25565,  // Must match an existing port in GameTypeDefinition
  "portProtocol": "tcp"          // Must match protocol too
}
```

### 4. Use Descriptive Value Mappings

**Bad:**
```json
"valueMappings": {
  "0": "Off",
  "1": "On"
}
```

**Good:**
```json
"valueMappings": {
  "0": "Disabled - Feature is turned off and will not run",
  "1": "Enabled - Feature is active and will function normally"
}
```

---

## Summary of Changes

| Feature | Old Behavior | New Behavior |
|---------|-------------|--------------|
| **AttachStdin** | Separate property | Removed (not needed) |
| **Port Mapping** | Created new ports | Updates existing ports via `LinkedContainerPort` |
| **Enums** | Not supported | `DataType = "enum"` with `AllowedValues` |
| **Value Descriptions** | Not supported | `ValueMappings` dictionary |
| **Default Port** | Not marked | `IsDefaultPort` property on `PortDefinition` |

---

## Quick Reference

### New SettingMetadata Properties

- `DataType = "enum"` - Dropdown selector
- `AllowedValues` - List of valid values for enums
- `ValueMappings` - Dictionary of value ? description
- `LinkedContainerPort` - Which port to update when setting changes

### New PortDefinition Properties

- `IsDefaultPort` - Mark as primary connection port

### New GameTypeMetadataApplier Methods

- `ApplyDynamicPortMappings()` - Apply setting values to ports
- `GetDefaultPort()` - Get the primary connection port

---

All features are now production-ready and fully integrated! ??
