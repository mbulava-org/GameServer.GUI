# UI Updates - Container UID/GID Display & File Editor Fix

## Changes Made

### 1. ✅ Added Container UID/GID Display to Server Details Page

**Location**: `src\GameServer.Web\Components\Pages\Servers\ServerDetails.razor`

Added a new info row in the "Server Information" card that displays the container's User/Group ID:

```razor
@if (!string.IsNullOrWhiteSpace(containerUserGroup))
{
  <div class="info-row">
    <span class="label">Container User/Group:</span>
    <span class="value">
      <code>@containerUserGroup</code>
      <RadzenBadge BadgeStyle="BadgeStyle.Info" 
                   Text="File Ownership" 
                   Size="BadgeSize.Small" 
                   class="ms-2" 
                   title="Files created by this server will be owned by this UID:GID" />
    </span>
  </div>
}
```

**Features**:
- Shows the container's User spec (e.g., `1000:1000`)
- Displays "root (0:0)" if no user is specified
- Info badge explains this controls file ownership
- Only shows if successfully retrieved (gracefully hidden on errors)

**Implementation**:
- Added `containerUserGroup` field
- Added `LoadContainerUserGroupAsync()` method
- Calls new API endpoint `/api/servers/{id}/usergroup`

---

### 2. ✅ Added API Endpoint for Container User/Group

**Location**: `src\GameServer.Docker\Controllers\GameServerController.cs`

New endpoint:
```csharp
[HttpGet("{id}/usergroup")]
public async Task<IActionResult> GetUserGroup(string id, [FromServices] DockerServiceHelper serviceHelper)
```

**What it does**:
- Queries Docker Swarm service for the User specification
- Returns the User spec (e.g., `"1000:1000"`)
- Returns `"root (0:0)"` if no user is specified
- Handles errors gracefully

**Usage**: `GET /api/servers/{serverId}/usergroup`

**Response**: Plain text string (e.g., `"1000:1000"`)

---

### 3. ✅ Fixed File Editor Save Button

**Location**: `src\GameServer.Web\Components\Server\FileEditorDialog.razor`

**Problem**: Save button was always disabled because `hasChanges` was false after loading

**Solution**: Removed the `hasChanges` check from the button's `Disabled` attribute

**Before**:
```razor
<RadzenButton Text="Save" 
              Disabled="@(!hasChanges || saving)" />
```

**After**:
```razor
<RadzenButton Text="Save" 
              Disabled="@saving" />
```

**Result**: Save button is now enabled after file loads (as requested)

**Notes**:
- The "Modified" badge still tracks changes for user visibility
- Users can now save even without making changes (idempotent operation)
- Prevents confusion when button is unnecessarily disabled

---

## How to Verify

### UID/GID Display:
1. Navigate to Server Details page
2. Look for "Container User/Group" in the Server Information card
3. Should show either:
   - The actual UID:GID from the service (e.g., `1000:1000`)
   - `root (0:0)` if container runs as root
   - Hidden if retrieval fails

### File Editor Save Button:
1. Open file editor
2. Verify Save button is enabled after file loads
3. Click Save without changes - should work
4. Make changes - "Modified" badge should appear
5. Save changes - should work

---

## Benefits

### UID/GID Display:
✅ **Validation** - Admins can verify the impersonation is working correctly  
✅ **Debugging** - Easier to troubleshoot permission issues  
✅ **Transparency** - Users know what ownership files will have  
✅ **Documentation** - Self-documenting configuration  

### File Editor Fix:
✅ **Better UX** - No more confusing disabled button  
✅ **Flexibility** - Users can save even without changes  
✅ **Consistency** - Matches expected behavior  

---

## Related Files

- `src\GameServer.Web\Components\Pages\Servers\ServerDetails.razor` - UI display
- `src\GameServer.Docker\Controllers\GameServerController.cs` - API endpoint
- `src\GameServer.Web\Components\Server\FileEditorDialog.razor` - Save button fix
- `src\GameServer.Docker\Services\GameServerFileManagerService.cs` - UID/GID impersonation (already implemented)

---

## Future Enhancements

1. **Cache UID/GID** - Store in GameServer model to avoid API call
2. **Real-time updates** - Refresh when server restarts/updates
3. **Validation indicator** - Show if impersonation is active/working
4. **Permission check** - Warn if service doesn't have required capabilities
