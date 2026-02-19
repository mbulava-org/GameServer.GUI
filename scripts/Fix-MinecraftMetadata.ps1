# Fix Minecraft Extended Metadata
# This script fetches the current metadata, applies all recommended fixes, and POSTs it back

$apiBase = "http://192.168.10.50:5164"
$gameTypeKey = "minecraft"

Write-Host "Fetching current Minecraft metadata..." -ForegroundColor Cyan

# Fetch current metadata
$currentMetadata = Invoke-RestMethod -Uri "$apiBase/api/gametypes/extended/$gameTypeKey" -Method Get
Write-Host "? Fetched metadata with $($currentMetadata.settingsMetadata.Count) settings" -ForegroundColor Green

# Clone the metadata for modifications
$fixedMetadata = $currentMetadata | ConvertTo-Json -Depth 10 | ConvertFrom-Json

Write-Host "`nApplying fixes..." -ForegroundColor Cyan

# Define dataType fixes
$dataTypeFixes = @{
    # JVM/Memory Settings
    "INIT_MEMORY" = "number"
    "MAX_MEMORY" = "number"
    "MEMORY" = "string"  # Can be "1G", "2048M", etc.
    
    # Boolean Settings
    "USE_AIKAR_FLAGS" = "boolean"
    "USE_MEOWICE_FLAGS" = "boolean"
    "USE_MEOWICE_GRAALVM_FLAGS" = "boolean"
    "USE_FLARE_FLAGS" = "boolean"
    "USE_SIMD_FLAGS" = "boolean"
    "ENABLE_JMX" = "boolean"
    "ENABLE_ROLLING_LOGS" = "boolean"
    "ALLOW_NETHER" = "boolean"
    "ANNOUNCE_PLAYER_ACHIEVEMENTS" = "boolean"
    "GENERATE_STRUCTURES" = "boolean"
    "SNOOPER_ENABLED" = "boolean"
    "SPAWN_NPCS" = "boolean"
    "PVP" = "boolean"
    "ONLINE_MODE" = "boolean"
    "ALLOW_FLIGHT" = "boolean"
    "ENABLE_STATUS" = "boolean"
    "PREVENT_PROXY_CONNECTIONS" = "boolean"
    "USE_NATIVE_TRANSPORT" = "boolean"
    "CONSOLE" = "boolean"
    "GUI" = "boolean"
    "SETUP_ONLY" = "boolean"
    "RESOURCE_PACK_ENFORCE" = "boolean"
    "ENABLE_WHITELIST" = "boolean"
    "OVERRIDE_WHITELIST" = "boolean"
    "ENABLE_RCON" = "boolean"
    "BROADCAST_RCON_TO_OPS" = "boolean"
    "ENABLE_AUTOPAUSE" = "boolean"
    "DEBUG_AUTOPAUSE" = "boolean"
    "ENABLE_AUTOSTOP" = "boolean"
    "DEBUG_AUTOSTOP" = "boolean"
    "CF_FORCE_SYNCHRONIZE" = "boolean"
    "CF_OVERRIDES_SKIP_EXISTING" = "boolean"
    "SYNC_CHUNK_WRITES" = "boolean"
    
    # Number Settings
    "MAX_BUILD_HEIGHT" = "number"
    "SPAWN_PROTECTION" = "number"
    "PLAYER_IDLE_TIMEOUT" = "number"
    "ENTITY_BROADCAST_RANGE_PERCENTAGE" = "number"
    "FUNCTION_PERMISSION_LEVEL" = "number"
    "NETWORK_COMPRESSION_THRESHOLD" = "number"
    "OP_PERMISSION_LEVEL" = "number"
    "SIMULATION_DISTANCE" = "number"
    "STOP_DURATION" = "number"
    "ROLLING_LOG_MAX_FILES" = "number"
    "AUTOPAUSE_TIMEOUT_EST" = "number"
    "AUTOPAUSE_TIMEOUT_INIT" = "number"
    "AUTOPAUSE_TIMEOUT_KN" = "number"
    "AUTOPAUSE_PERIOD" = "number"
    "AUTOSTOP_TIMEOUT_EST" = "number"
    "AUTOSTOP_TIMEOUT_INIT" = "number"
    "AUTOSTOP_PERIOD" = "number"
    "CF_PARALLEL_DOWNLOADS" = "number"
    "UID" = "number"
    "GID" = "number"
    "RCON_PORT" = "port"
    "STOP_SERVER_ANNOUNCE_DELAY" = "number"
    
    # String Settings
    "TZ" = "string"
    "LOG_LEVEL" = "string"
    "LOG_CONSOLE_FORMAT" = "string"
    "LOG_FILE_FORMAT" = "string"
    "LOG_TERMINAL_FORMAT" = "string"
    "ROLLING_LOG_FILE_PATTERN" = "string"
    "JMX_HOST" = "string"
    "JVM_OPTS" = "string"
    "JVM_XX_OPTS" = "string"
    "JVM_DD_OPTS" = "string"
    "EXTRA_ARGS" = "string"
    "LOG_TIMESTAMP" = "string"
    "LEVEL_TYPE" = "string"
    "GENERATOR_SETTINGS" = "string"
    "SERVER_NAME" = "string"
    "RESOURCE_PACK" = "string"
    "RESOURCE_PACK_SHA1" = "string"
    "WHITELIST" = "string"
    "WHITELIST_FILE" = "string"
    "RCON_PASSWORD" = "string"
    "RCON_CMDS_STARTUP" = "string"
    "RCON_CMDS_ON_CONNECT" = "string"
    "RCON_CMDS_FIRST_CONNECT" = "string"
    "RCON_CMDS_ON_DISCONNECT" = "string"
    "RCON_CMDS_LAST_DISCONNECT" = "string"
    "AUTOPAUSE_KNOCK_INTERFACE" = "string"
    "CF_API_KEY" = "string"
    "CF_API_KEY_FILE" = "string"
    "CF_PAGE_URL" = "string"
    "CF_SLUG" = "string"
    "CF_FILE_ID" = "string"
    "CF_FILENAME_MATCHER" = "string"
    "CF_EXCLUDE_INCLUDE_FILE" = "string"
    "CF_EXCLUDE_MODS" = "string"
    "CF_FORCE_INCLUDE_MODS" = "string"
    "CF_SET_LEVEL_FROM" = "string"
    "CF_MOD_LOADER_VERSION" = "string"
    "ADDITIONAL_MODS" = "string"
    "EXTRA_OPTS" = "string"
    "SERVER_PROPERTIES_TEMPLATE" = "string"
    "INIT_COMMAND" = "string"
}

# Define category reassignments (moving from "zzzz" to proper categories)
$categoryFixes = @{
    # JVM Category
    "INIT_MEMORY" = @{ Category = "JVM"; DisplayOrder = 0 }
    "MAX_MEMORY" = @{ Category = "JVM"; DisplayOrder = 1 }
    "USE_MEOWICE_FLAGS" = @{ Category = "JVM"; DisplayOrder = 2 }
    "USE_MEOWICE_GRAALVM_FLAGS" = @{ Category = "JVM"; DisplayOrder = 3 }
    "JVM_OPTS" = @{ Category = "JVM"; DisplayOrder = 4 }
    "JVM_XX_OPTS" = @{ Category = "JVM"; DisplayOrder = 5 }
    "JVM_DD_OPTS" = @{ Category = "JVM"; DisplayOrder = 6 }
    "EXTRA_ARGS" = @{ Category = "JVM"; DisplayOrder = 7 }
    "USE_FLARE_FLAGS" = @{ Category = "JVM"; DisplayOrder = 8 }
    "USE_SIMD_FLAGS" = @{ Category = "JVM"; DisplayOrder = 9 }
    
    # Logging Category
    "LOG_LEVEL" = @{ Category = "Logging"; DisplayOrder = 0 }
    "LOG_CONSOLE_FORMAT" = @{ Category = "Logging"; DisplayOrder = 1 }
    "LOG_FILE_FORMAT" = @{ Category = "Logging"; DisplayOrder = 2 }
    "LOG_TERMINAL_FORMAT" = @{ Category = "Logging"; DisplayOrder = 3 }
    "LOG_TIMESTAMP" = @{ Category = "Logging"; DisplayOrder = 4 }
    "ENABLE_ROLLING_LOGS" = @{ Category = "Logging"; DisplayOrder = 5 }
    "ROLLING_LOG_FILE_PATTERN" = @{ Category = "Logging"; DisplayOrder = 6 }
    "ROLLING_LOG_MAX_FILES" = @{ Category = "Logging"; DisplayOrder = 7 }
    
    # Security Category
    "ONLINE_MODE" = @{ Category = "Security"; DisplayOrder = 0 }
    "ENABLE_WHITELIST" = @{ Category = "Security"; DisplayOrder = 1 }
    "WHITELIST" = @{ Category = "Security"; DisplayOrder = 2 }
    "WHITELIST_FILE" = @{ Category = "Security"; DisplayOrder = 3 }
    "OVERRIDE_WHITELIST" = @{ Category = "Security"; DisplayOrder = 4 }
    "ENABLE_RCON" = @{ Category = "Security"; DisplayOrder = 5 }
    "RCON_PASSWORD" = @{ Category = "Security"; DisplayOrder = 6 }
    "RCON_PORT" = @{ Category = "Security"; DisplayOrder = 7 }
    "BROADCAST_RCON_TO_OPS" = @{ Category = "Security"; DisplayOrder = 8 }
    
    # RCON Commands Category
    "RCON_CMDS_STARTUP" = @{ Category = "RCON Commands"; DisplayOrder = 0 }
    "RCON_CMDS_ON_CONNECT" = @{ Category = "RCON Commands"; DisplayOrder = 1 }
    "RCON_CMDS_FIRST_CONNECT" = @{ Category = "RCON Commands"; DisplayOrder = 2 }
    "RCON_CMDS_ON_DISCONNECT" = @{ Category = "RCON Commands"; DisplayOrder = 3 }
    "RCON_CMDS_LAST_DISCONNECT" = @{ Category = "RCON Commands"; DisplayOrder = 4 }
    
    # Automation Category
    "ENABLE_AUTOPAUSE" = @{ Category = "Automation"; DisplayOrder = 0 }
    "AUTOPAUSE_TIMEOUT_EST" = @{ Category = "Automation"; DisplayOrder = 1 }
    "AUTOPAUSE_TIMEOUT_INIT" = @{ Category = "Automation"; DisplayOrder = 2 }
    "AUTOPAUSE_TIMEOUT_KN" = @{ Category = "Automation"; DisplayOrder = 3 }
    "AUTOPAUSE_PERIOD" = @{ Category = "Automation"; DisplayOrder = 4 }
    "AUTOPAUSE_KNOCK_INTERFACE" = @{ Category = "Automation"; DisplayOrder = 5 }
    "DEBUG_AUTOPAUSE" = @{ Category = "Automation"; DisplayOrder = 6 }
    "ENABLE_AUTOSTOP" = @{ Category = "Automation"; DisplayOrder = 7 }
    "AUTOSTOP_TIMEOUT_EST" = @{ Category = "Automation"; DisplayOrder = 8 }
    "AUTOSTOP_TIMEOUT_INIT" = @{ Category = "Automation"; DisplayOrder = 9 }
    "AUTOSTOP_PERIOD" = @{ Category = "Automation"; DisplayOrder = 10 }
    "DEBUG_AUTOSTOP" = @{ Category = "Automation"; DisplayOrder = 11 }
    
    # Mods (CurseForge) Category
    "CF_API_KEY" = @{ Category = "Mods"; DisplayOrder = 0 }
    "CF_API_KEY_FILE" = @{ Category = "Mods"; DisplayOrder = 1 }
    "CF_PAGE_URL" = @{ Category = "Mods"; DisplayOrder = 2 }
    "CF_SLUG" = @{ Category = "Mods"; DisplayOrder = 3 }
    "CF_FILE_ID" = @{ Category = "Mods"; DisplayOrder = 4 }
    "CF_FILENAME_MATCHER" = @{ Category = "Mods"; DisplayOrder = 5 }
    "CF_EXCLUDE_INCLUDE_FILE" = @{ Category = "Mods"; DisplayOrder = 6 }
    "CF_EXCLUDE_MODS" = @{ Category = "Mods"; DisplayOrder = 7 }
    "CF_FORCE_INCLUDE_MODS" = @{ Category = "Mods"; DisplayOrder = 8 }
    "CF_FORCE_SYNCHRONIZE" = @{ Category = "Mods"; DisplayOrder = 9 }
    "CF_SET_LEVEL_FROM" = @{ Category = "Mods"; DisplayOrder = 10 }
    "CF_PARALLEL_DOWNLOADS" = @{ Category = "Mods"; DisplayOrder = 11 }
    "CF_OVERRIDES_SKIP_EXISTING" = @{ Category = "Mods"; DisplayOrder = 12 }
    "CF_MOD_LOADER_VERSION" = @{ Category = "Mods"; DisplayOrder = 13 }
    "ADDITIONAL_MODS" = @{ Category = "Mods"; DisplayOrder = 14 }
    
    # Resources Category
    "RESOURCE_PACK" = @{ Category = "Resources"; DisplayOrder = 0 }
    "RESOURCE_PACK_SHA1" = @{ Category = "Resources"; DisplayOrder = 1 }
    "RESOURCE_PACK_ENFORCE" = @{ Category = "Resources"; DisplayOrder = 2 }
    
    # Advanced World Category
    "MAX_WORLD_SIZE" = @{ Category = "World"; DisplayOrder = 22 }
    "ANNOUNCE_PLAYER_ACHIEVEMENTS" = @{ Category = "World"; DisplayOrder = 23 }
    "GENERATE_STRUCTURES" = @{ Category = "World"; DisplayOrder = 24 }
    "SNOOPER_ENABLED" = @{ Category = "World"; DisplayOrder = 25 }
    "MAX_BUILD_HEIGHT" = @{ Category = "World"; DisplayOrder = 26 }
    "SPAWN_PROTECTION" = @{ Category = "World"; DisplayOrder = 27 }
    "LEVEL_TYPE" = @{ Category = "World"; DisplayOrder = 28 }
    "GENERATOR_SETTINGS" = @{ Category = "World"; DisplayOrder = 29 }
    "PVP" = @{ Category = "World"; DisplayOrder = 30 }
    "ALLOW_FLIGHT" = @{ Category = "World"; DisplayOrder = 31 }
    
    # Advanced Network Category
    "PLAYER_IDLE_TIMEOUT" = @{ Category = "Network"; DisplayOrder = 11 }
    "SYNC_CHUNK_WRITES" = @{ Category = "Network"; DisplayOrder = 12 }
    "ENABLE_STATUS" = @{ Category = "Network"; DisplayOrder = 13 }
    "ENTITY_BROADCAST_RANGE_PERCENTAGE" = @{ Category = "Network"; DisplayOrder = 14 }
    "FUNCTION_PERMISSION_LEVEL" = @{ Category = "Network"; DisplayOrder = 15 }
    "NETWORK_COMPRESSION_THRESHOLD" = @{ Category = "Network"; DisplayOrder = 16 }
    "OP_PERMISSION_LEVEL" = @{ Category = "Network"; DisplayOrder = 17 }
    "PREVENT_PROXY_CONNECTIONS" = @{ Category = "Network"; DisplayOrder = 18 }
    "USE_NATIVE_TRANSPORT" = @{ Category = "Network"; DisplayOrder = 19 }
    "SIMULATION_DISTANCE" = @{ Category = "Network"; DisplayOrder = 20 }
    "STOP_SERVER_ANNOUNCE_DELAY" = @{ Category = "Network"; DisplayOrder = 21 }
    
    # Advanced Category
    "JMX_HOST" = @{ Category = "Advanced"; DisplayOrder = 2 }
    "CONSOLE" = @{ Category = "Advanced"; DisplayOrder = 3 }
    "GUI" = @{ Category = "Advanced"; DisplayOrder = 4 }
    "STOP_DURATION" = @{ Category = "Advanced"; DisplayOrder = 5 }
    "SETUP_ONLY" = @{ Category = "Advanced"; DisplayOrder = 6 }
    "EXTRA_OPTS" = @{ Category = "Advanced"; DisplayOrder = 7 }
    "SERVER_PROPERTIES_TEMPLATE" = @{ Category = "Advanced"; DisplayOrder = 8 }
    "INIT_COMMAND" = @{ Category = "Advanced"; DisplayOrder = 9 }
    "SERVER_NAME" = @{ Category = "Advanced"; DisplayOrder = 10 }
}

# Apply fixes
$fixCount = 0
foreach ($key in $fixedMetadata.settingsMetadata.PSObject.Properties.Name) {
    $setting = $fixedMetadata.settingsMetadata.$key
    
    # Fix dataType
    if ($dataTypeFixes.ContainsKey($key)) {
        $setting.dataType = $dataTypeFixes[$key]
        $fixCount++
    }
    
    # Fix category and displayOrder
    if ($categoryFixes.ContainsKey($key)) {
        $setting.category = $categoryFixes[$key].Category
        $setting.displayOrder = $categoryFixes[$key].DisplayOrder
        $fixCount++
    }
}

Write-Host "? Applied $fixCount fixes to settings" -ForegroundColor Green

# Convert back to JSON for POST
$jsonBody = $fixedMetadata | ConvertTo-Json -Depth 10

Write-Host "`nPosting updated metadata back to server..." -ForegroundColor Cyan

try {
    $response = Invoke-RestMethod -Uri "$apiBase/api/gametypes/extended/$gameTypeKey" `
        -Method Post `
        -Body $jsonBody `
        -ContentType "application/json"
    
    Write-Host "? Successfully updated Minecraft metadata!" -ForegroundColor Green
    Write-Host "`nSummary:" -ForegroundColor Yellow
    Write-Host "  - Fixed null dataTypes" -ForegroundColor White
    Write-Host "  - Reorganized categories (moved from 'zzzz')" -ForegroundColor White
    Write-Host "  - Fixed displayOrder values" -ForegroundColor White
    Write-Host "`n? Metadata update complete!" -ForegroundColor Green
}
catch {
    Write-Host "? Error posting metadata: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.Exception.Response -ForegroundColor Red
}
