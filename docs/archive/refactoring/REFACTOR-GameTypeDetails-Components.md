# Refactoring: Split GameTypeDetails.razor into Components

## Overview
**Current State:** GameTypeDetails.razor is 1900+ lines  
**Target State:** Split into 4-5 focused components (~300-500 lines each)  
**Priority:** Medium (Technical Debt)  
**Estimated Effort:** 3-4 hours

---

## Problem Statement

The `GameTypeDetails.razor` page has grown too large to maintain effectively:
- 1900+ lines in a single file
- Multiple responsibilities (CRUD, validation, UI rendering for 4 tabs)
- Difficult to test individual features
- Hard to navigate and understand
- Changes in one tab can affect others

---

## Proposed Component Structure

```
src/GameServer.Web/Components/Pages/GameTypes/
├── GameTypeDetails.razor                    (Main coordinator, ~200 lines)
└── Components/
    ├── GameTypeBasicInfo.razor              (~250 lines)
    ├── GameTypePortsEditor.razor            (~300 lines)
    ├── GameTypeVolumesEditor.razor          (~200 lines)
    └── GameTypeSettingsEditor.razor         (~800 lines)
        └── Components/
            ├── SettingsList.razor           (~200 lines)
            ├── SettingDetailEditor.razor    (~400 lines)
            └── SettingMetadataEditor.razor  (~200 lines)
```

---

## Component Responsibilities

### 1. **GameTypeDetails.razor** (Main Coordinator)
**Lines:** ~200  
**Responsibilities:**
- Load GameTypeDefinition and ExtendedMetadata from APIs
- Coordinate saving data across all tabs
- Handle navigation and routing
- Display tab navigation UI
- Manage global state (isLoading, isSaving, isNew)
- Show notifications

**Parameters:** 
- `Key` (string) - Game type key from route

**Does NOT:**
- Render individual tab content
- Handle tab-specific validation
- Manage tab-specific state

---

### 2. **GameTypeBasicInfo.razor**
**Lines:** ~250  
**Responsibilities:**
- Display and edit basic game type info (Key, DisplayName, Description, Image)
- Handle image URL validation
- Display thumbnail preview
- Manage documentation URL

**Parameters:**
```csharp
[Parameter] public GameTypeDefinition GameType { get; set; }
[Parameter] public bool IsNew { get; set; }
[Parameter] public EventCallback<GameTypeDefinition> GameTypeChanged { get; set; }
```

---

### 3. **GameTypePortsEditor.razor**
**Lines:** ~300  
**Responsibilities:**
- Display list of ports
- Add/remove port definitions
- Edit port number, protocol, default flag
- Validate port uniqueness
- Show port conflicts

**Parameters:**
```csharp
[Parameter] public List<PortDefinition> Ports { get; set; }
[Parameter] public EventCallback<List<PortDefinition>> PortsChanged { get; set; }
```

---

### 4. **GameTypeVolumesEditor.razor**
**Lines:** ~200  
**Responsibilities:**
- Display list of volumes
- Add/remove volume definitions
- Edit source/target paths
- Validate path formats

**Parameters:**
```csharp
[Parameter] public List<VolumeDefinition> Volumes { get; set; }
[Parameter] public EventCallback<List<VolumeDefinition>> VolumesChanged { get; set; }
```

---

### 5. **GameTypeSettingsEditor.razor** (Most Complex)
**Lines:** ~800 (may need further sub-components)  
**Responsibilities:**
- Display settings list with search/filter
- Manage selected setting
- Add/remove settings
- Coordinate between list and detail views
- Manage settingsMetadata dictionary
- Handle TTY toggle

**Parameters:**
```csharp
[Parameter] public Dictionary<string, string> DefaultSettings { get; set; }
[Parameter] public EventCallback<Dictionary<string, string>> DefaultSettingsChanged { get; set; }
[Parameter] public Dictionary<string, SettingMetadata> SettingsMetadata { get; set; }
[Parameter] public EventCallback<Dictionary<string, SettingMetadata>> SettingsMetadataChanged { get; set; }
[Parameter] public bool EnableTTY { get; set; }
[Parameter] public EventCallback<bool> EnableTTYChanged { get; set; }
```

**Potential Sub-Components:**
- `SettingsList.razor` - Left panel list view
- `SettingDetailEditor.razor` - Right panel detail view
- `SettingMetadataEditor.razor` - Extended metadata fields

---

## Migration Plan

### Phase 1: Preparation (30 minutes)
1. **Create branch:** `refactor/split-gametype-details`
2. **Create component folder structure:**
   ```bash
   mkdir -p src/GameServer.Web/Components/Pages/GameTypes/Components
   ```
3. **Copy current GameTypeDetails.razor** as backup:
   ```bash
   cp GameTypeDetails.razor GameTypeDetails.razor.backup
   ```
4. **Run all tests** to establish baseline

### Phase 2: Extract Basic Info (45 minutes)
1. Create `GameTypeBasicInfo.razor`
2. Move basic info HTML section
3. Move basic info validation logic
4. Add Parameters and EventCallbacks
5. Replace section in GameTypeDetails with component
6. Test: Create/edit game type basic info
7. Commit: `refactor: Extract GameTypeBasicInfo component`

### Phase 3: Extract Ports Editor (45 minutes)
1. Create `GameTypePortsEditor.razor`
2. Move ports tab HTML
3. Move AddPort, RemovePort methods
4. Add Parameters and EventCallbacks
5. Replace ports tab with component
6. Test: Add/edit/remove ports
7. Commit: `refactor: Extract GameTypePortsEditor component`

### Phase 4: Extract Volumes Editor (30 minutes)
1. Create `GameTypeVolumesEditor.razor`
2. Move volumes tab HTML
3. Move AddVolume, RemoveVolume methods
4. Add Parameters and EventCallbacks
5. Replace volumes tab with component
6. Test: Add/edit/remove volumes
7. Commit: `refactor: Extract GameTypeVolumesEditor component`

### Phase 5: Extract Settings Editor (90 minutes)
1. Create `GameTypeSettingsEditor.razor`
2. Move settings tab HTML
3. Move settings-related methods
4. Decide if further sub-components needed
5. Add Parameters and EventCallbacks
6. Replace settings tab with component
7. Test: Full settings workflow
8. Commit: `refactor: Extract GameTypeSettingsEditor component`

### Phase 6: Cleanup & Finalize (30 minutes)
1. Remove backup file
2. Update documentation
3. Run full test suite
4. Performance check
5. Code review
6. Merge to main

---

## Code Example: GameTypeBasicInfo.razor

```razor
@using GameServer.Docker.Models

<div class="basic-info-section">
    <RadzenStack Gap="1rem">
        <RadzenFormField Text="Game Type Key" Variant="@Variant.Outlined">
            <RadzenTextBox @bind-Value="@localGameType.Key" 
                           Disabled="@(!IsNew)"
                           Placeholder="minecraft" 
                           class="w-100" />
        </RadzenFormField>
        <small class="text-muted">Unique identifier (lowercase, no spaces)</small>

        <RadzenFormField Text="Display Name" Variant="@Variant.Outlined">
            <RadzenTextBox @bind-Value="@localGameType.DisplayName" 
                           Placeholder="Minecraft" 
                           class="w-100" />
        </RadzenFormField>

        <RadzenFormField Text="Description" Variant="@Variant.Outlined">
            <RadzenTextArea @bind-Value="@localGameType.Description" 
                            Rows="3"
                            Placeholder="Server description..." 
                            class="w-100" />
        </RadzenFormField>

        <RadzenFormField Text="Docker Image" Variant="@Variant.Outlined">
            <RadzenTextBox @bind-Value="@localGameType.Image" 
                           Placeholder="itzg/minecraft-server:latest" 
                           class="w-100" />
        </RadzenFormField>

        @if (!string.IsNullOrEmpty(localGameType.ThumbnailUrl))
        {
            <div class="thumbnail-preview">
                <img src="@localGameType.ThumbnailUrl" alt="Thumbnail" />
            </div>
        }

        <RadzenFormField Text="Thumbnail URL" Variant="@Variant.Outlined">
            <RadzenTextBox @bind-Value="@localGameType.ThumbnailUrl" 
                           Placeholder="https://..." 
                           class="w-100" />
        </RadzenFormField>

        <RadzenFormField Text="Documentation URL" Variant="@Variant.Outlined">
            <RadzenTextBox @bind-Value="@localGameType.DocumentationUrl" 
                           Placeholder="https://..." 
                           class="w-100" />
        </RadzenFormField>
    </RadzenStack>
</div>

@code {
    [Parameter] public GameTypeDefinition GameType { get; set; } = null!;
    [Parameter] public bool IsNew { get; set; }
    [Parameter] public EventCallback<GameTypeDefinition> GameTypeChanged { get; set; }

    private GameTypeDefinition localGameType = new();

    protected override void OnParametersSet()
    {
        localGameType = GameType;
    }

    private async Task NotifyChanged()
    {
        await GameTypeChanged.InvokeAsync(localGameType);
    }
}
```

---

## Testing Strategy

### Unit Tests (New)
- Each component should have isolated tests
- Mock Parameters and EventCallbacks
- Test validation logic independently

### Integration Tests
- Test parent-child component communication
- Test data flow: Change in child → Event → Parent update
- Test save workflow across all components

### Manual Testing Checklist
- [ ] Create new game type (all tabs)
- [ ] Edit existing game type (all tabs)
- [ ] Add/remove ports
- [ ] Add/remove volumes
- [ ] Add/remove settings
- [ ] Edit setting metadata
- [ ] Save and verify in database
- [ ] Navigation between tabs preserves changes
- [ ] Validation errors display correctly

---

## Rollback Plan

If issues arise:
1. **Immediate:** Revert merge commit
2. **Investigation:** Review failed tests/scenarios
3. **Fix Forward:** Address issues in refactor branch
4. **Re-deploy:** After validation

---

## Success Criteria

✅ All existing functionality works identically  
✅ Each component file < 500 lines  
✅ All tests pass  
✅ No performance degradation  
✅ Code is easier to understand  
✅ Future changes are easier to make  

---

## Related Documentation

- `docs/ARCHITECTURE.md` - Component architecture patterns
- `docs/guides/GameType-Editor-Complete-Functionality-Guide.md` - Feature reference
- `.github/copilot-instructions.md` - UI component guidelines

---

## Future Improvements (Post-Refactor)

1. **Add component tests** for each new component
2. **Extract common validation** into shared service
3. **Consider form state management** library (if complexity grows)
4. **Add loading skeletons** for better UX
5. **Optimize re-rendering** with ShouldRender logic

---

## Questions/Decisions

- [ ] Should SettingsEditor be split further?
- [ ] Do we need a shared service for validation?
- [ ] Should we use Blazor EditForm for validation?
- [ ] Keep styles in components or separate CSS files?

---

**Created:** {Date}  
**Status:** Planned  
**Assigned:** TBD  
**Target Version:** TBD
