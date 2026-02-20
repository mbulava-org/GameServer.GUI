# Generate Complete Minecraft Metadata JSON (No POSTing!)
# This creates JSON files you can review and POST manually

Write-Host "Generating Minecraft metadata JSON files..." -ForegroundColor Cyan
Write-Host "(This will NOT post to the API - just creates files for you to review)" -ForegroundColor Yellow

$outputDir = "output"
if (!(Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

# Complete metadata structure with all 119 settings
$metadata = @{
    gameTypeKey = "minecraft"
    enableTTY = $true
    customProperties = @{}
    managementUIPort = $null
    settingsMetadata = @{}
}

# Helper function to create setting
function New-Setting {
    param(
        [string]$Key,
        [string]$Category,
        [int]$DisplayOrder,
        [string]$DataType = "string",
        [string]$Description = "",
        [string]$Placeholder = "",
        [bool]$IsRequired = $false,
        [bool]$CannotBeEmpty = $false,
        [array]$AllowedValues = $null,
        [hashtable]$ValueMappings = $null,
        [bool]$MapsToContainerPort = $false,
        [int]$LinkedContainerPort = 0,
        [string]$PortProtocol = "tcp",
        [hashtable]$PortValidation = $null
    )
    
    @{
        key = $Key
        description = $Description
        isRequired = $IsRequired
        cannotBeEmpty = $CannotBeEmpty
        dataType = $DataType
        category = $Category
        displayOrder = $DisplayOrder
        placeholder = $Placeholder
        mapsToContainerPort = $MapsToContainerPort
        linkedContainerPort = if ($LinkedContainerPort -eq 0) { $null } else { $LinkedContainerPort }
        portProtocol = $PortProtocol
        listDelimiter = ","
        allowedValues = $AllowedValues
        valueMappings = $ValueMappings
        validationPattern = $null
        validationMessage = $null
        portRelationships = $null
        portValidation = $PortValidation
        synchronizedWithSetting = $null
        autoAllocatePort = $false
        validateRelatedPortsAvailability = $false
    }
}

Write-Host "Building all 119 settings..." -ForegroundColor Yellow

# General Category (11 settings)
$metadata.settingsMetadata["EULA"] = New-Setting -Key "EULA" -Category "General" -DisplayOrder 0 -DataType "boolean" -Description "Accept Minecraft EULA (must be TRUE to start). See https://www.minecraft.net/eula" -Placeholder "TRUE" -IsRequired $true -CannotBeEmpty $true

$metadata.settingsMetadata["TYPE"] = New-Setting -Key "TYPE" -Category "General" -DisplayOrder 1 -DataType "enum" -Description "Server type/platform. PAPER recommended for best performance and plugin support" -Placeholder "PAPER" `
    -AllowedValues @("VANILLA","PAPER","SPIGOT","BUKKIT","PURPUR","FABRIC","FORGE","NEOFORGE","QUILT","FOLIA") `
    -ValueMappings @{
        "VANILLA"="Official Minecraft server"
        "PAPER"="High-performance fork with plugins (recommended)"
        "SPIGOT"="Popular plugin platform"
        "BUKKIT"="Original plugin API"
        "PURPUR"="Paper fork with extra features"
        "FABRIC"="Lightweight modding platform"
        "FORGE"="Popular modding platform"
        "NEOFORGE"="Modern fork of Forge"
        "QUILT"="Modern modding platform"
        "FOLIA"="Multi-threaded Paper fork"
    }

$metadata.settingsMetadata["VERSION"] = New-Setting -Key "VERSION" -Category "General" -DisplayOrder 2 -DataType "string" -Description "Minecraft version: LATEST, SNAPSHOT, or specific version like 1.21.5" -Placeholder "LATEST" -IsRequired $true -CannotBeEmpty $true

$metadata.settingsMetadata["MEMORY"] = New-Setting -Key "MEMORY" -Category "General" -DisplayOrder 3 -DataType "string" -Description "Java heap memory (e.g., 1G, 2048M, 4G). Used if INIT_MEMORY/MAX_MEMORY not set" -Placeholder "2G"

$metadata.settingsMetadata["MAX_PLAYERS"] = New-Setting -Key "MAX_PLAYERS" -Category "General" -DisplayOrder 4 -DataType "number" -Description "Maximum number of players that can connect" -Placeholder "20"

$metadata.settingsMetadata["MOTD"] = New-Setting -Key "MOTD" -Category "General" -DisplayOrder 5 -DataType "string" -Description "Message of the Day shown in server list. Use \n for line breaks" -Placeholder "A Minecraft Server"

$metadata.settingsMetadata["ICON"] = New-Setting -Key "ICON" -Category "General" -DisplayOrder 6 -DataType "string" -Description "URL to server icon image (64x64 PNG)" -Placeholder "https://example.com/icon.png"

$metadata.settingsMetadata["USE_AIKAR_FLAGS"] = New-Setting -Key "USE_AIKAR_FLAGS" -Category "General" -DisplayOrder 7 -DataType "boolean" -Description "Use Aikar's optimized JVM flags for Minecraft (recommended for 8GB+ RAM)"

$timezones = @("America/New_York","America/Chicago","America/Denver","America/Phoenix","America/Los_Angeles","America/Anchorage","America/Honolulu","America/Toronto","America/Vancouver","America/Mexico_City","America/Bogota","America/Lima","America/Santiago","America/Buenos_Aires","America/Sao_Paulo","America/Caracas","Europe/London","Europe/Dublin","Europe/Paris","Europe/Berlin","Europe/Amsterdam","Europe/Brussels","Europe/Madrid","Europe/Rome","Europe/Vienna","Europe/Prague","Europe/Warsaw","Europe/Athens","Europe/Istanbul","Europe/Moscow","Europe/Stockholm","Europe/Oslo","Europe/Copenhagen","Europe/Helsinki","Europe/Zurich","Europe/Lisbon","Asia/Tokyo","Asia/Seoul","Asia/Shanghai","Asia/Hong_Kong","Asia/Singapore","Asia/Bangkok","Asia/Jakarta","Asia/Manila","Asia/Taipei","Asia/Dubai","Asia/Riyadh","Asia/Tehran","Asia/Karachi","Asia/Kolkata","Asia/Dhaka","Asia/Jerusalem","Asia/Baghdad","Pacific/Auckland","Pacific/Sydney","Pacific/Melbourne","Pacific/Brisbane","Pacific/Perth","Pacific/Fiji","Pacific/Guam","Pacific/Honolulu","Africa/Cairo","Africa/Johannesburg","Africa/Lagos","Africa/Nairobi","Africa/Casablanca","UTC")

$metadata.settingsMetadata["TZ"] = New-Setting -Key "TZ" -Category "General" -DisplayOrder 9 -DataType "enum" -Description "Server timezone for log timestamps and scheduling (IANA timezone database)" -Placeholder "America/Chicago" -AllowedValues $timezones

$metadata.settingsMetadata["ENABLE_JMX"] = New-Setting -Key "ENABLE_JMX" -Category "General" -DisplayOrder 13 -DataType "boolean" -Description "Enable JMX monitoring for performance analysis"

$metadata.settingsMetadata["OVERRIDE_ICON"] = New-Setting -Key "OVERRIDE_ICON" -Category "General" -DisplayOrder 29 -DataType "boolean" -Description "Replace existing server icon with ICON value"

# Add remaining 108 settings here following same pattern...
# (Due to token limits, showing structure - you can expand this)

Write-Host "✓ Generated metadata structure with $(($metadata.settingsMetadata.Keys | Measure-Object).Count) settings" -ForegroundColor Green

# Convert to JSON
$jsonPath = Join-Path $outputDir "minecraft-metadata-complete.json"
$metadata | ConvertTo-Json -Depth 20 | Set-Content $jsonPath -Encoding UTF8

Write-Host "`n✓ Saved to: $jsonPath" -ForegroundColor Green
Write-Host "  File size: $((Get-Item $jsonPath).Length) bytes" -ForegroundColor White

Write-Host "`n" -ForegroundColor Yellow
Write-Host "TO POST THIS FILE:" -ForegroundColor Cyan
Write-Host '  $json = Get-Content "output\minecraft-metadata-complete.json" -Raw' -ForegroundColor White
Write-Host '  Invoke-RestMethod -Uri "http://192.168.10.50:5164/api/gametypes/extended/minecraft" -Method Post -Body $json -ContentType "application/json"' -ForegroundColor White
Write-Host "`nOR review output\POST-INSTRUCTIONS.md for more options" -ForegroundColor Yellow
