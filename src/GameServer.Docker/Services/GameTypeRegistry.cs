using GameServer.Docker.Interfaces;
using GameServer.Docker.Models;

namespace GameServer.Docker.Services
{
    public class GameTypeRegistry : IGameTypeRegistry
    {
        private readonly Dictionary<string, GameTypeDefinition> _definitions = new();

        public static GameTypeDefinition MinecraftV1 =>
            new GameTypeDefinition
            {
                Key = "minecraft",
                DisplayName = "Minecraft",
                Description = "Java Edition server using itzg/minecraft-server",
                Image = "itzg/minecraft-server",
                ThumbnailUrl = "https://www.minecraft.net/content/dam/games/minecraft/key-art/Vanilla-PMP_Collection-Carousel-0_Buzzy-Bees_1280x768.jpg",
                DocumentationUrl = "https://github.com/itzg/docker-minecraft-server",

                Ports = new()
                {
                    new PortDefinition(25565, "tcp", true) // Mark as default port
                },

                Volumes = new()
                {
                    new VolumeDefinition("", "/data")
                },

                // DefaultSettings uses exact environment variable names (verbatim casing)
                // populated from the docker-minecraft-server variables documentation.
                DefaultSettings = new()
                {
                    // General options
                    ["UID"] = "1000",
                    ["GID"] = "1000",
                    ["MEMORY"] = "1G",
                    ["INIT_MEMORY"] = "1G",
                    ["MAX_MEMORY"] = "1G",
                    ["TZ"] = "UTC",
                    ["LOG_LEVEL"] = "info",
                    ["LOG_CONSOLE_FORMAT"] = "[%d{HH:mm:ss}] [%t/%level]: %msg%n",
                    ["LOG_FILE_FORMAT"] = "[%d{HH:mm:ss}] [%t/%level]: %msg%n",
                    ["LOG_TERMINAL_FORMAT"] = "[%d{HH:mm:ss} %level]: %msg%n",
                    ["ROLLING_LOG_FILE_PATTERN"] = "logs/%d{yyyy-MM-dd}-%i.log.gz",
                    ["ROLLING_LOG_MAX_FILES"] = "1000",
                    ["ENABLE_ROLLING_LOGS"] = "false",
                    ["ENABLE_JMX"] = "false",
                    ["JMX_HOST"] = "",
                    ["USE_AIKAR_FLAGS"] = "false",
                    ["USE_MEOWICE_FLAGS"] = "false",
                    ["USE_MEOWICE_GRAALVM_FLAGS"] = "true",
                    ["JVM_OPTS"] = "",
                    ["JVM_XX_OPTS"] = "",
                    ["JVM_DD_OPTS"] = "",
                    ["EXTRA_ARGS"] = "",
                    ["LOG_TIMESTAMP"] = "false",

                    // Server
                    ["TYPE"] = "VANILLA",
                    ["EULA"] = "false",
                    ["VERSION"] = "LATEST",
                    ["MOTD"] = "",
                    ["DIFFICULTY"] = "easy",
                    ["ICON"] = "",
                    ["OVERRIDE_ICON"] = "false",
                    ["MAX_PLAYERS"] = "20",
                    ["MAX_WORLD_SIZE"] = "",
                    ["ALLOW_NETHER"] = "true",
                    ["ANNOUNCE_PLAYER_ACHIEVEMENTS"] = "true",
                    ["ENABLE_COMMAND_BLOCK"] = "",
                    ["FORCE_GAMEMODE"] = "false",
                    ["GENERATE_STRUCTURES"] = "true",
                    ["HARDCORE"] = "false",
                    ["SNOOPER_ENABLED"] = "true",
                    ["MAX_BUILD_HEIGHT"] = "256",
                    ["SPAWN_ANIMALS"] = "true",
                    ["SPAWN_MONSTERS"] = "true",
                    ["SPAWN_NPCS"] = "true",
                    ["SPAWN_PROTECTION"] = "",
                    ["VIEW_DISTANCE"] = "",
                    ["SEED"] = "",
                    ["MODE"] = "",
                    ["PVP"] = "true",
                    ["LEVEL_TYPE"] = "minecraft:default",
                    ["GENERATOR_SETTINGS"] = "",
                    ["LEVEL"] = "world",
                    ["ONLINE_MODE"] = "true",
                    ["ALLOW_FLIGHT"] = "false",
                    ["SERVER_NAME"] = "",
                    ["SERVER_PORT"] = "",
                    ["PLAYER_IDLE_TIMEOUT"] = "",
                    ["SYNC_CHUNK_WRITES"] = "",
                    ["ENABLE_STATUS"] = "",
                    ["ENTITY_BROADCAST_RANGE_PERCENTAGE"] = "",
                    ["FUNCTION_PERMISSION_LEVEL"] = "",
                    ["NETWORK_COMPRESSION_THRESHOLD"] = "",
                    ["OP_PERMISSION_LEVEL"] = "",
                    ["PREVENT_PROXY_CONNECTIONS"] = "",
                    ["USE_NATIVE_TRANSPORT"] = "",
                    ["SIMULATION_DISTANCE"] = "",
                    ["STOP_SERVER_ANNOUNCE_DELAY"] = "",
                    ["PROXY"] = "false",
                    ["CONSOLE"] = "true",
                    ["GUI"] = "true",
                    ["STOP_DURATION"] = "60",
                    ["SETUP_ONLY"] = "false",
                    ["USE_FLARE_FLAGS"] = "",
                    ["USE_SIMD_FLAGS"] = "false",

                    // Custom resource pack
                    ["RESOURCE_PACK"] = "",
                    ["RESOURCE_PACK_SHA1"] = "",
                    ["RESOURCE_PACK_ENFORCE"] = "false",

                    // Whitelist
                    ["ENABLE_WHITELIST"] = "false",
                    ["WHITELIST"] = "",
                    ["WHITELIST_FILE"] = "",
                    ["OVERRIDE_WHITELIST"] = "false",

                    // RCON
                    ["ENABLE_RCON"] = "true",
                    ["RCON_PASSWORD"] = "",
                    ["RCON_PORT"] = "25575",
                    ["BROADCAST_RCON_TO_OPS"] = "false",
                    ["RCON_CMDS_STARTUP"] = "",
                    ["RCON_CMDS_ON_CONNECT"] = "",
                    ["RCON_CMDS_FIRST_CONNECT"] = "",
                    ["RCON_CMDS_ON_DISCONNECT"] = "",
                    ["RCON_CMDS_LAST_DISCONNECT"] = "",

                    // Auto-Pause
                    ["ENABLE_AUTOPAUSE"] = "false",
                    ["AUTOPAUSE_TIMEOUT_EST"] = "3600",
                    ["AUTOPAUSE_TIMEOUT_INIT"] = "600",
                    ["AUTOPAUSE_TIMEOUT_KN"] = "120",
                    ["AUTOPAUSE_PERIOD"] = "10",
                    ["AUTOPAUSE_KNOCK_INTERFACE"] = "eth0",
                    ["DEBUG_AUTOPAUSE"] = "false",

                    // Auto-Stop
                    ["ENABLE_AUTOSTOP"] = "false",
                    ["AUTOSTOP_TIMEOUT_EST"] = "3600",
                    ["AUTOSTOP_TIMEOUT_INIT"] = "1800",
                    ["AUTOSTOP_PERIOD"] = "10",
                    ["DEBUG_AUTOSTOP"] = "false",

                    // CurseForge / Mod management (CF_)
                    ["CF_API_KEY"] = "",
                    ["CF_API_KEY_FILE"] = "",
                    ["CF_PAGE_URL"] = "",
                    ["CF_SLUG"] = "",
                    ["CF_FILE_ID"] = "",
                    ["CF_FILENAME_MATCHER"] = "",
                    ["CF_EXCLUDE_INCLUDE_FILE"] = "",
                    ["CF_EXCLUDE_MODS"] = "",
                    ["CF_FORCE_INCLUDE_MODS"] = "",
                    ["CF_FORCE_SYNCHRONIZE"] = "",
                    ["CF_SET_LEVEL_FROM"] = "",
                    ["CF_PARALLEL_DOWNLOADS"] = "4",
                    ["CF_OVERRIDES_SKIP_EXISTING"] = "false",
                    ["CF_MOD_LOADER_VERSION"] = "",

                    // placeholders for additional options
                    ["ADDITIONAL_MODS"] = "",
                    ["EXTRA_OPTS"] = "",
                    ["SERVER_PROPERTIES_TEMPLATE"] = "",
                    ["INIT_COMMAND"] = ""
                }
            };

        public static GameTypeDefinition MinecraftBedrockV1 =>
            new GameTypeDefinition
            {
                Key = "minecraft-bedrock",
                DisplayName = "Minecraft Bedrock Edition",
                Description = "Bedrock Edition server using itzg/minecraft-bedrock-server",
                Image = "itzg/minecraft-bedrock-server",
                ThumbnailUrl = "https://lutris.net/media/igdb/c4ed8ee43ea6c38fadbe895e5594122c.png",
                DocumentationUrl = "https://github.com/itzg/docker-minecraft-bedrock-server",

                Ports = new()
                {
                    new PortDefinition(19132, "udp"),
                    new PortDefinition(19133, "udp")
                },

                Volumes = new()
                {
                    new VolumeDefinition("", "/data")
                },

                // DefaultSettings uses exact environment variable names (verbatim casing)
                // populated from the docker-minecraft-bedrock-server documentation
                DefaultSettings = new()
                {
                    // Container Specific
                    ["EULA"] = "FALSE",
                    ["VERSION"] = "LATEST",
                    ["UID"] = "",
                    ["GID"] = "",
                    ["TZ"] = "",
                    ["PACKAGE_BACKUP_KEEP"] = "2",
                    ["DIRECT_DOWNLOAD_URL"] = "",
                    ["ENABLE_SSH"] = "false",
                    
                    // Server Properties (from server.properties)
                    ["SERVER_NAME"] = "Dedicated Server",
                    ["GAMEMODE"] = "survival",
                    ["FORCE_GAMEMODE"] = "false",
                    ["DIFFICULTY"] = "easy",
                    ["ALLOW_CHEATS"] = "false",
                    ["MAX_PLAYERS"] = "10",
                    ["ONLINE_MODE"] = "true",
                    ["WHITE_LIST"] = "false",
                    ["ALLOW_LIST"] = "false",
                    ["SERVER_PORT"] = "19132",
                    ["SERVER_PORT_V6"] = "19133",
                    ["ENABLE_LAN_VISIBILITY"] = "true",
                    ["VIEW_DISTANCE"] = "32",
                    ["TICK_DISTANCE"] = "4",
                    ["PLAYER_IDLE_TIMEOUT"] = "30",
                    ["MAX_THREADS"] = "8",
                    ["LEVEL_NAME"] = "Bedrock level",
                    ["LEVEL_SEED"] = "",
                    ["LEVEL_TYPE"] = "DEFAULT",
                    ["DEFAULT_PLAYER_PERMISSION_LEVEL"] = "member",
                    ["TEXTUREPACK_REQUIRED"] = "false",
                    
                    // Content and Logging
                    ["CONTENT_LOG_FILE_ENABLED"] = "false",
                    ["CONTENT_LOG_LEVEL"] = "info",
                    ["CONTENT_LOG_CONSOLE_OUTPUT_ENABLED"] = "true",
                    
                    // Compression
                    ["COMPRESSION_THRESHOLD"] = "1",
                    ["COMPRESSION_ALGORITHM"] = "zlib",
                    
                    // Server Authoritative Movement
                    ["SERVER_AUTHORITATIVE_MOVEMENT"] = "server-auth",
                    ["PLAYER_POSITION_ACCEPTANCE_THRESHOLD"] = "0.5",
                    ["PLAYER_MOVEMENT_SCORE_THRESHOLD"] = "20",
                    ["PLAYER_MOVEMENT_ACTION_DIRECTION_THRESHOLD"] = "0.85",
                    ["PLAYER_MOVEMENT_DISTANCE_THRESHOLD"] = "0.3",
                    ["PLAYER_MOVEMENT_DURATION_THRESHOLD_IN_MS"] = "500",
                    ["CORRECT_PLAYER_MOVEMENT"] = "false",
                    
                    // Server Authoritative Block Breaking
                    ["SERVER_AUTHORITATIVE_BLOCK_BREAKING"] = "false",
                    ["SERVER_AUTHORITATIVE_BLOCK_BREAKING_PICK_RANGE_SCALAR"] = "1.5",
                    
                    // Chat and Player Interaction
                    ["CHAT_RESTRICTION"] = "None",
                    ["DISABLE_PLAYER_INTERACTION"] = "false",
                    
                    // Client-side features
                    ["CLIENT_SIDE_CHUNK_GENERATION_ENABLED"] = "true",
                    ["BLOCK_NETWORK_IDS_ARE_HASHES"] = "true",
                    ["DISABLE_PERSONA"] = "false",
                    ["DISABLE_CUSTOM_SKINS"] = "false",
                    
                    // Server Build
                    ["SERVER_BUILD_RADIUS_RATIO"] = "Disabled",
                    
                    // Script Debugging
                    ["ALLOW_OUTBOUND_SCRIPT_DEBUGGING"] = "false",
                    ["ALLOW_INBOUND_SCRIPT_DEBUGGING"] = "false",
                    ["FORCE_INBOUND_DEBUG_PORT"] = "false",
                    ["SCRIPT_DEBUGGER_AUTO_ATTACH"] = "disabled",
                    ["SCRIPT_DEBUGGER_AUTO_ATTACH_CONNECT_ADDRESS"] = "",
                    
                    // Script Watchdog
                    ["SCRIPT_WATCHDOG_ENABLE"] = "true",
                    ["SCRIPT_WATCHDOG_ENABLE_EXCEPTION_HANDLING"] = "true",
                    ["SCRIPT_WATCHDOG_ENABLE_SHUTDOWN"] = "true",
                    ["SCRIPT_WATCHDOG_HANG_EXCEPTION"] = "true",
                    ["SCRIPT_WATCHDOG_HANG_THRESHOLD"] = "10000",
                    ["SCRIPT_WATCHDOG_SPIKE_THRESHOLD"] = "100",
                    ["SCRIPT_WATCHDOG_SLOW_THRESHOLD"] = "100",
                    ["SCRIPT_WATCHDOG_MEMORY_WARNING"] = "100",
                    ["SCRIPT_WATCHDOG_MEMORY_LIMIT"] = "250",
                    
                    // Permissions
                    ["OP_PERMISSION_LEVEL"] = "4",
                    ["OPS"] = "",
                    ["MEMBERS"] = "",
                    ["VISITORS"] = "",
                    ["ALLOW_LIST_USERS"] = "",
                    
                    // Telemetry and Logging
                    ["EMIT_SERVER_TELEMETRY"] = "false",
                    ["MSA_GAMERTAGS_ONLY"] = "false",
                    ["ITEM_TRANSACTION_LOGGING_ENABLED"] = "true",
                    
                    // Variables (custom server variables)
                    ["VARIABLES"] = ""
                }
            };

        public static GameTypeDefinition ValhiemV1 =>
            new GameTypeDefinition
            {
                Key = "valheim",
                DisplayName = "Valheim",
                Description = "Valheim dedicated server using lloesche/valheim-server",
                Image = "lloesche/valheim-server",
                ThumbnailUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/892970/header.jpg",
                DocumentationUrl = "https://github.com/lloesche/valheim-server-docker",

                Ports = new()
                {
                    new PortDefinition(2456, "udp"),
                    new PortDefinition(2457, "udp"),
                    new PortDefinition(2458, "udp")
                },

                Volumes = new()
                {
                    new VolumeDefinition("", "/config"),
                    new VolumeDefinition("", "/opt/valheim")
                },

                // DefaultSettings uses exact environment variable names (verbatim casing)
                // populated from the lloesche/valheim-server Docker Hub documentation
                DefaultSettings = new()
                {
                    // Basic server configuration
                    ["SERVER_NAME"] = "My Server",
                    ["SERVER_PORT"] = "2456",
                    ["WORLD_NAME"] = "Dedicated",
                    ["SERVER_PASS"] = "secret",
                    ["SERVER_PUBLIC"] = "true",
                    ["SERVER_ARGS"] = "",
                    
                    // Admin and access control
                    ["ADMINLIST_IDS"] = "",
                    ["BANNEDLIST_IDS"] = "",
                    ["PERMITTEDLIST_IDS"] = "",
                    
                    // Update and restart scheduling
                    ["UPDATE_CRON"] = "*/15 * * * *",
                    ["UPDATE_IF_IDLE"] = "true",
                    ["RESTART_CRON"] = "0 5 * * *",
                    ["RESTART_IF_IDLE"] = "true",
                    ["TZ"] = "Etc/UTC",
                    
                    // Backup configuration
                    ["BACKUPS"] = "true",
                    ["BACKUPS_CRON"] = "0 * * * *",
                    ["BACKUPS_DIRECTORY"] = "/config/backups",
                    ["BACKUPS_MAX_AGE"] = "3",
                    ["BACKUPS_MAX_COUNT"] = "0",
                    ["BACKUPS_IF_IDLE"] = "true",
                    ["BACKUPS_IDLE_GRACE_PERIOD"] = "3600",
                    
                    // Permissions and system
                    ["PERMISSIONS_UMASK"] = "022",
                    ["STEAMCMD_ARGS"] = "validate",
                    ["PUID"] = "0",
                    ["PGID"] = "0",
                    
                    // Mod support
                    ["VALHEIM_PLUS"] = "false",
                    ["BEPINEX"] = "false",
                    
                    // Supervisor HTTP server
                    ["SUPERVISOR_HTTP"] = "false",
                    ["SUPERVISOR_HTTP_PORT"] = "9001",
                    ["SUPERVISOR_HTTP_USER"] = "admin",
                    ["SUPERVISOR_HTTP_PASS"] = "",
                    
                    // Status HTTP server
                    ["STATUS_HTTP"] = "false",
                    ["STATUS_HTTP_PORT"] = "80",
                    ["STATUS_HTTP_CONF"] = "/config/httpd.conf",
                    ["STATUS_HTTP_HTDOCS"] = "/opt/valheim/htdocs",
                    
                    // Remote syslog
                    ["SYSLOG_REMOTE_HOST"] = "",
                    ["SYSLOG_REMOTE_PORT"] = "514",
                    ["SYSLOG_REMOTE_AND_LOCAL"] = "true",
                    
                    // Log filtering
                    ["VALHEIM_LOG_FILTER_EMPTY"] = "true",
                    ["VALHEIM_LOG_FILTER_UTF8"] = "true",
                    ["VALHEIM_LOG_FILTER_MATCH"] = " ",
                    ["VALHEIM_LOG_FILTER_STARTSWITH"] = "(Filename:",
                    ["VALHEIM_LOG_FILTER_ENDSWITH"] = "",
                    ["VALHEIM_LOG_FILTER_CONTAINS"] = "",
                    ["VALHEIM_LOG_FILTER_REGEXP"] = ""
                }
            };

        public static GameTypeDefinition PalworldV1 =>
            new GameTypeDefinition
            {
                Key = "palworld",
                DisplayName = "Palworld",
                Description = "Palworld dedicated server using thijsvanloef/palworld-server-docker",
                Image = "thijsvanloef/palworld-server-docker",
                ThumbnailUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/1623730/header.jpg",
                DocumentationUrl = "https://github.com/thijsvanloef/palworld-server-docker",

                Ports = new()
                {
                    new PortDefinition(8211, "udp"),
                    new PortDefinition(8212, "tcp"),
                    new PortDefinition(27015, "udp"),
                    new PortDefinition(25575, "tcp")
                },

                Volumes = new()
                {
                    new VolumeDefinition("", "/palworld")
                },

                // DefaultSettings uses exact environment variable names (verbatim casing)
                // populated from the palworld-server-docker documentation
                DefaultSettings = new()
                {
                    // Server Settings - Container Specific
                    ["TZ"] = "UTC",
                    ["PLAYERS"] = "16",
                    ["PORT"] = "8211",
                    ["PUID"] = "1000",
                    ["PGID"] = "1000",
                    ["MULTITHREADING"] = "false",
                    ["COMMUNITY"] = "false",
                    ["PUBLIC_IP"] = "",
                    ["PUBLIC_PORT"] = "",
                    ["SERVER_NAME"] = "",
                    ["SERVER_DESCRIPTION"] = "",
                    ["SERVER_PASSWORD"] = "",
                    ["ADMIN_PASSWORD"] = "",
                    
                    // Server Settings - Update and Boot
                    ["UPDATE_ON_BOOT"] = "true",
                    ["RCON_ENABLED"] = "false",
                    ["RCON_PORT"] = "25575",
                    ["REST_API_ENABLED"] = "true",
                    ["REST_API_PORT"] = "8212",
                    ["QUERY_PORT"] = "27015",
                    ["ALLOW_CONNECT_PLATFORM"] = "Steam",
                    
                    // Server Settings - Backup Configuration
                    ["BACKUP_CRON_EXPRESSION"] = "0 0 * * *",
                    ["BACKUP_ENABLED"] = "true",
                    ["USE_BACKUP_SAVE_DATA"] = "true",
                    ["DELETE_OLD_BACKUPS"] = "false",
                    ["OLD_BACKUP_DAYS"] = "30",
                    
                    // Server Settings - Auto Update Configuration
                    ["AUTO_UPDATE_CRON_EXPRESSION"] = "0 * * * *",
                    ["AUTO_UPDATE_ENABLED"] = "false",
                    ["AUTO_UPDATE_WARN_MINUTES"] = "30",
                    
                    // Server Settings - Auto Reboot Configuration
                    ["AUTO_REBOOT_CRON_EXPRESSION"] = "0 0 * * *",
                    ["AUTO_REBOOT_ENABLED"] = "false",
                    ["AUTO_REBOOT_WARN_MINUTES"] = "5",
                    ["AUTO_REBOOT_EVEN_IF_PLAYERS_ONLINE"] = "false",
                    
                    // Server Settings - Auto Pause Configuration
                    ["AUTO_PAUSE_ENABLED"] = "false",
                    ["AUTO_PAUSE_TIMEOUT_EST"] = "180",
                    ["AUTO_PAUSE_LOG"] = "true",
                    ["AUTO_PAUSE_DEBUG"] = "false",
                    
                    // Server Settings - Version Control
                    ["TARGET_MANIFEST_ID"] = "",
                    ["STEAM_USERNAME"] = "",
                    ["STEAM_PASSWORD"] = "",
                    ["INSTALL_BETA_INSIDER"] = "false",
                    
                    // Server Settings - Discord Webhook Configuration
                    ["DISCORD_WEBHOOK_URL"] = "",
                    ["DISCORD_SUPPRESS_NOTIFICATIONS"] = "false",
                    ["DISCORD_CONNECT_TIMEOUT"] = "30",
                    ["DISCORD_MAX_TIMEOUT"] = "30",
                    ["DISCORD_PRE_UPDATE_BOOT_MESSAGE"] = "Server is updating...",
                    ["DISCORD_PRE_UPDATE_BOOT_MESSAGE_ENABLED"] = "true",
                    ["DISCORD_PRE_UPDATE_BOOT_MESSAGE_URL"] = "",
                    ["DISCORD_POST_UPDATE_BOOT_MESSAGE"] = "Server update complete!",
                    ["DISCORD_POST_UPDATE_BOOT_MESSAGE_ENABLED"] = "true",
                    ["DISCORD_POST_UPDATE_BOOT_MESSAGE_URL"] = "",
                    ["DISCORD_PRE_START_MESSAGE"] = "Server has been started!",
                    ["DISCORD_PRE_START_MESSAGE_ENABLED"] = "true",
                    ["DISCORD_PRE_START_MESSAGE_URL"] = "",
                    ["DISCORD_PRE_SHUTDOWN_MESSAGE"] = "Server is shutting down...",
                    ["DISCORD_PRE_SHUTDOWN_MESSAGE_ENABLED"] = "true",
                    ["DISCORD_PRE_SHUTDOWN_MESSAGE_URL"] = "",
                    ["DISCORD_POST_SHUTDOWN_MESSAGE"] = "Server is stopped!",
                    ["DISCORD_POST_SHUTDOWN_MESSAGE_ENABLED"] = "true",
                    ["DISCORD_POST_SHUTDOWN_MESSAGE_URL"] = "",
                    ["DISCORD_PLAYER_JOIN_MESSAGE"] = "player_name has joined Palworld!",
                    ["DISCORD_PLAYER_JOIN_MESSAGE_ENABLED"] = "true",
                    ["DISCORD_PLAYER_JOIN_MESSAGE_URL"] = "",
                    ["DISCORD_PLAYER_LEAVE_MESSAGE"] = "player_name has left Palworld.",
                    ["DISCORD_PLAYER_LEAVE_MESSAGE_ENABLED"] = "true",
                    ["DISCORD_PLAYER_LEAVE_MESSAGE_URL"] = "",
                    ["DISCORD_PRE_BACKUP_MESSAGE"] = "Creating backup...",
                    ["DISCORD_PRE_BACKUP_MESSAGE_ENABLED"] = "true",
                    ["DISCORD_PRE_BACKUP_MESSAGE_URL"] = "",
                    ["DISCORD_POST_BACKUP_MESSAGE"] = "Backup created at file_path",
                    ["DISCORD_POST_BACKUP_MESSAGE_ENABLED"] = "true",
                    ["DISCORD_POST_BACKUP_MESSAGE_URL"] = "",
                    ["DISCORD_PRE_BACKUP_DELETE_MESSAGE"] = "Removing backups older than old_backup_days days",
                    ["DISCORD_PRE_BACKUP_DELETE_MESSAGE_ENABLED"] = "true",
                    ["DISCORD_PRE_BACKUP_DELETE_MESSAGE_URL"] = "",
                    ["DISCORD_POST_BACKUP_DELETE_MESSAGE"] = "Removed backups older than old_backup_days days",
                    ["DISCORD_POST_BACKUP_DELETE_MESSAGE_ENABLED"] = "true",
                    ["DISCORD_POST_BACKUP_DELETE_MESSAGE_URL"] = "",
                    ["DISCORD_ERR_BACKUP_DELETE_MESSAGE"] = "Unable to delete old backups, OLD_BACKUP_DAYS is not an integer. OLD_BACKUP_DAYS=old_backup_days",
                    ["DISCORD_ERR_BACKUP_DELETE_MESSAGE_ENABLED"] = "true",
                    ["DISCORD_ERR_BACKUP_DELETE_MESSAGE_URL"] = "",
                    
                    // Server Settings - Generation Options
                    ["DISABLE_GENERATE_SETTINGS"] = "false",
                    ["DISABLE_GENERATE_ENGINE"] = "true",
                    ["ENABLE_PLAYER_LOGGING"] = "true",
                    ["PLAYER_LOGGING_POLL_PERIOD"] = "5",
                    ["USE_DEPOT_DOWNLOADER"] = "false",
                    ["LOG_FILTER_ENABLED"] = "true",
                    ["LOG_FORMAT_TYPE"] = "default",
                    
                    // Game Settings - Gameplay Difficulty
                    ["DIFFICULTY"] = "None",
                    ["RANDOMIZER_TYPE"] = "None",
                    ["RANDOMIZER_SEED"] = "",
                    ["IS_RANDOMIZER_PAL_LEVEL_RANDOM"] = "False",
                    ["DAYTIME_SPEEDRATE"] = "1.000000",
                    ["NIGHTTIME_SPEEDRATE"] = "1.000000",
                    ["EXP_RATE"] = "1.000000",
                    ["PAL_CAPTURE_RATE"] = "1.000000",
                    ["PAL_SPAWN_NUM_RATE"] = "1.000000",
                    
                    // Game Settings - Damage and Combat
                    ["PAL_DAMAGE_RATE_ATTACK"] = "1.000000",
                    ["PAL_DAMAGE_RATE_DEFENSE"] = "1.000000",
                    ["PLAYER_DAMAGE_RATE_ATTACK"] = "1.000000",
                    ["PLAYER_DAMAGE_RATE_DEFENSE"] = "1.000000",
                    
                    // Game Settings - Player Stats
                    ["PLAYER_STOMACH_DECREASE_RATE"] = "1.000000",
                    ["PLAYER_STAMINA_DECREASE_RATE"] = "1.000000",
                    ["PLAYER_AUTO_HP_REGEN_RATE"] = "1.000000",
                    ["PLAYER_AUTO_HP_REGEN_RATE_IN_SLEEP"] = "1.000000",
                    
                    // Game Settings - Pal Stats
                    ["PAL_STOMACH_DECREASE_RATE"] = "1.000000",
                    ["PAL_STAMINA_DECREASE_RATE"] = "1.000000",
                    ["PAL_AUTO_HP_REGEN_RATE"] = "1.000000",
                    ["PAL_AUTO_HP_REGEN_RATE_IN_SLEEP"] = "1.000000",
                    
                    // Game Settings - Building and Objects
                    ["BUILD_OBJECT_HP_RATE"] = "1.000000",
                    ["BUILD_OBJECT_DAMAGE_RATE"] = "1.000000",
                    ["BUILD_OBJECT_DETERIORATION_DAMAGE_RATE"] = "1.000000",
                    
                    // Game Settings - Collection and Resources
                    ["COLLECTION_DROP_RATE"] = "1.000000",
                    ["COLLECTION_OBJECT_HP_RATE"] = "1.000000",
                    ["COLLECTION_OBJECT_RESPAWN_SPEED_RATE"] = "1.000000",
                    ["ENEMY_DROP_ITEM_RATE"] = "1.000000",
                    
                    // Game Settings - Death and PvP
                    ["DEATH_PENALTY"] = "All",
                    ["ENABLE_PLAYER_TO_PLAYER_DAMAGE"] = "False",
                    ["ENABLE_FRIENDLY_FIRE"] = "False",
                    ["ENABLE_INVADER_ENEMY"] = "True",
                    ["ACTIVE_UNKO"] = "False",
                    
                    // Game Settings - Aim Assist
                    ["ENABLE_AIM_ASSIST_PAD"] = "True",
                    ["ENABLE_AIM_ASSIST_KEYBOARD"] = "False",
                    
                    // Game Settings - Items and Limits
                    ["DROP_ITEM_MAX_NUM"] = "3000",
                    ["DROP_ITEM_MAX_NUM_UNKO"] = "100",
                    ["BASE_CAMP_MAX_NUM"] = "128",
                    ["BASE_CAMP_WORKER_MAX_NUM"] = "15",
                    ["DROP_ITEM_ALIVE_MAX_HOURS"] = "1.000000",
                    
                    // Game Settings - Guild Settings
                    ["AUTO_RESET_GUILD_NO_ONLINE_PLAYERS"] = "False",
                    ["AUTO_RESET_GUILD_TIME_NO_ONLINE_PLAYERS"] = "72.000000",
                    ["GUILD_PLAYER_MAX_NUM"] = "20",
                    ["BASE_CAMP_MAX_NUM_IN_GUILD"] = "4",
                    
                    // Game Settings - Breeding and Work
                    ["PAL_EGG_DEFAULT_HATCHING_TIME"] = "72.000000",
                    ["WORK_SPEED_RATE"] = "1.000000",
                    ["AUTO_SAVE_SPAN"] = "30.000000",
                    
                    // Game Settings - Multiplayer and Game Modes
                    ["IS_MULTIPLAY"] = "False",
                    ["IS_PVP"] = "False",
                    ["HARDCORE"] = "False",
                    ["CHARACTER_RECREATE_IN_HARDCORE"] = "False",
                    ["PAL_LOST"] = "False",
                    ["CAN_PICKUP_OTHER_GUILD_DEATH_PENALTY_DROP"] = "False",
                    ["ENABLE_NON_LOGIN_PENALTY"] = "True",
                    ["ENABLE_FAST_TRAVEL"] = "True",
                    ["IS_START_LOCATION_SELECT_BY_MAP"] = "True",
                    ["EXIST_PLAYER_AFTER_LOGOUT"] = "False",
                    ["ENABLE_DEFENSE_OTHER_GUILD_PLAYER"] = "False",
                    ["INVISIBLE_OTHER_GUILD_BASE_CAMP_AREA_FX"] = "False",
                    ["BUILD_AREA_LIMIT"] = "False",
                    ["ITEM_WEIGHT_RATE"] = "1.000000",
                    ["COOP_PLAYER_MAX_NUM"] = "4",
                    
                    // Game Settings - Server Features
                    ["REGION"] = "",
                    ["USEAUTH"] = "True",
                    ["BAN_LIST_URL"] = "https://api.palworldgame.com/api/banlist.txt",
                    ["SHOW_PLAYER_LIST"] = "True",
                    ["CHAT_POST_LIMIT_PER_MINUTE"] = "10",
                    ["SUPPLY_DROP_SPAN"] = "180",
                    ["ENABLE_PREDATOR_BOSS_PAL"] = "true",
                    ["MAX_BUILDING_LIMIT_NUM"] = "0",
                    ["SERVER_REPLICATE_PAWN_CULL_DISTANCE"] = "15000.000000",
                    ["CROSSPLAY_PLATFORMS"] = "(Steam,Xbox,PS5,Mac)",
                    
                    // Game Settings - Palbox and Equipment
                    ["ALLOW_GLOBAL_PALBOX_EXPORT"] = "True",
                    ["ALLOW_GLOBAL_PALBOX_IMPORT"] = "False",
                    ["EQUIPMENT_DURABILITY_DAMAGE_RATE"] = "1.000000",
                    ["ITEM_CONTAINER_FORCE_MARK_DIRTY_INTERVAL"] = "1.000000",
                    ["ITEM_CORRUPTION_MULTIPLIER"] = "1.000000",
                    
                    // Engine Settings - Network Performance
                    ["LAN_SERVER_MAX_TICK_RATE"] = "120",
                    ["NET_SERVER_MAX_TICK_RATE"] = "120",
                    ["CONFIGURED_INTERNET_SPEED"] = "104857600",
                    ["CONFIGURED_LAN_SPEED"] = "104857600",
                    ["MAX_CLIENT_RATE"] = "104857600",
                    ["MAX_INTERNET_CLIENT_RATE"] = "104857600",
                    
                    // Engine Settings - Frame Rate
                    ["SMOOTH_FRAME_RATE"] = "true",
                    ["SMOOTH_FRAME_RATE_UPPER_LIMIT"] = "120.000000",
                    ["SMOOTH_FRAME_RATE_LOWER_LIMIT"] = "30.000000",
                    ["USE_FIXED_FRAME_RATE"] = "false",
                    ["FIXED_FRAME_RATE"] = "120.000000",
                    ["MIN_DESIRED_FRAME_RATE"] = "60.000000",
                    ["NET_CLIENT_TICKS_PER_SECOND"] = "120",
                    
                    // ARM64-specific settings (optional)
                    ["BOX64_DYNAREC_STRONGMEM"] = "1",
                    ["BOX64_DYNAREC_BIGBLOCK"] = "1",
                    ["BOX64_DYNAREC_SAFEFLAGS"] = "1",
                    ["BOX64_DYNAREC_FASTROUND"] = "1",
                    ["BOX64_DYNAREC_FASTNAN"] = "1",
                    ["BOX64_DYNAREC_X87DOUBLE"] = "0",
                    ["ARM64_DEVICE"] = "generic",
                }
            };

        public static GameTypeDefinition SatisfactoryV1 =>
            new GameTypeDefinition
            {
                Key = "satisfactory",
                DisplayName = "Satisfactory",
                Description = "Satisfactory dedicated server using wolveix/satisfactory-server",
                Image = "wolveix/satisfactory-server",
                ThumbnailUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/526870/header.jpg",
                DocumentationUrl = "https://hub.docker.com/r/wolveix/satisfactory-server",

                Ports = new()
                {
                    new PortDefinition(7777, "tcp"),
                    new PortDefinition(7777, "udp"),
                    new PortDefinition(8888, "tcp")
                },

                Volumes = new()
                {
                    new VolumeDefinition("", "/config")
                },

                // DefaultSettings uses exact environment variable names (verbatim casing)
                // populated from the wolveix/satisfactory-server documentation
                DefaultSettings = new()
                {
                    // Server Settings
                    ["AUTOSAVENUM"] = "5",
                    ["DEBUG"] = "false",
                    ["DISABLESEASONALEVENTS"] = "false",
                    ["LOG"] = "false",
                    ["MAXOBJECTS"] = "2162688",
                    ["MAXPLAYERS"] = "4",
                    ["MAXTICKRATE"] = "30",
                    ["MULTIHOME"] = "::",
                    ["PGID"] = "1000",
                    ["PUID"] = "1000",
                    ["SERVERGAMEPORT"] = "7777",
                    ["SERVERMESSAGINGPORT"] = "8888",
                    ["SERVERSTREAMING"] = "true",
                    ["SKIPUPDATE"] = "false",
                    ["STEAMBETA"] = "false",
                    ["STEAMBETAID"] = "",
                    ["STEAMBETAKEY"] = "",
                    ["TIMEOUT"] = "30",
                    ["VMOVERRIDE"] = "false"
                }
            };

        public static GameTypeDefinition SevenDaysToDieV1 =>
            new GameTypeDefinition
            {
                Key = "7daystodie",
                DisplayName = "7 Days to Die",
                Description = "7 Days to Die dedicated server using vinanrra/7daystodie-server",
                Image = "vinanrra/7daystodie-server",
                ThumbnailUrl = "https://cdn.cloudflare.steamstatic.com/steam/apps/251570/header.jpg",
                DocumentationUrl = "https://github.com/vinanrra/Docker-7DaysToDie",

                Ports = new()
                {
                    new PortDefinition(26900, "tcp"),
                    new PortDefinition(26900, "udp"),
                    new PortDefinition(26901, "udp"),
                    new PortDefinition(26902, "udp"),
                    new PortDefinition(8080, "tcp"),
                    new PortDefinition(8081, "tcp"),
                    new PortDefinition(8082, "tcp")
                },

                Volumes = new()
                {
                    new VolumeDefinition("", "/home/sdtdserver/.local/share/7DaysToDie/Saves"),
                    new VolumeDefinition("", "/home/sdtdserver/serverfiles"),
                    new VolumeDefinition("", "/home/sdtdserver/log"),
                    new VolumeDefinition("", "/home/sdtdserver/backups")
                },

                // DefaultSettings uses exact environment variable names (verbatim casing)
                // populated from the vinanrra/Docker-7DaysToDie documentation
                DefaultSettings = new()
                {
                    // Container Settings
                    ["START_MODE"] = "1",
                    ["VERSION"] = "stable",
                    ["PUID"] = "1000",
                    ["PGID"] = "1000",
                    ["TimeZone"] = "America/New_York",
                    
                    // Server Basic Settings
                    ["ServerName"] = "My 7DTD Server",
                    ["ServerDescription"] = "A 7 Days to Die server",
                    ["ServerWebsiteURL"] = "",
                    ["ServerPassword"] = "",
                    ["ServerLoginConfirmationText"] = "",
                    ["Region"] = "NorthAmericaEast",
                    ["Language"] = "English",
                    
                    // Networking
                    ["ServerPort"] = "26900",
                    ["ServerVisibility"] = "2",
                    ["ServerDisabledNetworkProtocols"] = "SteamNetworking",
                    ["ServerMaxWorldTransferSpeedKiBs"] = "512",
                    
                    // Slots
                    ["ServerMaxPlayerCount"] = "8",
                    ["ServerReservedSlots"] = "0",
                    ["ServerReservedSlotsPermission"] = "100",
                    ["ServerAdminSlots"] = "0",
                    ["ServerAdminSlotsPermission"] = "0",
                    
                    // Admin Interfaces
                    ["WebDashboardEnabled"] = "false",
                    ["WebDashboardPort"] = "8080",
                    ["WebDashboardUrl"] = "",
                    ["EnableMapRendering"] = "false",
                    ["TelnetEnabled"] = "true",
                    ["TelnetPort"] = "8081",
                    ["TelnetPassword"] = "",
                    ["TelnetFailedLoginLimit"] = "10",
                    ["TelnetFailedLoginsBlocktime"] = "10",
                    ["TerminalWindowEnabled"] = "true",
                    
                    // Folder and File Locations
                    ["AdminFileName"] = "serveradmin.xml",
                    ["UserDataFolder"] = "UserDataFolder",
                    ["SaveGameFolder"] = "SaveGameFolder",
                    
                    // Other Technical Settings
                    ["EACEnabled"] = "true",
                    ["HideCommandExecutionLog"] = "0",
                    ["MaxUncoveredMapChunksPerPlayer"] = "131072",
                    ["PersistentPlayerProfiles"] = "false",
                    
                    // Game World
                    ["GameWorld"] = "Navezgane",
                    ["WorldGenSeed"] = "asdf",
                    ["WorldGenSize"] = "6144",
                    ["GameName"] = "My Game",
                    ["GameMode"] = "GameModeSurvival",
                    
                    // Difficulty
                    ["GameDifficulty"] = "2",
                    ["BlockDamagePlayer"] = "100",
                    ["BlockDamageAI"] = "100",
                    ["BlockDamageAIBM"] = "100",
                    ["XPMultiplier"] = "100",
                    ["PlayerSafeZoneLevel"] = "5",
                    ["PlayerSafeZoneHours"] = "5",
                    
                    // Day/Night Settings
                    ["DayNightLength"] = "60",
                    ["DayLightLength"] = "18",
                    ["DayCount"] = "1",
                    
                    // Loot
                    ["LootAbundance"] = "100",
                    ["LootRespawnDays"] = "7",
                    ["AirDropFrequency"] = "72",
                    ["AirDropMarker"] = "false",
                    
                    // Multiplayer
                    ["DropOnDeath"] = "1",
                    ["DropOnQuit"] = "0",
                    ["BedrollDeadZoneSize"] = "15",
                    ["BedrollExpiryTime"] = "45",
                    
                    // Performance
                    ["MaxSpawnedZombies"] = "64",
                    ["MaxSpawnedAnimals"] = "50",
                    ["ServerMaxAllowedViewDistance"] = "12",
                    ["MaxQueuedMeshLayers"] = "1000",
                    
                    // Zombie Settings
                    ["EnemySpawnMode"] = "true",
                    ["EnemyDifficulty"] = "0",
                    ["ZombieFeralSense"] = "0",
                    ["ZombieMove"] = "0",
                    ["ZombieMoveNight"] = "3",
                    ["ZombieFeralMove"] = "3",
                    ["ZombieBMMove"] = "3",
                    ["BloodMoonFrequency"] = "7",
                    ["BloodMoonRange"] = "0",
                    ["BloodMoonWarning"] = "8",
                    ["BloodMoonEnemyCount"] = "8",
                    
                    // Land Claim Options
                    ["LandClaimCount"] = "3",
                    ["LandClaimSize"] = "41",
                    ["LandClaimDeadZone"] = "30",
                    ["LandClaimExpiryTime"] = "7",
                    ["LandClaimDecayMode"] = "0",
                    ["LandClaimOnlineDurabilityModifier"] = "4",
                    ["LandClaimOfflineDurabilityModifier"] = "4",
                    ["LandClaimOfflineDelay"] = "0",
                    
                    // Backup Settings
                    ["BACKUP"] = "YES",
                    ["BACKUP_INTERVAL"] = "24"
                }
            };

        public static GameTypeDefinition HytaleV1 =>
            new GameTypeDefinition
            {
                Key = "hytale",
                DisplayName = "Hytale",
                Description = "Hytale dedicated server using indifferentbroccoli/hytale-server-docker",
                Image = "indifferentbroccoli/hytale-server-docker",
                ThumbnailUrl = "https://hytale.com/static/og-image.jpg",
                DocumentationUrl = "https://github.com/IndifferentBroccoli/hytale-server-docker",

                Ports = new()
                {
                    new PortDefinition(5520, "udp")
                },

                Volumes = new()
                {
                    new VolumeDefinition("", "/home/hytale/server-files")
                },

                // DefaultSettings uses exact environment variable names (verbatim casing)
                // populated from the indifferentbroccoli/hytale-server-docker documentation
                DefaultSettings = new()
                {
                    // User and Group Settings
                    ["PUID"] = "1000",
                    ["PGID"] = "1000",
                    
                    // Server Configuration
                    ["SERVER_NAME"] = "hytale-server-docker",
                    ["DEFAULT_PORT"] = "5520",
                    ["MAX_PLAYERS"] = "20",
                    ["VIEW_DISTANCE"] = "12",
                    ["AUTH_MODE"] = "authenticated",
                    
                    // Backup Settings
                    ["ENABLE_BACKUPS"] = "false",
                    ["BACKUP_FREQUENCY"] = "30",
                    ["BACKUP_DIR"] = "/home/hytale/server-files/backups",
                    
                    // Server Features
                    ["DISABLE_SENTRY"] = "true",
                    ["USE_AOT_CACHE"] = "true",
                    ["ACCEPT_EARLY_PLUGINS"] = "false",
                    
                    // JVM Memory Settings
                    ["MIN_MEMORY"] = "",
                    ["MAX_MEMORY"] = "8G",
                    ["JVM_ARGS"] = "",
                    
                    // Update and Version Settings
                    ["PATCHLINE"] = "release",
                    ["DOWNLOAD_ON_START"] = "true"
                }
            };

        public GameTypeRegistry()
        {
            RegisterBuiltInTypes();
        }

        private List<GameTypeDefinition> GetAll() => _definitions.Values.ToList();

        private GameTypeDefinition? Get(string key)
        {
            _definitions.TryGetValue(key, out var def);
            return def;
        }

        private void AddOrUpdate(GameTypeDefinition def)
        {
            _definitions[def.Key] = def;
        }

        private void Delete(string key)
        {
            _definitions.Remove(key);
        }

        internal void RegisterBuiltInTypes()
        {
            AddOrUpdate(MinecraftV1);
            AddOrUpdate(MinecraftBedrockV1);
            AddOrUpdate(ValhiemV1);
            AddOrUpdate(PalworldV1);
            AddOrUpdate(SatisfactoryV1);
            AddOrUpdate(SevenDaysToDieV1);
            AddOrUpdate(HytaleV1);
        }

        Task<List<GameTypeDefinition>> IGameTypeRegistry.GetAll()
        {
            return Task.FromResult(GetAll());
        }

        Task<GameTypeDefinition?> IGameTypeRegistry.Get(string key)
        {
            return Task.FromResult(Get(key));   
        }

        Task IGameTypeRegistry.AddOrUpdate(GameTypeDefinition def)
        {
            return Task.Run(() => AddOrUpdate(def));
        }

        Task IGameTypeRegistry.Delete(string key)
        {
            return Task.Run(() => Delete(key));
        }
    }
}
