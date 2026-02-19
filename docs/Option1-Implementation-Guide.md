# Option 1 Implementation - Step by Step Guide

## Current Status

? **Models Created** - PortRelationship.cs in GameServer.Docker/Models  
? **SettingMetadata Extended** - New properties added  
? **Service Created** - PortMappingService.cs in GameServer.Web/Services  
? **Service Registered** - Added to Program.cs  
? **Build Failing** - Docker.Client needs regeneration

## The Issue

The **GameServer.Docker.Client** is auto-generated from the Docker API using NSwag. The generated code includes:
- `SettingMetadata` class (without our new properties)
- Various API clients

When we extended `SettingMetadata.cs` in the Docker project, the Docker.Client package doesn't automatically know about these changes yet.

## Solution Steps

### Step 1: Trigger Docker.Client Regeneration

The Docker.Client project has an MSBuild target that regenerates the client from the API:

```bash
cd src/GameServer.Docker.Client
dotnet build
```

This will:
1. Read the Docker API's OpenAPI specification
2. Generate new client code with updated SettingMetadata
3. Include the new PortRelationship models

### Step 2: Verify SettingMetadata Has New Properties

After regeneration, check `src/GameServer.Docker.Client/GameServer.Docker.Client.v1.g.cs`:

Look for the `SettingMetadata` class and verify it includes:
```csharp
public partial class SettingMetadata
{
    // ... existing properties ...
    
    [Newtonsoft.Json.JsonProperty("portRelationships", ...)]
    public System.Collections.Generic.ICollection<PortRelationship> PortRelationships { get; set; }
    
    [Newtonsoft.Json.JsonProperty("portValidation", ...)]
    public PortValidationRule PortValidation { get; set; }
    
    // ... other new properties ...
}
```

### Step 3: Rebuild Everything

```bash
cd ../..
dotnet build
```

## Alternative: Manual Client Update

If NSwag regeneration doesn't work automatically, you can manually ensure the models are available:

### Option A: Keep Dual References (Temporary)

The Web project currently has:
```xml
<ProjectReference Include="..\GameServer.Docker.Client\GameServer.Docker.Client.csproj" />
<ProjectReference Include="..\GameServer.Docker\GameServer.Docker.csproj" />
```

This allows PortMappingService to use models from Docker while waiting for Client regeneration.

### Option B: Comment Out Port Mapping Service (Temporary)

Until Docker.Client is regenerated, you can comment out the service registration:

```csharp
// Temporary: Commented until Docker.Client is regenerated with new models
// builder.Services.AddScoped<GameServer.Web.Services.PortMappingService>();
```

### Option C: Stub Implementation (Quick Fix)

Create a temporary stub that compiles:

```csharp
// Temporary stub until Docker.Client regeneration
public class PortMappingService
{
    public PortMappingService(IPortApi portApi, ILogger<PortMappingService> logger)
    {
        // TODO: Implement after Docker.Client regeneration
    }
    
    public Task<object> ValidatePortSettingAsync(params object[] args)
    {
        throw new NotImplementedException("Waiting for Docker.Client regeneration");
    }
}
```

## Expected Files After Regeneration

### In Docker.Client.v1.g.cs:

```csharp
[System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "...")]
public partial class PortRelationship
{
    [Newtonsoft.Json.JsonProperty("relationType", Required = ...)]
    public PortRelationshipType RelationType { get; set; }
    
    [Newtonsoft.Json.JsonProperty("targetContainerPort", Required = ...)]
    public int TargetContainerPort { get; set; }
    
    // ... other properties ...
}

[System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "...")]
public enum PortRelationshipType
{
    Offset = 0,
    Fixed = 1,
    Multiplier = 2,
}

[System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "...")]
public partial class PortValidationRule
{
    [Newtonsoft.Json.JsonProperty("minPort", Required = ...)]
    public int MinPort { get; set; }
    
    // ... other properties ...
}
```

## Verification Checklist

After regeneration, verify:

- [ ] `PortRelationship` class exists in Docker.Client
- [ ] `PortRelationshipType` enum exists in Docker.Client
- [ ] `PortValidationRule` class exists in Docker.Client
- [ ] `SettingMetadata` has `PortRelationships` property
- [ ] `SettingMetadata` has `PortValidation` property
- [ ] `SettingMetadata` has other new properties
- [ ] Solution builds without errors
- [ ] PortMappingService compiles

## Next Actions

### Immediate (To Unblock Development):

**Option 1: Regenerate Client Now**
```bash
cd src/GameServer.Docker.Client
dotnet build --force
```

**Option 2: Temporarily Disable Service**
```csharp
// Comment out in Program.cs
// builder.Services.AddScoped<GameServer.Web.Services.PortMappingService>();
```

### Once Client is Regenerated:

1. ? Uncomment service registration (if disabled)
2. ? Remove Docker project reference from Web (if added)
3. ? Test PortMappingService methods
4. ? Integrate into UI components
5. ? Create example configurations for game types

## Testing After Integration

```csharp
// Example test in a Razor component
@inject PortMappingService PortMapping

private async Task TestPortMapping()
{
    var settingMetadata = new SettingMetadata
    {
        Key = "SERVER_PORT",
        MapsToContainerPort = true,
        LinkedContainerPort = 27015,
        PortProtocol = "udp",
        PortRelationships = new List<PortRelationship>
        {
            new PortRelationship
            {
                RelationType = PortRelationshipType.Offset,
                TargetContainerPort = 27016,
                TargetProtocol = "udp",
                Offset = 1,
                Description = "Query Port"
            }
        }
    };
    
    var validation = await PortMapping.ValidatePortSettingAsync(
        27020, settingMetadata, gameType, settings);
        
    if (validation.IsValid)
    {
        Console.WriteLine("Port 27020 is valid!");
        Console.WriteLine($"Related ports: {string.Join(", ", validation.RelatedPortsToUpdate.Select(p => p.NewPort))}");
    }
}
```

## Current Build Errors Explained

The errors you're seeing are expected because:

1. **GameServer.Docker.Client** doesn't have the new models yet
2. **SettingMetadata** in Docker.Client doesn't have new properties yet  
3. **IPortApi** methods signatures may have changed

All of these will be resolved once Docker.Client is regenerated from the updated API.

## Summary

**Current State:** ? All models and service code complete  
**Blocker:** ? Docker.Client needs regeneration  
**Solution:** ?? Run `dotnet build` in Docker.Client project  
**Timeline:** ~5 minutes to regenerate and verify

**After Regeneration:** Everything should compile and be ready for UI integration! ??
