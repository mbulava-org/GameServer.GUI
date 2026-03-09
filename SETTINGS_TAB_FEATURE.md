# Settings Tab Added to Server Details

## Summary
Added a new **Settings** tab to the Server Details page that allows viewing and editing the GameServer's Settings dictionary (key-value pairs used for server configuration).

## Changes Made

### 1. New Tab in UI
**Location**: `src\GameServer.Web\Components\Pages\Servers\ServerDetails.razor`

Added a new tab after the "Overview" tab that displays and manages server settings:

```razor
<RadzenTabsItem Text="Settings">
  <!-- Settings management UI -->
</RadzenTabsItem>
```

### 2. Features

#### **View Mode** (Default)
- Displays all settings as read-only key-value pairs
- Clean grid layout with keys and values clearly separated
- Empty state message when no settings exist
- Info alert explaining what settings are

#### **Edit Mode** (When modifying)
- Click "Add Setting" to enter edit mode
- Add new settings with key-value pairs
- Edit existing settings inline
- Delete individual settings
- Multi-line text support for values (useful for lists)
- Cancel changes to revert
- Save changes to persist

#### **Validation**
- Prevents duplicate keys
- Removes empty keys before saving
- Shows clear error messages for validation issues

### 3. Code Structure

#### **Fields Added**:
```csharp
private List<SettingItem> editableSettings = new();
private bool settingsModified = false;
private bool savingSettings = false;

private class SettingItem
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}
```

#### **Methods Added**:
- `InitializeEditableSettings()` - Loads settings from server into editable list
- `AddSetting()` - Adds a new empty setting
- `RemoveSetting(item)` - Removes a setting from the list
- `CancelSettingsChanges()` - Reverts to original settings
- `SaveSettings()` - Validates and saves settings via API

### 4. Styling

Added CSS for the settings grid:
- Responsive grid layout (3 columns: Key | Value | Actions)
- Mobile-friendly (stacks vertically on small screens)
- Monospace font for keys and values (better for technical data)
- Clean visual separation between settings

```css
.settings-grid {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.setting-row {
  display: grid;
  grid-template-columns: 1fr 2fr auto;
  gap: 1rem;
  padding: 1rem;
  background: var(--rz-base-50);
  border-radius: 8px;
}
```

### 5. User Experience

#### **Adding a Setting**:
1. Click "Add Setting" button
2. UI enters edit mode
3. New empty setting row appears
4. Fill in key and value
5. Click "Save Changes"

#### **Editing Settings**:
1. Click "Add Setting" to enter edit mode
2. Modify any key or value fields
3. Add or remove settings as needed
4. Click "Save Changes" or "Cancel"

#### **Notifications**:
- ✅ Success: "Server settings updated successfully. Restart the server for changes to take effect."
- ❌ Error: Shows specific error message (e.g., "Duplicate keys found")
- ℹ️ Info: Explains that settings require server restart

## Use Cases

### Common Settings Examples:

**Minecraft**:
```
SEED: 12345678
DIFFICULTY: hard
MAX_PLAYERS: 20
WHITELIST: player1\nplayer2\nplayer3
```

**Valheim**:
```
WORLD_NAME: MyWorld
PASSWORD: secretpass
PUBLIC: true
SERVER_NAME: My Valheim Server
```

**7 Days to Die**:
```
ServerName: My 7DTD Server
ServerPassword: pass123
MaxPlayers: 8
WorldName: Navezgane
```

## Technical Notes

### API Integration
- Uses `ServerApi.DeployAsync(server)` to save settings
- This endpoint handles both create and update operations
- Settings are persisted to the database via GameServer model

### Data Format
- Settings are stored as `Dictionary<string, string>`
- Multi-line values supported (e.g., player lists with `\n` separators)
- Empty keys are automatically removed during save
- Duplicate keys are prevented via validation

### Server Restart Required
After changing settings, the server must be restarted for changes to take effect because:
- Settings are read during container startup
- Container environment variables are set at launch
- Game servers typically don't support hot-reloading configuration

## Testing Checklist

- [ ] Navigate to Server Details → Settings tab
- [ ] Verify existing settings display correctly
- [ ] Click "Add Setting" and add a new setting
- [ ] Edit an existing setting value
- [ ] Try to create duplicate keys (should show error)
- [ ] Delete a setting
- [ ] Cancel changes (should revert)
- [ ] Save changes (should persist)
- [ ] Verify empty state shows when no settings exist
- [ ] Test mobile responsive layout
- [ ] Verify multi-line values work (e.g., player lists)

## Future Enhancements

1. **Setting Templates** - Predefined settings based on game type
2. **Validation Rules** - Game-specific validation (e.g., port ranges)
3. **Setting Descriptions** - Tooltips explaining what each setting does
4. **Import/Export** - Bulk import settings from file
5. **Setting History** - Track changes over time
6. **Auto-restart** - Option to automatically restart server after save
7. **Setting Groups** - Organize settings by category (Network, Gameplay, etc.)

## Related Files

- `src\GameServer.Web\Components\Pages\Servers\ServerDetails.razor` - Main implementation
- `src\GameServer.Docker\Models\GameServer.cs` - Settings property definition
- `src\GameServer.Docker\Controllers\GameServerController.cs` - API endpoint (Deploy)

## Screenshots (Visual Description)

### View Mode:
```
┌─────────────────────────────────────────────────┐
│ Settings                                  [Add] │
├─────────────────────────────────────────────────┤
│ SEED          │ 12345678                       │
│ DIFFICULTY    │ hard                           │
│ MAX_PLAYERS   │ 20                             │
└─────────────────────────────────────────────────┘
```

### Edit Mode:
```
┌─────────────────────────────────────────────────┐
│ Settings              [Add] [Cancel] [Save]     │
├─────────────────────────────────────────────────┤
│ [SEED     ]   │ [12345678              ] [🗑]  │
│ [DIFFICULTY]  │ [hard                  ] [🗑]  │
│ [MAX_PLAYERS] │ [20                    ] [🗑]  │
└─────────────────────────────────────────────────┘
```
