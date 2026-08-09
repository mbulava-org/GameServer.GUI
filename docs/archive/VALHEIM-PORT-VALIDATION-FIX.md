# Valheim SERVER_PORT Validation Fix

## Problem

The Valheim SERVER_PORT environment variable isn't being validated in the Create Server Wizard.

## Root Cause

Valheim's SERVER_PORT setting exists in the game type definition, but **doesn't have extended metadata configured in the database**.

The validation code in `ServerEnvironmentEditor.ValidateAsync()` requires these conditions to be met:

```csharp
if (settingMeta.DataType?.ToLowerInvariant() == "port" &&
    Server.Settings.TryGetValue(settingMeta.Key, out var portValue))
{
    if (settingMeta.MapsToContainerPort && settingMeta.LinkedContainerPort.HasValue)
    {
        // Validation runs here
    }
}
```

Without extended metadata, these conditions are never met, so validation never runs.

## Solution: Configure Valheim Extended Metadata

### Step 1: Open GameType Editor

1. Navigate to the GameTypes page in the web UI
2. Find "Valheim" in the list
3. Click to open Valheim's details/editor page

### Step 2: Configure SERVER_PORT Metadata

On the **Environment Variables** tab (formerly "Advanced Settings"), configure the SERVER_PORT setting:

#### Basic Properties
- **Key**: `SERVER_PORT` (already exists)
- **Description**: "Main server port for client connections"
- **Data Type**: `port`
- **Is Required**: ☑ (checked)
- **Cannot Be Empty**: ☑ (checked)
- **Category**: "Network" (or "Server")

#### Port Mapping Properties
- **Maps To Container Port**: ☑ (checked)
- **Linked Container Port**: `2456`
- **Port Protocol**: `udp`
- **Validate Related Ports Availability**: ☑ (checked)

#### Port Validation (Optional but Recommended)
- **Min Port**: `2400`
- **Max Port**: `2500`
- **Check Availability**: ☑ (checked)
- **Is User Editable**: ☑ (checked)
- **Validation Message**: "Port must be between 2400 and 2500 and available"

### Step 3: Add Port Relationships

Valheim uses THREE consecutive ports:
- **2456** (udp) - Main server port (controlled by SERVER_PORT)
- **2457** (udp) - Connection port (SERVER_PORT + 1)
- **2458** (udp) - Steam server list port (SERVER_PORT + 2)

Add two port relationships for SERVER_PORT:

#### Relationship 1: Connection Port
- **Relation Type**: `Offset`
- **Target Container Port**: `2457`
- **Target Protocol**: `udp`
- **Offset Value**: `1`
- **Description**: "Connection Port (Query)"
- **Is Required**: ☑ (checked)

#### Relationship 2: Steam List Port
- **Relation Type**: `Offset`
- **Target Container Port**: `2458`
- **Target Protocol**: `udp`
- **Offset Value**: `2`
- **Description**: "Steam Server List Port"
- **Is Required**: ☑ (checked)

### Step 4: Save Changes

Click "Save" to persist the extended metadata to the database.

### Step 5: Clear Cache and Test

1. Restart the GameServer.Web application (or wait 10 minutes for cache to expire)
2. Open the Create Server Wizard
3. Select "Valheim" as the game type
4. Navigate to the "Environment Variables" step
5. Change the SERVER_PORT value
6. Validation should now run:
   - Port 2456 (entered value) will be checked for availability
   - Port 2457 (entered value + 1) will be checked for availability
   - Port 2458 (entered value + 2) will be checked for availability
   - If any port is unavailable, an error message will show the specific relationship that failed

## Technical Details

### Database Structure

When properly configured, the SERVER_PORT setting will have this database structure:

**DefaultSettings Table:**
```sql
SettingKey: "SERVER_PORT"
SettingValue: "2456"
```

**SettingsMetadata Table:**
```sql
DefaultSettingId: [FK to DefaultSettings]
DataType: "port"
MapsToContainerPort: 1
LinkedContainerPort: 2456
PortProtocol: "udp"
ValidateRelatedPortsAvailability: 1
```

**PortRelationships Table:**
```sql
-- Relationship 1
SettingMetadataId: [FK to SettingsMetadata]
RelationType: 0  -- Offset
TargetContainerPort: 2457
TargetProtocol: "udp"
OffsetValue: 1
Description: "Connection Port (Query)"
IsRequired: 1

-- Relationship 2
SettingMetadataId: [FK to SettingsMetadata]
RelationType: 0  -- Offset
TargetContainerPort: 2458
TargetProtocol: "udp"
OffsetValue: 2
Description: "Steam Server List Port"
IsRequired: 1
```

### Validation Flow

Once configured, the validation flow is:

1. User enters SERVER_PORT value (e.g., "2460") in Environment Variables tab
2. `ServerEnvironmentEditor.ValidateAsync()` runs:
   - Checks if DataType == "port" ✓
   - Checks if MapsToContainerPort == true ✓
   - Checks if LinkedContainerPort has value ✓
3. Main port validation:
   - Calls `await PortApi.CheckAsync("udp", 2460)`
   - Shows error if unavailable
4. Related port validation (loops through PortRelationships):
   - Calculates port 2457 (2460 + offset 1)
   - Validates range (1-65535)
   - Calls `await PortApi.CheckAsync("udp", 2461)`
   - Shows error with description "Connection Port (Query)" if unavailable
   - Calculates port 2458 (2460 + offset 2)
   - Validates range (1-65535)
   - Calls `await PortApi.CheckAsync("udp", 2462)`
   - Shows error with description "Steam Server List Port" if unavailable
5. `OnPortsChanged` callback fires to update Technical Details step
6. Next button enables/disables based on validation results

### Why This Works

The validation infrastructure was already implemented in Phase 7 (PORT-RELATIONSHIP-VALIDATION.md). The code is waiting for metadata to exist. Once metadata exists:

- ✅ Validation conditions are met
- ✅ Port availability checks run
- ✅ Related ports are calculated and validated
- ✅ Descriptive error messages appear
- ✅ Wizard Next button respects validation state

## Alternative: Bulk Configuration Script

If you need to configure multiple game types, you can use the GameTypeExtendedMetadataController API:

```powershell
# Example: Configure Valheim SERVER_PORT via API
$apiUrl = "http://localhost:5068/api/gametypes/valheim/extended-metadata"

$metadata = @{
    GameTypeKey = "valheim"
    EnableTTY = $false
    SettingsMetadata = @{
        SERVER_PORT = @{
            Key = "SERVER_PORT"
            Description = "Main server port for client connections"
            DataType = "port"
            IsRequired = $true
            CannotBeEmpty = $true
            Category = "Network"
            MapsToContainerPort = $true
            LinkedContainerPort = 2456
            PortProtocol = "udp"
            ValidateRelatedPortsAvailability = $true
            PortValidation = @{
                MinPort = 2400
                MaxPort = 2500
                CheckAvailability = $true
                IsUserEditable = $true
                ValidationMessage = "Port must be between 2400 and 2500 and available"
            }
            PortRelationships = @(
                @{
                    RelationType = 0  # Offset
                    TargetContainerPort = 2457
                    TargetProtocol = "udp"
                    Offset = 1
                    Description = "Connection Port (Query)"
                    IsRequired = $true
                },
                @{
                    RelationType = 0  # Offset
                    TargetContainerPort = 2458
                    TargetProtocol = "udp"
                    Offset = 2
                    Description = "Steam Server List Port"
                    IsRequired = $true
                }
            )
        }
    }
}

$json = $metadata | ConvertTo-Json -Depth 10
Invoke-RestMethod -Uri $apiUrl -Method Post -Body $json -ContentType "application/json"
```

## Related Documentation

- `docs/PORT-RELATIONSHIP-VALIDATION.md` - Port validation implementation details
- `docs/PORT-CONFIGURATION-FIX.md` - Port editing and relationship architecture
- `docs/guides/GameType-Metadata-Complete-Guide.md` - Complete metadata configuration guide
- `docs/reference/SQLite-GameType-Database-Schema.md` - Database schema reference

## Status

- ✅ Validation infrastructure implemented
- ⏸️ Valheim metadata configuration **PENDING** (manual UI configuration required)
- ⏸️ Testing after configuration **PENDING**

Once metadata is configured through the UI or API, Valheim SERVER_PORT validation will work automatically.
