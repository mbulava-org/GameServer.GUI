# GameType Extended Metadata System

## Overview

The GameType Extended Metadata system provides advanced configuration capabilities for game server types beyond the basic `GameTypeDefinition`. It allows you to:

- Define TTY/stdin attachment options for interactive containers
- Add rich metadata for individual settings (environment variables)
- Mark settings as required or non-empty
- Map settings to dynamic container ports
- Override automatic type detection
- Provide user-friendly descriptions and validation rules

The extended metadata is stored in a **separate file** (`/data/game-types-extended.json`) from the base game type definitions, following the Record of Authority pattern.

---

## Architecture

### Models

#### `SettingMetadata`
Defines metadata for a single environment variable/setting:

```csharp
public class SettingMetadata
{
    public string Key { get; set; }                    // Setting name (e.g., "EULA")
    public string Description { get; set; }            // Human-readable description
    public bool IsRequired { get; set; }               // Must be provided
    public bool CannotBeEmpty { get; set; }            // Cannot be blank
    public string? DataType { get; set; }              // "string", "number", "boolean", "list", "port"
    public bool MapsToContainerPort { get; set; }      // Maps value to container port
    public string PortProtocol { get; set; }           // Protocol when mapping port (default: "tcp")
    public string ListDelimiter { get; set; }          // Delimiter for list types (default: ",")
    public int DisplayOrder { get; set; }              // UI ordering hint
    public string? Category { get; set; }              // Group/category name
    public string? Placeholder { get; set; }           // Placeholder text
    public string? ValidationPattern { get; set; }     // Regex validation
    public string? ValidationMessage { get; set; }     // Validation error message
}
```

#### `GameTypeExtendedMetadata`
Extended configuration for a game type:

```csharp
public class GameTypeExtendedMetadata
{
    public string GameTypeKey { get; set; }                                    // Must match GameTypeDefinition.Key
    public bool EnableTTY { get; set; }                                        // Enable pseudo-terminal
    public bool AttachStdin { get; set; }                                      // Attach stdin
    public Dictionary<string, SettingMetadata> SettingsMetadata { get; set; }  // Setting metadata
    public Dictionary<string, string> CustomProperties { get; set; }           // Extensibility
}
```

---

## Storage & Persistence

### File Locations

- **Base GameTypes**: `/data/game-types.json`
- **Extended Metadata**: `/data/game-types-extended.json`

Both files follow the **Record of Authority** pattern:
- If the file exists on startup, it is loaded and becomes the authoritative source
- If the file doesn't exist, built-in defaults are created and saved
- All changes are immediately persisted (thread-safe with semaphore locking)

### Configuration

Add to `appsettings.json`:

```json
{
  "GameTypeRegistryData": {
    "FilePath": "/data/game-types.json"
  },
  "GameTypeExtendedMetadataRegistryData": {
    "FilePath": "/data/game-types-extended.json"
  }
}
```

---

## API Endpoints

### Extended Metadata Management

#### Get All Extended Metadata
```http
GET /api/gametypes/extended
```

**Response:**
```json
[
  {
    "gameTypeKey": "minecraft",
    "enableTTY": true,
    "attachStdin": false,
    "settingsMetadata": { ... }
  }
]
```

#### Get Extended Metadata for Specific Game Type
```http
GET /api/gametypes/extended/{gameTypeKey}
```

**Example:**
```http
GET /api/gametypes/extended/minecraft
```

#### Save Extended Metadata
```http
POST /api/gametypes/extended
Content-Type: application/json

{
  "gameTypeKey": "minecraft",
  "enableTTY": true,
  "settingsMetadata": {
    "EULA": {
      "key": "EULA",
      "description": "Accept Minecraft EULA",
      "isRequired": true,
      "cannotBeEmpty": true,
      "dataType": "boolean"
    }
  }
}
```

#### Delete Extended Metadata
```http
DELETE /api/gametypes/extended/{gameTypeKey}
```

### Individual Setting Metadata Management

#### Get Setting Metadata
```http
GET /api/gametypes/extended/{gameTypeKey}/settings/{settingKey}
```

**Example:**
```http
GET /api/gametypes/extended/minecraft/settings/EULA
```

#### Update Setting Metadata
```http
PUT /api/gametypes/extended/{gameTypeKey}/settings/{settingKey}
Content-Type: application/json

{
  "key": "EULA",
  "description": "You must accept the Minecraft EULA to run the server",
  "isRequired": true,
  "cannotBeEmpty": true,
  "dataType": "boolean",
  "validationPattern": "^(true|false)$",
  "validationMessage": "Must be 'true' or 'false'"
}
```

#### Delete Setting Metadata
```http
DELETE /api/gametypes/extended/{gameTypeKey}/settings/{settingKey}
```

---

## Usage Examples

### Example 1: Minecraft EULA Requirement

```json
{
  "gameTypeKey": "minecraft",
  "enableTTY": true,
  "settingsMetadata": {
    "EULA": {
      "key": "EULA",
      "description": "You must accept the Minecraft EULA to run the server. Set to 'true' to accept.",
      "isRequired": true,
      "cannotBeEmpty": true,
      "dataType": "boolean",
      "category": "Legal",
      "displayOrder": 1,
      "validationPattern": "^(true|false)$",
      "validationMessage": "Must be 'true' or 'false'. You must accept the EULA to run the server."
    }
  }
}
```

### Example 2: Dynamic Port Mapping

```json
{
  "gameTypeKey": "minecraft",
  "settingsMetadata": {
    "SERVER_PORT": {
      "key": "SERVER_PORT",
      "description": "Server port number (default: 25565)",
      "dataType": "port",
      "mapsToContainerPort": true,
      "portProtocol": "tcp",
      "category": "Network",
      "displayOrder": 20,
      "placeholder": "25565"
    }
  }
}
```

**Effect**: When a user sets `SERVER_PORT` to `25566`, the container will automatically expose port `25566/tcp`.

### Example 3: Memory Configuration with Validation

```json
{
  "settingsMetadata": {
    "MEMORY": {
      "key": "MEMORY",
      "description": "Maximum memory for the server (e.g., 1G, 2G, 4G)",
      "dataType": "string",
      "category": "Performance",
      "displayOrder": 10,
      "placeholder": "1G",
      "validationPattern": "^\\d+[MG]$",
      "validationMessage": "Must be a number followed by M or G (e.g., 1G, 2048M)"
    }
  }
}
```

### Example 4: List Type Setting

```json
{
  "settingsMetadata": {
    "MODS": {
      "key": "MODS",
      "description": "Comma-separated list of mod URLs to install",
      "dataType": "list",
      "listDelimiter": ",",
      "category": "Mods",
      "placeholder": "https://example.com/mod1.jar,https://example.com/mod2.jar"
    }
  }
}
```

---

## Built-in Metadata

The system initializes with built-in metadata for Minecraft, including:

- **EULA**: Required boolean
- **VERSION**: Server version selector
- **TYPE**: Server type (VANILLA, PAPER, etc.)
- **MEMORY**: Memory allocation with validation
- **SERVER_PORT**: Dynamic port mapping
- **MAX_PLAYERS**: Number validation
- **MOTD**: Message of the Day
- **DIFFICULTY**: Game difficulty
- **MODE**: Game mode
- **LEVEL**: World name
- **SEED**: World seed

Additional game types can be added by extending `RegisterBuiltInMetadata()` in `GameTypeExtendedMetadataRegistryFile.cs`.

---

## Integration with UI/Client

### Recommended UI Flow

1. **Fetch GameType**: `GET /api/gametypes/{key}`
2. **Fetch Extended Metadata**: `GET /api/gametypes/extended/{key}`
3. **Merge Data**: Combine `DefaultSettings` with `SettingsMetadata`
4. **Render Form**: Use metadata for:
   - Field ordering (`DisplayOrder`)
   - Grouping (`Category`)
   - Validation (`ValidationPattern`, `IsRequired`, `CannotBeEmpty`)
   - Help text (`Description`, `Placeholder`)
   - Input type (`DataType`)

### Validation Logic

```csharp
// Pseudo-code for client-side validation
foreach (var setting in userInput)
{
    var metadata = extendedMetadata.SettingsMetadata[setting.Key];
    
    if (metadata.IsRequired && string.IsNullOrEmpty(setting.Value))
        throw new ValidationException("Required field");
    
    if (metadata.CannotBeEmpty && string.IsNullOrWhiteSpace(setting.Value))
        throw new ValidationException("Cannot be empty");
    
    if (!string.IsNullOrEmpty(metadata.ValidationPattern))
    {
        if (!Regex.IsMatch(setting.Value, metadata.ValidationPattern))
            throw new ValidationException(metadata.ValidationMessage);
    }
}
```

---

## Thread Safety

Both registries use `SemaphoreSlim` for thread-safe file operations:
- Prevents race conditions when multiple users save simultaneously
- Ensures file writes are atomic (fully written before lock release)
- Uses `FileMode.Create` to truncate and overwrite files completely

---

## Extensibility

### Adding Custom Properties

Use `CustomProperties` for extensibility:

```json
{
  "gameTypeKey": "minecraft",
  "customProperties": {
    "supportUrl": "https://support.example.com",
    "requiresLicense": "false",
    "maxRecommendedPlayers": "50"
  }
}
```

### Adding New Data Types

To add custom data types:

1. Add the type to `SettingMetadata.DataType` documentation
2. Update client-side rendering logic to handle the new type
3. Update validation logic as needed

---

## Migration Path

### From Existing GameTypes

If you already have game types defined without extended metadata:

1. System automatically creates `/data/game-types-extended.json` on first startup
2. Built-in metadata is created for known game types (e.g., Minecraft)
3. Custom game types will have empty extended metadata initially
4. Use the API to add metadata for your custom game types

### Example Migration Script

```bash
# Get existing game type
curl http://localhost:5000/api/gametypes/mygame

# Create extended metadata
curl -X POST http://localhost:5000/api/gametypes/extended \
  -H "Content-Type: application/json" \
  -d '{
    "gameTypeKey": "mygame",
    "enableTTY": false,
    "settingsMetadata": {
      "PORT": {
        "key": "PORT",
        "description": "Game server port",
        "dataType": "port",
        "mapsToContainerPort": true
      }
    }
  }'
```

---

## Troubleshooting

### Metadata Not Loading

1. Check file path in configuration
2. Verify file permissions
3. Check logs for deserialization errors
4. Validate JSON syntax

### Changes Not Persisting

1. Verify `_canSave` is `true` (should be logged on startup)
2. Check directory write permissions
3. Look for exceptions in logs during save operations

### Port Mapping Not Working

1. Ensure `MapsToContainerPort = true`
2. Verify `DataType = "port"`
3. Check that the setting value is a valid port number
4. Implement port mapping logic in your container orchestration code

---

## Future Enhancements

Potential additions:
- Conditional settings (show/hide based on other setting values)
- Setting dependencies (e.g., "FORGE_VERSION requires TYPE=FORGE")
- Multi-select list types
- File upload settings
- Color picker for settings
- Templated default values (e.g., `{serverId}`, `{timestamp}`)
