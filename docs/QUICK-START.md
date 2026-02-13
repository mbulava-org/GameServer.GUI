# Quick Start Guide - Extended Metadata System

Get started with the GameType Extended Metadata system in 5 minutes!

---

## Step 1: Verify Configuration

Ensure your `appsettings.Development.json` (or `appsettings.json`) contains:

```json
{
  "GameTypeRegistryData": {
    "FilePath": "/data/game-types.json"
  },
  "GameTypeExtendedMetadataRegistryData": {
    "FilePath": "/data/game-types-extended.json"
  }
}
```

---

## Step 2: Start the Application

Run your application:

```bash
dotnet run --project src/GameServer.Docker
```

On first startup, both files will be created with built-in defaults.

---

## Step 3: Explore the API

### Get Minecraft Extended Metadata

```bash
curl http://localhost:5000/api/gametypes/extended/minecraft | jq
```

**Response:**
```json
{
  "gameTypeKey": "minecraft",
  "enableTTY": true,
  "attachStdin": false,
  "settingsMetadata": {
    "EULA": {
      "key": "EULA",
      "description": "You must accept the Minecraft EULA...",
      "isRequired": true,
      "cannotBeEmpty": true,
      "dataType": "boolean",
      "category": "Legal",
      "displayOrder": 1
    },
    // ... more settings
  }
}
```

### Get All Extended Metadata

```bash
curl http://localhost:5000/api/gametypes/extended | jq
```

---

## Step 4: Create Extended Metadata for Your Game Type

### Example: Terraria Server

```bash
curl -X POST http://localhost:5000/api/gametypes/extended \
  -H "Content-Type: application/json" \
  -d '{
    "gameTypeKey": "terraria",
    "enableTTY": true,
    "attachStdin": false,
    "settingsMetadata": {
      "WORLD_NAME": {
        "key": "WORLD_NAME",
        "description": "Name of the world to create or load",
        "isRequired": true,
        "cannotBeEmpty": true,
        "dataType": "string",
        "category": "World",
        "displayOrder": 1,
        "placeholder": "MyWorld"
      },
      "MAX_PLAYERS": {
        "key": "MAX_PLAYERS",
        "description": "Maximum number of players (1-255)",
        "dataType": "number",
        "category": "Server",
        "displayOrder": 2,
        "validationPattern": "^([1-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$",
        "validationMessage": "Must be between 1 and 255"
      },
      "SERVER_PORT": {
        "key": "SERVER_PORT",
        "description": "Server port number",
        "dataType": "port",
        "mapsToContainerPort": true,
        "portProtocol": "tcp",
        "category": "Network",
        "displayOrder": 3,
        "placeholder": "7777"
      }
    }
  }'
```

---

## Step 5: Use Extended Metadata in Your Application

### Validate Server Settings

```csharp
[ApiController]
[Route("api/gameservers")]
public class GameServerController : ControllerBase
{
    private readonly GameTypeMetadataApplier _metadataApplier;
    
    [HttpPost]
    public async Task<IActionResult> CreateServer([FromBody] GameServer server)
    {
        // Validate using extended metadata
        var errors = await _metadataApplier.ValidateSettings(server, server.GameType);
        
        if (errors.Any())
        {
            return BadRequest(new 
            { 
                message = "Validation failed", 
                errors 
            });
        }
        
        // Continue with server creation...
        return Ok(server);
    }
}
```

### Apply Metadata to Container

```csharp
// In your DockerServiceHelper or similar service
private async Task<ServiceSpec> BuildServiceSpec(GameServer server, GameTypeDefinition definition)
{
    var containerSpec = new ContainerSpec
    {
        Image = definition.Image,
        Env = BuildEnvironmentVariables(server, definition),
        // ... other properties
    };
    
    // Apply extended metadata (TTY, stdin)
    containerSpec = await _metadataApplier.ApplyMetadata(containerSpec, server.GameType);
    
    // Get dynamic ports from settings
    var dynamicPorts = await _metadataApplier.GetDynamicPorts(server, server.GameType);
    var allPorts = definition.Ports.Concat(dynamicPorts).ToList();
    
    // Continue building...
}
```

---

## Step 6: Test the System

### Test 1: Validation with Missing Required Field

```bash
# This should fail because EULA is required
curl -X POST http://localhost:5000/api/gameservers \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Test Server",
    "gameType": "minecraft",
    "settings": {
      "VERSION": "LATEST"
    }
  }'
```

**Expected Response:**
```json
{
  "message": "Validation failed",
  "errors": [
    "Setting 'EULA' is required but not provided. You must accept the Minecraft EULA..."
  ]
}
```

### Test 2: Valid Server Creation

```bash
curl -X POST http://localhost:5000/api/gameservers \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Test Server",
    "gameType": "minecraft",
    "settings": {
      "EULA": "true",
      "VERSION": "LATEST",
      "MEMORY": "2G",
      "MAX_PLAYERS": "10"
    }
  }'
```

**Expected:** Server created successfully with TTY enabled!

### Test 3: Dynamic Port Mapping

```bash
# Create server with custom port
curl -X POST http://localhost:5000/api/gameservers \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Custom Port Server",
    "gameType": "minecraft",
    "settings": {
      "EULA": "true",
      "SERVER_PORT": "25566"
    }
  }'
```

**Expected:** Container exposes port 25566 in addition to default 25565!

---

## Step 7: Update Individual Setting Metadata

```bash
# Make MEMORY setting required
curl -X PUT http://localhost:5000/api/gametypes/extended/minecraft/settings/MEMORY \
  -H "Content-Type: application/json" \
  -d '{
    "key": "MEMORY",
    "description": "Server memory allocation (REQUIRED)",
    "isRequired": true,
    "cannotBeEmpty": true,
    "dataType": "string",
    "category": "Performance",
    "validationPattern": "^\\d+[MG]$",
    "validationMessage": "Must be a number followed by M or G (e.g., 1G, 2048M)"
  }'
```

---

## Common Use Cases

### 1. Make a Setting Required

```bash
curl -X PUT http://localhost:5000/api/gametypes/extended/{gameType}/settings/{settingKey} \
  -H "Content-Type: application/json" \
  -d '{ "key": "...", "isRequired": true, ... }'
```

### 2. Add Port Mapping

```bash
curl -X PUT http://localhost:5000/api/gametypes/extended/{gameType}/settings/{settingKey} \
  -H "Content-Type: application/json" \
  -d '{ 
    "key": "SERVER_PORT", 
    "dataType": "port", 
    "mapsToContainerPort": true,
    "portProtocol": "tcp"
  }'
```

### 3. Add Validation Pattern

```bash
curl -X PUT http://localhost:5000/api/gametypes/extended/{gameType}/settings/{settingKey} \
  -H "Content-Type: application/json" \
  -d '{ 
    "key": "EMAIL",
    "validationPattern": "^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}$",
    "validationMessage": "Must be a valid email address"
  }'
```

### 4. Enable TTY for Interactive Servers

```bash
curl -X POST http://localhost:5000/api/gametypes/extended \
  -H "Content-Type: application/json" \
  -d '{ 
    "gameTypeKey": "your-game",
    "enableTTY": true,
    "attachStdin": true
  }'
```

---

## Verification

### Check File Persistence

```bash
# View the extended metadata file
cat /data/game-types-extended.json | jq

# Restart the service
docker restart gameserver-docker

# Verify data is still there
curl http://localhost:5000/api/gametypes/extended/minecraft | jq
```

**Expected:** All your metadata is preserved after restart! ?

---

## Troubleshooting

### Issue: Metadata not persisting
**Solution:** Check file permissions and path configuration

```bash
# Check file exists
ls -la /data/game-types-extended.json

# Check logs
docker logs gameserver-docker | grep "GameTypeExtendedMetadata"
```

### Issue: Validation not working
**Solution:** Ensure GameTypeMetadataApplier is injected and used in your controller

```csharp
// In ConfigureServices
services.AddSingleton<GameTypeMetadataApplier>();

// In Controller constructor
public GameServerController(GameTypeMetadataApplier metadataApplier)
{
    _metadataApplier = metadataApplier;
}
```

### Issue: TTY not being applied
**Solution:** Ensure you're calling ApplyMetadata when building ContainerSpec

```csharp
containerSpec = await _metadataApplier.ApplyMetadata(containerSpec, server.GameType);
```

---

## Next Steps

1. ? Explore the full documentation: `docs/GameType-Extended-Metadata.md`
2. ? Check integration examples: `docs/GameType-Extended-Metadata-Integration.md`
3. ? Add metadata for your game types
4. ? Build UI forms using the categorization features
5. ? Implement validation in your controllers

---

## Quick Reference

### File Locations
- Code: `src/GameServer.Docker/Services/GameTypeExtendedMetadataRegistryFile.cs`
- Data: `/data/game-types-extended.json`
- Docs: `docs/GameType-Extended-Metadata.md`

### Key Services
- `IGameTypeExtendedMetadataRegistry` - CRUD operations
- `GameTypeMetadataApplier` - Apply metadata and validate

### API Endpoints
- `GET /api/gametypes/extended` - List all
- `GET /api/gametypes/extended/{key}` - Get one
- `POST /api/gametypes/extended` - Create/update
- `DELETE /api/gametypes/extended/{key}` - Delete
- `PUT /api/gametypes/extended/{key}/settings/{settingKey}` - Update setting

---

**You're all set!** Start extending your game types with rich metadata! ??
