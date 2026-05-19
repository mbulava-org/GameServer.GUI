# Extended Metadata Editor - Navigation Integration

## Summary

Integrated the Extended Metadata Editor into the Game Types Manager with proper navigation and routing.

## Changes Made

### 1. Added "Edit Metadata" Button to Game Types List
**File**: `src/GameServer.Web/Components/Pages/GameTypes/GameTypeManager.razor`

**Changes**:
- ✅ Added new button with "extension" icon in the Actions column
- ✅ Button styled with `ButtonStyle.Info` (blue)
- ✅ Increased Actions column width from 140px to 180px
- ✅ Added `EditExtendedMetadata(string key)` method

**Button Order** (left to right):
1. Edit (pencil) - Edit game type
2. **Edit Metadata (extension)** - **NEW!**
3. Duplicate (copy) - Duplicate game type
4. Delete (trash) - Delete game type

### 2. Created Extended Metadata Editor Page
**File**: `src/GameServer.Web/Components/Pages/GameTypes/ExtendedMetadataEditorPage.razor`

**Features**:
- ✅ Route: `/gametypes/{GameTypeKey}/metadata`
- ✅ Back button to return to game types list
- ✅ Displays game type name and key
- ✅ Loading state
- ✅ Error handling for missing game types
- ✅ Embeds `<ExtendedMetadataEditor>` component

## Navigation Flow

```
Game Types List
  (/gametypes)
       ↓
  [Edit Metadata Button]
       ↓
Extended Metadata Editor Page
  (/gametypes/minecraft/metadata)
       ↓
  [Back Button]
       ↓
  Back to Game Types List
```

## How to Use

### From Game Types Manager

1. Navigate to **Game Types** (`/gametypes`)
2. Find the game type you want to edit
3. Click the **blue "extension" icon** button
4. You'll be taken to `/gametypes/{key}/metadata`

### Direct Navigation

You can also navigate directly via URL:
```
/gametypes/minecraft/metadata
/gametypes/valheim/metadata
/gametypes/palworld/metadata
```

## UI Screenshots (Conceptual)

### Game Types Manager - Actions Column
```
┌─────────────────────────────────────────┐
│ Actions                                 │
├─────────────────────────────────────────┤
│ [Edit] [Metadata] [Copy] [Delete]      │
│   📝      🧩        📋      🗑️         │
└─────────────────────────────────────────┘
```

### Extended Metadata Editor Page
```
┌────────────────────────────────────────┐
│ [← Back] Extended Metadata             │
│ 🎮 Minecraft (minecraft)               │
├────────────────────────────────────────┤
│                                        │
│  [Extended Metadata Editor Component]  │
│                                        │
│  • General Settings                    │
│  • Settings Metadata                   │
│  • Custom Properties                   │
│                                        │
│  [Save] [Cancel]                       │
│                                        │
└────────────────────────────────────────┘
```

## Code Examples

### Navigation Method
```csharp
private void EditExtendedMetadata(string key)
{
    // URL encode the key to handle special characters
    var encodedKey = Uri.EscapeDataString(key);
    Navigation.NavigateTo($"/gametypes/{encodedKey}/metadata");
}
```

### Button in Data Grid
```razor
<RadzenButton Icon="extension" 
              Size="ButtonSize.Small" 
              ButtonStyle="ButtonStyle.Info" 
              Click="@(() => EditExtendedMetadata(gameType.Key))"
              title="Edit Extended Metadata"
              Variant="Variant.Flat" />
```

## Testing Steps

1. ✅ Start the application
2. ✅ Navigate to `/gametypes`
3. ✅ Verify the blue "extension" button appears for each game type
4. ✅ Click the button
5. ✅ Verify navigation to `/gametypes/{key}/metadata`
6. ✅ Verify game type name displays at top
7. ✅ Verify back button works
8. ✅ Test with different game types

## Files Modified

1. **`src/GameServer.Web/Components/Pages/GameTypes/GameTypeManager.razor`**
   - Added "Edit Metadata" button
   - Added `EditExtendedMetadata()` method
   - Increased Actions column width

2. **`src/GameServer.Web/Components/Pages/GameTypes/ExtendedMetadataEditorPage.razor`** (NEW)
   - Created wrapper page with route
   - Implements proper page structure
   - Handles loading and error states

## Related Documentation

- `docs/WEB-HOST-UI-IMPLEMENTATION.md` - Web Host configuration UI
- Extended Metadata Editor component documentation

---

**Status**: ✅ **Complete and integrated**
**Route**: `/gametypes/{GameTypeKey}/metadata`
**Parent Page**: `/gametypes` (Game Types Manager)
