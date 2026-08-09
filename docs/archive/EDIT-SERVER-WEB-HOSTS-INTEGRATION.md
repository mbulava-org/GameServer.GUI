# Server Editor Integration - Web Hosts Preview

## Overview

The **Server Editor** now includes a **real-time Web Hosts Preview** panel in the Settings tab. This provides immediate feedback to users as they configure environment variables, showing which web interfaces will be accessible based on their choices.

---

## Component: WebHostsPreview.razor

**Location**: `src\GameServer.Web\Components\Server\WebHostsPreview.razor`

**Integration**: Added to `EditServer.razor` → Settings tab (bottom section)

---

## Features

### 1. **Real-Time Status Evaluation**
- Evaluates conditions **as the user types** in the environment editor
- No need to save and view details - instant feedback!
- Shows live count of active hosts: "2 of 3 Active"

### 2. **Visual Status Indicators**
- ✅ **Green check** = Host will be accessible
- ❌ **Gray cancel** = Host is disabled (condition not met)
- **Status badges**: "Will be accessible" vs "Disabled"

### 3. **Port Resolution Preview**
- Shows actual port that will be used:
  - Fixed: `Port: 8123`
  - Dynamic (resolved): `Port: 9090 (from WEBUI_PORT)`
  - Dynamic (missing): ⚠️ `Port variable WEBUI_PORT not set`

### 4. **Condition Display**
- Shows the condition that enables/disables the host
- Indicates whether condition is currently met:
  - ✅ `Condition: DYNMAP_ENABLED=true` (green)
  - ❌ `Condition: DYNMAP_ENABLED=true → Not met` (red)

### 5. **Helpful Enable Hints**
For disabled hosts, shows actionable hint:
- 💡 **"To enable: Set DYNMAP_ENABLED to 'true'"**
- Parses the condition and explains what to do
- Handles both `=` and `!=` conditions

### 6. **Summary Alert**
If any hosts are enabled, shows reminder:
- ℹ️ "After saving, access these interfaces via the Web Access tab"

---

## User Experience

### Before (Without Preview)
```
1. User edits server settings
2. User guesses which variables to set
3. User saves
4. User goes to Web Access tab
5. User sees some interfaces disabled
6. User goes back to edit, confused about what to change
7. Trial and error...
```

### After (With Preview)
```
1. User edits server settings
2. User sees immediate feedback in preview panel
3. User sees: "Dynmap: Will be accessible ✅"
4. User sees: "BlueMap: Disabled ❌"
   → Hint: "To enable: Set BLUEMAP_ENABLED to 'true'"
5. User adds BLUEMAP_ENABLED=true
6. Preview updates instantly: "BlueMap: Will be accessible ✅"
7. User saves with confidence!
```

---

## UI Layout

### Settings Tab Structure
```
┌─────────────────────────────────────────────────────┐
│ [Basic Info] [Ports] [Volumes] [Settings]          │ ← Tabs
├─────────────────────────────────────────────────────┤
│                                                     │
│  ServerEnvironmentEditor                            │
│  (User edits environment variables here)            │
│                                                     │
│  ┌───────────────────────────────────────────────┐ │
│  │ 🌐 Web Access Preview    [2 of 3 Active]     │ │ ← New!
│  ├───────────────────────────────────────────────┤ │
│  │                                               │ │
│  │ ✅ Dynmap         [Will be accessible]       │ │
│  │    Real-time world map                       │ │
│  │    📡 Port: 8123                             │ │
│  │                                               │ │
│  │ ❌ BlueMap        [Disabled]                 │ │
│  │    3D renderer                               │ │
│  │    📡 Port: 8100                             │ │
│  │    ❌ Condition: BLUEMAP_ENABLED=true        │ │
│  │       → Not met                              │ │
│  │    💡 To enable: Set BLUEMAP_ENABLED         │ │
│  │       to 'true'                              │ │
│  │                                               │ │
│  │ ✅ Admin          [Will be accessible]       │ │
│  │    Web admin interface                       │ │
│  │    📡 Port: 9090 (from WEBUI_PORT)          │ │
│  │                                               │ │
│  └───────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────┘
```

---

## Example Scenarios

### Scenario 1: User Enables Dynmap

**Initial State** (Settings):
```json
{}
```

**Preview Shows**:
```
❌ Dynmap [Disabled]
   Condition: DYNMAP_ENABLED=true → Not met
   💡 To enable: Set DYNMAP_ENABLED to 'true'
```

**User Action**: Adds `DYNMAP_ENABLED = true` in environment editor

**Preview Updates Instantly**:
```
✅ Dynmap [Will be accessible]
   Port: 8123
   ✅ Condition: DYNMAP_ENABLED=true
```

---

### Scenario 2: User Sets Dynamic Port

**Initial State**:
```json
{
  "WEB_ENABLED": "true"
}
```

**Preview Shows**:
```
❌ Admin Panel [Disabled]
   ⚠️ Port variable WEBUI_PORT not set
   ✅ Condition: WEB_ENABLED=true
```

**User Action**: Adds `WEBUI_PORT = 9090`

**Preview Updates**:
```
✅ Admin Panel [Will be accessible]
   📡 Port: 9090 (from WEBUI_PORT)
   ✅ Condition: WEB_ENABLED=true
```

---

### Scenario 3: Multiple Hosts with Mixed Status

**Settings**:
```json
{
  "DYNMAP_ENABLED": "true",
  "WEBUI_PORT": "8080"
}
```

**Preview Shows**:
```
╔════════════════════════════════════╗
║ 🌐 Web Access Preview [2 of 3]   ║
╠════════════════════════════════════╣
║ ✅ Dynmap     [Will be accessible]║
║ ❌ BlueMap    [Disabled]          ║
║    💡 Hint: Set BLUEMAP_ENABLED   ║
║ ✅ Admin      [Will be accessible]║
╚════════════════════════════════════╝
```

---

## Technical Implementation

### Data Flow
```
1. User types in ServerEnvironmentEditor
   ↓
2. ServerEnvironmentEditor updates Server.Settings
   ↓
3. EditServer.OnServerChanged() called
   ↓
4. State updated, UI re-renders
   ↓
5. WebHostsPreview receives new ServerSettings
   ↓
6. Component evaluates each host:
   - Checks EnabledWhen condition
   - Resolves port (fixed or from variable)
   - Generates hint if disabled
   ↓
7. Preview displays updated status
```

### Condition Evaluation Logic

```csharp
// Example: "DYNMAP_ENABLED=true"
if (condition.Contains("="))
{
    var parts = condition.Split("=", 2);
    varName = parts[0].Trim();          // "DYNMAP_ENABLED"
    expectedValue = parts[1].Trim();    // "true"
    
    actualValue = ServerSettings[varName];  // Get from settings
    
    matches = actualValue.Equals(expectedValue, OrdinalIgnoreCase);
    return isNegated ? !matches : matches;
}
```

### Hint Generation Logic

```csharp
// "DYNMAP_ENABLED=true" → "Set DYNMAP_ENABLED to 'true'"
// "MODE!=disabled" → "Set MODE to any value except 'disabled'"
private string GetEnableHint(WebHostDefinition host)
{
    if (condition.Contains("!="))
        return $"Set {varName} to any value except '{expectedValue}'";
    else
        return $"Set {varName} to '{expectedValue}'";
}
```

---

## Benefits

### For Users
1. **Instant Feedback**: See results immediately, no save-and-check cycle
2. **Clear Guidance**: Know exactly what to change to enable features
3. **Confidence**: Save knowing which interfaces will work
4. **Discovery**: Learn about available features through the preview

### For Administrators
1. **Reduced Support**: Users don't need help figuring out settings
2. **Better Adoption**: Users discover and enable features
3. **Self-Service**: Clear hints reduce need for documentation lookups

---

## Styling Details

### Card Style
- Background: `var(--rz-base-50)` (subtle highlight)
- Border: Standard Radzen border
- Padding: Consistent with other cards

### Host Items
- Left border: 3px solid
  - Green for enabled
  - Gray for disabled
- Opacity: 0.7 for disabled hosts
- Smooth transitions on state changes

### Badge Colors
- Active count: Success (green) if any enabled, Secondary (gray) if none
- Status badges: Success for "Will be accessible", Secondary for "Disabled"
- Port badges: Info for fixed, Warning for missing

### Enable Hints
- Background: `var(--rz-info-lighter)` (light blue)
- Icon: Lightbulb (💡)
- Color: `var(--rz-info-dark)` (readable blue)
- Padding: 0.5rem
- Border radius: 4px

---

## Code Changes

### Files Modified
1. **`src/GameServer.Web/Components/Pages/Servers/EditServer.razor`**
   - Added `IGameTypeExtendedMetadataApi` injection
   - Added `extendedMetadata` field
   - Loads metadata in `LoadServerAsync()`
   - Added `WebHostsPreview` component in Settings tab

### Files Created
2. **`src/GameServer.Web/Components/Server/WebHostsPreview.razor`**
   - New component for real-time preview
   - ~250 lines including styles
   - Reactive to ServerSettings changes

---

## Testing Checklist

- [ ] Preview appears when GameType has web hosts
- [ ] Preview hidden when GameType has no web hosts
- [ ] Status updates when adding/removing settings
- [ ] Port resolution works for dynamic ports
- [ ] Conditions evaluate correctly (= and !=)
- [ ] Enable hints show correct variable names
- [ ] Count badge shows accurate number
- [ ] Smooth transitions on status changes
- [ ] Works with empty settings
- [ ] Works with multiple hosts
- [ ] Info alert appears when hosts are enabled

---

## Future Enhancements

1. **Live Port Validation**: Check if port is already in use
2. **URL Preview**: Show actual URL that will be generated
3. **Quick Actions**: "Click to enable" buttons for disabled hosts
4. **History**: Show what changed compared to saved state
5. **Warnings**: Alert if conflicting settings detected

---

## Summary

The **WebHostsPreview** component transforms the server configuration experience by providing **instant, actionable feedback**. Users no longer need to guess or use trial-and-error to configure web interfaces. They see exactly what will happen, get clear hints on how to fix issues, and can save with confidence.

**Key Achievement**: Reduced cognitive load and support burden while increasing feature discovery and adoption.

🎯 **Result**: A more intuitive, self-explanatory server configuration experience!
