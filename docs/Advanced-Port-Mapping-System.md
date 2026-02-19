# Advanced Port Mapping & Validation System

## Overview

The Advanced Port Mapping system provides automatic port relationship management, validation, and availability checking for game server configurations. This is essential for games that require multiple related ports that must be coordinated.

## Key Features

? **Automatic Port Relationships** - When one port changes, related ports update automatically  
? **Port Availability Validation** - Checks if ports are available before assignment  
? **Reserved Port Protection** - Prevents use of system-reserved ports  
? **Range Validation** - Enforces min/max port ranges  
? **Multiple Relationship Types** - Offset, Fixed, and Multiplier relationships  
? **User-Editable Control** - Some ports can be auto-managed, others user-controlled

---

## Architecture

### Components

1. **PortRelationship** - Defines how ports relate to each other
2. **PortValidationRule** - Defines validation rules for ports
3. **SettingMetadata Extensions** - Port-specific properties
4. **PortMappingService** - Service for validation and updates

### Flow

```
User Changes Port Setting
         ?
   Validate New Port
    ?          ?
  Valid?    Invalid ? Show Error
    ?
Calculate Related Ports
         ?
Validate Related Ports Available
    ?          ?
  Valid?    Invalid ? Show Error
    ?
Apply All Port Changes
         ?
   Update Game Type
```

---

## Usage Examples

### Example 1: Source Engine Game (Offset Relationships)

**Game:** Counter-Strike, Team Fortress 2, etc.  
**Pattern:** Query port = Game port + 1

```json
{
  "Key": "SERVER_PORT",
  "Description": "Game server port",
  "DataType": "port",
  "MapsToContainerPort": true,
  "LinkedContainerPort": 27015,
  "PortProtocol": "udp",
  "PortValidation": {
    "MinPort": 27000,
    "MaxPort": 27100,
    "CheckAvailability": true,
    "SuggestedPorts": [27015, 27016, 27017]
  },
  "PortRelationships": [
    {
      "RelationType": "Offset",
      "TargetContainerPort": 27016,
      "TargetProtocol": "udp",
      "Offset": 1,
      "Description": "Query Port (Steam Server Browser)",
      "IsRequired": true
    }
  ],
  "ValidateRelatedPortsAvailability": true
}
```

**Behavior:**
- User sets SERVER_PORT to 27020
- System automatically updates query port to 27021
- Validates both 27020 and 27021 are available
- If either is unavailable, prevents the change

---

### Example 2: Minecraft Server (Fixed + Offset)

**Game:** Minecraft  
**Pattern:** Query port = Game port (same), RCON has fixed port

```json
{
  "Key": "SERVER_PORT",
  "Description": "Minecraft server port",
  "DataType": "port",
  "MapsToContainerPort": true,
  "LinkedContainerPort": 25565,
  "PortProtocol": "tcp",
  "PortValidation": {
    "MinPort": 25500,
    "MaxPort": 25600,
    "CheckAvailability": true,
    "ReservedPorts": [25570, 25580],
    "SuggestedPorts": [25565, 25566, 25567]
  },
  "PortRelationships": [
    {
      "RelationType": "Offset",
      "TargetContainerPort": 25565,
      "TargetProtocol": "udp",
      "Offset": 0,
      "Description": "Query Port (Server List Ping)",
      "IsRequired": true
    },
    {
      "RelationType": "Fixed",
      "TargetContainerPort": 25575,
      "TargetProtocol": "tcp",
      "FixedValue": 25575,
      "Description": "RCON Port (Remote Console)",
      "IsRequired": false
    }
  ]
}
```

**Behavior:**
- User sets SERVER_PORT to 25566
- TCP game port updates to 25566
- UDP query port updates to 25566 (offset 0)
- RCON port stays at 25575 (fixed)
- Validates all ports are available

---

### Example 3: ARK: Survival Evolved (Multiple Offsets)

**Game:** ARK: Survival Evolved  
**Pattern:** Query = Game + 1, RCON = Game + 2

```json
{
  "Key": "SESSION_PORT",
  "Description": "Game session port",
  "DataType": "port",
  "MapsToContainerPort": true,
  "LinkedContainerPort": 7777,
  "PortProtocol": "udp",
  "PortValidation": {
    "MinPort": 7777,
    "MaxPort": 7877,
    "CheckAvailability": true
  },
  "PortRelationships": [
    {
      "RelationType": "Offset",
      "TargetContainerPort": 7778,
      "TargetProtocol": "udp",
      "Offset": 1,
      "Description": "Query Port (Steam)",
      "IsRequired": true
    },
    {
      "RelationType": "Offset",
      "TargetContainerPort": 27020,
      "TargetProtocol": "tcp",
      "Offset": 19243,
      "Description": "RCON Port",
      "IsRequired": true
    },
    {
      "RelationType": "Offset",
      "TargetContainerPort": 7779,
      "TargetProtocol": "udp",
      "Offset": 2,
      "Description": "Raw UDP Socket",
      "IsRequired": false
    }
  ]
}
```

**Behavior:**
- User sets SESSION_PORT to 7800
- Game port updates to 7800 (UDP)
- Query port updates to 7801 (7800 + 1)
- RCON port updates to 27043 (7800 + 19243)
- Raw UDP port updates to 7802 (7800 + 2)

---

## SettingMetadata Properties

### Port Mapping Properties

| Property | Type | Description |
|----------|------|-------------|
| `MapsToContainerPort` | bool | Whether this setting controls a port |
| `LinkedContainerPort` | uint? | The original container port to update |
| `PortProtocol` | string | Protocol: "tcp" or "udp" |
| `PortRelationships` | List | Related ports that update automatically |
| `PortValidation` | PortValidationRule | Validation rules for this port |
| `SynchronizedWithSetting` | string? | Another setting to stay synced with |
| `AutoAllocatePort` | bool | Automatically allocate from port pool |
| `ValidateRelatedPortsAvailability` | bool | Check related ports are available |

### PortRelationship Properties

| Property | Type | Description |
|----------|------|-------------|
| `RelationType` | enum | Offset, Fixed, or Multiplier |
| `TargetContainerPort` | uint | The port to update |
| `TargetProtocol` | string | Protocol of target port |
| `Offset` | int | For Offset type: value to add |
| `FixedValue` | uint? | For Fixed type: fixed port value |
| `Description` | string? | Human-readable description |
| `IsRequired` | bool | Whether this port is required |

### PortValidationRule Properties

| Property | Type | Description |
|----------|------|-------------|
| `MinPort` | uint | Minimum allowed port (default: 1024) |
| `MaxPort` | uint | Maximum allowed port (default: 65535) |
| `ReservedPorts` | List<uint>? | Ports that cannot be used |
| `CheckAvailability` | bool | Check if port is available |
| `IsUserEditable` | bool | Allow user to change the port |
| `SuggestedPorts` | List<uint>? | Recommended port numbers |
| `ValidationMessage` | string? | Custom error message |

---

## Relationship Types

### Offset (Most Common)

**Formula:** `TargetPort = SourcePort + Offset`

**Examples:**
- Query port = Game port + 1
- RCON port = Game port + 10
- Voice port = Game port - 5

```json
{
  "RelationType": "Offset",
  "TargetContainerPort": 27016,
  "Offset": 1,
  "Description": "Query Port"
}
```

### Fixed

**Formula:** `TargetPort = FixedValue`

**Examples:**
- RCON always at 25575
- Web admin always at 8080
- Metrics always at 9090

```json
{
  "RelationType": "Fixed",
  "TargetContainerPort": 25575,
  "FixedValue": 25575,
  "Description": "RCON Port (Fixed)"
}
```

### Multiplier (Rare)

**Formula:** `TargetPort = SourcePort * Offset`

**Examples:**
- Mirror port on different range
- Scaled port numbers

```json
{
  "RelationType": "Multiplier",
  "TargetContainerPort": 54000,
  "Offset": 2,
  "Description": "Mirrored Port"
}
```

---

## Validation Scenarios

### Scenario 1: Basic Range Validation

```json
{
  "PortValidation": {
    "MinPort": 25000,
    "MaxPort": 26000
  }
}
```

**Result:**
- ? 25500 ? Valid
- ? 24999 ? Error: "Port must be between 25000 and 26000"
- ? 26001 ? Error: "Port must be between 25000 and 26000"

### Scenario 2: Reserved Ports

```json
{
  "PortValidation": {
    "MinPort": 25000,
    "MaxPort": 26000,
    "ReservedPorts": [25555, 25666]
  }
}
```

**Result:**
- ? 25500 ? Valid
- ? 25555 ? Error: "Port 25555 is reserved and cannot be used"

### Scenario 3: Availability Check

```json
{
  "PortValidation": {
    "CheckAvailability": true
  }
}
```

**Result:**
- If port 27015 is in use by another server:
- ? 27015 ? Error: "Port 27015 (udp) is already in use"

### Scenario 4: Related Ports Unavailable

```json
{
  "PortRelationships": [{
    "Offset": 1,
    "TargetContainerPort": 27016
  }],
  "ValidateRelatedPortsAvailability": true
}
```

**Result:**
- User tries to set game port to 27015
- Query port would be 27016 (offset +1)
- If 27016 is already in use:
- ? Error: "Related port 27016 (Query Port) is not available"

---

## Implementation Guide

### Step 1: Define Port Setting Metadata

```json
{
  "Key": "GAME_PORT",
  "Description": "Main game server port",
  "DataType": "port",
  "MapsToContainerPort": true,
  "LinkedContainerPort": 7777,
  "PortProtocol": "udp",
  "IsRequired": true,
  "Placeholder": "7777"
}
```

### Step 2: Add Validation Rules

```json
{
  "PortValidation": {
    "MinPort": 7000,
    "MaxPort": 8000,
    "CheckAvailability": true,
    "ReservedPorts": [7500],
    "SuggestedPorts": [7777, 7778, 7779],
    "ValidationMessage": "Port must be between 7000-8000 and available"
  }
}
```

### Step 3: Define Port Relationships

```json
{
  "PortRelationships": [
    {
      "RelationType": "Offset",
      "TargetContainerPort": 7778,
      "TargetProtocol": "udp",
      "Offset": 1,
      "Description": "Query Port",
      "IsRequired": true
    }
  ],
  "ValidateRelatedPortsAvailability": true
}
```

### Step 4: Register Service (Program.cs)

```csharp
// Add port mapping service
builder.Services.AddScoped<PortMappingService>();
```

### Step 5: Use in Server Creation

```csharp
// When user changes port setting
var validation = await portMappingService.ValidatePortSettingAsync(
    newPortValue, settingMetadata, gameType, currentSettings);

if (!validation.IsValid)
{
    // Show validation errors
    foreach (var error in validation.Errors)
    {
        NotificationService.Notify(error, NotificationSeverity.Error);
    }
    return;
}

// Apply port changes
var result = portMappingService.ApplyPortChanges(
    gameType, settings, settingMetadata, newPortValue);

if (result.Success)
{
    // Show what was updated
    foreach (var update in result.UpdatedPorts)
    {
        Console.WriteLine($"Updated {update.Description}: {update.OldPort} ? {update.NewPort}");
    }
}
```

---

## Common Game Server Port Patterns

### Source Engine Games (CS:GO, TF2, L4D2)
```
Game Port:  27015 (UDP)
Query Port: 27016 (UDP) = Game + 1
RCON Port:  27015 (TCP) = Same as game (different protocol)
```

### Minecraft
```
Game Port:  25565 (TCP)
Query Port: 25565 (UDP) = Same as game (different protocol)
RCON Port:  25575 (TCP) = Fixed or configurable
```

### ARK: Survival Evolved
```
Game Port:  7777 (UDP)
Query Port: 7778 (UDP) = Game + 1
RCON Port:  27020 (TCP) = Fixed
Raw UDP:    7779 (UDP) = Game + 2
```

### Rust
```
Game Port:  28015 (UDP)
RCON Port:  28016 (TCP) = Game + 1
App Port:   28082 (TCP) = Fixed
```

### Valheim
```
Game Port:  2456 (UDP)
Query Port: 2457 (UDP) = Game + 1
```

---

## Best Practices

### ? DO:
- Always set `IsRequired = true` for essential ports (query, RCON)
- Use `CheckAvailability = true` to prevent conflicts
- Provide `SuggestedPorts` for common configurations
- Set reasonable `MinPort` and `MaxPort` ranges
- Add descriptive `Description` for each relationship
- Use `ValidateRelatedPortsAvailability = true` for reliability

### ? DON'T:
- Allow users to edit auto-managed ports (`IsUserEditable = false`)
- Set overly restrictive port ranges
- Forget to document port relationships
- Use complex multiplier relationships unless necessary
- Skip validation on critical ports

---

## Troubleshooting

### Port Already in Use

**Error:** "Port 27015 (udp) is already in use"

**Solutions:**
1. Check if another server is using the port
2. Use a different port number
3. Stop the conflicting server
4. Check system firewall rules

### Related Port Unavailable

**Error:** "Related port 27016 (Query Port) is not available"

**Solutions:**
1. Choose a different base port (e.g., 27020 instead of 27015)
2. Ensure sufficient port range is available
3. Check if port range is blocked by firewall

### Validation Failed

**Error:** "Port must be between 25000 and 26000"

**Solutions:**
1. Choose a port within the allowed range
2. Check game type metadata for constraints
3. Use suggested ports if available

---

## Future Enhancements

1. **Auto Port Selection** - Automatically find next available port block
2. **Port Pools** - Reserve ranges of ports for different game types
3. **Dynamic Port Discovery** - Detect game's actual ports from container
4. **Port Health Monitoring** - Check if ports remain accessible
5. **Port Analytics** - Track port usage patterns
6. **Conflict Resolution UI** - Visual port conflict resolver

---

## API Reference

### PortMappingService Methods

```csharp
// Validate a port setting
Task<PortValidationResult> ValidatePortSettingAsync(
    uint portValue,
    SettingMetadata settingMetadata,
    GameTypeDefinition gameType,
    Dictionary<string, string> currentSettings)

// Apply port changes
PortApplicationResult ApplyPortChanges(
    GameTypeDefinition gameType,
    Dictionary<string, string> settings,
    SettingMetadata settingMetadata,
    uint newPortValue)

// Get suggested ports
List<uint> GetSuggestedPorts(SettingMetadata settingMetadata)
```

---

## Summary

The Advanced Port Mapping system provides:
- ? Automatic port relationship management
- ? Comprehensive validation
- ? Availability checking
- ? User-friendly error messages
- ? Flexible relationship types
- ? Production-ready reliability

**Ready to implement in your game server management system!** ??
