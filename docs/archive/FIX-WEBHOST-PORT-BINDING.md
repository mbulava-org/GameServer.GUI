# Fix: Container Port Value Not Storing

## Issue

When configuring a WebHost with `PortSource = "ContainerPort"`, the Container Port numeric input field wasn't storing the value correctly, preventing users from saving.

## Root Cause

Two issues:
1. **Type mismatch**: NSwag client generated `int?` for `PortContainerPort` while the UI was trying to use `uint?`
2. **Binding issue**: Standard `@bind-Value` wasn't working correctly with nullable numeric types in Radzen
3. **Missing initialization**: When switching from "Setting" to "ContainerPort", no default value was set

## Solution

### 1. Fixed Type Mismatch
Changed `RadzenNumeric` to use `int?` instead of `uint?`:

```razor
<RadzenNumeric TValue="int?" 
               Value="@webHost.PortContainerPort"
               ValueChanged="@((int? value) => webHost.PortContainerPort = value)"
               Min="1" 
               Max="65535"
               ShowUpDown="false"
               class="w-100" />
```

**Key Changes**:
- ✅ Explicitly set `TValue="int?"` to match client type
- ✅ Used `Value`/`ValueChanged` pattern instead of `@bind-Value`
- ✅ Added `ShowUpDown="false"` for cleaner UI

### 2. Added Port Source Change Handler
Created `OnPortSourceChanged` method to handle switching between Setting and ContainerPort:

```csharp
private void OnPortSourceChanged(WebHost webHost, string newSource)
{
    webHost.PortSource = newSource;
    
    // Initialize default values when switching port source
    if (newSource == "ContainerPort")
    {
        // Set a default port if not already set
        if (!webHost.PortContainerPort.HasValue)
        {
            webHost.PortContainerPort = 8080;
        }
        webHost.PortSettingKey = null;
    }
    else
    {
        // Clear container port when switching to Setting
        webHost.PortContainerPort = null;
    }
    
    StateHasChanged();
}
```

**Features**:
- ✅ Sets default port (8080) when switching to ContainerPort
- ✅ Clears port when switching back to Setting
- ✅ Clears opposite field to avoid confusion

### 3. Updated Port Source Dropdown
Changed from `@bind-Value` to use `Value`/`ValueChanged` pattern:

```razor
<RadzenDropDown TValue="string"
                Value="@webHost.PortSource"
                ValueChanged="@((string value) => OnPortSourceChanged(webHost, value))"
                Data="@(new[] { "Setting", "ContainerPort" })"
                class="w-100" />
```

## Testing Steps

1. ✅ Navigate to `/gametypes/minecraft/metadata`
2. ✅ Find a port-type setting (e.g., "SERVER_PORT")
3. ✅ Click "Add Web Host"
4. ✅ Change "Port Source" to "ContainerPort"
5. ✅ Verify default value (8080) appears
6. ✅ Change the port value (e.g., to 25565)
7. ✅ Verify value is retained
8. ✅ Click "Save" at bottom
9. ✅ Reload page and verify value is saved

## Files Modified

**File**: `src/GameServer.Web/Components/Pages/GameTypes/ExtendedMetadataEditor.razor`

**Changes**:
- Line ~302: Changed Port Source dropdown to use `ValueChanged`
- Line ~322: Changed Container Port numeric to use explicit `int?` type binding
- Added `OnPortSourceChanged` method to handle source switching

## Why This Approach Works

### Type Consistency
- NSwag generates `int?` for nullable integer properties
- Using `int?` in UI matches client expectations
- Range 1-65535 fits within `int` (max 2,147,483,647)

### Explicit Binding
- `Value`/`ValueChanged` pattern gives more control
- Handles nullable types better than `@bind-Value`
- Allows custom logic on value changes

### User Experience
- Default value (8080) makes it clear the field is active
- Automatic clearing prevents confusion
- Immediate feedback via `StateHasChanged()`

## Related Issues

If you still see issues, check:
1. Browser cache - hard refresh (Ctrl+Shift+R)
2. Client regeneration - ensure NSwag client is up to date
3. Network tab - verify API request includes `portContainerPort` value

---

**Status**: ✅ **Fixed** - Container Port now stores correctly
**Version**: Fixed in current commit
**Component**: ExtendedMetadataEditor.razor
