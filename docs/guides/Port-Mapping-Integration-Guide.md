# Advanced Port Mapping - Integration Guide

## ?? Architecture Decision Required

The advanced port mapping system has been designed and documented, but requires an **architecture decision** about where to place the shared models.

### Current Situation

- **GameServer.Docker** - API backend project
- **GameServer.Docker.Client** - NuGet package (auto-generated from API)
- **GameServer.Web** - Blazor frontend project

### The Issue

The `PortMappingService` needs to:
1. Reference `IPortApi` (from Docker.Client)
2. Use `PortDefinition`, `GameTypeDefinition` (from Docker.Client)
3. Define new models: `PortRelationship`, `PortValidationRule`

**Problem:** Where should the new models live?

---

## Solution Options

### Option 1: Add to Docker.Client Package ? RECOMMENDED

**Approach:** Extend the SettingMetadata model in the Docker project/API

**Pros:**
- ? Models are part of the API contract
- ? Shared between frontend and backend
- ? Type-safe across all projects
- ? Auto-generated client includes new properties

**Cons:**
- ? Requires API project update
- ? Requires client package regeneration

**Implementation:**
1. Add `PortRelationship.cs` to `src/GameServer.Docker/Models/`
2. Extend `SettingMetadata.cs` in Docker project
3. Regenerate Docker.Client package
4. Add `PortMappingService.cs` to `src/GameServer.Web/Services/`

### Option 2: Web Project Only ?? LIMITED

**Approach:** Keep everything in the Web project

**Pros:**
- ? Quick to implement
- ? No API changes needed

**Cons:**
- ? Models not shared with backend
- ? Validation only happens on frontend
- ? No server-side enforcement

**Implementation:**
1. Add models to `src/GameServer.Web/Models/`
2. Add service to `src/GameServer.Web/Services/`
3. Frontend-only validation

### Option 3: Shared Library ?? FUTURE-PROOF

**Approach:** Create `GameServer.Shared` project

**Pros:**
- ? Clean separation
- ? Reusable across projects
- ? Future-proof architecture

**Cons:**
- ? More complex setup
- ? Additional project to maintain

---

## Recommended Implementation (Option 1)

### Step 1: Update Docker Project

Add to `src/GameServer.Docker/Models/`:

```csharp
// PortRelationship.cs
public class PortRelationship { ... }
public enum PortRelationshipType { ... }
public class PortValidationRule { ... }
```

### Step 2: Extend SettingMetadata

Update `src/GameServer.Docker/Models/SettingMetadata.cs`:

```csharp
public class SettingMetadata
{
    // ... existing properties ...
    
    public List<PortRelationship>? PortRelationships { get; set; }
    public PortValidationRule? PortValidation { get; set; }
    public string? SynchronizedWithSetting { get; set; }
    public bool AutoAllocatePort { get; set; } = false;
    public bool ValidateRelatedPortsAvailability { get; set; } = true;
}
```

### Step 3: Regenerate Client

```bash
cd src/GameServer.Docker.Client
dotnet build
# This will regenerate the client with new properties
```

### Step 4: Add Service to Web Project

Create `src/GameServer.Web/Services/PortMappingService.cs`:

```csharp
using GameServer.Docker.Client;

namespace GameServer.Web.Services
{
    public class PortMappingService
    {
        private readonly IPortApi _portApi;
        // ... implementation from the provided code ...
    }
}
```

### Step 5: Register Service

In `src/GameServer.Web/Program.cs`:

```csharp
// Add port mapping service
builder.Services.AddScoped<PortMappingService>();
```

### Step 6: Use in Components

```csharp
@inject PortMappingService PortMapping

// In your component
var validation = await PortMapping.ValidatePortSettingAsync(...);
var result = PortMapping.ApplyPortChanges(...);
```

---

## Files Created (Ready to Use)

### ? Complete Documentation

1. **`docs/Advanced-Port-Mapping-System.md`** (~800 lines)
   - Complete system overview
   - Usage examples
   - API reference
   - Troubleshooting

2. **`docs/Game-Server-Port-Examples.json`** (~500 lines)
   - Real-world configurations
   - 8+ game server examples
   - Ready-to-use JSON

3. **`docs/Port-Mapping-Implementation-Summary.md`** (~200 lines)
   - Implementation overview
   - Integration steps
   - Testing scenarios

### ?? Code Files (Require Placement Decision)

4. **`src/GameServer.Docker/Models/PortRelationship.cs`** (~100 lines)
   - PortRelationship class
   - PortRelationshipType enum
   - PortValidationRule class

5. **`src/GameServer.Docker/Models/SettingMetadata.cs`** (Extended)
   - Added port-specific properties

6. **`src/GameServer.Web/Services/PortMappingService.cs`** (~300 lines)
   - Validation logic
   - Port update logic
   - Availability checking

---

## Current Status

### ? Complete
- [x] System design
- [x] Models defined
- [x] Service implemented
- [x] Complete documentation
- [x] Real-world examples
- [x] Testing scenarios

### ?? Pending
- [ ] Architecture decision (where to place models)
- [ ] Models added to correct project
- [ ] SettingMetadata extended in API
- [ ] Client package regenerated
- [ ] Service registered in DI
- [ ] UI integration

---

## Quick Start (Once Architecture Decided)

### If Choosing Option 1 (Recommended):

1. **Add models to Docker project**
   ```bash
   # Copy PortRelationship.cs to src/GameServer.Docker/Models/
   ```

2. **Extend SettingMetadata**
   ```csharp
   // Add new properties to existing SettingMetadata.cs
   ```

3. **Regenerate client**
   ```bash
   cd src/GameServer.Docker.Client
   dotnet build
   ```

4. **Update Web project**
   ```csharp
   // PortMappingService is already in correct location
   // Just register in Program.cs
   builder.Services.AddScoped<PortMappingService>();
   ```

5. **Use in components**
   ```razor
   @inject PortMappingService PortMapping
   ```

### If Choosing Option 2 (Web Only):

1. **Models already in Web project**
   - `src/GameServer.Web/Models/PortRelationship.cs` ?

2. **Service already in Web project**
   - `src/GameServer.Web/Services/PortMappingService.cs` ?

3. **Just need to update namespace references**
   ```csharp
   using GameServer.Web.Models;
   using GameServer.Web.Services;
   ```

4. **Register and use**
   ```csharp
   builder.Services.AddScoped<PortMappingService>();
   ```

---

## Example Usage (Ready to Copy)

```csharp
// In your server creation wizard or port editor
@inject PortMappingService PortMapping
@inject NotificationService Notifications

private async Task OnPortValueChanged(uint newPort)
{
    // Get the setting metadata for this port
    var settingMetadata = GetSettingMetadata("SERVER_PORT");
    
    // Validate the new port value
    var validation = await PortMapping.ValidatePortSettingAsync(
        newPort,
        settingMetadata,
        currentGameType,
        currentSettings
    );
    
    if (!validation.IsValid)
    {
        // Show validation errors
        foreach (var error in validation.Errors)
        {
            Notifications.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Error,
                Summary = "Port Validation Failed",
                Detail = error,
                Duration = 5000
            });
        }
        return;
    }
    
    // Apply the port changes
    var result = PortMapping.ApplyPortChanges(
        currentGameType,
        currentSettings,
        settingMetadata,
        newPort
    );
    
    if (result.Success)
    {
        // Show what was updated
        var updates = string.Join(", ", result.UpdatedPorts
            .Select(u => $"{u.Description}: {u.OldPort} ? {u.NewPort}"));
            
        Notifications.Notify(new NotificationMessage
        {
            Severity = NotificationSeverity.Success,
            Summary = "Ports Updated",
            Detail = updates,
            Duration = 5000
        });
        
        // Update the game type
        await GameTypeApi.UpdateAsync(currentGameType.Key, currentGameType);
        StateHasChanged();
    }
}
```

---

## What's Working Now

The following files are complete and ready:

? **Documentation** - All guides complete  
? **Models** - All classes defined  
? **Service** - All logic implemented  
? **Examples** - Real-world configs provided  

## What Needs Decision

The following requires architecture decision:

?? **Model Location** - Docker project vs Web project  
?? **Client Regeneration** - If adding to Docker project  
?? **Service Registration** - After model location decided  

---

## Next Steps

1. **Decide on architecture** (Option 1 recommended)
2. **Place models in chosen location**
3. **Update SettingMetadata** (if Option 1)
4. **Regenerate client** (if Option 1)
5. **Register service** in Program.cs
6. **Integrate with UI** components
7. **Test with real game types**

---

## Support Files

All documentation and examples are in `docs/`:
- `Advanced-Port-Mapping-System.md` - Complete guide
- `Game-Server-Port-Examples.json` - Ready configurations
- `Port-Mapping-Implementation-Summary.md` - Quick reference

Code files are in their respective locations awaiting architecture decision.

---

**Status:** ?? **AWAITING ARCHITECTURE DECISION**

Once you decide where the models should live (Docker API project or Web project), the integration can be completed in ~30 minutes.
