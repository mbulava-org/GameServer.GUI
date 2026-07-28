# GameType Metadata System - Complete Guide

## Overview

> **This guide describes the legacy (V1) metadata system.** The primary service now uses the V2 data model (`GameType`, `GameTypeRevision`, `GameTypeSettingDefinition`, `GameTypeSettingMetadata`, `GameTypeSettingPortMapping`, `GameTypeWebHost`, `MountTypeConfig`, and `GameServerVolume`). The legacy `DefaultSettings`, `SettingsMetadata`, `PortValidation`, `PortRelationships`, `ExtendedMetadata`, `VolumeSetupConfig`, and `VolumeDriverConfigOptions` types have been removed.

The GameType metadata system provides a comprehensive way to define, validate, and manage game server configurations. This guide explains how all the pieces work together when building a game server.

---

## Architecture Overview (Legacy)

```
???????????????????????????????????????????????????????????
?                    GameType Definition                  ?
?  (Base configuration for a game like Minecraft)         ?
???????????????????????????????????????????????????????????
             ?
             ???? Ports (Which ports to expose)
             ???? Volumes (Data persistence)
             ???? DefaultSettings (Environment variables with defaults)
             ?         ???? SettingsMetadata (Optional: How to present/validate)
             ?                   ???? PortValidation (Port ranges, availability)
             ?                   ???? PortRelationships (Auto-update related ports)
             ???? ExtendedMetadata (Game-level config like TTY)
```

---

## Data Flow: Creating a Server

### Step 1: User Selects GameType

**User Action:** Clicks "Create Server" ? Selects "Minecraft"

**System Loads:**
```sql
SELECT * FROM GameTypes WHERE Key = 'minecraft';
-- Loads: DisplayName, Image, Ports, Volumes, DefaultSettings
```

**UI Shows:**
- Game thumbnail
- Display name: "Minecraft Server"
- Description
- Default ports: 25565/tcp, 25565/udp

---

### Step 2: Configure Settings

**System Loads Settings with Metadata:**
```sql
SELECT 
    ds.SettingKey,
    ds.SettingValue as DefaultValue,
    ds.Description as SettingDescription,
    sm.DataType,
    sm.IsRequired,
    sm.Category,
    sm.Placeholder,
    sm.ValidationPattern,
    sm.MapsToContainerPort
FROM DefaultSettings ds
LEFT JOIN SettingsMetadata sm ON ds.Id = sm.DefaultSettingId
WHERE ds.GameTypeId = (SELECT Id FROM GameTypes WHERE Key = 'minecraft')
ORDER BY COALESCE(sm.DisplayOrder, ds.DisplayOrder), ds.SettingKey;
```

**Example Settings Shown:**

| Setting | Default | Type | Required | Validation |
|---------|---------|------|----------|------------|
| EULA | TRUE | boolean | ? Yes | Must accept |
| VERSION | LATEST | enum | ? No | Dropdown: LATEST, 1.21, 1.20... |
| SERVER_PORT | 25565 | port | ? No | Range: 25500-25600, Check availability |
| MAX_MEMORY | 2G | string | ? No | Pattern: ^[0-9]+[MGmg]$ |

---

### Step 3: User Changes Port

**User Action:** Changes SERVER_PORT from 25565 ? 25570

**System Validates:**
```csharp
// 1. Load SettingsMetadata for SERVER_PORT
var portSetting = await _context.DefaultSettings
    .Include(ds => ds.SettingsMetadata)
        .ThenInclude(sm => sm.PortValidation)
    .Include(ds => ds.SettingsMetadata)
        .ThenInclude(sm => sm.PortRelationships)
    .FirstOrDefaultAsync(ds => ds.SettingKey == "SERVER_PORT" && 
                              ds.GameTypeId == gameTypeId);

// 2. Validate port value
var metadata = portSetting.SettingsMetadata;
var validation = metadata.PortValidation;

if (newPort < validation.MinPort || newPort > validation.MaxPort)
    throw new ValidationException($"Port must be between {validation.MinPort} and {validation.MaxPort}");

// 3. Check port availability
if (validation.CheckAvailability)
{
    bool isAvailable = await portAllocator.IsAvailableAsync(newPort, metadata.PortProtocol);
    if (!isAvailable)
        throw new ValidationException($"Port {newPort} is already in use");
}

// 4. Calculate related ports
foreach (var relationship in metadata.PortRelationships)
{
    uint relatedPort = relationship.RelationType switch
    {
        0 => newPort + relationship.OffsetValue,  // Offset
        1 => relationship.FixedValue,              // Fixed
        _ => newPort * relationship.OffsetValue    // Multiplier
    };
    
    // Validate related port is also available
    if (metadata.ValidateRelatedPortsAvailability)
    {
        bool isRelatedAvailable = await portAllocator.IsAvailableAsync(
            relatedPort, relationship.TargetProtocol);
        if (!isRelatedAvailable)
            throw new ValidationException(
                $"Related port {relatedPort} ({relationship.Description}) is not available");
    }
}
```

**If Valid:**
- ? SERVER_PORT = 25570 (TCP)
- ? Query Port = 25570 (UDP) - auto-calculated (offset +0)
- ? All ports marked as allocated

---

### Step 4: Apply Settings to Container

**System Creates Container Config:**
```csharp
var containerConfig = new CreateContainerParameters
{
    Image = gameType.Image, // "itzg/minecraft-server:latest"
    Env = BuildEnvironmentVariables(settings, gameType),
    HostConfig = new HostConfig
    {
        PortBindings = BuildPortBindings(settings, gameType),
        Binds = BuildVolumeMounts(settings, gameType),
        Memory = ParseMemoryLimit(settings["MAX_MEMORY"]),
        // TTY from ExtendedMetadata
        Tty = gameType.ExtendedMetadata?.EnableTTY ?? false
    }
};

// Build environment variables
List<string> BuildEnvironmentVariables(Dictionary<string, string> settings, GameType gameType)
{
    var env = new List<string>();
    
    foreach (var setting in settings)
    {
        // Get metadata if it exists
        var metadata = gameType.DefaultSettings
            .FirstOrDefault(ds => ds.SettingKey == setting.Key)
            ?.SettingsMetadata;
        
        // Transform value based on metadata
        string value = setting.Value;
        if (metadata?.DataType == "boolean")
        {
            value = value.ToUpperInvariant(); // TRUE/FALSE
        }
        
        env.Add($"{setting.Key}={value}");
    }
    
    return env;
}

// Build port bindings
Dictionary<string, IList<PortBinding>> BuildPortBindings(
    Dictionary<string, string> settings, GameType gameType)
{
    var portBindings = new Dictionary<string, IList<PortBinding>>();
    
    foreach (var port in gameType.Ports)
    {
        // Check if this port is controlled by a setting
        var portSetting = gameType.DefaultSettings
            .FirstOrDefault(ds => ds.SettingsMetadata?.LinkedContainerPort == port.Port);
        
        int actualPort = port.Port;
        if (portSetting != null && settings.ContainsKey(portSetting.SettingKey))
        {
            actualPort = int.Parse(settings[portSetting.SettingKey]);
        }
        
        string containerPort = $"{actualPort}/{port.Protocol}";
        portBindings[containerPort] = new List<PortBinding>
        {
            new PortBinding { HostPort = actualPort.ToString() }
        };
    }
    
    return portBindings;
}
```

---

## Complete Example: Minecraft Server

### Database Records

**GameType:**
```sql
INSERT INTO GameTypes (Key, DisplayName, Image, ThumbnailUrl) VALUES
('minecraft', 'Minecraft Server', 'itzg/minecraft-server:latest', 'https://...');
```

**Ports:**
```sql
INSERT INTO Ports (GameTypeId, Port, Protocol, IsDefaultPort, Description) VALUES
(1, 25565, 'tcp', 1, 'Game Port'),
(1, 25565, 'udp', 0, 'Query Port');
```

**DefaultSettings:**
```sql
INSERT INTO DefaultSettings (GameTypeId, SettingKey, SettingValue, Description) VALUES
(1, 'EULA', 'TRUE', 'Accept EULA to run server'),
(1, 'VERSION', 'LATEST', 'Minecraft version'),
(1, 'SERVER_PORT', '25565', 'Game server port'),
(1, 'MAX_MEMORY', '2G', 'Maximum memory allocation');
```

**SettingsMetadata (Optional - only for settings needing special handling):**
```sql
-- EULA must be boolean and required
INSERT INTO SettingsMetadata (DefaultSettingId, DataType, IsRequired, Category) VALUES
(1, 'boolean', 1, 'Legal');

-- VERSION is an enum
INSERT INTO SettingsMetadata (DefaultSettingId, DataType, AllowedValuesJson, Category) VALUES
(2, 'enum', '["LATEST","1.21","1.20","1.19"]', 'Server');

-- SERVER_PORT maps to container ports with validation
INSERT INTO SettingsMetadata (DefaultSettingId, DataType, MapsToContainerPort, LinkedContainerPort, PortProtocol) VALUES
(3, 'port', 1, 25565, 'tcp');

-- Add port validation
INSERT INTO PortValidation (SettingMetadataId, MinPort, MaxPort, CheckAvailability) VALUES
(3, 25500, 25600, 1);

-- Add port relationship (UDP query port = TCP game port)
INSERT INTO PortRelationships (SettingMetadataId, RelationType, TargetContainerPort, TargetProtocol, OffsetValue, Description) VALUES
(3, 0, 25565, 'udp', 0, 'Query Port (Server List Ping)');

-- MAX_MEMORY has validation pattern
INSERT INTO SettingsMetadata (DefaultSettingId, ValidationPattern, ValidationMessage, Placeholder, Category) VALUES
(4, '^[0-9]+[MGmg]$', 'Format: 2G or 2048M', '2G', 'Performance');
```

**ExtendedMetadata:**
```sql
INSERT INTO ExtendedMetadata (GameTypeId, EnableTTY) VALUES
(1, 1); -- Minecraft needs TTY for interactive console
```

---

## UI Flow

### GameType Selection Page

**Query:**
```csharp
var gameTypes = await _context.GameTypes
    .Include(gt => gt.Ports)
    .Where(gt => gt.IsActive)
    .ToListAsync();
```

**Display:**
```razor
@foreach (var gameType in gameTypes)
{
    <RadzenCard>
        <img src="@gameType.ThumbnailUrl" />
        <h5>@gameType.DisplayName</h5>
        <p>@gameType.Description</p>
        <RadzenBadge>@gameType.Ports.Count ports</RadzenBadge>
        <RadzenButton Text="Select" Click="@(() => SelectGameType(gameType.Key))" />
    </RadzenCard>
}
```

---

### Settings Configuration Page

**Query:**
```csharp
var settings = await _context.DefaultSettings
    .Include(ds => ds.SettingsMetadata)
        .ThenInclude(sm => sm.PortValidation)
    .Include(ds => ds.SettingsMetadata)
        .ThenInclude(sm => sm.PortRelationships)
    .Where(ds => ds.GameTypeId == gameTypeId)
    .OrderBy(ds => ds.SettingsMetadata.DisplayOrder ?? ds.DisplayOrder)
    .ToListAsync();
```

**Display by DataType:**

**Boolean:**
```razor
@if (metadata?.DataType == "boolean")
{
    <RadzenCheckBox @bind-Value="@settingValue" />
}
```

**Enum:**
```razor
@if (metadata?.DataType == "enum")
{
    var options = JsonSerializer.Deserialize<List<string>>(metadata.AllowedValuesJson);
    <RadzenDropDown Data="@options" @bind-Value="@settingValue" />
}
```

**Port:**
```razor
@if (metadata?.DataType == "port")
{
    <RadzenNumeric @bind-Value="@settingValue" 
                   Min="@metadata.PortValidation?.MinPort" 
                   Max="@metadata.PortValidation?.MaxPort"
                   Change="@(async () => await ValidatePortAsync(settingKey, settingValue))" />
    
    @if (metadata.PortRelationships?.Any() == true)
    {
        <small class="text-muted">
            Related ports will be auto-configured:
            @foreach (var rel in metadata.PortRelationships)
            {
                <span>@rel.Description (@(settingValue + rel.OffsetValue)/@rel.TargetProtocol)</span>
            }
        </small>
    }
}
```

**String with Validation:**
```razor
@if (!string.IsNullOrEmpty(metadata?.ValidationPattern))
{
    <RadzenTextBox @bind-Value="@settingValue" 
                   Pattern="@metadata.ValidationPattern" 
                   Placeholder="@metadata.Placeholder" />
    @if (!string.IsNullOrEmpty(metadata.ValidationMessage))
    {
        <small class="text-muted">@metadata.ValidationMessage</small>
    }
}
```

---

## Validation Flow

### 1. Field-Level Validation

**Triggered:** User types in field, on blur, or on change

```csharp
private async Task ValidateFieldAsync(string settingKey, string value)
{
    var setting = settings.First(s => s.SettingKey == settingKey);
    var metadata = setting.SettingsMetadata;
    
    // Check if required
    if (metadata?.IsRequired == true && string.IsNullOrEmpty(value))
    {
        ShowError(settingKey, "This field is required");
        return;
    }
    
    // Check if cannot be empty
    if (metadata?.CannotBeEmpty == true && string.IsNullOrWhiteSpace(value))
    {
        ShowError(settingKey, "This field cannot be empty");
        return;
    }
    
    // Validate pattern
    if (!string.IsNullOrEmpty(metadata?.ValidationPattern))
    {
        if (!Regex.IsMatch(value, metadata.ValidationPattern))
        {
            ShowError(settingKey, metadata.ValidationMessage ?? "Invalid format");
            return;
        }
    }
    
    // Validate port if applicable
    if (metadata?.DataType == "port")
    {
        await ValidatePortAsync(settingKey, uint.Parse(value));
    }
    
    ClearError(settingKey);
}
```

### 2. Port Validation

**Triggered:** User changes a port setting

```csharp
private async Task ValidatePortAsync(string settingKey, uint portValue)
{
    var setting = settings.First(s => s.SettingKey == settingKey);
    var metadata = setting.SettingsMetadata;
    var validation = metadata.PortValidation;
    
    // Check range
    if (portValue < validation.MinPort || portValue > validation.MaxPort)
    {
        ShowError(settingKey, 
            $"Port must be between {validation.MinPort} and {validation.MaxPort}");
        return;
    }
    
    // Check reserved ports
    if (validation.ReservedPortsJson != null)
    {
        var reserved = JsonSerializer.Deserialize<List<uint>>(validation.ReservedPortsJson);
        if (reserved.Contains(portValue))
        {
            ShowError(settingKey, $"Port {portValue} is reserved");
            return;
        }
    }
    
    // Check availability
    if (validation.CheckAvailability)
    {
        var isAvailable = await PortApi.CheckAvailabilityAsync(portValue, metadata.PortProtocol);
        if (!isAvailable)
        {
            ShowError(settingKey, $"Port {portValue} is already in use");
            return;
        }
    }
    
    // Validate related ports
    if (metadata.ValidateRelatedPortsAvailability && 
        metadata.PortRelationships?.Any() == true)
    {
        foreach (var relationship in metadata.PortRelationships)
        {
            var relatedPort = CalculateRelatedPort(portValue, relationship);
            var isAvailable = await PortApi.CheckAvailabilityAsync(
                relatedPort, relationship.TargetProtocol);
            
            if (!isAvailable)
            {
                ShowError(settingKey, 
                    $"Related port {relatedPort} ({relationship.Description}) is not available");
                return;
            }
        }
    }
    
    ClearError(settingKey);
    
    // Show preview of port mappings
    ShowPortPreview(settingKey, portValue, metadata);
}
```

### 3. Form Submission Validation

**Triggered:** User clicks "Create Server"

```csharp
private async Task<bool> ValidateAllAsync()
{
    bool isValid = true;
    
    foreach (var setting in settings)
    {
        var metadata = setting.SettingsMetadata;
        var value = userInputs[setting.SettingKey];
        
        // Validate each field
        await ValidateFieldAsync(setting.SettingKey, value);
        
        if (HasError(setting.SettingKey))
        {
            isValid = false;
        }
    }
    
    return isValid;
}
```

---

## Container Creation

### Final Step: Build and Start Container

```csharp
public async Task<GameServer> CreateServerAsync(
    string gameTypeKey, 
    string serverName, 
    Dictionary<string, string> settings)
{
    // 1. Load GameType with all metadata
    var gameType = await _context.GameTypes
        .Include(gt => gt.Ports)
        .Include(gt => gt.Volumes)
        .Include(gt => gt.DefaultSettings)
            .ThenInclude(ds => ds.SettingsMetadata)
                .ThenInclude(sm => sm.PortRelationships)
        .Include(gt => gt.ExtendedMetadata)
        .FirstOrDefaultAsync(gt => gt.Key == gameTypeKey);
    
    // 2. Merge user settings with defaults
    var finalSettings = MergeSettings(gameType.DefaultSettings, settings);
    
    // 3. Apply port mappings from SettingsMetadata
    var portBindings = await BuildPortBindingsAsync(gameType, finalSettings);
    
    // 4. Create container
    var containerConfig = new CreateContainerParameters
    {
        Name = serverName,
        Image = gameType.Image,
        Env = finalSettings.Select(kvp => $"{kvp.Key}={kvp.Value}").ToList(),
        HostConfig = new HostConfig
        {
            PortBindings = portBindings,
            Binds = BuildVolumeMounts(gameType.Volumes, serverName),
            Tty = gameType.ExtendedMetadata?.EnableTTY ?? false,
            Memory = ParseMemoryLimit(finalSettings.GetValueOrDefault("MAX_MEMORY"))
        }
    };
    
    var container = await _dockerClient.Containers.CreateContainerAsync(containerConfig);
    
    // 5. Start container
    await _dockerClient.Containers.StartContainerAsync(container.ID, new ContainerStartParameters());
    
    // 6. Create GameServer record
    var server = new GameServer
    {
        Id = serverName,
        ContainerId = container.ID,
        GameTypeKey = gameTypeKey,
        Settings = finalSettings,
        Status = "Running"
    };
    
    return server;
}
```

---

## Summary

### The Complete Flow

1. **GameType** defines what the game is (Minecraft, Valheim, etc.)
2. **DefaultSettings** define environment variables with defaults
3. **SettingsMetadata** (optional) defines HOW to present/validate settings
4. **PortValidation** ensures ports are valid and available
5. **PortRelationships** auto-update related ports
6. **ExtendedMetadata** stores game-level config (TTY, etc.)

### Benefits

? **Type Safety** - DataType ensures correct input widgets  
? **Validation** - Prevents invalid configurations  
? **Port Management** - Auto-updates related ports  
? **Flexibility** - Not all settings need metadata  
? **User Friendly** - Clear UI with validation messages  
? **Maintainable** - Database relationships enforce integrity

---

**This system provides a complete, validated, and user-friendly way to create game servers!** ??
