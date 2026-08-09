# DataType Handling - No Validation Approach

## Updated Approach

**The CHECK constraint has been removed.** The system now takes a permissive approach:
- ✅ **Any DataType value is allowed** (including custom types)
- ✅ **Null and empty strings are respected** as intentional "not set" values
- ✅ **Only case normalization** is performed (to lowercase)
- ✅ **No silent conversions** to null for "invalid" types
- ✅ **UI layer validation** is recommended for user experience

---

## Design Philosophy

### Respect Explicit Values
- If a user/system sets a value, it's respected
- Null or empty string means "intentionally not set"
- Don't silently convert or "fix" values
- Application layer handles validation if needed

### Extensibility
- Allow custom DataType values for future extensibility
- No database constraint limiting types
- System can evolve without schema changes

---

## Implementation

### Database Schema (NO CHECK Constraint)
```sql
CREATE TABLE SettingsMetadata (
    ...
    DataType TEXT, -- No constraint, any value allowed
    ...
);
```

### NormalizeDataType Method
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

    return dataType.ToLowerInvariant();
}
```

**Location**: `src/GameServer.Docker/Repositories/GameTypeRepository.cs`

---

## Test Updates

### Tests Now Validate Permissive Behavior

#### Any DataType Accepted
```csharp
[Theory]
[InlineData("invalid", "invalid")]
[InlineData("text", "text")]
[InlineData("CustomType", "customtype")] // Custom types allowed
public async Task SaveExtendedMetadata_WithAnyDataType_ShouldNormalizeCaseOnly(
    string inputType, string expectedType)
{
    // Arrange
    var metadata = CreateTestMetadata(gameType.Key, inputType);

    // Act
    var result = await _repository.SaveExtendedMetadataAsync(gameType.Key, metadata);

    // Assert
    Assert.Equal(expectedType, result.SettingsMetadata["TEST_SETTING"].DataType);
}
```

#### Null/Empty Preserved
```csharp
[Fact]
public async Task SaveExtendedMetadata_WithNullDataType_ShouldPreserveNull()
{
    var metadata = CreateTestMetadata(gameType.Key, null);
    var result = await _repository.SaveExtendedMetadataAsync(gameType.Key, metadata);
    Assert.Null(result.SettingsMetadata["TEST_SETTING"].DataType);
}
```

---

## Migration

### Removed CHECK Constraint
**Migration**: `20260304191221_RemoveDataTypeCheckConstraint.cs`

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // Remove the DataType check constraint to allow application-level validation
    migrationBuilder.DropCheckConstraint(
        name: "CK_SettingsMetadata_DataType",
        table: "SettingsMetadata");
}
```

**To apply**:
```bash
dotnet ef database update --context GameServerDbContext
```

---

## Recommended DataTypes (Not Enforced)

While the database accepts any value, the UI should suggest common types:

- `string` - Text values
- `number` - Numeric values  
- `boolean` - True/false
- `enum` - Select from list
- `list` - Multiple values
- `port` - Port numbers
- `timezone` - Timezone strings

**Custom types** are fully supported for extensibility.

---

## UI Layer Validation (Optional)

If validation is desired, implement it in the UI:

```typescript
const RECOMMENDED_TYPES = [
    'string', 'number', 'boolean', 'enum', 'list', 'port', 'timezone'
];

function warnIfUnusualType(dataType: string) {
    if (!RECOMMENDED_TYPES.includes(dataType.toLowerCase())) {
        console.warn(`Unusual DataType: ${dataType}`);
        // Show warning to user, but allow it
    }
}
```

---

## Benefits

### 1. **Flexibility**
- Support custom types without schema changes
- Future-proof for new requirements

### 2. **No Silent Failures**
- What you set is what you get
- No unexpected null conversions

### 3. **Explicit Intent**
- Null means "not set", not "invalid"
- Empty string can be meaningful

### 4. **Application Layer Control**
- Validation where it makes sense
- Can evolve independently of database

---

## When to Use Null vs Custom Type

### Use Null When:
- Setting doesn't need a DataType
- Type is not applicable
- Intentionally omitting the type

### Use Custom Type When:
- Need special handling in UI
- Extending system with new types
- Domain-specific requirements

**Example**: `"port-range"`, `"color-picker"`, `"file-path"` are all valid custom types.

---

## Summary

**Previous Approach**: ❌
- CHECK constraint in database
- Invalid types converted to null
- Silent data changes

**New Approach**: ✅  
- No constraint - any value allowed
- Values preserved as-is (case-normalized)
- Explicit validation where needed
- Future-proof and extensible

**Migration**: Available in `20260304191221_RemoveDataTypeCheckConstraint`
**Status**: ✅ **Implemented and Ready**
