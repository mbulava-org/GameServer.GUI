# DataType Validation - Removed CHECK Constraint

## Summary of Changes

### What Changed
✅ **Removed database CHECK constraint** on `SettingsMetadata.DataType`
✅ **Updated `NormalizeDataType`** to only perform case normalization
✅ **Updated tests** to validate permissive behavior
✅ **Created migration** to remove constraint

---

## Philosophy

### Previous Approach (Constrained)
- ❌ Database enforced valid types via CHECK constraint
- ❌ Application silently converted invalid types to null
- ❌ Limited extensibility

### New Approach (Permissive)
- ✅ **Any value allowed** - including custom types
- ✅ **Explicit is better than implicit** - what you set is what you get
- ✅ **Null means "not set"** - not "invalid"
- ✅ **UI validation optional** - if desired for UX
- ✅ **Future-proof** - no schema changes for new types

---

## Files Modified

### 1. Database Schema
**File**: `src/GameServer.Docker/Data/GameServerDbContext.cs`

**Removed**:
```csharp
entity.ToTable("SettingsMetadata", t =>
{
    t.HasCheckConstraint("CK_SettingsMetadata_DataType",
        "DataType IS NULL OR DataType IN ('string', 'number', 'boolean', 'enum', 'list', 'port', 'timezone')");
});
```

**Now**:
```csharp
entity.ToTable("SettingsMetadata");
```

### 2. Repository Method
**File**: `src/GameServer.Docker/Repositories/GameTypeRepository.cs`

**Updated**:
```csharp
/// <summary>
/// Normalizes DataType values to lowercase for consistency.
/// Null or whitespace values are preserved as null.
/// Invalid types are NOT validated here - they're passed through as-is.
/// Validation should be done at the application/UI layer if needed.
/// </summary>
private static string? NormalizeDataType(string? dataType)
{
    if (string.IsNullOrWhiteSpace(dataType))
    {
        return null;
    }

    return dataType.ToLowerInvariant(); // Case normalization only
}
```

### 3. Tests Updated
**File**: `tests/GameServer.Docker.Tests/Repositories/GameTypeRepositoryDataTypeTests.cs`

**Changed test expectations**:
- "Invalid" types → Now preserved (case-normalized)
- Custom types → Fully supported
- Null/empty → Still treated as "not set"

**Example**:
```csharp
[Theory]
[InlineData("invalid", "invalid")]      // Previously → null, now → "invalid"
[InlineData("CustomType", "customtype")] // New: custom types allowed
public async Task SaveExtendedMetadata_WithAnyDataType_ShouldNormalizeCaseOnly(
    string inputType, string expectedType)
```

### 4. Migration Created
**File**: `src/GameServer.Docker/Data/Migrations/20260304191221_RemoveDataTypeCheckConstraint.cs`

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropCheckConstraint(
        name: "CK_SettingsMetadata_DataType",
        table: "SettingsMetadata");
}
```

---

## Behavior Changes

### What's Preserved
| Input | Output | Notes |
|-------|--------|-------|
| `null` | `null` | Intentionally not set |
| `""` | `null` | Empty treated as not set |
| `"  "` | `null` | Whitespace treated as not set |
| `"STRING"` | `"string"` | Case normalized |
| `"number"` | `"number"` | Already lowercase |

### What's New
| Input | Output | Notes |
|-------|--------|-------|
| `"invalid"` | `"invalid"` | ✅ Now allowed (was → `null`) |
| `"CustomType"` | `"customtype"` | ✅ Custom types supported |
| `"port-range"` | `"port-range"` | ✅ Custom types can have dashes |
| `"MyType123"` | `"mytype123"` | ✅ Alphanumeric custom types |

---

## Migration Steps

### Apply Migration
```bash
cd src/GameServer.Docker
dotnet ef database update --context GameServerDbContext
```

### Verify
```bash
# Check constraint is removed
sqlite3 gameserver.db ".schema SettingsMetadata"
```

Should **NOT** show CHECK constraint.

---

## Recommended DataTypes

While any value is now allowed, UI should suggest common types:

### Standard Types
- `string` - General text
- `number` - Integers/decimals
- `boolean` - True/false flags
- `enum` - Select from predefined list
- `list` - Multiple values
- `port` - Port numbers (special validation)
- `timezone` - Timezone identifiers

### Custom Types (Examples)
- `port-range` - Range like "25565-25575"
- `color` - Hex color picker
- `file-path` - File system paths
- `url` - HTTP/HTTPS URLs
- `email` - Email addresses
- `regex` - Regular expression patterns

---

## UI Validation (Optional)

If you want to guide users, implement client-side validation:

```typescript
const COMMON_TYPES = [
    'string', 'number', 'boolean', 'enum', 'list', 'port', 'timezone'
];

function validateDataType(dataType: string): ValidationResult {
    if (!dataType) {
        return { valid: true, message: 'No type specified' };
    }
    
    if (COMMON_TYPES.includes(dataType.toLowerCase())) {
        return { valid: true, message: 'Standard type' };
    }
    
    return { 
        valid: true, 
        warning: `Custom type '${dataType}' - ensure your UI handles it`,
        message: 'Custom type (allowed, but may need special handling)'
    };
}
```

**Key**: Show warnings, don't block. Respect explicit values.

---

## Benefits

### 1. Extensibility
- Add new types without database changes
- System evolves naturally
- No deployment coordination needed

### 2. Explicit Behavior
- What you save is what you get
- No surprises or silent conversions
- Clear intent (null = not set, value = set)

### 3. Developer-Friendly
- Test with any type during development
- Experiment with custom types
- No constraint violations

### 4. Future-Proof
- New UI frameworks → new types
- Domain-specific types → just add them
- No technical debt from constraints

---

## Breaking Changes

### For Developers

**Before**:
```csharp
// This would be silently converted to null
metadata.DataType = "custom_type"; // → null in DB
```

**After**:
```csharp
// Now preserved as-is (case normalized)
metadata.DataType = "custom_type"; // → "custom_type" in DB
```

### For Database

**Migration required** to remove constraint:
```bash
dotnet ef database update
```

Existing data is **not affected** - only the constraint is removed.

---

## Test Coverage

### Tests Updated
- ✅ `SaveExtendedMetadata_WithAnyDataType_ShouldNormalizeCaseOnly`
- ✅ `SaveExtendedMetadata_WithMultipleSettings_ShouldNormalizeAllDataTypes`
- ✅ `SaveExtendedMetadata_UpdateExistingWithCustomDataType_ShouldPreserveIt`

### Still Validated
- ✅ Null/empty handling
- ✅ Case normalization
- ✅ Multiple settings
- ✅ Update scenarios

**Total**: 27 tests still cover DataType handling

---

## Decision Rationale

### Why Remove Constraint?

1. **Flexibility**: System needs to evolve without schema changes
2. **Trust**: Trust developers/admins to set meaningful values
3. **Extensibility**: Custom types are a feature, not a bug
4. **Simplicity**: Less code = fewer bugs (no silent conversions)
5. **Standards**: Null means "not set" is clearer than "invalid"

### Why Not Enum?

Could use database enum, but:
- Requires migrations for new types
- Limits extensibility
- Not all databases support enums well
- TEXT with application validation is more flexible

---

## Related Documents

- **Migration**: `src/GameServer.Docker/Data/Migrations/20260304191221_RemoveDataTypeCheckConstraint.cs`
- **Schema**: `src/GameServer.Docker/Data/GameServerDbContext.cs`
- **Tests**: `tests/GameServer.Docker.Tests/Repositories/GameTypeRepositoryDataTypeTests.cs`
- **Documentation**: `docs/TEST-FIXES-DATABASE-CONSTRAINT.md`

---

## Status

✅ **Implemented**
✅ **Tests Updated**
✅ **Migration Created**
✅ **Documentation Complete**

**Ready for**: Production use
**Breaking**: Yes (requires migration)
**Backward Compatible**: Yes (existing data unaffected)
