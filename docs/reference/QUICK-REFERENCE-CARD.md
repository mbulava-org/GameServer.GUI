# Extended Metadata - Quick Reference Card

## New Features at a Glance

### 1. Enum Fields (Dropdowns)
```json
{
  "dataType": "enum",
  "allowedValues": ["option1", "option2", "option3"]
}
```

### 2. Value Descriptions
```json
{
  "valueMappings": {
    "0": "Description for 0",
    "1": "Description for 1"
  }
}
```

### 3. Port Mapping (Fixed)
```json
{
  "mapsToContainerPort": true,
  "linkedContainerPort": 25565,
  "portProtocol": "tcp"
}
```

### 4. Default Port
```csharp
new PortDefinition(25565, "tcp", true)  // 3rd param = is default
```

---

## Code Snippets

### Apply Port Mappings
```csharp
var ports = await _metadataApplier.ApplyDynamicPortMappings(server, definition);
```

### Get Default Port
```csharp
var defaultPort = _metadataApplier.GetDefaultPort(ports);
Console.WriteLine($"Connect to: {serverIp}:{defaultPort.Port}");
```

### Render Dropdown (React)
```tsx
{meta.dataType === 'enum' && (
  <select value={value} onChange={e => setValue(e.target.value)}>
    {meta.allowedValues?.map(val => (
      <option key={val} value={val}>
        {meta.valueMappings?.[val] || val}
      </option>
    ))}
  </select>
)}
```

### Show Description
```tsx
{meta.valueMappings?.[value] && (
  <p className="help-text">{meta.valueMappings[value]}</p>
)}
```

---

## Complete Example

### GameTypeDefinition
```csharp
new GameTypeDefinition
{
    Key = "minecraft",
    Ports = new() 
    { 
        new PortDefinition(25565, "tcp", true)  // Default port
    },
    DefaultSettings = new()
    {
        ["SERVER_PORT"] = "25565",
        ["DIFFICULTY"] = "easy"
    }
}
```

### Extended Metadata
```json
{
  "gameTypeKey": "minecraft",
  "enableTTY": true,
  "settingsMetadata": {
    "SERVER_PORT": {
      "key": "SERVER_PORT",
      "dataType": "port",
      "mapsToContainerPort": true,
      "linkedContainerPort": 25565,
      "portProtocol": "tcp"
    },
    "DIFFICULTY": {
      "key": "DIFFICULTY",
      "dataType": "enum",
      "allowedValues": ["peaceful", "easy", "normal", "hard"],
      "valueMappings": {
        "peaceful": "Peaceful - No mobs",
        "easy": "Easy",
        "normal": "Normal",
        "hard": "Hard"
      }
    }
  }
}
```

### User Creates Server
```json
{
  "gameType": "minecraft",
  "settings": {
    "SERVER_PORT": "25566",
    "DIFFICULTY": "hard"
  }
}
```

### Result
- ? Port updates: 25565 ? 25566
- ? Difficulty validated against enum
- ? TTY enabled
- ? Container created successfully

---

## Data Types

| Type | Use Case | Example |
|------|----------|---------|
| `string` | Free text | Name, Description |
| `number` | Numeric input | Max Players, Timeout |
| `boolean` | True/False | Enable Feature |
| `enum` | Dropdown selection | Difficulty, Game Mode |
| `list` | Comma-separated | Mod URLs, Banned IPs |
| `port` | Port number + mapping | Server Port |
| `timezone` | Timezone selection | TZ environment variable |

---

## Common Patterns

### Required Enum
```json
{
  "key": "EULA",
  "isRequired": true,
  "dataType": "enum",
  "allowedValues": ["true", "false"]
}
```

### Port with Default
```json
{
  "key": "SERVER_PORT",
  "dataType": "port",
  "mapsToContainerPort": true,
  "linkedContainerPort": 25565,
  "placeholder": "25565"
}
```

### Numeric Enum with Descriptions
```json
{
  "key": "LOG_LEVEL",
  "dataType": "enum",
  "allowedValues": ["0", "1", "2", "3"],
  "valueMappings": {
    "0": "Silent",
    "1": "Errors Only",
    "2": "Warnings",
    "3": "Verbose"
  }
}
```

### List with Custom Delimiter
```json
{
  "key": "MODS",
  "dataType": "list",
  "listDelimiter": "|"
}
```

---

## Validation Rules

### Required Field
```json
{ "isRequired": true }
```

### Cannot Be Empty
```json
{ "cannotBeEmpty": true }
```

### Regex Pattern
```json
{
  "validationPattern": "^\\d+[MG]$",
  "validationMessage": "Must be number + M or G"
}
```

### Enum (Automatic)
```json
{
  "dataType": "enum",
  "allowedValues": ["val1", "val2"]
}
```

---

## UI Integration Checklist

- [ ] Fetch extended metadata
- [ ] Render enum fields as dropdowns
- [ ] Show value descriptions
- [ ] Apply validation rules
- [ ] Display default port prominently
- [ ] Handle port mapping updates
- [ ] Show validation errors

---

## API Endpoints

| Method | Endpoint | Purpose |
|--------|----------|---------|
| GET | `/api/gametypes/extended` | List all |
| GET | `/api/gametypes/extended/{key}` | Get one |
| POST | `/api/gametypes/extended` | Create/Update |
| DELETE | `/api/gametypes/extended/{key}` | Delete |
| PUT | `/api/gametypes/extended/{key}/settings/{settingKey}` | Update setting |

---

## Testing Commands

### Get Minecraft Metadata
```bash
curl http://localhost:5000/api/gametypes/extended/minecraft | jq
```

### Update Setting
```bash
curl -X PUT http://localhost:5000/api/gametypes/extended/minecraft/settings/DIFFICULTY \
  -H "Content-Type: application/json" \
  -d '{ "key": "DIFFICULTY", "dataType": "enum", "allowedValues": ["easy", "hard"] }'
```

### Create Server
```bash
curl -X POST http://localhost:5000/api/gameservers \
  -H "Content-Type: application/json" \
  -d '{ "gameType": "minecraft", "settings": { "SERVER_PORT": "25566" } }'
```

---

**Keep this card handy for quick reference!** ??
