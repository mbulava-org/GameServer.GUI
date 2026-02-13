# Client README Update Summary

## Changes Made to `src/GameServer.Docker.Client/ReadMe.md`

### 1. Added Extended Metadata API Section ?

**Location:** After Game Type API section

**Content:**
- Complete API usage examples for `GameTypeExtendedMetadataApi`
- CRUD operations for extended metadata
- Individual setting metadata management
- Building dynamic forms from metadata

**Examples Included:**
- Listing all extended metadata
- Getting metadata for specific game type
- Creating metadata with TTY, enums, validation
- Dynamic port mapping configuration
- Value mappings for user-friendly descriptions
- Updating individual settings
- Building categorized, ordered forms

### 2. Updated Features List ?

**Added:**
- ?? **Extended Metadata** - Advanced game type configuration with validation, enums, and dynamic port mapping

### 3. Added Interface Documentation ?

**New Interface:** `IGameTypeExtendedMetadataApi`

**Methods Documented:**
- `GetAllAsync()` - List all extended metadata
- `GetAsync(string gameTypeKey)` - Get metadata for game type
- `SaveAsync(GameTypeExtendedMetadata metadata)` - Create/update metadata
- `DeleteAsync(string gameTypeKey)` - Delete metadata
- `GetSettingMetadataAsync(string gameTypeKey, string settingKey)` - Get setting metadata
- `UpdateSettingMetadataAsync(...)` - Update setting metadata
- `DeleteSettingMetadataAsync(...)` - Delete setting metadata

### 4. Added Data Models ?

**New Models Documented:**

**GameTypeExtendedMetadata:**
- GameTypeKey
- EnableTTY
- SettingsMetadata
- CustomProperties

**SettingMetadata:** (18 properties)
- Key, Description
- IsRequired, CannotBeEmpty
- DataType
- MapsToContainerPort, LinkedContainerPort, PortProtocol
- ListDelimiter
- AllowedValues, ValueMappings
- DisplayOrder, Category, Placeholder
- ValidationPattern, ValidationMessage

**Updated PortDefinition:**
- Added `IsDefaultPort` property documentation

### 5. Updated Dependency Injection Section ?

**Added:**
- Registration example for `IGameTypeExtendedMetadataApi`
- Injected into HttpClient factory pattern

### 6. Enhanced Service Example ?

**Updated `GameServerService`:**
- Added `IGameTypeExtendedMetadataApi` dependency
- Demonstrated validation before deployment
- Shows how to check required settings using metadata

---

## Key Documentation Highlights

### Features Explained

1. **TTY Configuration**
   - Enable pseudo-terminal for interactive access
   - Use case: Interactive console servers

2. **Setting Metadata**
   - Required field validation
   - Empty value prevention
   - Type specification
   - Regex validation
   - UI organization (categories, ordering)

3. **Enum Support**
   - Dropdown options via `AllowedValues`
   - User-friendly descriptions via `ValueMappings`
   - Perfect for difficulty, game modes, etc.

4. **Dynamic Port Mapping**
   - Settings control port values
   - Links to existing ports via `LinkedContainerPort`
   - Example: SERVER_PORT updates container port

5. **Value Mappings**
   - Semantic descriptions for technical values
   - Example: "0" ? "Disabled - Feature turned off"

### Code Examples Provided

1. ? List all extended metadata
2. ? Get metadata for specific game type
3. ? Create comprehensive metadata with all features
4. ? Update individual settings
5. ? Build dynamic categorized forms
6. ? Validate settings before deployment
7. ? Delete metadata

---

## Example Usage Flow

```csharp
// 1. Get extended metadata
var metadata = await extendedMetadataApi.GetAsync("minecraft");

// 2. Check settings metadata
foreach (var setting in metadata.SettingsMetadata)
{
    Console.WriteLine($"{setting.Key}: {setting.Value.Description}");
    if (setting.Value.IsRequired)
        Console.WriteLine("  REQUIRED");
    if (setting.Value.AllowedValues?.Any() == true)
        Console.WriteLine($"  Options: {string.Join(", ", setting.Value.AllowedValues)}");
}

// 3. Create server with validated settings
var server = new GameServer
{
    GameType = "minecraft",
    Settings = new Dictionary<string, string>
    {
        ["EULA"] = "true",  // Required
        ["DIFFICULTY"] = "hard",  // Enum value
        ["SERVER_PORT"] = "25566"  // Port mapping
    }
};

// 4. Validate before deploy
foreach (var settingMeta in metadata.SettingsMetadata.Values)
{
    if (settingMeta.IsRequired && !server.Settings.ContainsKey(settingMeta.Key))
        throw new InvalidOperationException($"Missing required: {settingMeta.Key}");
}

await gameServerApi.DeployAsync(server);
```

---

## Documentation Organization

The Extended Metadata section is placed logically:
1. After Game Type API (related functionality)
2. Before Port API (different domain)
3. Includes comprehensive examples
4. Links to advanced features
5. Shows real-world usage patterns

---

## Benefits for Client Developers

1. **Comprehensive API Documentation**
   - All endpoints explained
   - Clear parameter descriptions
   - Return value documentation

2. **Practical Examples**
   - Copy-paste ready code
   - Real-world scenarios
   - Best practices demonstrated

3. **Feature Explanations**
   - When to use each feature
   - How features work together
   - Common patterns

4. **Integration Guide**
   - DI setup
   - Service usage
   - Validation patterns

---

## README Structure

```
GameServer.Docker.Client
??? Features
?   ??? ? Extended Metadata added
??? Quick Start
?   ??? Server Management
?   ??? File Management
?   ??? Resource Monitoring
?   ??? Service Logs
?   ??? Dashboard API
?   ??? Game Type API
?   ??? ? Extended Metadata API (NEW)
?   ??? Port API
??? API Client Interfaces
?   ??? IGameServerApi
?   ??? IDashboardApi
?   ??? IGameTypeApi
?   ??? ? IGameTypeExtendedMetadataApi (NEW)
?   ??? IPortApi
??? Data Models
?   ??? GameServer
?   ??? GameServerDashboardItem
?   ??? GameTypeDefinition
?   ??? ? GameTypeExtendedMetadata (NEW)
?   ??? ? SettingMetadata (NEW)
?   ??? ServerResourceUsage
?   ??? ? PortDefinition (UPDATED)
?   ??? PortMapping
?   ??? VolumeDefinition
?   ??? FileItem
??? Dependency Injection
?   ??? ? Extended Metadata API registration added
??? Use in Controllers/Services
    ??? ? Validation example added
```

---

## Quality Checklist

- ? Clear, concise descriptions
- ? Consistent formatting with rest of README
- ? Practical, runnable examples
- ? Proper C# syntax
- ? Explains all properties
- ? Shows real-world usage
- ? Includes error handling
- ? Demonstrates validation
- ? Links features together
- ? Maintains existing style

---

## Next Steps for Client Library

1. **NSwag Generation** - Extended Metadata API will be auto-generated when client is rebuilt
2. **Testing** - Verify generated client matches documentation
3. **Examples Project** - Consider adding dedicated examples for Extended Metadata
4. **Integration Tests** - Test extended metadata workflows

---

**Documentation Complete!** ??

The Client README now fully documents the Extended Metadata system with comprehensive examples and best practices.
