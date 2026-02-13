# GameType Extended Metadata System - Implementation Summary

## Overview

This implementation extends the GameType system with advanced metadata capabilities stored in a separate data file. The system allows for:
- TTY/stdin attachment configuration
- Rich setting metadata (descriptions, validation, types)
- Required/non-empty field enforcement
- Dynamic port mapping from settings
- Type override capabilities
- UI-friendly categorization and ordering

---

## Files Created

### Models
1. **`src/GameServer.Docker/Models/SettingMetadata.cs`**
   - Defines metadata for individual settings/environment variables
   - Properties: Description, IsRequired, CannotBeEmpty, DataType, MapsToContainerPort, ValidationPattern, etc.

2. **`src/GameServer.Docker/Models/GameTypeExtendedMetadata.cs`**
   - Container for extended game type configuration
   - Properties: GameTypeKey, EnableTTY, AttachStdin, SettingsMetadata

### Configuration
3. **`src/GameServer.Docker/Configurations/GameTypeExtendedMetadataRegistryData.cs`**
   - Configuration class for extended metadata file path
   - Default: `/data/game-types-extended.json`

### Interfaces
4. **`src/GameServer.Docker/Interfaces/IGameTypeExtendedMetadataRegistry.cs`**
   - Interface for extended metadata registry operations
   - Methods: GetAll, Get, AddOrUpdate, Delete

### Services
5. **`src/GameServer.Docker/Services/GameTypeExtendedMetadataRegistryFile.cs`**
   - File-based implementation of extended metadata registry
   - Thread-safe with semaphore locking
   - Follows Record of Authority pattern
   - Includes built-in Minecraft metadata

6. **`src/GameServer.Docker/Services/GameTypeMetadataApplier.cs`**
   - Helper service for applying metadata to containers
   - Methods:
     - `ApplyMetadata()` - Apply TTY/stdin settings to ContainerSpec
     - `ValidateSettings()` - Validate server settings against rules
     - `GetDynamicPorts()` - Extract dynamic port mappings
     - `GetSettingsByCategory()` - Organize settings for UI
     - `ParseListSetting()` - Parse list-type settings

### Controllers
7. **`src/GameServer.Docker/Controllers/GameTypeExtendedMetadataController.cs`**
   - REST API for managing extended metadata
   - Endpoints:
     - `GET /api/gametypes/extended` - Get all
     - `GET /api/gametypes/extended/{key}` - Get one
     - `POST /api/gametypes/extended` - Create/update
     - `DELETE /api/gametypes/extended/{key}` - Delete
     - `GET /api/gametypes/extended/{key}/settings/{settingKey}` - Get setting metadata
     - `PUT /api/gametypes/extended/{key}/settings/{settingKey}` - Update setting metadata
     - `DELETE /api/gametypes/extended/{key}/settings/{settingKey}` - Delete setting metadata

### Documentation
8. **`docs/GameType-Extended-Metadata.md`**
   - Comprehensive documentation of the extended metadata system
   - API reference, usage examples, integration guide

9. **`docs/GameType-Extended-Metadata-Integration.md`**
   - Practical integration examples
   - Code samples for backend and frontend
   - Testing strategies

---

## Files Modified

### Program.cs
- Added configuration binding for `GameTypeExtendedMetadataRegistryData`
- Registered `IGameTypeExtendedMetadataRegistry` as singleton
- Registered `GameTypeMetadataApplier` as singleton

### appsettings.Development.json
- Added `GameTypeRegistryData` section with file path
- Added `GameTypeExtendedMetadataRegistryData` section with file path

---

## Built-in Metadata

The system initializes with comprehensive metadata for Minecraft, including:

### Minecraft Settings Metadata
- **EULA**: Required boolean with validation
- **VERSION**: Server version selector
- **TYPE**: Server type (VANILLA, PAPER, etc.)
- **MEMORY**: Memory allocation with pattern validation
- **SERVER_PORT**: Dynamic port mapping
- **MAX_PLAYERS**: Number validation
- **MOTD**: Message of the Day
- **DIFFICULTY**: Game difficulty setting
- **MODE**: Game mode setting
- **LEVEL**: World name
- **SEED**: World generation seed

All settings are organized by category (Legal, Server, Performance, Network, Gameplay, World) with display ordering.

---

## Key Features

### 1. Thread-Safe Persistence
- Uses `SemaphoreSlim` for lock-based concurrency control
- Atomic file operations with `FileMode.Create`
- Automatic flush before releasing lock

### 2. Record of Authority Pattern
- Existing file is always respected on startup
- Built-in defaults only created when file doesn't exist
- Immediate persistence of all changes

### 3. Validation System
- Required field checking
- Non-empty validation
- Regex pattern matching
- Custom validation messages
- Server-side and client-side capable

### 4. Dynamic Port Mapping
- Settings can map to container ports
- Automatic port exposure based on setting values
- Protocol specification (tcp/udp)

### 5. Type System
- Supported types: string, number, boolean, list, port
- Override automatic type detection
- Custom delimiters for list types

### 6. UI Integration
- Category-based grouping
- Display order hints
- Placeholder text
- Description fields
- Validation feedback

---

## API Endpoints Summary

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/gametypes/extended` | Get all extended metadata |
| GET | `/api/gametypes/extended/{key}` | Get metadata for specific game type |
| POST | `/api/gametypes/extended` | Create or update extended metadata |
| DELETE | `/api/gametypes/extended/{key}` | Delete extended metadata |
| GET | `/api/gametypes/extended/{key}/settings/{settingKey}` | Get specific setting metadata |
| PUT | `/api/gametypes/extended/{key}/settings/{settingKey}` | Update specific setting metadata |
| DELETE | `/api/gametypes/extended/{key}/settings/{settingKey}` | Delete specific setting metadata |

---

## Configuration

### Default File Paths
- **GameTypes**: `/data/game-types.json`
- **Extended Metadata**: `/data/game-types-extended.json`

### Configuration Example
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

## Usage Flow

### Creating a Server with Validation
1. Client fetches game type definition
2. Client fetches extended metadata
3. Client renders form with validation rules
4. User fills in settings
5. Client validates input (optional but recommended)
6. Server validates input (mandatory)
7. Server applies extended metadata (TTY, dynamic ports)
8. Container is created with applied metadata

### Updating Extended Metadata
1. Define metadata rules
2. POST to `/api/gametypes/extended`
3. Metadata persisted to file immediately
4. Available for subsequent server creation

---

## Integration Points

### Container Creation
```csharp
// In DockerServiceHelper
containerSpec = await _metadataApplier.ApplyMetadata(containerSpec, server.GameType);
```

### Validation
```csharp
// In Controller
var errors = await _metadataApplier.ValidateSettings(server, server.GameType);
if (errors.Any()) return BadRequest(errors);
```

### Dynamic Ports
```csharp
// In Port Configuration
var dynamicPorts = await _metadataApplier.GetDynamicPorts(server, server.GameType);
allPorts.AddRange(dynamicPorts);
```

---

## Next Steps

### Recommended Enhancements
1. **Update DockerServiceHelper**: Inject `GameTypeMetadataApplier` and apply metadata during container creation
2. **Add Validation to Controllers**: Use `ValidateSettings()` before creating servers
3. **Implement Dynamic Ports**: Use `GetDynamicPorts()` in port configuration logic
4. **Create UI Components**: Build forms using the categorization and validation features
5. **Add More Game Types**: Extend built-in metadata for other game types (Valheim, Palworld, etc.)

### Optional Features
- Conditional settings (show/hide based on other values)
- Setting dependencies
- Multi-select types
- File upload settings
- Default value templating

---

## Testing Checklist

- [ ] Create extended metadata via API
- [ ] Update individual setting metadata
- [ ] Validate that file is persisted correctly
- [ ] Test validation rules (required, pattern matching)
- [ ] Test dynamic port mapping
- [ ] Test TTY/stdin attachment
- [ ] Test list parsing with custom delimiters
- [ ] Verify thread-safety with concurrent updates
- [ ] Test Record of Authority pattern (restart service, verify file is loaded)
- [ ] Build UI form using categorized settings

---

## Architecture Benefits

1. **Separation of Concerns**: Core game types separate from UI metadata
2. **Extensibility**: Easy to add new validation rules and metadata fields
3. **Flexibility**: Metadata can be modified without code changes
4. **Type Safety**: Strong typing with C# models
5. **Thread Safety**: Built-in concurrency control
6. **RESTful**: Clean API for CRUD operations
7. **Documentation**: Comprehensive docs and examples

---

## Migration Notes

Existing game types continue to work without extended metadata. The system gracefully handles missing metadata:
- Returns empty validation errors
- Skips TTY/stdin configuration
- No dynamic ports added
- Default type detection used

To add metadata for existing game types, simply POST to the extended metadata endpoint.

---

## Support

For questions or issues:
1. Review the documentation in `docs/GameType-Extended-Metadata.md`
2. Check integration examples in `docs/GameType-Extended-Metadata-Integration.md`
3. Examine the built-in Minecraft metadata in `GameTypeExtendedMetadataRegistryFile.cs`
4. Test with the API endpoints using tools like Postman or curl

---

**Implementation Complete!** ?

All code compiles successfully and is ready for integration and testing.
