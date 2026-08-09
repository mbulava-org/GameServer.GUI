# Port Relationship Validation Fix

## Issue Summary
Environment variables with port relationships were not validating port availability for the main port OR the calculated related ports. Users could set port values that would fail at deployment time.

## Requirements (Clarified)
1. **Environment Variable → Container Port**: Value must honor the original mapping
2. **Port Relationships**: Calculate from base port value using configured relationships
3. **Validation**: Port environment variable AND all calculated related ports must ALL validate
4. **Error Messages**: Show which specific port relationship failed with descriptive names

## Example Scenario
```yaml
SERVER_PORT=25565 (main game port, TCP)
Port Relationships:
  - QUERY_PORT: SERVER_PORT + 1 = 25566 (UDP)
  - RCON_PORT: SERVER_PORT + 10 = 25575 (TCP)
```

**Validation Flow:**
1. User sets SERVER_PORT=25565
2. System calculates:
   - QUERY_PORT = 25566
   - RCON_PORT = 25575
3. System validates:
   - Is 25565/tcp available? ✓
   - Is 25566/udp available? ✗ (in use)
   - Is 25575/tcp available? ✓
4. Show error: "Query Port: Port 25566/udp is already in use"

## Implementation

### Added Port Availability Validation to ServerEnvironmentEditor

```csharp
// Validate port ranges if DataType is "port"
if (settingMeta.DataType?.ToLowerInvariant() == "port" &&
    Server.Settings.TryGetValue(settingMeta.Key, out var portValue) &&
    !string.IsNullOrWhiteSpace(portValue))
{
    if (int.TryParse(portValue, out var port))
    {
        // Validate range
        if (port < 1 || port > 65535)
        {
            validationErrors.Add($"{settingMeta.Key} must be a valid port (1-65535)");
            continue; // Skip availability check if range is invalid
        }

        // Validate availability if this setting maps to a container port
        if (settingMeta.MapsToContainerPort && settingMeta.LinkedContainerPort.HasValue)
        {
            var protocol = settingMeta.PortProtocol ?? "tcp";
            
            // Check if the main port is available
            var isMainPortAvailable = await PortApi.CheckAsync(protocol, port);
            if (!isMainPortAvailable)
            {
                validationErrors.Add($"{settingMeta.Key}: Port {port}/{protocol} is already in use");
            }

            // Validate all related ports from relationships
            if (settingMeta.PortRelationships?.Any() == true)
            {
                foreach (var relationship in settingMeta.PortRelationships)
                {
                    // Calculate the related port value
                    int calculatedPort = relationship.RelationType switch
                    {
                        PortRelationshipType.Offset => port + relationship.Offset,
                        PortRelationshipType.Fixed => (int)(relationship.FixedValue ?? relationship.TargetContainerPort),
                        PortRelationshipType.Multiplier => port * relationship.Offset,
                        _ => 0
                    };

                    // Validate calculated port range
                    if (calculatedPort < 1 || calculatedPort > 65535)
                    {
                        validationErrors.Add($"{relationship.Description ?? $"Related port {relationship.TargetContainerPort}/{relationship.TargetProtocol}"}: Calculated port {calculatedPort} is out of valid range (1-65535)");
                        continue;
                    }

                    // Check if calculated port is available
                    var isRelatedPortAvailable = await PortApi.CheckAsync(relationship.TargetProtocol, calculatedPort);
                    if (!isRelatedPortAvailable)
                    {
                        validationErrors.Add($"{relationship.Description ?? $"Related port {relationship.TargetContainerPort}/{relationship.TargetProtocol}"}: Port {calculatedPort}/{relationship.TargetProtocol} is already in use");
                    }
                }
            }
        }
    }
}
```

### Injected IPortApi for Availability Checking

```razor
@inject IGameTypeExtendedMetadataApi ExtendedMetadataApi
@inject IPortApi PortApi
@inject NotificationService NotificationService
```

## Error Message Examples

### Main Port In Use
```
SERVER_PORT: Port 25565/tcp is already in use
```

### Related Port In Use (with Description)
```
Query Port: Port 25566/udp is already in use
```

### Related Port In Use (without Description)
```
Related port 25566/udp: Port 25566/udp is already in use
```

### Calculated Port Out of Range
```
Query Port: Calculated port 70000 is out of valid range (1-65535)
```

## Validation Timing

Validation runs:
1. **On every keystroke** in environment variable inputs (via `SetSettingValue` → `ValidateAsync`)
2. **When moving to next step** in wizard (via `StepGameSettings.NextAsync` → `environmentEditor.ValidateAsync()`)
3. **When ports change** via relationships (via `OnPortsChanged` callback chain)

## Port Relationship Types Supported

### Offset
```csharp
// Example: Query port is always Game Port + 1
RelationType = PortRelationshipType.Offset
Offset = 1
// If SERVER_PORT = 25565, Query Port = 25566
```

### Fixed
```csharp
// Example: RCON always on port 27015 regardless of game port
RelationType = PortRelationshipType.Fixed
FixedValue = 27015
// RCON Port = 27015 (always)
```

### Multiplier
```csharp
// Example: Voice port is always double the game port
RelationType = PortRelationshipType.Multiplier
Offset = 2 // Used as multiplier
// If SERVER_PORT = 10000, Voice Port = 20000
```

## Testing Checklist

- [x] Build successful
- [ ] Set environment variable port that maps to container port
- [ ] Verify main port validates for availability
- [ ] Change port to one that's in use, verify error shows
- [ ] Verify related ports calculate correctly
- [ ] Set port where related port would conflict, verify specific error shows
- [ ] Verify error messages include relationship descriptions
- [ ] Test all three relationship types (Offset, Fixed, Multiplier)
- [ ] Verify validation runs real-time as user types
- [ ] Verify wizard Next button respects validation state

## Files Changed
1. ✅ `src/GameServer.Web/Components/Server/ServerEnvironmentEditor.razor`
   - Added IPortApi injection
   - Added port availability validation for main ports
   - Added port availability validation for all related ports
   - Added descriptive error messages with relationship names

## Architecture

```
User Changes Port Environment Variable
       │
       ▼
┌─────────────────────────────────────────────┐
│ ServerEnvironmentEditor.SetSettingValue     │
│  1. Update Server.Settings[key]             │
│  2. Find linked PortMapping                 │
│  3. Update ContainerPort = value            │
│  4. Update PublishedPort (if matching)      │
│  5. Calculate related ports:                │
│     - Offset: port + offset                 │
│     - Fixed: fixedValue                     │
│     - Multiplier: port * offset             │
│  6. Update related PortMappings             │
│  7. Call ValidateAsync() ────────┐          │
│  8. Call ServerChanged            │          │
│  9. Call OnPortsChanged           │          │
└───────────────────────────────────┼──────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────┐
│ ServerEnvironmentEditor.ValidateAsync       │
│                                             │
│ For each port environment variable:        │
│  1. Validate range (1-65535)               │
│  2. Check main port availability ───┐      │
│  3. For each port relationship:     │      │
│     a. Calculate related port       │      │
│     b. Validate range               │      │
│     c. Check availability ───────┐  │      │
│  4. Add errors to list           │  │      │
│                                  │  │      │
│  ┌───────────────────────────────┘  │      │
│  │  ┌────────────────────────────────┘      │
│  ▼  ▼                                       │
│  IPortApi.CheckAsync(protocol, port)       │
│   - Returns true if port is available      │
│   - Returns false if port is in use        │
│                                             │
│ OnValidationChanged(isValid) ───────────────┤
└─────────────────────────────────────────────┘
                    │
                    ▼
    ┌───────────────────────────┐
    │ UI Updates:               │
    │  - Show/hide errors       │
    │  - Enable/disable Next    │
    │  - Update button state    │
    └───────────────────────────┘
```

## Key Behaviors

1. **Real-time Validation**: Runs as user types, provides immediate feedback
2. **Complete Coverage**: Validates main port + all calculated related ports
3. **Descriptive Errors**: Uses relationship descriptions or falls back to "Related port X/protocol"
4. **Blocks Progress**: Wizard Next button stays disabled if any port (main or related) fails validation
5. **Protocol-Aware**: Checks availability per protocol (tcp/udp), same port can be used for different protocols
6. **Range Validation First**: Checks range before availability to avoid unnecessary API calls
7. **Relationship Preservation**: Container port always matches environment variable value, relationships calculate from that
