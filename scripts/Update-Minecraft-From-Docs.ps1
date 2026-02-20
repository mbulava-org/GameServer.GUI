# Update Minecraft Metadata from itzg/docker-minecraft-server Documentation
# Based on official documentation at docker-minecraft-server.readthedocs.io

$apiBase = "http://192.168.10.50:5164"
$gameTypeKey = "minecraft"

Write-Host "Updating Minecraft metadata based on itzg/docker-minecraft-server documentation..." -ForegroundColor Cyan

# Fetch current metadata
$metadata = Invoke-RestMethod -Uri "$apiBase/api/gametypes/extended/$gameTypeKey"
$gameType = Invoke-RestMethod -Uri "$apiBase/api/gametypes/$gameTypeKey"

Write-Host "✓ Fetched current configuration" -ForegroundColor Green

# Update descriptions and configurations based on official documentation
$updates = @{
    # Core Server Settings
    "EULA" = @{
        Description = "Accept Minecraft EULA (must be TRUE to start). See https://www.minecraft.net/eula"
        Placeholder = "TRUE"
    }
    "VERSION" = @{
        Description = "Minecraft version: LATEST, SNAPSHOT, or specific version like 1.21.5"
        Placeholder = "LATEST"
    }
    "TYPE" = @{
        Description = "Server type"
        AllowedValues = @("VANILLA", "PAPER", "SPIGOT", "BUKKIT", "PURPUR", "FABRIC", "FORGE", "NEOFORGE", "QUILT", "MAGMA", "MOHIST", "CATSERVER", "CANYON", "FOLIA")
        ValueMappings = @{
            "VANILLA" = "Official Minecraft server"
            "PAPER" = "High-performance fork with plugins"
            "SPIGOT" = "Popular plugin platform"
            "BUKKIT" = "Original plugin API"
            "PURPUR" = "Paper fork with extra features"
            "FABRIC" = "Lightweight modding platform"
            "FORGE" = "Popular modding platform"
            "NEOFORGE" = "Modern fork of Forge"
            "QUILT" = "Modern modding platform"
            "FOLIA" = "Multi-threaded Paper fork"
        }
    }
    "MEMORY" = @{
        Description = "Java heap memory (e.g., 1G, 2048M, 4G). Used if INIT_MEMORY/MAX_MEMORY not set"
        Placeholder = "1G"
    }
    "INIT_MEMORY" = @{
        Description = "Initial Java heap memory in megabytes"
        Placeholder = "1024"
    }
    "MAX_MEMORY" = @{
        Description = "Maximum Java heap memory in megabytes"
        Placeholder = "2048"
    }
    
    # JVM Optimization Flags
    "USE_AIKAR_FLAGS" = @{
        Description = "Use Aikar's optimized JVM flags for Minecraft (recommended for 8GB+ RAM)"
    }
    "USE_LARGE_PAGES" = @{
        Description = "Enable large pages for better memory performance (requires host configuration)"
    }
    "USE_FLARE_FLAGS" = @{
        Description = "Use Flare profiler flags for performance monitoring"
    }
    
    # Server Properties
    "SERVER_PORT" = @{
        Description = "Server port (default: 25565)"
        Placeholder = "25565"
    }
    "MAX_PLAYERS" = @{
        Description = "Maximum number of players"
        Placeholder = "20"
    }
    "MOTD" = @{
        Description = "Message of the Day shown in server list. Use \\n for newlines"
        Placeholder = "A Minecraft Server"
    }
    "DIFFICULTY" = @{
        Description = "Game difficulty"
        AllowedValues = @("peaceful", "easy", "normal", "hard")
    }
    "MODE" = @{
        Description = "Default game mode"
        AllowedValues = @("survival", "creative", "adventure", "spectator")
        ValueMappings = @{
            "survival" = "Survival Mode"
            "creative" = "Creative Mode"  
            "adventure" = "Adventure Mode"
            "spectator" = "Spectator Mode"
        }
    }
    "LEVEL" = @{
        Description = "World/level name"
        Placeholder = "world"
    }
    "SEED" = @{
        Description = "World generation seed (leave empty for random)"
        Placeholder = ""
    }
    "LEVEL_TYPE" = @{
        Description = "World type"
        AllowedValues = @("default", "flat", "largeBiomes", "amplified", "buffet")
        ValueMappings = @{
            "default" = "Default terrain"
            "flat" = "Superflat world"
            "largeBiomes" = "Large biomes"
            "amplified" = "Extreme hills"
            "buffet" = "Single biome (requires GENERATOR_SETTINGS)"
        }
    }
    "VIEW_DISTANCE" = @{
        Description = "View distance in chunks (2-32). Lower values improve performance"
        Placeholder = "10"
    }
    "SIMULATION_DISTANCE" = @{
        Description = "Simulation distance in chunks (affects mob spawning and crop growth)"
        Placeholder = "10"
    }
    
    # Multiplayer Settings
    "PVP" = @{
        Description = "Enable player vs player combat"
    }
    "ONLINE_MODE" = @{
        Description = "Verify player authentication with Mojang (disable only for LAN/offline)"
    }
    "ALLOW_FLIGHT" = @{
        Description = "Allow flight in survival mode"
    }
    "FORCE_GAMEMODE" = @{
        Description = "Force players into default game mode on join"
    }
    "HARDCORE" = @{
        Description = "Hardcore mode: Players become spectators on death, difficulty locked to hard"
    }
    
    # World Features
    "SPAWN_ANIMALS" = @{
        Description = "Allow passive mobs (animals) to spawn"
    }
    "SPAWN_MONSTERS" = @{
        Description = "Allow hostile mobs (monsters) to spawn"
    }
    "SPAWN_NPCS" = @{
        Description = "Allow villagers (NPCs) to spawn"
    }
    "GENERATE_STRUCTURES" = @{
        Description = "Generate structures (villages, temples, strongholds, etc.)"
    }
    "ALLOW_NETHER" = @{
        Description = "Enable the Nether dimension"
    }
    "ENABLE_COMMAND_BLOCK" = @{
        Description = "Enable command blocks"
    }
    "SPAWN_PROTECTION" = @{
        Description = "Spawn protection radius in blocks (0 to disable)"
        Placeholder = "16"
    }
    
    # RCON (Remote Console)
    "ENABLE_RCON" = @{
        Description = "Enable RCON remote console"
    }
    "RCON_PASSWORD" = @{
        Description = "RCON password (required if RCON enabled)"
        Placeholder = "minecraft"
    }
    "RCON_PORT" = @{
        Description = "RCON port"
        Placeholder = "25575"
    }
    
    # Whitelist
    "ENABLE_WHITELIST" = @{
        Description = "Enable whitelist (only listed players can join)"
    }
    "WHITELIST" = @{
        Description = "Comma or newline-separated player names/UUIDs"
        Placeholder = "player1,player2"
    }
    "OVERRIDE_WHITELIST" = @{
        Description = "Replace existing whitelist file with WHITELIST value"
    }
    
    # Operators
    "OPS" = @{
        Description = "Comma or newline-separated operator player names/UUIDs"
        Placeholder = "admin1,admin2"
    }
    "OVERRIDE_OPS" = @{
        Description = "Replace existing ops file with OPS value"
    }
    
    # Resource Pack
    "RESOURCE_PACK" = @{
        Description = "URL to resource pack ZIP file"
        Placeholder = "https://example.com/resourcepack.zip"
    }
    "RESOURCE_PACK_SHA1" = @{
        Description = "SHA1 hash of resource pack for verification"
    }
    "RESOURCE_PACK_ENFORCE" = @{
        Description = "Require clients to accept resource pack (kick if declined)"
    }
    
    # Advanced Networking
    "NETWORK_COMPRESSION_THRESHOLD" = @{
        Description = "Network compression threshold in bytes (256 default, -1 to disable)"
        Placeholder = "256"
    }
    "MAX_TICK_TIME" = @{
        Description = "Max milliseconds a tick may take before watchdog stops server (-1 to disable)"
        Placeholder = "60000"
    }
    "PLAYER_IDLE_TIMEOUT" = @{
        Description = "Kick idle players after minutes (0 to disable)"
        Placeholder = "0"
    }
    
    # Mods & Plugins
    "MODS" = @{
        Description = "Comma-separated URLs or file paths to mods"
    }
    "PLUGINS" = @{
        Description = "Comma-separated URLs or file paths to plugins"
    }
    "REMOVE_OLD_MODS" = @{
        Description = "Remove mods not in MODS list (cleanup)"
    }
    "REMOVE_OLD_PLUGINS" = @{
        Description = "Remove plugins not in PLUGINS list (cleanup)"
    }
    
    # CurseForge
    "CF_API_KEY" = @{
        Description = "CurseForge API key (get from https://console.curseforge.com/)"
    }
    "CF_PAGE_URL" = @{
        Description = "CurseForge modpack page URL"
        Placeholder = "https://www.curseforge.com/minecraft/modpacks/example"
    }
    "CF_SLUG" = @{
        Description = "CurseForge modpack slug (alternative to CF_PAGE_URL)"
        Placeholder = "example-modpack"
    }
    "CF_FILE_ID" = @{
        Description = "Specific CurseForge file ID to install"
    }
    
    # Modrinth
    "MODRINTH_PROJECT" = @{
        Description = "Modrinth modpack project ID or slug"
    }
    "MODRINTH_VERSION" = @{
        Description = "Specific Modrinth version ID (leave empty for latest)"
    }
    
    # Autopause/Autostop
    "ENABLE_AUTOPAUSE" = @{
        Description = "Automatically pause server when no players online (saves CPU)"
    }
    "AUTOPAUSE_TIMEOUT_EST" = @{
        Description = "Seconds to wait before checking if server is idle"
        Placeholder = "3600"
    }
    "ENABLE_AUTOSTOP" = @{
        Description = "Automatically stop container when no players online"
    }
    "AUTOSTOP_TIMEOUT_EST" = @{
        Description = "Seconds to wait before stopping server when idle"
        Placeholder = "3600"
    }
    
    # Logging
    "LOG_LEVEL" = @{
        Description = "Logging level: DEBUG, INFO, WARN, ERROR"
        Placeholder = "INFO"
        AllowedValues = @("DEBUG", "INFO", "WARN", "ERROR")
    }
    "ENABLE_ROLLING_LOGS" = @{
        Description = "Enable log rotation to prevent single large log file"
    }
    
    # System
    "TZ" = @{
        Description = "Server timezone for log timestamps and scheduling (IANA timezone)"
        Placeholder = "America/Chicago"
    }
    "UID" = @{
        Description = "User ID for file ownership (useful for volume permissions)"
        Placeholder = "1000"
    }
    "GID" = @{
        Description = "Group ID for file ownership (useful for volume permissions)"
        Placeholder = "1000"
    }
}

# Apply updates
$updateCount = 0
foreach ($key in $updates.Keys) {
    if ($metadata.settingsMetadata.$key) {
        $setting = $metadata.settingsMetadata.$key
        $update = $updates[$key]
        
        if ($update.Description) {
            $setting.description = $update.Description
            $updateCount++
        }
        if ($update.Placeholder) {
            $setting.placeholder = $update.Placeholder
        }
        if ($update.AllowedValues) {
            $setting.allowedValues = $update.AllowedValues
            $setting.dataType = "enum"
        }
        if ($update.ValueMappings) {
            $setting.valueMappings = $update.ValueMappings
        }
    }
}

Write-Host "✓ Updated $updateCount setting descriptions" -ForegroundColor Green

# Update GameType defaults based on itzg/minecraft-server best practices
$gameType.defaultSettings["EULA"] = "TRUE"
$gameType.defaultSettings["VERSION"] = "LATEST"
$gameType.defaultSettings["TYPE"] = "PAPER"  # Paper is recommended for best performance
$gameType.defaultSettings["MEMORY"] = "2G"
$gameType.defaultSettings["DIFFICULTY"] = "normal"
$gameType.defaultSettings["MODE"] = "survival"
$gameType.defaultSettings["VIEW_DISTANCE"] = "10"
$gameType.defaultSettings["SIMULATION_DISTANCE"] = "10"
$gameType.defaultSettings["ENABLE_ROLLING_LOGS"] = "true"
$gameType.defaultSettings["TZ"] = "America/Chicago"
$gameType.defaultSettings["ONLINE_MODE"] = "true"
$gameType.defaultSettings["ENABLE_COMMAND_BLOCK"] = "false"
$gameType.defaultSettings["ENABLE_RCON"] = "false"
$gameType.defaultSettings["ENABLE_WHITELIST"] = "false"
$gameType.defaultSettings["PVP"] = "true"
$gameType.defaultSettings["SPAWN_ANIMALS"] = "true"
$gameType.defaultSettings["SPAWN_MONSTERS"] = "true"
$gameType.defaultSettings["SPAWN_NPCS"] = "true"
$gameType.defaultSettings["ALLOW_NETHER"] = "true"
$gameType.defaultSettings["GENERATE_STRUCTURES"] = "true"

Write-Host "✓ Updated GameType defaults" -ForegroundColor Green

# Convert and POST
Write-Host "`nSaving updated configuration..." -ForegroundColor Cyan

$metadataJson = $metadata | ConvertTo-Json -Depth 20
$gameTypeJson = $gameType | ConvertTo-Json -Depth 10

try {
    Invoke-RestMethod -Uri "$apiBase/api/gametypes/extended/$gameTypeKey" -Method Post -Body $metadataJson -ContentType "application/json" | Out-Null
    Write-Host "✓ Updated extended metadata" -ForegroundColor Green
    
    Invoke-RestMethod -Uri "$apiBase/api/gametypes/$gameTypeKey" -Method Put -Body $gameTypeJson -ContentType "application/json" | Out-Null
    Write-Host "✓ Updated GameType defaults" -ForegroundColor Green
    
    Write-Host "`n✓✓✓ SUCCESS! Minecraft configuration updated with official documentation! ✓✓✓" -ForegroundColor Green
    Write-Host "`nUpdates include:" -ForegroundColor Yellow
    Write-Host "  - Accurate descriptions from docker-minecraft-server docs" -ForegroundColor White
    Write-Host "  - Proper placeholder values" -ForegroundColor White
    Write-Host "  - Enhanced server type options (PAPER, PURPUR, FABRIC, FORGE, etc.)" -ForegroundColor White
    Write-Host "  - Game mode with friendly labels" -ForegroundColor White
    Write-Host "  - Best practice defaults (PAPER server, 2G memory, rolling logs)" -ForegroundColor White
}
catch {
    Write-Host "✗ Error: $($_.Exception.Message)" -ForegroundColor Red
}
