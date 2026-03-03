# Bug Fix: Extended Metadata DataType Validation Failures

## Issue
Extended metadata could not be saved, resulting in:
```
SQLite Error 19: 'CHECK constraint failed: CK_SettingsMetadata_DataType'
HTTP POST /api/gametypes/extended/{key} responded 500
```

## Root Causes
1. **Missing DataType in UI**: `ExtendedMetadataEditor.razor` dropdown was missing `"timezone"` type
2. **No Server Validation**: Repository directly assigned DataType values without validation
3. **Auto-detection Removed**: Users expected explicit type selection, not auto-detection
4. **Null/Empty Handling**: DataType could be null or empty string, causing constraint violations

## Database Constraint
```sql
CK_SettingsMetadata_DataType: 
  DataType IS NULL OR DataType IN ('string', 'number', 'boolean', 'enum', 'list', 'port', 'timezone')
```

## Changes Made

### 1. Server-Side Validation (`GameTypeRepository.cs`)
**Added:** `NormalizeDataType()` method to validate and normalize DataType values
```csharp
private static string? NormalizeDataType(string? dataType)
{
    if (string.IsNullOrWhiteSpace(dataType))
        return null;

    var validTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "string", "number", "boolean", "enum", "list", "port", "timezone"
    };

    return validTypes.Contains(dataType) ? dataType.ToLowerInvariant() : null;
}
```
- Converts empty/whitespace to `null` (allowed by constraint)
- Case-insensitive validation
- Returns `null` for invalid values instead of throwing error

### 2. UI Updates

#### ExtendedMetadataEditor.razor
- ✅ Added `"timezone"` to dataTypes dropdown
- ✅ Changed `AllowClear="false"` (require selection)
- ✅ Changed placeholder from "Auto-detect" to "Select type..."
- ✅ Added helper text: "Default: string"
- ✅ Removed `DetectDataType()` method
- ✅ Set default DataType to `"string"` for all new settings

#### GameTypeDetails.razor
- ✅ Added `"timezone"` to dataTypes dropdown (was already there)
- ✅ Changed `AllowClear="false"` (require selection)
- ✅ Changed placeholder from "Auto-detect" to "Select type..."
- ✅ Added helper text: "Default: string"
- ✅ Set default DataType to `"string"` in `GetSettingMetadata()`
- ✅ Set default DataType to `"string"` in `AddSetting()`
- ✅ Default DataType to `"string"` when loading metadata with null/empty DataType

### 3. Documentation Updates
- ✅ `docs/reference/CONSTANTS-AND-CONVENTIONS.md` - Added `list` and `timezone`
- ✅ `docs/reference/QUICK-REFERENCE-CARD.md` - Added `timezone` to DataTypes table
- ✅ `src/GameServer.Docker/Models/SettingMetadata.cs` - Updated comment from "Overrides automatic type detection" to "Defaults to 'string' if not specified"

## Testing Performed
- ✅ Build successful
- ⏳ Manual testing pending deployment

## Manual Testing Checklist
- [ ] Create new game type with settings
- [ ] Edit existing game type settings
- [ ] Set DataType to each valid value (string, number, boolean, enum, list, port, timezone)
- [ ] Leave DataType unset (should default to "string")
- [ ] Save and verify in database
- [ ] Verify extended metadata loads correctly
- [ ] Check that old data with null DataType still works

## Files Changed
1. `src/GameServer.Docker/Repositories/GameTypeRepository.cs`
2. `src/GameServer.Web/Components/Pages/GameTypes/ExtendedMetadataEditor.razor`
3. `src/GameServer.Web/Components/Pages/GameTypes/GameTypeDetails.razor`
4. `src/GameServer.Docker/Models/SettingMetadata.cs`
5. `docs/reference/CONSTANTS-AND-CONVENTIONS.md`
6. `docs/reference/QUICK-REFERENCE-CARD.md`

## Rollback Plan
If issues occur:
1. Revert commit
2. Apply hotfix: Add server-side validation only (minimal change)
3. UI improvements can be deployed separately

## Related Issues
Fixes: Extended metadata save failures in GameTypeDetails and ExtendedMetadataEditor

---
**Date:** 2026-03-03  
**Priority:** URGENT  
**Status:** READY FOR COMMIT
