# Manual POST Instructions for Minecraft Metadata

## Files Generated:
1. `minecraft-metadata-complete.json` - Extended metadata with all 119 settings
2. `minecraft-gametype-defaults.json` - GameType with updated default values

## How to POST Manually:

### Option 1: Using PowerShell (Simple)
```powershell
# Extended Metadata
$metadata = Get-Content "output\minecraft-metadata-complete.json" -Raw
Invoke-RestMethod -Uri "http://192.168.10.50:5164/api/gametypes/extended/minecraft" `
    -Method Post `
    -Body $metadata `
    -ContentType "application/json; charset=utf-8"

# GameType Defaults
$gametype = Get-Content "output\minecraft-gametype-defaults.json" -Raw
Invoke-RestMethod -Uri "http://192.168.10.50:5164/api/gametypes/minecraft" `
    -Method Put `
    -Body $gametype `
    -ContentType "application/json; charset=utf-8"
```

### Option 2: Using curl
```bash
# Extended Metadata
curl -X POST http://192.168.10.50:5164/api/gametypes/extended/minecraft \
  -H "Content-Type: application/json; charset=utf-8" \
  -d @output/minecraft-metadata-complete.json

# GameType Defaults  
curl -X PUT http://192.168.10.50:5164/api/gametypes/minecraft \
  -H "Content-Type: application/json; charset=utf-8" \
  -d @output/minecraft-gametype-defaults.json
```

### Option 3: Using Postman/Insomnia
1. Create new POST request to `http://192.168.10.50:5164/api/gametypes/extended/minecraft`
2. Set Body to "raw" and "JSON"
3. Copy/paste content from `minecraft-metadata-complete.json`
4. Send

5. Create new PUT request to `http://192.168.10.50:5164/api/gametypes/minecraft`
6. Set Body to "raw" and "JSON"
7. Copy/paste content from `minecraft-gametype-defaults.json`
8. Send

## What's Included:

### Extended Metadata Updates:
- ✅ All 119 settings with accurate descriptions from itzg/docker-minecraft-server docs
- ✅ Proper dataTypes (boolean, number, string, enum, port)
- ✅ Organized into 11 categories
- ✅ TZ field as dropdown with 67 world timezones
- ✅ TYPE field with 10 server types (VANILLA, PAPER, FABRIC, FORGE, etc.)
- ✅ MODE field with friendly game mode labels
- ✅ DIFFICULTY field as dropdown
- ✅ LOG_LEVEL as dropdown
- ✅ Helpful placeholder values

### GameType Default Updates:
- ✅ EULA = TRUE (required to start)
- ✅ TYPE = PAPER (recommended for performance)
- ✅ VERSION = LATEST
- ✅ MEMORY = 2G
- ✅ TZ = America/Chicago
- ✅ Best practice defaults for all settings

## Verification:

After POST, verify with:
```powershell
# Check setting count
$m = Invoke-RestMethod "http://192.168.10.50:5164/api/gametypes/extended/minecraft"
$m.settingsMetadata.Count  # Should be 119

# Check TZ configuration
$m.settingsMetadata.TZ.dataType  # Should be "enum"
$m.settingsMetadata.TZ.allowedValues.Count  # Should be 67

# Check TYPE options
$m.settingsMetadata.TYPE.allowedValues  # Should show VANILLA, PAPER, etc.
```
