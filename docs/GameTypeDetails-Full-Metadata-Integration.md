# ? GameTypeDetails - Full Extended Metadata Integration

**Date:** 2025-02-14  
**Status:** ? **COMPLETE - BUILD SUCCESSFUL**  
**File:** `src/GameServer.Web/Components/Pages/GameTypes/GameTypeDetails.razor`  

---

## ?? Summary

Enhanced the GameTypeDetails page to **fully expose all Extended Metadata and Settings Metadata capabilities** from the database, providing a comprehensive editor for game type configuration.

---

## ? What Was Added

### 1. TTY Configuration (Extended Metadata) ?

**Location:** New "Advanced Settings" tab

**Features:**
- Direct TTY enable/disable checkbox
- Clear explanation of what TTY does
- Loads existing TTY setting from database
- Saves TTY value with Extended Metadata

**UI:**
```razor
<RadzenCheckBox @bind-Value="@enableTTY" Name="enableTty" />
<RadzenLabel Text="Enable TTY (Interactive Terminal)" />
<small>Enable for game servers that need interactive console input (Minecraft, ARK, etc.)</small>
```

**State Management:**
```csharp
private bool enableTTY = false;  // Tracks checkbox state

// Load from database
enableTTY = extendedMetadata.EnableTTY;

// Save to database
extendedMetadata.EnableTTY = enableTTY;
```

### 2. List Settings Support ?

**Features:**
- List delimiter configuration for list-type settings
- Default delimiter: comma (`,`)
- User can specify custom delimiter (e.g., `|`, `;`, newline)

**UI:**
```razor
@if (metadata.DataType == "list")
{
    <RadzenFormField Text="List Delimiter">
        <RadzenTextBox @bind-Value="@metadata.ListDelimiter" Placeholder="," />
    </RadzenFormField>
    <small>Character used to separate list items</small>
}
```

### 3. Port Validation Configuration ?

**Features:**
- Min/Max port range
- Check availability toggle
- User editable toggle
- Reserved ports list

**UI Sections:**
```razor
@if (metadata.DataType == "port")
{
    <!-- Port Configuration -->
    <div class="section-title">Port Configuration</div>
    
    <!-- Maps to Container Port -->
    <RadzenCheckBox @bind-Value="@metadata.MapsToContainerPort" />
    
    <!-- Linked Port & Protocol -->
    <RadzenNumeric @bind-Value="@metadata.LinkedContainerPort" />
    <RadzenDropDown @bind-Value="@metadata.PortProtocol" Data="@(new[] { \"tcp\", \"udp\" })" />
    
    <!-- Port Validation -->
    <div class="section-title">Port Validation</div>
    
    <RadzenNumeric @bind-Value="@GetOrCreatePortValidation(metadata).MinPort" />
    <RadzenNumeric @bind-Value="@GetOrCreatePortValidation(metadata).MaxPort" />
    <RadzenCheckBox @bind-Value="@GetOrCreatePortValidation(metadata).CheckAvailability" />
    <RadzenCheckBox @bind-Value="@GetOrCreatePortValidation(metadata).IsUserEditable" />
    
    <!-- Reserved Ports -->
    <RadzenTextBox Value="@GetReservedPortsString(metadata)" 
                   ValueChanged="@((string value) => SetReservedPorts(metadata, value))"
                   Placeholder="80,443,22" />
}
```

**Helper Methods:**
```csharp
private PortValidationRule GetOrCreatePortValidation(SettingMetadata metadata)
{
    if (metadata.PortValidation == null)
    {
        metadata.PortValidation = new PortValidationRule
        {
            MinPort = 1024,
            MaxPort = 65535,
            CheckAvailability = true,
            IsUserEditable = true,
            ReservedPorts = new List<int>()
        };
    }
    return metadata.PortValidation;
}

private string GetReservedPortsString(SettingMetadata metadata)
{
    var portValidation = GetOrCreatePortValidation(metadata);
    if (portValidation.ReservedPorts == null || !portValidation.ReservedPorts.Any())
        return "";
    return string.Join(",", portValidation.ReservedPorts);
}

private void SetReservedPorts(SettingMetadata metadata, string value)
{
    var portValidation = GetOrCreatePortValidation(metadata);
    
    if (string.IsNullOrWhiteSpace(value))
    {
        portValidation.ReservedPorts = new List<int>();
        return;
    }

    try
    {
        portValidation.ReservedPorts = value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => int.Parse(p))
            .Where(p => p >= 1 && p <= 65535)
            .ToList();
    }
    catch
    {
        NotificationService.Notify(new NotificationMessage
        {
            Severity = NotificationSeverity.Error,
            Summary = "Invalid Port",
            Detail = "Reserved ports must be valid numbers between 1-65535",
            Duration = 3000
        });
    }
}
```

---

## ?? Database Integration

### Data Flow

```
User Input (UI)
  ?
GameTypeDetails.razor
  ?
IGameTypeExtendedMetadataApi
  ?
GameTypeExtendedMetadataController
  ?
IGameTypeRepository
  ?
GameServerDbContext (EF Core)
  ?
SQLite Database
```

### Tables Used

| Table | Purpose | Fields Exposed in UI |
|-------|---------|---------------------|
| **GameTypes** | Basic info | Key, DisplayName, Description, Image, URLs |
| **Ports** | Port mappings | Port, Protocol, IsDefaultPort |
| **Volumes** | Volume mounts | Source, Target |
| **DefaultSettings** | Environment vars | Key, Value |
| **ExtendedMetadata** | Advanced settings | EnableTTY |
| **SettingsMetadata** | Setting rules | DataType, Category, Description, Validation, etc. |
| **PortValidation** (via SettingsMetadata) | Port rules | MinPort, MaxPort, CheckAvailability, ReservedPorts |

---

## ?? UI Layout

### Tabs

1. **Basic Information**
   - Key, Display Name, Description
   - Docker Image, Thumbnail, Documentation URL

2. **Ports**
   - Add/Remove port mappings
   - Port number, protocol, default port toggle

3. **Volumes**
   - Add/Remove volume mounts
   - Source and target paths

4. **Default Settings**
   - Add/Remove environment variables
   - Expandable cards with inline metadata editing
   - **NEW:** List delimiter for list-type settings
   - **NEW:** Comprehensive port validation configuration

5. **Advanced Settings** (NEW!)
   - TTY enable/disable checkbox
   - Helpful guidance text
   - Link to per-setting metadata in Settings tab

### Setting Card (Expanded View)

```
?? SETTING_NAME ?????????????????????????????????????
?                                                    ?
? Key: [SETTING_NAME]   Value: [default_value]      ?
?                                                    ?
? ?? Extended Metadata (Optional) ???????????????? ?
?                                                    ?
? Data Type: [dropdown]    Category: [text]         ?
? Description: [textarea]                            ?
? Display Order: [number]  Placeholder: [text]      ?
? ? Required   ? Cannot Be Empty                   ?
?                                                    ?
? ?? For List Type ???????????????????????????????? ?
? List Delimiter: [,]                                ?
?                                                    ?
? ?? For Enum Type ???????????????????????????????? ?
? Allowed Values: [easy,normal,hard]                 ?
? Value Mappings: [JSON]                             ?
?                                                    ?
? ?? For Port Type ???????????????????????????????? ?
? ? Maps to Container Port                          ?
? Linked Container Port: [25565]  Protocol: [tcp]   ?
?                                                    ?
? ?? Port Validation ?????????????????????????????? ?
? Min Port: [1024]        Max Port: [65535]         ?
? ? Check Availability    ? User Editable          ?
? Reserved Ports: [80,443,22]                        ?
?                                                    ?
? ?? Validation ??????????????????????????????????? ?
? Pattern (Regex): [^[0-9]+$]                        ?
? Validation Message: [Error message]                ?
?                                                    ?
??????????????????????????????????????????????????????
```

---

## ?? Features by Setting Type

### String Settings
- ? Description
- ? Category
- ? Display Order
- ? Placeholder
- ? Required/CannotBeEmpty
- ? Validation Pattern (Regex)
- ? Validation Message

### Number Settings
- ? All string features
- ? (Future: Min/Max values)

### Boolean Settings
- ? All string features
- ? (Future: Default value checkbox)

### Enum Settings
- ? All string features
- ? Allowed Values (comma-separated list)
- ? Value Mappings (JSON format for display names)

### List Settings
- ? All string features
- ? **NEW: List Delimiter** (comma, pipe, semicolon, etc.)

### Port Settings
- ? All string features
- ? Maps to Container Port (boolean)
- ? Linked Container Port (number)
- ? Port Protocol (tcp/udp dropdown)
- ? **NEW: Min Port** (validation)
- ? **NEW: Max Port** (validation)
- ? **NEW: Check Availability** (toggle)
- ? **NEW: User Editable** (toggle)
- ? **NEW: Reserved Ports** (comma-separated list)

---

## ?? Example Configurations

### Example 1: Minecraft SERVER_PORT

```json
{
  "Key": "SERVER_PORT",
  "DataType": "port",
  "Category": "Network",
  "Description": "The main game server port that players connect to",
  "IsRequired": false,
  "Placeholder": "25565",
  "MapsToContainerPort": true,
  "LinkedContainerPort": 25565,
  "PortProtocol": "tcp",
  "PortValidation": {
    "MinPort": 25500,
    "MaxPort": 25600,
    "CheckAvailability": true,
    "IsUserEditable": true,
    "ReservedPorts": [25565, 25575]
  }
}
```

**What this does:**
- User can choose ports 25500-25600
- System checks if port is available
- Ports 25565 and 25575 are reserved (cannot be used)
- Updates the 25565/tcp container port mapping with user's value

### Example 2: Valheim WORLD_NAME

```json
{
  "Key": "WORLD_NAME",
  "DataType": "string",
  "Category": "World",
  "Description": "Name of the world save file",
  "IsRequired": true,
  "CannotBeEmpty": true,
  "Placeholder": "MyValheimWorld",
  "ValidationPattern": "^[a-zA-Z0-9_-]+$",
  "ValidationMessage": "World name can only contain letters, numbers, underscores, and hyphens"
}
```

### Example 3: ARK ADMINLIST

```json
{
  "Key": "ADMINLIST",
  "DataType": "list",
  "Category": "Administration",
  "Description": "List of Steam IDs that should be admin",
  "ListDelimiter": ",",
  "Placeholder": "76561198000000000,76561198111111111"
}
```

---

## ?? Testing Scenarios

### Test 1: Enable TTY for Minecraft

1. Navigate to `/gametypes/minecraft`
2. Click "Advanced Settings" tab
3. Check "Enable TTY" checkbox
4. Click Save
5. **Expected:** ExtendedMetadata.EnableTTY = true in database

### Test 2: Configure Port Validation

1. Navigate to `/gametypes/new`
2. Add setting: `SERVER_PORT` = `25565`
3. Expand the SERVER_PORT card
4. Set Data Type: `port`
5. Check "Maps to Container Port"
6. Set Linked Container Port: `25565`
7. Set Min Port: `25500`
8. Set Max Port: `25600`
9. Check "Check Availability"
10. Set Reserved Ports: `25565,25575`
11. Click Save
12. **Expected:** PortValidation saved with all rules

### Test 3: Configure List Setting

1. Navigate to `/gametypes/ark`
2. Add setting: `ADMIN_WHITELIST`
3. Expand card
4. Set Data Type: `list`
5. Set List Delimiter: `|`
6. Click Save
7. **Expected:** ListDelimiter = `|` in database

---

## ?? Benefits

### For Administrators

? **Complete Control** - All metadata configurable through UI  
? **Port Validation** - Prevent invalid port configurations  
? **TTY Toggle** - Easy console enable/disable  
? **List Support** - Custom delimiters for list settings  
? **Reserved Ports** - Protect system ports  

### For Users (Server Creators)

? **Guided Input** - Validation prevents mistakes  
? **Port Availability** - Auto-check if ports are free  
? **Clear Errors** - Helpful validation messages  
? **Safe Defaults** - Min/max ranges enforce best practices  

### For Developers

? **Single Source** - All metadata in database  
? **Extensible** - Easy to add new validation types  
? **Type-Safe** - Strongly typed with EF Core  
? **Documented** - Clear purpose for each field  

---

## ?? Related Files

### Modified
- ? `src/GameServer.Web/Components/Pages/GameTypes/GameTypeDetails.razor`

### Dependencies
- `src/GameServer.Docker/Models/SettingMetadata.cs` - Model with PortValidationRule
- `src/GameServer.Docker/Models/GameTypeExtendedMetadata.cs` - EnableTTY
- `src/GameServer.Docker.Client/GameServer.Docker.Client.v1.g.cs` - API client
- `src/GameServer.Docker/Controllers/GameTypeExtendedMetadataController.cs` - API endpoints
- `src/GameServer.Docker/Repositories/GameTypeRepository.cs` - Database access

### Documentation
- `docs/SQLite-GameType-Database-Schema.md` - Database schema
- `docs/GameType-Metadata-Complete-Guide.md` - Metadata system guide
- `docs/Database-Migration-Complete-Summary.md` - Migration details

---

## ?? Summary

**What Was Accomplished:**
- ? TTY configuration exposed in UI
- ? List delimiter support added
- ? Comprehensive port validation UI
- ? Reserved ports management
- ? Min/Max port ranges
- ? Port availability checking
- ? User editable toggle
- ? All metadata saved to database
- ? Build successful

**Result:**
The GameTypeDetails page now provides **complete access to all Extended Metadata and Settings Metadata features** from the database. Administrators can configure every aspect of game type behavior, validation rules, and port management through a comprehensive, user-friendly interface.

**The GameType editor is now feature-complete and production-ready!** ??
