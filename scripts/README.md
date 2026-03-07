# GameType Synchronization Scripts

Scripts to synchronize GameTypes and their extended metadata between two GameServer instances.

## Scripts Available

### 1. PowerShell Script (Recommended)
**File**: `Sync-GameTypes.ps1`

**Usage**:
```powershell
# Default (uses hardcoded URLs)
.\scripts\Sync-GameTypes.ps1

# Custom URLs
.\scripts\Sync-GameTypes.ps1 -SourceBaseUrl "http://192.168.10.50:5164" -TargetBaseUrl "http://192.168.10.50:5163"
```

### 2. C# Script (dotnet-script)
**File**: `SyncGameTypes.csx`

**Prerequisites**:
```bash
dotnet tool install -g dotnet-script
```

**Usage**:
```bash
dotnet script scripts/SyncGameTypes.csx
```

---

## What It Does

1. ✅ Fetches all GameTypes from source server
2. ✅ For each GameType:
   - Creates/updates the GameType on target server
   - Fetches extended metadata from source
   - Creates/updates extended metadata on target

---

## Output Example

```
🔄 Starting GameType synchronization...
   Source: http://192.168.10.50:5164
   Target: http://192.168.10.50:5163

📥 Fetching GameTypes from source...
✅ Found 5 GameTypes on source

🔄 Syncing: Minecraft (minecraft)
   ✅ GameType synced
   ✅ Extended metadata synced

🔄 Syncing: Valheim (valheim)
   ✅ GameType synced
   ℹ️  No extended metadata found

==================================================
✅ Successfully synced: 5/5
==================================================
```

---

## Error Handling

- **Connection errors**: Script will report and continue with next GameType
- **404 errors**: Treated as "no extended metadata" (not an error)
- **Other errors**: Logged and counted in summary

---

## API Endpoints Used

### Source Server
- `GET /api/gametype` - Get all GameTypes
- `GET /api/gametype/{key}/extended-metadata` - Get extended metadata

### Target Server  
- `POST /api/gametype` - Create/update GameType
- `PUT /api/gametype/{key}/extended-metadata` - Create/update extended metadata

---

## Notes

- **Idempotent**: Safe to run multiple times
- **Non-destructive**: Only creates/updates, never deletes
- **Preserves data**: Target server's existing data is updated, not replaced
- **Fast**: Runs synchronously for reliability

---

## Troubleshooting

### Connection Refused
- Ensure both servers are running
- Check firewall settings
- Verify URLs are correct

### Authentication Errors
- Scripts currently don't support authentication
- Ensure APIs are accessible without auth

### JSON Errors
- Check that both servers are running compatible versions
- Verify API responses are valid JSON

---

## For Your Use Case

The default configuration is:
- **Source**: `http://192.168.10.50:5164` (port 5164)
- **Target**: `http://192.168.10.50:5163` (port 5163)

Just run:
```powershell
.\scripts\Sync-GameTypes.ps1
```

And it will synchronize everything! 🚀
