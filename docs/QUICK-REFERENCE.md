# ?? Advanced Port Mapping - Quick Reference

## ? What's Complete

| Component | Status | Location |
|-----------|--------|----------|
| **PortRelationship Model** | ? Ready | `src/GameServer.Docker/Models/PortRelationship.cs` |
| **PortValidationRule Model** | ? Ready | `src/GameServer.Docker/Models/PortRelationship.cs` |
| **Extended SettingMetadata** | ? Ready | `src/GameServer.Docker/Models/SettingMetadata.cs` |
| **PortMappingService** | ?? Packaged | `src/GameServer.Web/Services/PortMappingService.cs.future` |
| **Documentation** | ? Complete | `docs/Advanced-Port-Mapping-*.md` (7 files) |
| **Examples** | ? Ready | `docs/Game-Server-Port-Examples.json` |
| **Build** | ? Success | No errors |

## ?? Activation Checklist

- [ ] **API exposes PortRelationship model**
- [ ] **API exposes PortValidationRule model**
- [ ] **API returns extended SettingMetadata**
- [ ] **Regenerate Docker.Client** (`dotnet build` in Client project)
- [ ] **Rename PortMappingService.cs.future** ? `PortMappingService.cs`
- [ ] **Uncomment service registration** in Program.cs
- [ ] **Build and test**

## ?? Quick Start Examples

### Counter-Strike (Offset +1 Pattern)
```
Game Port:  27015 (UDP) ? User sets
Query Port: 27016 (UDP) ? Auto: game + 1
RCON Port:  27015 (TCP) ? Same port, different protocol
```

### Minecraft (Same Port + Fixed RCON)
```
Game Port:  25565 (TCP) ? User sets
Query Port: 25565 (UDP) ? Same port, UDP
RCON Port:  25575 (TCP) ? Fixed, never changes
```

### ARK (Multiple Offsets)
```
Game Port:  7777 (UDP) ? User sets
Query Port: 7778 (UDP) ? Auto: game + 1
RCON Port:  27020 (TCP) ? Auto: game + 19243
```

## ?? Documentation Files

| File | Purpose |
|------|---------|
| **Advanced-Port-Mapping-System.md** | Complete guide (800 lines) |
| **Game-Server-Port-Examples.json** | 8+ game configs (500 lines) |
| **Port-Mapping-Implementation-Summary.md** | Quick reference |
| **Port-Mapping-Integration-Guide.md** | Architecture & integration |
| **Option1-Implementation-Guide.md** | Step-by-step |
| **Docker-Client-Regeneration-Status.md** | Current blockers |
| **Advanced-Port-Mapping-FINAL-STATUS.md** | This summary |

## ?? Code Locations

```
src/
??? GameServer.Docker/
?   ??? Models/
?       ??? PortRelationship.cs ? (100 lines)
?       ??? SettingMetadata.cs ? (extended +40 lines)
??? GameServer.Web/
    ??? Services/
        ??? PortMappingService.cs.future ?? (300 lines)
```

## ?? Ready-to-Use Game Configs

- ? Counter-Strike / CS:GO
- ? Minecraft
- ? ARK: Survival Evolved
- ? Rust
- ? Valheim
- ? Team Fortress 2
- ? 7 Days to Die
- ? Palworld

All in `docs/Game-Server-Port-Examples.json`!

## ? Quick Manual Implementation

While waiting for API, use this pattern:

```csharp
// Calculate related ports
private uint GetQueryPort(uint gamePort) => gamePort + 1;

// Validate port
private bool IsValidPort(uint port, uint min, uint max) 
    => port >= min && port <= max;

// Add ports to game type
gameType.Ports.Add(new PortDefinition 
{ 
    Port = (int)gamePort, 
    Protocol = "udp" 
});
gameType.Ports.Add(new PortDefinition 
{ 
    Port = (int)GetQueryPort(gamePort), 
    Protocol = "udp" 
});
```

## ?? Activation Commands

```bash
# When API is ready:

# 1. Regenerate Docker.Client
cd src/GameServer.Docker.Client
dotnet clean
dotnet build --no-incremental

# 2. Activate service
cd ../GameServer.Web/Services
Rename-Item "PortMappingService.cs.future" "PortMappingService.cs"

# 3. Build solution
cd ../../..
dotnet build

# Should compile successfully!
```

## ?? Statistics

- **Total Lines:** ~3,000 (code + docs)
- **Models:** 3 new classes
- **Service Methods:** 5 public methods
- **Game Examples:** 8 complete configs
- **Documentation:** 7 comprehensive guides
- **Time to Activate:** ~30 minutes (when API ready)

## ?? Use Cases

### 1. Server Creation
When user creates server, auto-calculate all required ports.

### 2. Port Validation
Prevent invalid ports before server deployment.

### 3. Port Conflicts
Check availability before allocating.

### 4. Documentation
Reference for game-specific port patterns.

### 5. Migration
Guide for existing server port updates.

## ? What Works Now

Even without the service:
- ? Documentation available
- ? Patterns documented
- ? Examples ready to copy
- ? Models in Docker project
- ? Manual implementation possible

## ?? Support

**Questions?** Check the comprehensive docs:
- System design ? `Advanced-Port-Mapping-System.md`
- Examples ? `Game-Server-Port-Examples.json`
- Integration ? `Port-Mapping-Integration-Guide.md`
- Status ? `Advanced-Port-Mapping-FINAL-STATUS.md`

---

**Status:** ?? **PACKAGED AND READY**  
**Build:** ? **SUCCESS**  
**Activation:** Waiting for API support OR use manual patterns now!
