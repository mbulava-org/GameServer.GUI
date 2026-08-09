# Port Configuration & Validation Fix

## Issue Summary
Fixed multiple issues with port configuration in the Create Server Wizard and Edit Server pages:
1. Tab context loss when typing in Environment Variables
2. Port validation not running for port relationships
3. Wizard Ports step Next button not enabling
4. Incorrect port editing permissions

## Requirements (Clarified)
- **Default Port**: ONLY the default port is editable in PortMappingEditor
- **Related Ports**: Automatically calculate based on port relationships (offset/fixed/multiplier)
- **Validation**: ALL ports (user-edited + auto-calculated) must validate for availability
- **Two Ways to Change Default Port**:
  1. Directly in Ports step (edit published port)
  2. Via Environment Variables step (change setting that maps to port)

## Implementation

### 1. Fixed Tab Context Loss (`ServerEnvironmentEditor.razor`)
**Problem**: `RenderMode="TabRenderMode.Client"` causing page reload on state changes  
**Fix**: Removed RenderMode attribute from RadzenTabs

```razor
<!-- BEFORE -->
<RadzenTabs @bind-SelectedIndex="selectedTabIndex" RenderMode="TabRenderMode.Client">

<!-- AFTER -->
<RadzenTabs @bind-SelectedIndex="selectedTabIndex">
```

### 2. Added Port Change Notification Chain
**Flow**: Environment Variables → Settings Step → Wizard → Technical Details Step → Revalidate

#### ServerEnvironmentEditor.razor
- Added `[Parameter] public EventCallback OnPortsChanged { get; set; }`
- Triggers callback after updating ports via relationships:
```csharp
if (OnPortsChanged.HasDelegate)
{
    await OnPortsChanged.InvokeAsync();
}
```

#### StepGameSettings.razor
- Added `[Parameter] public EventCallback OnPortsChanged { get; set; }`
- Wires callback to ServerEnvironmentEditor:
```razor
<ServerEnvironmentEditor ... OnPortsChanged="@OnPortsChanged" />
```

#### CreateServerWizard.razor
- Added step references and handler:
```csharp
private StepTechnicalDetails? stepTechnicalDetails;

private async Task OnPortsChangedFromSettings()
{
    if (stepTechnicalDetails != null)
    {
        await stepTechnicalDetails.RevalidatePortsAsync();
    }
}
```

#### StepTechnicalDetails.razor
- Added public method to trigger revalidation:
```csharp
public async Task RevalidatePortsAsync()
{
    if (portMappingEditor != null)
    {
        await portMappingEditor.RevalidateAsync();
    }
}
```

#### PortMappingEditor.razor
- Added public revalidate method:
```csharp
public async Task RevalidateAsync()
{
    await ValidateAllPublishedAsync();
    StateHasChanged();
}
```

### 3. Port Relationship Handling in PortMappingEditor

Added support for updating related ports when default port changes directly:

```csharp
[Inject] public required IGameTypeExtendedMetadataApi ExtendedMetadataApi { get; set; }
private GameTypeExtendedMetadata? extendedMetadata;

// Load metadata on initialization
private async Task LoadExtendedMetadataAsync()
{
    if (GameTypeDefinition == null) return;
    try
    {
        extendedMetadata = await ExtendedMetadataApi.GetAsync(GameTypeDefinition.Key);
    }
    catch { extendedMetadata = null; }
}

// Update related ports when default port changes
void OnPublishedChanged(PortMapping pm, uint value)
{
    // ... update port value ...
    
    // If this is the default port, update related ports
    if (IsDefaultPort(pm))
    {
        _ = UpdateRelatedPortsAsync(pm);
    }
    
    _ = ValidateAllPublishedAsync();
    _ = NotifyChangedAsync();
}

private async Task UpdateRelatedPortsAsync(PortMapping defaultPort)
{
    // Find setting that maps to this port
    // Apply port relationships (offset/fixed/multiplier)
    // Update related port values
}
```

### 4. Port Editing Permissions

**ONLY Default Port Editable:**
```razor
@foreach (var pm in Server.Ports)
{
    var isDefault = IsDefaultPort(pm);
    var isReadOnly = !isDefault; // Only default port is editable
    
    <RadzenNumeric ... Disabled="@isReadOnly" />
    
    <small class="text-muted">
        Container Port: @pm.ContainerPort
        @if (isReadOnly)
        {
            <span> • Auto-calculated from port relationships</span>
        }
        else if (isDefault)
        {
            <span> • Primary connection port (editable)</span>
        }
    </small>
}
```

**Alert Message:**
```razor
<RadzenAlert AlertStyle="AlertStyle.Info">
    <strong>Port Configuration:</strong> The default port is editable and can be changed here or via Environment Variables. 
    Related ports are automatically calculated based on port relationships defined in the game type.
    All ports (including auto-calculated ones) are validated for availability.
</RadzenAlert>
```

### 5. Validation Coverage

All ports (default + related) validate for:
1. **Range**: 1-65535
2. **Duplicates**: Same port + protocol across mappings
3. **Availability**: Check via `PortApi.CheckAsync(protocol, port)`

```csharp
async Task ValidateAllPublishedAsync()
{
    publishedErrors.Clear();
    
    var effective = Server.Ports.Select(p => new { 
        Instance = p, 
        Port = (p.PublishedPort == 0 ? p.ContainerPort : p.PublishedPort),
        Protocol = p.Protocol
    }).ToList();
    
    foreach (var e in effective)
    {
        // Range check
        if (e.Port < 1 || e.Port > 65535) { ... }
        
        // Duplicate check
        var duplicates = effective.Count(x => x.Port == e.Port && x.Protocol == e.Protocol);
        if (duplicates > 1) { ... }
        
        // Availability check
        var isFree = await PortApi.CheckAsync(e.Protocol, e.Port);
        if (!isFree) { ... }
    }
    
    await OnValidityChanged.InvokeAsync(publishedErrors.Count == 0);
}
```

## Files Changed
1. ✅ `src/GameServer.Web/Components/Server/ServerEnvironmentEditor.razor`
2. ✅ `src/GameServer.Web/Components/Server/PortMappingEditor.razor`
3. ✅ `src/GameServer.Web/Components/Server/Wizards/Steps/StepGameSettings.razor`
4. ✅ `src/GameServer.Web/Components/Server/Wizards/Steps/StepTechnicalDetails.razor`
5. ✅ `src/GameServer.Web/Components/Server/Wizards/CreateServerWizard.razor`

## Testing Checklist
- [x] Build successful
- [ ] Environment Variables: Type in controls, verify no page reload/tab loss
- [ ] Environment Variables: Change port setting with relationships, verify related ports update
- [ ] Ports Step: Verify only default port is editable
- [ ] Ports Step: Edit default port, verify related ports auto-calculate
- [ ] Ports Step: Verify validation runs on all ports (including auto-calculated)
- [ ] Ports Step: Verify Next button enables when all ports valid
- [ ] Ports Step: Try invalid ports (duplicate, out of range, in use), verify errors show
- [ ] EditServer: Same port behavior as wizard

## Architecture
```
User Changes Port (Two Paths):

Path 1: Environment Variables
┌─────────────────────────────┐
│ ServerEnvironmentEditor     │
│  - User changes port        │
│  - Updates Server.Ports     │
│  - Applies relationships    │
│  - Calls OnPortsChanged ───┐
└─────────────────────────────┘│
                               │
Path 2: Direct Port Edit      │
┌─────────────────────────────┐│
│ PortMappingEditor           ││
│  - User edits default port  ││
│  - Updates Server.Ports     ││
│  - Applies relationships    ││
│  - Calls ValidateAsync      ││
└─────────────────────────────┘│
                               │
       ┌───────────────────────┘
       │
       ▼
┌─────────────────────────────┐
│ StepTechnicalDetails        │
│  - RevalidatePortsAsync()   │
│  - Triggers port validation │
└─────────────────────────────┘
       │
       ▼
┌─────────────────────────────┐
│ PortMappingEditor           │
│  - ValidateAllPublishedAsync│
│  - Check range/dupe/avail   │
│  - Update OnValidityChanged │
│  - Enable/disable Next      │
└─────────────────────────────┘
```

## Key Behaviors
1. **Default Port Only**: Only default port editable in Ports step
2. **Auto-Calculate**: Related ports update automatically via relationships
3. **Dual Entry**: Change default port via Environment Variables OR Ports step
4. **Full Validation**: ALL ports validated regardless of how they were set
5. **Real-time Feedback**: Validation errors show immediately
6. **Next Button**: Enables only when ALL ports pass validation
