# Corrected Database Relationships

## ? Fixed Relationship Model

### The Problem (Before)
```
GameType ? DefaultSettings (settings with default values)
GameType ? ExtendedMetadata ? SettingsMetadata (metadata about settings)
```
**Issue:** SettingsMetadata was NOT connected to actual DefaultSettings!

### The Solution (After)
```
GameType ? DefaultSettings ? SettingsMetadata (0:1 optional)
GameType ? ExtendedMetadata (game-level metadata like TTY)
```
**Fixed:** Each DefaultSetting can have 0 or 1 SettingsMetadata record!

---

## Visual Diagram

```
???????????????????????????????????????????????????????????
? GameType                                                ?
? ??????????????????????????????????????????????????????? ?
? ? Id, Key, DisplayName, Image, etc.                   ? ?
? ??????????????????????????????????????????????????????? ?
??????????????????????????????????????????????????????????
   ?              ?               ?             ?
   ? 1:N          ? 1:N           ? 1:N         ? 1:1
   ?              ?               ?             ?
????????    ????????????   ??????????????  ????????????????
?Ports ?    ? Volumes  ?   ?Default     ?  ?Extended      ?
?      ?    ?          ?   ?Settings    ?  ?Metadata      ?
????????    ????????????   ?            ?  ?              ?
                            ?SettingKey  ?  ?EnableTTY     ?
                            ?SettingValue?  ?CustomProps   ?
                            ??????????????  ????????????????
                                   ?
                                   ? 0:1 (optional)
                                   ?
                            ??????????????????????
                            ?SettingsMetadata    ????????
                            ?                    ?      ?
                            ?Description         ?      ? 1:1
                            ?IsRequired          ?      ?
                            ?DataType            ?      ?
                            ?MapsToContainerPort ?  ????????????????
                            ?LinkedContainerPort ?  ?PortValidation?
                            ?                    ?  ?              ?
                            ??????????????????????  ?MinPort       ?
                                      ?             ?MaxPort       ?
                                      ? 1:N         ????????????????
                                      ?
                            ??????????????????????
                            ?PortRelationships   ?
                            ?                    ?
                            ?RelationType        ?
                            ?TargetContainerPort ?
                            ?OffsetValue         ?
                            ??????????????????????
```

---

## Example: Minecraft Server

### GameType: minecraft
```sql
INSERT INTO GameTypes (Key, DisplayName, Image) 
VALUES ('minecraft', 'Minecraft Server', 'itzg/minecraft-server:latest');
```

### DefaultSettings (with values)
```sql
-- Basic settings
INSERT INTO DefaultSettings (GameTypeId, SettingKey, SettingValue) VALUES
(1, 'EULA', 'TRUE'),
(1, 'VERSION', 'LATEST'),
(1, 'SERVER_PORT', '25565'),
(1, 'RCON_PORT', '25575'),
(1, 'MAX_MEMORY', '2G');
```

### SettingsMetadata (optional - only for settings that need special handling)
```sql
-- EULA needs to be required and boolean
INSERT INTO SettingsMetadata (DefaultSettingId, DataType, IsRequired) 
VALUES (1, 'boolean', 1);

-- SERVER_PORT maps to container port with validation
INSERT INTO SettingsMetadata (DefaultSettingId, DataType, MapsToContainerPort, LinkedContainerPort, PortProtocol)
VALUES (3, 'port', 1, 25565, 'tcp');

-- Add port validation
INSERT INTO PortValidation (SettingMetadataId, MinPort, MaxPort, CheckAvailability)
VALUES (2, 25500, 25600, 1);

-- Add port relationship (query port = game port for UDP)
INSERT INTO PortRelationships (SettingMetadataId, RelationType, TargetContainerPort, TargetProtocol, OffsetValue)
VALUES (2, 0, 25565, 'udp', 0);
```

### ExtendedMetadata (game-type-level config)
```sql
INSERT INTO ExtendedMetadata (GameTypeId, EnableTTY)
VALUES (1, 1);  -- Minecraft needs TTY for interactive console
```

---

## Query Examples

### Get Setting with Metadata
```sql
-- Get the SERVER_PORT setting and its metadata
SELECT 
    ds.SettingKey,
    ds.SettingValue as DefaultValue,
    sm.DataType,
    sm.IsRequired,
    sm.MapsToContainerPort,
    sm.LinkedContainerPort,
    pv.MinPort,
    pv.MaxPort
FROM DefaultSettings ds
LEFT JOIN SettingsMetadata sm ON ds.Id = sm.DefaultSettingId
LEFT JOIN PortValidation pv ON sm.Id = pv.SettingMetadataId
WHERE ds.SettingKey = 'SERVER_PORT' 
  AND ds.GameTypeId = (SELECT Id FROM GameTypes WHERE Key = 'minecraft');
```

### Get All Settings for a GameType
```sql
SELECT 
    ds.SettingKey,
    ds.SettingValue,
    ds.Description,
    sm.DataType,
    sm.IsRequired,
    sm.Category,
    CASE WHEN sm.Id IS NOT NULL THEN 1 ELSE 0 END as HasMetadata
FROM DefaultSettings ds
LEFT JOIN SettingsMetadata sm ON ds.Id = sm.DefaultSettingId
WHERE ds.GameTypeId = (SELECT Id FROM GameTypes WHERE Key = 'minecraft')
ORDER BY ds.DisplayOrder;
```

### Get Port-Mapped Settings
```sql
SELECT 
    gt.Key as GameTypeKey,
    ds.SettingKey,
    ds.SettingValue as DefaultPort,
    sm.LinkedContainerPort,
    sm.PortProtocol,
    COUNT(pr.Id) as RelatedPortsCount
FROM DefaultSettings ds
INNER JOIN SettingsMetadata sm ON ds.Id = sm.DefaultSettingId
INNER JOIN GameTypes gt ON ds.GameTypeId = gt.Id
LEFT JOIN PortRelationships pr ON sm.Id = pr.SettingMetadataId
WHERE sm.MapsToContainerPort = 1
GROUP BY ds.Id;
```

---

## Benefits of This Design

### ? Correct Relationships
- Each DefaultSetting represents an actual setting (like EULA=TRUE)
- SettingsMetadata OPTIONALLY describes how to present/validate it
- Not all settings need metadata (simple strings don't need validation)

### ? Flexible
- Can add settings without metadata
- Can add metadata later
- Can remove metadata without losing setting

### ? Efficient
- Only store metadata for settings that need it
- Simple settings (like VERSION=LATEST) don't need extra tables

### ? Query Friendly
```sql
-- Find all settings that need validation
SELECT * FROM DefaultSettings ds
INNER JOIN SettingsMetadata sm ON ds.Id = sm.DefaultSettingId
WHERE sm.ValidationPattern IS NOT NULL;

-- Find all port-mapped settings
SELECT * FROM DefaultSettings ds
INNER JOIN SettingsMetadata sm ON ds.Id = sm.DefaultSettingId
WHERE sm.MapsToContainerPort = 1;
```

---

## Migration from Old Design

If you have existing data with the old design:

```sql
-- Migrate SettingsMetadata from ExtendedMetadata to DefaultSettings
UPDATE SettingsMetadata 
SET DefaultSettingId = (
    SELECT ds.Id 
    FROM DefaultSettings ds
    INNER JOIN ExtendedMetadata em ON ds.GameTypeId = em.GameTypeId
    WHERE ds.SettingKey = SettingsMetadata.SettingKey
      AND em.Id = SettingsMetadata.ExtendedMetadataId
)
WHERE DefaultSettingId IS NULL;
```

---

## EF Core Usage

```csharp
// Get GameType with all settings and their metadata
var gameType = await _context.GameTypes
    .Include(gt => gt.DefaultSettings)
        .ThenInclude(ds => ds.SettingsMetadata)
            .ThenInclude(sm => sm.PortValidation)
    .Include(gt => gt.DefaultSettings)
        .ThenInclude(ds => ds.SettingsMetadata)
            .ThenInclude(sm => sm.PortRelationships)
    .FirstOrDefaultAsync(gt => gt.Key == "minecraft");

// Check if a setting has metadata
var eulaSetting = gameType.DefaultSettings.First(ds => ds.SettingKey == "EULA");
if (eulaSetting.SettingsMetadata != null)
{
    // This setting has special handling
    bool isRequired = eulaSetting.SettingsMetadata.IsRequired;
    string dataType = eulaSetting.SettingsMetadata.DataType;
}

// Find all port-mapped settings
var portSettings = gameType.DefaultSettings
    .Where(ds => ds.SettingsMetadata?.MapsToContainerPort == true)
    .ToList();
```

---

## Summary

### Before (Wrong) ?
```
GameType ? ExtendedMetadata ? SettingsMetadata
GameType ? DefaultSettings
```
**Problem:** SettingsMetadata and DefaultSettings were disconnected!

### After (Correct) ?
```
GameType ? DefaultSettings ? SettingsMetadata (0:1)
GameType ? ExtendedMetadata
```
**Solution:** Each DefaultSetting can optionally have SettingsMetadata!

### Key Points

1. **DefaultSettings** = The actual settings and their default values
2. **SettingsMetadata** = Optional UI/validation rules for specific settings
3. **ExtendedMetadata** = Game-type-level metadata (like EnableTTY)
4. **1:1 Optional** = A setting exists with or without metadata

**This is the correct relational model!** ??
