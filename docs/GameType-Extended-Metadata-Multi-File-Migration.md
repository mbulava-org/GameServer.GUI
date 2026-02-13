# GameType Extended Metadata - Multi-File Storage Migration

## Overview

The `GameTypeExtendedMetadataRegistryFile` service has been refactored to store each game type's extended metadata in its own individual JSON file, rather than storing all game types in a single file.

## Changes Made

### 1. Configuration Update

**File**: `src\GameServer.Docker\Configurations\GameTypeExtendedMetadataRegistryData.cs`

- **Changed**: `FilePath` property to `DirectoryPath`
- **Old**: Single file path (`/data/game-types-extended.json`)
- **New**: Directory path (`/data/game-types-extended`)
- **Backward Compatibility**: Old `FilePath` property marked as `[Obsolete]` but still available for migration

### 2. Storage Structure

**Old Structure:**
```
/data/game-types-extended.json
```
Single file containing all game types:
```json
{
  "minecraft": { ... },
  "hytale": { ... },
  "valheim": { ... }
}
```

**New Structure:**
```
/data/game-types-extended/
  ??? minecraft.json
  ??? hytale.json
  ??? valheim.json
```
Each game type in its own file:
```json
{
  "GameTypeKey": "minecraft",
  "EnableTTY": true,
  "SettingsMetadata": { ... }
}
```

### 3. Service Refactoring

**File**: `src\GameServer.Docker\Services\GameTypeExtendedMetadataRegistryFile.cs`

#### Key Changes:

1. **File Path Management**
   - Added `GetFilePathForGameType(string gameTypeKey)` method
   - Sanitizes game type keys for safe filename usage
   - Format: `{DirectoryPath}/{gameTypeKey}.json`

2. **Loading Logic**
   - Changed from single file load to directory scan
   - `LoadAllMetadataFiles()` discovers and loads all `.json` files
   - Each file is deserialized individually

3. **Saving Logic**
   - Changed from saving entire dictionary to saving individual files
   - `SaveData(string gameTypeKey)` saves only the specified game type
   - `SaveMetadataToFile()` handles file I/O for individual metadata

4. **Deletion Logic**
   - Added `DeleteDataFile(string gameTypeKey)` to remove individual files
   - Deletes only the specific game type's file

5. **Migration Support**
   - **Automatic Migration**: `MigrateLegacyFile()` runs on service startup
   - Detects old single-file format at legacy path
   - Extracts each game type from the legacy file
   - Saves each to its own file in the new directory
   - Renames legacy file to `.migrated` to preserve it

### 4. Configuration Update

**File**: `src\GameServer.Docker\appsettings.Development.json`

```json
"GameTypeExtendedMetadataRegistryData": {
  "DirectoryPath": "/data/game-types-extended"
}
```

## Migration Process

### Automatic Migration

When the service starts with the new code:

1. **Checks for legacy file**: Looks for `/data/game-types-extended.json`
2. **If found**:
   - Loads all game types from the legacy file
   - Creates the new directory if it doesn't exist
   - Saves each game type to its own file
   - Renames legacy file to `/data/game-types-extended.json.migrated`
3. **Logs migration**: All steps are logged for audit trail

### Manual Migration (if needed)

If automatic migration fails or you need manual control:

```bash
# 1. Backup existing file
cp /data/game-types-extended.json /data/game-types-extended.json.backup

# 2. Create new directory
mkdir -p /data/game-types-extended

# 3. Update appsettings to use DirectoryPath

# 4. Restart service - migration will occur automatically
```

## Benefits

### 1. **Performance**
- ? Only affected files are written on updates
- ? Reduced I/O for single game type operations
- ? No need to serialize/deserialize entire dictionary

### 2. **Concurrency**
- ? Better isolation between game type updates
- ? Reduced lock contention (per-file operations)
- ? Parallel operations possible in the future

### 3. **Maintainability**
- ? Easier to inspect/edit individual game type metadata
- ? Simpler version control (smaller diffs)
- ? Can add/remove game types without touching others

### 4. **Scalability**
- ? No single file size limit concerns
- ? Better filesystem distribution
- ? Easier to implement caching strategies

### 5. **Reliability**
- ? Corruption in one file doesn't affect others
- ? Easier backup/restore of individual game types
- ? Atomic operations per game type

## API Behavior

### No Changes to Public Interface

The `IGameTypeExtendedMetadataRegistry` interface remains unchanged:

```csharp
Task AddOrUpdate(GameTypeExtendedMetadata metadata);  // Saves to individual file
Task Delete(string gameTypeKey);                      // Deletes individual file
Task<GameTypeExtendedMetadata?> Get(string gameTypeKey);
Task<List<GameTypeExtendedMetadata>> GetAll();
```

All methods work identically from the consumer's perspective.

## Testing Recommendations

### 1. Migration Testing
```csharp
// Test 1: Verify migration from legacy file
// - Place old format file at legacy path
// - Start service
// - Verify new directory created with individual files
// - Verify legacy file renamed to .migrated

// Test 2: Verify no-op when no legacy file exists
// - Remove legacy file
// - Start service
// - Verify built-in metadata initialized
```

### 2. CRUD Operations
```csharp
// Test 3: Add new game type
var metadata = new GameTypeExtendedMetadata { GameTypeKey = "newgame", ... };
await registry.AddOrUpdate(metadata);
// Verify: /data/game-types-extended/newgame.json created

// Test 4: Update existing game type
metadata.EnableTTY = false;
await registry.AddOrUpdate(metadata);
// Verify: File updated, no new files created

// Test 5: Delete game type
await registry.Delete("newgame");
// Verify: /data/game-types-extended/newgame.json deleted
```

### 3. Concurrency Testing
```csharp
// Test 6: Concurrent updates to different game types
// Should not block each other (different files)

// Test 7: Concurrent updates to same game type
// Should be serialized by _saveLock
```

## Rollback Strategy

If issues arise and you need to rollback:

1. **Restore legacy format**:
   ```bash
   # If .migrated file exists
   mv /data/game-types-extended.json.migrated /data/game-types-extended.json
   ```

2. **Update configuration**:
   ```json
   "GameTypeExtendedMetadataRegistryData": {
     "FilePath": "/data/game-types-extended.json"
   }
   ```

3. **Deploy previous version** of the service

## Logging

The service provides comprehensive logging:

- **Migration**: `"Legacy metadata file detected at {LegacyPath}. Starting migration..."`
- **File Operations**: `"Saving metadata for {GameType} to {FilePath}"`
- **Errors**: `"Error loading metadata from file {FilePath}"`
- **Summary**: `"GameTypeExtendedMetadata registry initialized with {Count} game type(s)"`

## Configuration Reference

### New Configuration (Recommended)
```json
"GameTypeExtendedMetadataRegistryData": {
  "DirectoryPath": "/data/game-types-extended"
}
```

### Legacy Configuration (Deprecated)
```json
"GameTypeExtendedMetadataRegistryData": {
  "FilePath": "/data/game-types-extended.json"
}
```

## File Naming Convention

Game type keys are sanitized to ensure filesystem safety:

- **Allowed characters**: Letters, digits, hyphens, underscores
- **Example**: `"minecraft-java-edition"` ? `minecraft-java-edition.json`
- **Sanitization**: Other characters are removed

## Future Enhancements

Potential improvements enabled by this architecture:

1. **Lazy Loading**: Load metadata on-demand rather than all at startup
2. **File Watching**: Auto-reload when files change externally
3. **Compression**: Compress individual files for large metadata
4. **Versioning**: Track version history per game type
5. **Validation**: JSON schema validation per file
6. **Caching**: Implement per-file caching strategies

## Summary

This refactoring provides a more scalable, maintainable, and performant storage solution for game type extended metadata while maintaining full backward compatibility through automatic migration.
