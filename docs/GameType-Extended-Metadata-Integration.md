# Extended Metadata Integration Examples

This document provides practical examples for integrating the GameType Extended Metadata system into your container orchestration and UI code.

---

## Example 1: Applying Extended Metadata to Container Creation

### Updating DockerServiceHelper to Use Extended Metadata

Inject the `GameTypeMetadataApplier` into your `DockerServiceHelper`:

```csharp
public class DockerServiceHelper
{
    private readonly IGameTypeExtendedMetadataRegistry _metadataRegistry;
    private readonly GameTypeMetadataApplier _metadataApplier;
    
    public DockerServiceHelper(
        ILogger<DockerServiceHelper> logger,
        IDockerClient client,
        IGameTypeRegistry gameTypeRegistry,
        IGameTypeExtendedMetadataRegistry metadataRegistry,
        GameTypeMetadataApplier metadataApplier,
        IOptions<Configurations.VolumeDriverConfigOptions> volOptions,
        IOptions<Configurations.NetworkOptions> netOptions)
    {
        _metadataRegistry = metadataRegistry;
        _metadataApplier = metadataApplier;
        // ... other initializations
    }
    
    private async Task<ServiceSpec> BuildGameServerServiceSpec(
        Models.GameServer server,
        Models.GameTypeDefinition definition,
        ServiceSpec? existingSpec = null,
        bool stopService = false)
    {
        // ... existing code ...
        
        // Create base ContainerSpec
        var containerSpec = new ContainerSpec
        {
            Image = definition.Image,
            Env = env,
            Mounts = mounts,
            Labels = labels
        };
        
        // Apply extended metadata (TTY, stdin, etc.)
        containerSpec = await _metadataApplier.ApplyMetadata(containerSpec, server.GameType);
        
        // ... rest of the ServiceSpec building ...
    }
}
```

---

## Example 2: Validating Settings Before Creating a Server

### Server Controller with Validation

```csharp
[ApiController]
[Route("api/gameservers")]
public class GameServerController : ControllerBase
{
    private readonly IGameServerManager _manager;
    private readonly GameTypeMetadataApplier _metadataApplier;
    
    [HttpPost]
    public async Task<IActionResult> CreateServer([FromBody] GameServer server)
    {
        // Validate settings against extended metadata
        var validationErrors = await _metadataApplier.ValidateSettings(server, server.GameType);
        
        if (validationErrors.Any())
        {
            return BadRequest(new
            {
                message = "Validation failed",
                errors = validationErrors
            });
        }
        
        // Proceed with server creation
        await _manager.CreateAsync(server);
        return Ok(server);
    }
}
```

---

## Example 3: Dynamic Port Mapping

### Handling Ports with Extended Metadata

```csharp
private async Task<List<PortDefinition>> GetAllPorts(
    GameServer server,
    GameTypeDefinition definition)
{
    // Start with ports from definition
    var allPorts = new List<PortDefinition>(definition.Ports);
    
    // Add dynamic ports from settings metadata
    var dynamicPorts = await _metadataApplier.GetDynamicPorts(server, server.GameType);
    
    // Merge, avoiding duplicates
    foreach (var dynPort in dynamicPorts)
    {
        if (!allPorts.Any(p => p.Port == dynPort.Port && p.Protocol == dynPort.Protocol))
        {
            allPorts.Add(dynPort);
            _logger.LogInformation("Added dynamic port {Port}/{Protocol}", 
                dynPort.Port, dynPort.Protocol);
        }
    }
    
    return allPorts;
}
```

### Example Scenario

Given this extended metadata:
```json
{
  "gameTypeKey": "minecraft",
  "settingsMetadata": {
    "SERVER_PORT": {
      "key": "SERVER_PORT",
      "dataType": "port",
      "mapsToContainerPort": true,
      "portProtocol": "tcp"
    }
  }
}
```

When a user creates a server with:
```json
{
  "gameType": "minecraft",
  "settings": {
    "SERVER_PORT": "25566"
  }
}
```

The system will automatically expose port `25566/tcp` in addition to the default port `25565/tcp`.

---

## Example 4: UI Integration - React Form Generator

### Fetching and Rendering Settings

```typescript
interface SettingFormProps {
  gameTypeKey: string;
  onSubmit: (settings: Record<string, string>) => void;
}

function SettingForm({ gameTypeKey, onSubmit }: SettingFormProps) {
  const [metadata, setMetadata] = useState<GameTypeExtendedMetadata | null>(null);
  const [gameType, setGameType] = useState<GameTypeDefinition | null>(null);
  const [settings, setSettings] = useState<Record<string, string>>({});
  const [errors, setErrors] = useState<Record<string, string>>({});

  useEffect(() => {
    // Fetch game type and extended metadata
    Promise.all([
      fetch(`/api/gametypes/${gameTypeKey}`).then(r => r.json()),
      fetch(`/api/gametypes/extended/${gameTypeKey}`).then(r => r.json())
    ]).then(([gt, meta]) => {
      setGameType(gt);
      setMetadata(meta);
      // Initialize with default values
      setSettings({ ...gt.defaultSettings });
    });
  }, [gameTypeKey]);

  const validateSetting = (key: string, value: string): string | null => {
    const meta = metadata?.settingsMetadata[key];
    if (!meta) return null;

    if (meta.isRequired && !value) {
      return `${key} is required`;
    }

    if (meta.cannotBeEmpty && !value.trim()) {
      return `${key} cannot be empty`;
    }

    if (meta.validationPattern) {
      const regex = new RegExp(meta.validationPattern);
      if (!regex.test(value)) {
        return meta.validationMessage || `Invalid format for ${key}`;
      }
    }

    return null;
  };

  const handleChange = (key: string, value: string) => {
    setSettings(prev => ({ ...prev, [key]: value }));
    
    // Validate on change
    const error = validateSetting(key, value);
    setErrors(prev => ({
      ...prev,
      [key]: error || ''
    }));
  };

  const handleSubmit = () => {
    // Validate all required fields
    const newErrors: Record<string, string> = {};
    
    Object.values(metadata?.settingsMetadata || {}).forEach(meta => {
      if (meta.isRequired) {
        const error = validateSetting(meta.key, settings[meta.key] || '');
        if (error) newErrors[meta.key] = error;
      }
    });

    if (Object.keys(newErrors).length > 0) {
      setErrors(newErrors);
      return;
    }

    onSubmit(settings);
  };

  // Group settings by category and sort by display order
  const categorizedSettings = Object.values(metadata?.settingsMetadata || {})
    .reduce((acc, meta) => {
      const category = meta.category || 'General';
      if (!acc[category]) acc[category] = [];
      acc[category].push(meta);
      return acc;
    }, {} as Record<string, SettingMetadata[]>);

  // Sort within each category by displayOrder
  Object.keys(categorizedSettings).forEach(category => {
    categorizedSettings[category].sort((a, b) => a.displayOrder - b.displayOrder);
  });

  return (
    <form onSubmit={(e) => { e.preventDefault(); handleSubmit(); }}>
      {Object.entries(categorizedSettings).map(([category, metas]) => (
        <div key={category} className="setting-category">
          <h3>{category}</h3>
          {metas.map(meta => (
            <div key={meta.key} className="setting-field">
              <label>
                {meta.key}
                {meta.isRequired && <span className="required">*</span>}
              </label>
              <p className="description">{meta.description}</p>
              
              {renderInput(meta, settings[meta.key], (val) => handleChange(meta.key, val))}
              
              {errors[meta.key] && (
                <span className="error">{errors[meta.key]}</span>
              )}
            </div>
          ))}
        </div>
      ))}
      <button type="submit">Create Server</button>
    </form>
  );
}

function renderInput(
  meta: SettingMetadata,
  value: string,
  onChange: (value: string) => void
) {
  switch (meta.dataType) {
    case 'boolean':
      return (
        <select value={value} onChange={(e) => onChange(e.target.value)}>
          <option value="">-- Select --</option>
          <option value="true">True</option>
          <option value="false">False</option>
        </select>
      );
    
    case 'number':
    case 'port':
      return (
        <input
          type="number"
          value={value}
          placeholder={meta.placeholder}
          onChange={(e) => onChange(e.target.value)}
        />
      );
    
    case 'list':
      return (
        <textarea
          value={value}
          placeholder={meta.placeholder || `Separate values with ${meta.listDelimiter}`}
          onChange={(e) => onChange(e.target.value)}
        />
      );
    
    default:
      return (
        <input
          type="text"
          value={value}
          placeholder={meta.placeholder}
          onChange={(e) => onChange(e.target.value)}
        />
      );
  }
}
```

---

## Example 5: Backend Service - Parsing List Settings

### Processing Mod Lists

```csharp
public class MinecraftModInstaller
{
    private readonly GameTypeMetadataApplier _metadataApplier;
    
    public async Task InstallMods(GameServer server)
    {
        if (!server.Settings.TryGetValue("MODS", out var modListValue))
            return;
        
        // Parse the list using metadata rules
        var modUrls = await _metadataApplier.ParseListSetting(
            "MODS", 
            modListValue, 
            server.GameType);
        
        foreach (var modUrl in modUrls)
        {
            await DownloadAndInstallMod(modUrl);
        }
    }
}
```

Given extended metadata:
```json
{
  "settingsMetadata": {
    "MODS": {
      "key": "MODS",
      "dataType": "list",
      "listDelimiter": "|",
      "description": "Pipe-separated list of mod URLs"
    }
  }
}
```

Input:
```
MODS = "https://example.com/mod1.jar|https://example.com/mod2.jar|https://example.com/mod3.jar"
```

Result:
```csharp
modUrls = [
    "https://example.com/mod1.jar",
    "https://example.com/mod2.jar",
    "https://example.com/mod3.jar"
]
```

---

## Example 6: Creating Extended Metadata via API

### Adding Metadata for a Custom Game Type

```bash
# Create base game type
curl -X POST http://localhost:5000/api/gametypes \
  -H "Content-Type: application/json" \
  -d '{
    "key": "terraria",
    "displayName": "Terraria",
    "description": "Terraria dedicated server",
    "image": "ryshe/terraria:latest",
    "ports": [
      { "port": 7777, "protocol": "tcp" }
    ],
    "volumes": [
      { "source": "", "target": "/root/.local/share/Terraria/Worlds" }
    ],
    "defaultSettings": {
      "WORLD_NAME": "MyWorld",
      "MAX_PLAYERS": "8",
      "PORT": "7777",
      "PASSWORD": ""
    }
  }'

# Create extended metadata
curl -X POST http://localhost:5000/api/gametypes/extended \
  -H "Content-Type: application/json" \
  -d '{
    "gameTypeKey": "terraria",
    "enableTTY": true,
    "settingsMetadata": {
      "WORLD_NAME": {
        "key": "WORLD_NAME",
        "description": "Name of the world to create or load",
        "isRequired": true,
        "cannotBeEmpty": true,
        "category": "World",
        "displayOrder": 1
      },
      "MAX_PLAYERS": {
        "key": "MAX_PLAYERS",
        "description": "Maximum number of players (1-255)",
        "dataType": "number",
        "category": "Server",
        "displayOrder": 2,
        "validationPattern": "^([1-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$",
        "validationMessage": "Must be a number between 1 and 255"
      },
      "PORT": {
        "key": "PORT",
        "description": "Server port",
        "dataType": "port",
        "mapsToContainerPort": true,
        "portProtocol": "tcp",
        "category": "Network",
        "displayOrder": 3
      },
      "PASSWORD": {
        "key": "PASSWORD",
        "description": "Server password (leave empty for no password)",
        "category": "Security",
        "displayOrder": 4
      }
    }
  }'
```

---

## Example 7: Updating Individual Setting Metadata

### Modifying a Single Setting

```bash
# Update the EULA setting to be less strict
curl -X PUT http://localhost:5000/api/gametypes/extended/minecraft/settings/EULA \
  -H "Content-Type: application/json" \
  -d '{
    "key": "EULA",
    "description": "Accept Minecraft EULA (required to run server)",
    "isRequired": false,
    "cannotBeEmpty": false,
    "dataType": "boolean",
    "category": "Legal",
    "displayOrder": 1
  }'
```

---

## Example 8: Complete Server Creation Flow with Validation

### TypeScript Client Example

```typescript
async function createMinecraftServer() {
  // 1. Fetch game type and extended metadata
  const gameType = await fetch('/api/gametypes/minecraft').then(r => r.json());
  const extendedMeta = await fetch('/api/gametypes/extended/minecraft').then(r => r.json());
  
  // 2. Build settings with user input
  const serverData = {
    name: "My Awesome Server",
    description: "A fun server for friends",
    gameType: "minecraft",
    settings: {
      EULA: "true",
      VERSION: "1.20.4",
      MEMORY: "4G",
      MAX_PLAYERS: "20",
      SERVER_PORT: "25565",
      DIFFICULTY: "normal"
    }
  };
  
  // 3. Client-side validation (optional but recommended)
  const validationErrors = validateSettings(serverData.settings, extendedMeta);
  if (validationErrors.length > 0) {
    console.error("Validation errors:", validationErrors);
    return;
  }
  
  // 4. Create server
  const response = await fetch('/api/gameservers', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(serverData)
  });
  
  if (!response.ok) {
    const error = await response.json();
    console.error("Server creation failed:", error);
    return;
  }
  
  const server = await response.json();
  console.log("Server created:", server);
}

function validateSettings(
  settings: Record<string, string>,
  metadata: GameTypeExtendedMetadata
): string[] {
  const errors: string[] = [];
  
  for (const [key, meta] of Object.entries(metadata.settingsMetadata)) {
    const value = settings[key];
    
    if (meta.isRequired && !value) {
      errors.push(`${key} is required`);
    }
    
    if (value && meta.cannotBeEmpty && !value.trim()) {
      errors.push(`${key} cannot be empty`);
    }
    
    if (value && meta.validationPattern) {
      const regex = new RegExp(meta.validationPattern);
      if (!regex.test(value)) {
        errors.push(meta.validationMessage || `${key} is invalid`);
      }
    }
  }
  
  return errors;
}
```

---

## Example 9: Bulk Metadata Management

### Loading Metadata from Configuration File

```csharp
public class MetadataSeeder
{
    private readonly IGameTypeExtendedMetadataRegistry _registry;
    
    public async Task SeedFromFile(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var metadataList = JsonSerializer.Deserialize<List<GameTypeExtendedMetadata>>(json);
        
        foreach (var metadata in metadataList ?? new())
        {
            await _registry.AddOrUpdate(metadata);
        }
    }
}
```

Seed file (`metadata-seed.json`):
```json
[
  {
    "gameTypeKey": "minecraft",
    "enableTTY": true,
    "settingsMetadata": { ... }
  },
  {
    "gameTypeKey": "valheim",
    "enableTTY": false,
    "settingsMetadata": { ... }
  }
]
```

---

## Best Practices

### 1. Always Validate on the Server Side
Even if you validate on the client, always validate again on the server:

```csharp
[HttpPost]
public async Task<IActionResult> CreateServer([FromBody] GameServer server)
{
    // Server-side validation is mandatory
    var errors = await _metadataApplier.ValidateSettings(server, server.GameType);
    if (errors.Any())
        return BadRequest(new { errors });
    
    // Proceed...
}
```

### 2. Cache Extended Metadata in UI
Cache the extended metadata to avoid repeated API calls:

```typescript
const metadataCache = new Map<string, GameTypeExtendedMetadata>();

async function getMetadata(gameTypeKey: string) {
  if (!metadataCache.has(gameTypeKey)) {
    const meta = await fetch(`/api/gametypes/extended/${gameTypeKey}`).then(r => r.json());
    metadataCache.set(gameTypeKey, meta);
  }
  return metadataCache.get(gameTypeKey);
}
```

### 3. Provide Defaults for Missing Metadata
Handle cases where extended metadata doesn't exist:

```csharp
var metadata = await _registry.Get(gameTypeKey) ?? new GameTypeExtendedMetadata
{
    GameTypeKey = gameTypeKey,
    EnableTTY = false,
    AttachStdin = false,
    SettingsMetadata = new()
};
```

### 4. Document Your Metadata
Add comprehensive descriptions to help users understand what each setting does:

```json
{
  "key": "MEMORY",
  "description": "Maximum memory allocation for the server. Format: <number>M or <number>G (e.g., 1024M, 2G). Recommended minimum: 1G for vanilla, 4G for modded servers.",
  "placeholder": "2G"
}
```

---

## Testing

### Unit Test Example

```csharp
[Fact]
public async Task ValidateSettings_RequiredFieldMissing_ReturnsError()
{
    // Arrange
    var metadata = new GameTypeExtendedMetadata
    {
        GameTypeKey = "test",
        SettingsMetadata = new()
        {
            ["EULA"] = new SettingMetadata
            {
                Key = "EULA",
                IsRequired = true
            }
        }
    };
    
    var registry = new Mock<IGameTypeExtendedMetadataRegistry>();
    registry.Setup(r => r.Get("test")).ReturnsAsync(metadata);
    
    var applier = new GameTypeMetadataApplier(registry.Object, Mock.Of<ILogger<GameTypeMetadataApplier>>());
    
    var server = new GameServer
    {
        GameType = "test",
        Settings = new() // EULA missing
    };
    
    // Act
    var errors = await applier.ValidateSettings(server, "test");
    
    // Assert
    Assert.Single(errors);
    Assert.Contains("EULA", errors[0]);
}
```

---

This integration guide should help you fully utilize the extended metadata system in your application!
