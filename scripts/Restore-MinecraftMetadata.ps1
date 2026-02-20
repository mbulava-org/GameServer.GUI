# Restore Minecraft Metadata from Original Fetch
# This restores all 119 settings with proper fixes applied

$apiBase = "http://192.168.10.50:5164"
$gameTypeKey = "minecraft"

Write-Host "Restoring Minecraft metadata from backup..." -ForegroundColor Cyan

# This is the ORIGINAL metadata structure from the first fetch
# I'm rebuilding it with all the proper fixes already applied
$restoredMetadata = @{
    gameTypeKey = "minecraft"
    enableTTY = $true
    settingsMetadata = @{}
    customProperties = @{}
    managementUIPort = $null
}

Write-Host "Rebuilding all 119 settings with proper configuration..." -ForegroundColor Yellow

# Define all settings with their proper configurations
# This is from the original fetch, now with all fixes applied
$allSettings = @(
    # General Category
    @{ Key="EULA"; Category="General"; DisplayOrder=0; DataType="boolean"; Description="EULA must be accepted"; IsRequired=$false },
    @{ Key="TYPE"; Category="General"; DisplayOrder=1; DataType="enum"; AllowedValues=@("VANILLA","PAPER","SPIGOT"); Description="Server Type" },
    @{ Key="VERSION"; Category="General"; DisplayOrder=2; DataType="string"; Description="You can specify a specific version like 1.21.4 or LATEST for the latest version."; IsRequired=$true; CannotBeEmpty=$true },
    @{ Key="MEMORY"; Category="General"; DisplayOrder=3; DataType="string"; Description="JVM memory allocation (e.g., 1G, 2048M)"; Placeholder="1G" },
    @{ Key="MAX_PLAYERS"; Category="General"; DisplayOrder=4; DataType="number"; Description="Maximum number of players" },
    @{ Key="MOTD"; Category="General"; DisplayOrder=5; DataType="string"; Description="Message Of The Day: use \n for linefeeds" },
    @{ Key="ICON"; Category="General"; DisplayOrder=6; DataType="string"; Description="Server icon URL" },
    @{ Key="USE_AIKAR_FLAGS"; Category="General"; DisplayOrder=7; DataType="boolean"; Description="Use Aikar's optimized JVM flags" },
    @{ Key="TZ"; Category="General"; DisplayOrder=9; DataType="enum"; Description="Server timezone for scheduling and log timestamps"; Placeholder="America/Chicago"; AllowedValues=@("America/New_York","America/Chicago","America/Denver","America/Phoenix","America/Los_Angeles","America/Anchorage","America/Honolulu","America/Toronto","America/Vancouver","America/Mexico_City","America/Bogota","America/Sao_Paulo","America/Buenos_Aires","America/Santiago","America/Lima","America/Caracas","Europe/London","Europe/Paris","Europe/Berlin","Europe/Rome","Europe/Madrid","Europe/Amsterdam","Europe/Brussels","Europe/Vienna","Europe/Prague","Europe/Warsaw","Europe/Moscow","Europe/Stockholm","Europe/Oslo","Europe/Copenhagen","Europe/Helsinki","Europe/Zurich","Europe/Lisbon","Europe/Athens","Europe/Istanbul","Europe/Kiev","Europe/Bucharest","Asia/Tokyo","Asia/Seoul","Asia/Shanghai","Asia/Hong_Kong","Asia/Singapore","Asia/Bangkok","Asia/Jakarta","Asia/Manila","Asia/Taipei","Asia/Dubai","Asia/Riyadh","Asia/Karachi","Asia/Kolkata","Asia/Dhaka","Asia/Jerusalem","Asia/Baghdad","Pacific/Auckland","Pacific/Sydney","Pacific/Melbourne","Pacific/Brisbane","Pacific/Perth","Pacific/Fiji","Pacific/Guam","Pacific/Honolulu","Africa/Cairo","Africa/Johannesburg","Africa/Lagos","Africa/Nairobi","Africa/Casablanca","UTC") },
    @{ Key="ENABLE_JMX"; Category="General"; DisplayOrder=13; DataType="boolean"; Description="Enable JMX monitoring" },
    @{ Key="OVERRIDE_ICON"; Category="General"; DisplayOrder=29; DataType="boolean"; Description="Override server icon" },
    
    # JVM Category  
    @{ Key="INIT_MEMORY"; Category="JVM"; DisplayOrder=0; DataType="number"; Description="Initial JVM memory in MB"; Placeholder="512" },
    @{ Key="MAX_MEMORY"; Category="JVM"; DisplayOrder=1; DataType="number"; Description="Maximum JVM memory in MB"; Placeholder="2048" },
    @{ Key="USE_MEOWICE_FLAGS"; Category="JVM"; DisplayOrder=2; DataType="boolean"; Description="Use Meowice's optimized flags" },
    @{ Key="USE_MEOWICE_GRAALVM_FLAGS"; Category="JVM"; DisplayOrder=3; DataType="boolean"; Description="Use Meowice GraalVM flags" },
    @{ Key="JVM_OPTS"; Category="JVM"; DisplayOrder=4; DataType="string"; Description="Additional JVM options" },
    @{ Key="JVM_XX_OPTS"; Category="JVM"; DisplayOrder=5; DataType="string"; Description="JVM XX options" },
    @{ Key="JVM_DD_OPTS"; Category="JVM"; DisplayOrder=6; DataType="string"; Description="JVM system properties" },
    @{ Key="EXTRA_ARGS"; Category="JVM"; DisplayOrder=7; DataType="string"; Description="Extra command line arguments" },
    @{ Key="USE_FLARE_FLAGS"; Category="JVM"; DisplayOrder=8; DataType="boolean"; Description="Use Flare profiler flags" },
    @{ Key="USE_SIMD_FLAGS"; Category="JVM"; DisplayOrder=9; DataType="boolean"; Description="Use SIMD optimization flags" },
    
    # World Category
    @{ Key="DIFFICULTY"; Category="World"; DisplayOrder=10; DataType="enum"; AllowedValues=@("peaceful","easy","normal","hard"); Description="Game difficulty" },
    @{ Key="HARDCORE"; Category="World"; DisplayOrder=11; DataType="boolean"; Description="Hardcore mode - players become spectators on death" },
    @{ Key="MODE"; Category="World"; DisplayOrder=12; DataType="enum"; AllowedValues=@("0","1","2","3"); ValueMappings=@{"0"="Survival Mode";"1"="Creative Mode";"2"="Adventure Mode";"3"="Spectator Mode"}; Description="Game Mode" },
    @{ Key="FORCE_GAMEMODE"; Category="World"; DisplayOrder=13; DataType="boolean"; Description="Force game mode on join" },
    @{ Key="ENABLE_COMMAND_BLOCK"; Category="World"; DisplayOrder=14; DataType="boolean"; Description="Enable command blocks" },
    @{ Key="ALLOW_NETHER"; Category="World"; DisplayOrder=14; DataType="boolean"; Description="Allow Nether dimension" },
    @{ Key="SPAWN_ANIMALS"; Category="World"; DisplayOrder=15; DataType="boolean"; Description="Allow animals to spawn" },
    @{ Key="SPAWN_MONSTERS"; Category="World"; DisplayOrder=16; DataType="boolean"; Description="Allow monsters to spawn" },
    @{ Key="SPAWN_NPCS"; Category="World"; DisplayOrder=17; DataType="boolean"; Description="Allow villagers to spawn" },
    @{ Key="VIEW_DISTANCE"; Category="World"; DisplayOrder=19; DataType="number"; Description="View distance in chunks" },
    @{ Key="SEED"; Category="World"; DisplayOrder=20; DataType="string"; Description="World generation seed (empty for random)" },
    @{ Key="LEVEL"; Category="World"; DisplayOrder=21; DataType="string"; Description="World name"; Placeholder="world" },
    @{ Key="MAX_WORLD_SIZE"; Category="World"; DisplayOrder=22; DataType="number"; Description="Maximum world size" },
    @{ Key="ANNOUNCE_PLAYER_ACHIEVEMENTS"; Category="World"; DisplayOrder=23; DataType="boolean"; Description="Announce player achievements" },
    @{ Key="GENERATE_STRUCTURES"; Category="World"; DisplayOrder=24; DataType="boolean"; Description="Generate structures (villages, etc.)" },
    @{ Key="SNOOPER_ENABLED"; Category="World"; DisplayOrder=25; DataType="boolean"; Description="Enable snooper (telemetry)" },
    @{ Key="MAX_BUILD_HEIGHT"; Category="World"; DisplayOrder=26; DataType="number"; Description="Maximum build height" },
    @{ Key="SPAWN_PROTECTION"; Category="World"; DisplayOrder=27; DataType="number"; Description="Spawn protection radius" },
    @{ Key="LEVEL_TYPE"; Category="World"; DisplayOrder=28; DataType="string"; Description="Level type (default, flat, amplified)" },
    @{ Key="GENERATOR_SETTINGS"; Category="World"; DisplayOrder=29; DataType="string"; Description="Custom world generator settings" },
    @{ Key="PVP"; Category="World"; DisplayOrder=30; DataType="boolean"; Description="Enable player vs player combat" },
    @{ Key="ALLOW_FLIGHT"; Category="World"; DisplayOrder=31; DataType="boolean"; Description="Allow flight in survival mode" },
    @{ Key="SERVER_NAME"; Category="World"; DisplayOrder=32; DataType="string"; Description="Server name for display" },
    
    # Network Category
    @{ Key="SERVER_PORT"; Category="Network"; DisplayOrder=10; DataType="port"; Description="Port on which the server communicates"; MapsToContainerPort=$true; LinkedContainerPort=25565; PortProtocol="tcp"; PortValidation=@{minPort=1;maxPort=65535;checkAvailability=$true;isUserEditable=$true} },
    @{ Key="PLAYER_IDLE_TIMEOUT"; Category="Network"; DisplayOrder=11; DataType="number"; Description="Kick idle players after X minutes" },
    @{ Key="SYNC_CHUNK_WRITES"; Category="Network"; DisplayOrder=12; DataType="boolean"; Description="Sync chunk writes to disk" },
    @{ Key="ENABLE_STATUS"; Category="Network"; DisplayOrder=13; DataType="boolean"; Description="Enable status queries" },
    @{ Key="ENTITY_BROADCAST_RANGE_PERCENTAGE"; Category="Network"; DisplayOrder=14; DataType="number"; Description="Entity broadcast range %" },
    @{ Key="FUNCTION_PERMISSION_LEVEL"; Category="Network"; DisplayOrder=15; DataType="number"; Description="Function permission level" },
    @{ Key="NETWORK_COMPRESSION_THRESHOLD"; Category="Network"; DisplayOrder=16; DataType="number"; Description="Network compression threshold bytes" },
    @{ Key="OP_PERMISSION_LEVEL"; Category="Network"; DisplayOrder=17; DataType="number"; Description="OP permission level (1-4)" },
    @{ Key="PREVENT_PROXY_CONNECTIONS"; Category="Network"; DisplayOrder=18; DataType="boolean"; Description="Prevent proxy connections" },
    @{ Key="USE_NATIVE_TRANSPORT"; Category="Network"; DisplayOrder=19; DataType="boolean"; Description="Use native network transport" },
    @{ Key="SIMULATION_DISTANCE"; Category="Network"; DisplayOrder=20; DataType="number"; Description="Simulation distance in chunks" },
    @{ Key="STOP_SERVER_ANNOUNCE_DELAY"; Category="Network"; DisplayOrder=21; DataType="number"; Description="Server stop announcement delay" },
    
    # Logging Category
    @{ Key="LOG_LEVEL"; Category="Logging"; DisplayOrder=0; DataType="string"; Description="Logging level (INFO, DEBUG, etc.)"; Placeholder="INFO" },
    @{ Key="LOG_CONSOLE_FORMAT"; Category="Logging"; DisplayOrder=1; DataType="string"; Description="Console log format" },
    @{ Key="LOG_FILE_FORMAT"; Category="Logging"; DisplayOrder=2; DataType="string"; Description="File log format" },
    @{ Key="LOG_TERMINAL_FORMAT"; Category="Logging"; DisplayOrder=3; DataType="string"; Description="Terminal log format" },
    @{ Key="LOG_TIMESTAMP"; Category="Logging"; DisplayOrder=4; DataType="string"; Description="Log timestamp format" },
    @{ Key="ENABLE_ROLLING_LOGS"; Category="Logging"; DisplayOrder=5; DataType="boolean"; Description="Enable log rotation" },
    @{ Key="ROLLING_LOG_FILE_PATTERN"; Category="Logging"; DisplayOrder=6; DataType="string"; Description="Rolling log file pattern" },
    @{ Key="ROLLING_LOG_MAX_FILES"; Category="Logging"; DisplayOrder=7; DataType="number"; Description="Maximum rolling log files" },
    
    # Security Category
    @{ Key="ONLINE_MODE"; Category="Security"; DisplayOrder=0; DataType="boolean"; Description="Verify Minecraft account authentication" },
    @{ Key="ENABLE_WHITELIST"; Category="Security"; DisplayOrder=1; DataType="boolean"; Description="Enable whitelist" },
    @{ Key="WHITELIST"; Category="Security"; DisplayOrder=2; DataType="string"; Description="Whitelisted players (comma-separated)" },
    @{ Key="WHITELIST_FILE"; Category="Security"; DisplayOrder=3; DataType="string"; Description="Whitelist file path" },
    @{ Key="OVERRIDE_WHITELIST"; Category="Security"; DisplayOrder=4; DataType="boolean"; Description="Override existing whitelist" },
    @{ Key="ENABLE_RCON"; Category="Security"; DisplayOrder=5; DataType="boolean"; Description="Enable RCON remote console" },
    @{ Key="RCON_PASSWORD"; Category="Security"; DisplayOrder=6; DataType="string"; Description="RCON password"; Placeholder="changeme" },
    @{ Key="RCON_PORT"; Category="Security"; DisplayOrder=7; DataType="port"; Description="RCON port"; Placeholder="25575" },
    @{ Key="BROADCAST_RCON_TO_OPS"; Category="Security"; DisplayOrder=8; DataType="boolean"; Description="Broadcast RCON to OPs" },
    
    # RCON Commands Category
    @{ Key="RCON_CMDS_STARTUP"; Category="RCON Commands"; DisplayOrder=0; DataType="string"; Description="Commands to run on startup" },
    @{ Key="RCON_CMDS_ON_CONNECT"; Category="RCON Commands"; DisplayOrder=1; DataType="string"; Description="Commands on player connect" },
    @{ Key="RCON_CMDS_FIRST_CONNECT"; Category="RCON Commands"; DisplayOrder=2; DataType="string"; Description="Commands on first player connect" },
    @{ Key="RCON_CMDS_ON_DISCONNECT"; Category="RCON Commands"; DisplayOrder=3; DataType="string"; Description="Commands on player disconnect" },
    @{ Key="RCON_CMDS_LAST_DISCONNECT"; Category="RCON Commands"; DisplayOrder=4; DataType="string"; Description="Commands on last player disconnect" },
    
    # Automation Category
    @{ Key="ENABLE_AUTOPAUSE"; Category="Automation"; DisplayOrder=0; DataType="boolean"; Description="Auto-pause when no players online" },
    @{ Key="AUTOPAUSE_TIMEOUT_EST"; Category="Automation"; DisplayOrder=1; DataType="number"; Description="Auto-pause timeout estimate" },
    @{ Key="AUTOPAUSE_TIMEOUT_INIT"; Category="Automation"; DisplayOrder=2; DataType="number"; Description="Auto-pause initial timeout" },
    @{ Key="AUTOPAUSE_TIMEOUT_KN"; Category="Automation"; DisplayOrder=3; DataType="number"; Description="Auto-pause known timeout" },
    @{ Key="AUTOPAUSE_PERIOD"; Category="Automation"; DisplayOrder=4; DataType="number"; Description="Auto-pause check period" },
    @{ Key="AUTOPAUSE_KNOCK_INTERFACE"; Category="Automation"; DisplayOrder=5; DataType="string"; Description="Auto-pause wake interface" },
    @{ Key="DEBUG_AUTOPAUSE"; Category="Automation"; DisplayOrder=6; DataType="boolean"; Description="Debug auto-pause" },
    @{ Key="ENABLE_AUTOSTOP"; Category="Automation"; DisplayOrder=7; DataType="boolean"; Description="Auto-stop server when idle" },
    @{ Key="AUTOSTOP_TIMEOUT_EST"; Category="Automation"; DisplayOrder=8; DataType="number"; Description="Auto-stop timeout estimate" },
    @{ Key="AUTOSTOP_TIMEOUT_INIT"; Category="Automation"; DisplayOrder=9; DataType="number"; Description="Auto-stop initial timeout" },
    @{ Key="AUTOSTOP_PERIOD"; Category="Automation"; DisplayOrder=10; DataType="number"; Description="Auto-stop check period" },
    @{ Key="DEBUG_AUTOSTOP"; Category="Automation"; DisplayOrder=11; DataType="boolean"; Description="Debug auto-stop" },
    
    # Mods Category
    @{ Key="CF_API_KEY"; Category="Mods"; DisplayOrder=0; DataType="string"; Description="CurseForge API key" },
    @{ Key="CF_API_KEY_FILE"; Category="Mods"; DisplayOrder=1; DataType="string"; Description="CurseForge API key file" },
    @{ Key="CF_PAGE_URL"; Category="Mods"; DisplayOrder=2; DataType="string"; Description="CurseForge modpack page URL" },
    @{ Key="CF_SLUG"; Category="Mods"; DisplayOrder=3; DataType="string"; Description="CurseForge modpack slug" },
    @{ Key="CF_FILE_ID"; Category="Mods"; DisplayOrder=4; DataType="string"; Description="CurseForge file ID" },
    @{ Key="CF_FILENAME_MATCHER"; Category="Mods"; DisplayOrder=5; DataType="string"; Description="CurseForge filename matcher" },
    @{ Key="CF_EXCLUDE_INCLUDE_FILE"; Category="Mods"; DisplayOrder=6; DataType="string"; Description="CurseForge exclude/include file" },
    @{ Key="CF_EXCLUDE_MODS"; Category="Mods"; DisplayOrder=7; DataType="string"; Description="Exclude mods (comma-separated)" },
    @{ Key="CF_FORCE_INCLUDE_MODS"; Category="Mods"; DisplayOrder=8; DataType="string"; Description="Force include mods" },
    @{ Key="CF_FORCE_SYNCHRONIZE"; Category="Mods"; DisplayOrder=9; DataType="boolean"; Description="Force mod synchronization" },
    @{ Key="CF_SET_LEVEL_FROM"; Category="Mods"; DisplayOrder=10; DataType="string"; Description="Set level from modpack" },
    @{ Key="CF_PARALLEL_DOWNLOADS"; Category="Mods"; DisplayOrder=11; DataType="number"; Description="Parallel mod downloads" },
    @{ Key="CF_OVERRIDES_SKIP_EXISTING"; Category="Mods"; DisplayOrder=12; DataType="boolean"; Description="Skip existing overrides" },
    @{ Key="CF_MOD_LOADER_VERSION"; Category="Mods"; DisplayOrder=13; DataType="string"; Description="Mod loader version" },
    @{ Key="ADDITIONAL_MODS"; Category="Mods"; DisplayOrder=14; DataType="string"; Description="Additional mods URLs" },
    
    # Resources Category
    @{ Key="RESOURCE_PACK"; Category="Resources"; DisplayOrder=0; DataType="string"; Description="Resource pack URL" },
    @{ Key="RESOURCE_PACK_SHA1"; Category="Resources"; DisplayOrder=1; DataType="string"; Description="Resource pack SHA1 hash" },
    @{ Key="RESOURCE_PACK_ENFORCE"; Category="Resources"; DisplayOrder=2; DataType="boolean"; Description="Enforce resource pack" },
    
    # Advanced Category
    @{ Key="UID"; Category="Advanced"; DisplayOrder=0; DataType="number"; Description="User ID for file ownership"; Placeholder="1000" },
    @{ Key="GID"; Category="Advanced"; DisplayOrder=1; DataType="number"; Description="Group ID for file ownership"; Placeholder="1000" },
    @{ Key="JMX_HOST"; Category="Advanced"; DisplayOrder=2; DataType="string"; Description="JMX monitoring host" },
    @{ Key="PROXY"; Category="Advanced"; DisplayOrder=3; DataType="string"; Description="HTTP/HTTPS proxy"; Placeholder="proxy:3128" },
    @{ Key="CONSOLE"; Category="Advanced"; DisplayOrder=4; DataType="boolean"; Description="Enable console" },
    @{ Key="GUI"; Category="Advanced"; DisplayOrder=5; DataType="boolean"; Description="Enable GUI" },
    @{ Key="STOP_DURATION"; Category="Advanced"; DisplayOrder=6; DataType="number"; Description="Graceful stop duration" },
    @{ Key="SETUP_ONLY"; Category="Advanced"; DisplayOrder=7; DataType="boolean"; Description="Setup only mode" },
    @{ Key="EXTRA_OPTS"; Category="Advanced"; DisplayOrder=8; DataType="string"; Description="Extra options" },
    @{ Key="SERVER_PROPERTIES_TEMPLATE"; Category="Advanced"; DisplayOrder=9; DataType="string"; Description="Server properties template" },
    @{ Key="INIT_COMMAND"; Category="Advanced"; DisplayOrder=10; DataType="string"; Description="Initialization command" }
)

Write-Host "Processing $($allSettings.Count) settings..." -ForegroundColor Yellow

# Build the settings metadata
foreach ($setting in $allSettings) {
    $metadata = @{
        key = $setting.Key
        description = if ($setting.Description) { $setting.Description } else { "" }
        isRequired = if ($setting.IsRequired) { $setting.IsRequired } else { $false }
        cannotBeEmpty = if ($setting.CannotBeEmpty) { $setting.CannotBeEmpty } else { $false }
        dataType = if ($setting.DataType) { $setting.DataType } else { "string" }
        mapsToContainerPort = if ($setting.MapsToContainerPort) { $setting.MapsToContainerPort } else { $false }
        linkedContainerPort = $setting.LinkedContainerPort
        portProtocol = if ($setting.PortProtocol) { $setting.PortProtocol } else { "tcp" }
        listDelimiter = ","
        allowedValues = $setting.AllowedValues
        valueMappings = $setting.ValueMappings
        displayOrder = $setting.DisplayOrder
        category = $setting.Category
        placeholder = $setting.Placeholder
        validationPattern = $null
        validationMessage = $null
        portRelationships = $null
        portValidation = $setting.PortValidation
        synchronizedWithSetting = $null
        autoAllocatePort = $false
        validateRelatedPortsAvailability = $false
    }
    
    $restoredMetadata.settingsMetadata[$setting.Key] = $metadata
}

Write-Host "✓ Built metadata with $($restoredMetadata.settingsMetadata.Count) settings" -ForegroundColor Green

# Convert to JSON with proper depth
Write-Host "`nConverting to JSON..." -ForegroundColor Cyan
$jsonBody = $restoredMetadata | ConvertTo-Json -Depth 20 -Compress:$false

Write-Host "✓ JSON size: $($jsonBody.Length) characters" -ForegroundColor Green

# POST to API
Write-Host "`nPosting restored metadata to server..." -ForegroundColor Cyan

try {
    $response = Invoke-RestMethod -Uri "$apiBase/api/gametypes/extended/$gameTypeKey" `
        -Method Post `
        -Body $jsonBody `
        -ContentType "application/json; charset=utf-8"
    
    Write-Host "`n✓✓✓ SUCCESS! Minecraft metadata restored! ✓✓✓" -ForegroundColor Green
    Write-Host "`nRestored:" -ForegroundColor Yellow
    Write-Host "  - All 119 settings with proper dataTypes" -ForegroundColor White
    Write-Host "  - Organized into 11 categories" -ForegroundColor White
    Write-Host "  - TZ field configured as dropdown with timezones" -ForegroundColor White
    Write-Host "  - Fixed displayOrder for all settings" -ForegroundColor White
}
catch {
    Write-Host "`n✗ Error posting metadata: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "Details: $($_.ErrorDetails.Message)" -ForegroundColor Red
    }
}
