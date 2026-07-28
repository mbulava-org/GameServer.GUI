# Database Initialization - Solution for NSwag Client Generation

## Problem

Database initialization at startup can interfere with NSwag client generation, which needs a clean startup to generate the OpenAPI specification.

## Solution

Use a **conditional flag** to skip database initialization when generating the OpenAPI spec.

---

## Implementation

### 1. Program.cs - Conditional Initialization

**File:** `src\GameServer.Docker\Program.cs`

```csharp
var app = builder.Build();

// Initialize database (conditional - skip if generating OpenAPI spec)
if (!args.Contains("--no-db-init"))
{
    await InitializeDatabaseAsync(app.Services);
}
```

**How it works:**
- **Normal startup**: Database initializes automatically
- **Client generation**: Use `--no-db-init` flag to skip database init
- **Zero impact on production**: Flag not present = normal initialization

### 2. NSwag Configuration Update

**File:** `src\GameServer.Docker.Client\nswag.json`

Update the `aspNetCoreToOpenApi` section to pass the flag:

```json
{
  "documentGenerator": {
    "aspNetCoreToOpenApi": {
      "project": "../GameServer.Docker/GameServer.Docker.csproj",
      "arguments": [ "--no-db-init" ],
      "documentName": "v1",
      "configuration": "$(Configuration)",
      "targetFramework": "net10.0",
      "noBuild": false,
      "verbose": true
    }
  }
}
```

### 3. Manual Client Regeneration

**PowerShell:**
```powershell
cd src\GameServer.Docker.Client
dotnet nswag run
```

**Command Line:**
```bash
cd src/GameServer.Docker.Client
dotnet nswag run
```

**With explicit flag (if needed):**
```powershell
cd src\GameServer.Docker
dotnet run --no-db-init
```

---

## Database Initialization Logic

### InitializeDatabaseAsync

```csharp
private static async Task InitializeV2DatabaseAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var repository = scope.ServiceProvider.GetRequiredService<Repositories.V2.IGameTypeRepository>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("Initializing V2 database...");
        await repository.InitializeDatabaseAsync();
        logger.LogInformation("V2 database initialization complete.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error initializing V2 database");
        throw;
    }
}
```

### MigrateFromJsonIfExistsAsync

Migrates existing JSON game type definitions to SQLite database if found.

**Search locations:**
- `./data/gametypes/*.json`
- Configured via `GameTypeStorage:DataDirectory`

**Process:**
1. Scan for JSON files
2. Deserialize each file
3. Check if already exists in database
4. Create database entities
5. Save to database
6. Log migration results

---

## Testing

### Test Normal Startup (With DB Init)

```powershell
cd src\GameServer.Docker
dotnet run
```

**Expected output:**
```
[10:00:00 INF] Initializing database...
[10:00:00 INF] Database initialized. Found 3 game types.
```

### Test Client Generation (Without DB Init)

```powershell
cd src\GameServer.Docker.Client
dotnet nswag run
```

**Expected output:**
```
NSwag command line tool for .NET...
Duration: 00:00:05
```

**No database initialization messages should appear!**

### Test Manual Flag

```powershell
cd src\GameServer.Docker
dotnet run --no-db-init
```

**Expected output:**
```
Application started... (no database messages)
```

---

## CI/CD Integration

### GitHub Actions (Already Configured)

The workflow doesn't need changes! NSwag runs with `noBuild: false`, which means:
1. Project builds fresh
2. NSwag starts the app to discover endpoints
3. App detects it's running for NSwag (no args = normal startup would happen)
4. Update nswag.json to pass `--no-db-init`

### Docker Build

No changes needed. The Dockerfile runs the application normally, so database initialization happens automatically.

---

## Database Location

### V2 persistence

V2 is the only persistence layer. It is provider-driven through `V2Database` settings. **SQLite is the current default.** MySQL is supported. PostgreSQL is prepared in code but not fully implemented.

```json
{
  "ConnectionStrings": {
    "GameServerV2Db": "Data Source=./data/gameserver-v2.db",
    "GameServerV2MySqlDb": "Server=localhost;Database=gameserver-v2;Uid=root;Pwd=password;",
    "GameServerV2PostgresDb": "Host=localhost;Database=gameserver-v2;Username=postgres;Password=postgres"
  },
  "V2Database": {
    "Provider": "Sqlite",
    "ConnectionStringName": "GameServerV2Db"
  }
}
```

Valid values for `V2Database:Provider` are `Sqlite`, `MySql`, and `PostgreSql` (experimental).

When PostgreSQL becomes fully supported, deploy its schema with:

```powershell
.\scripts\Deploy-V2PostgresDatabase.ps1 -TargetConnectionString "Host=localhost;Database=gameserver-v2;Username=postgres;Password=postgres"
```

**Docker Mount:** Ensure `/data` is mounted as a volume to persist the legacy SQLite database across container restarts.

---

## Migration from JSON

### Automatic Migration

If database is empty on first startup:
1. Scans `./data/gametypes/` for `*.json` files
2. Reads each file
3. Converts to database entities
4. Saves to SQLite

### Manual Migration

If you want to force re-migration:
1. Delete `gameserver.db`
2. Restart application
3. JSON files will be re-imported

### Backup JSON Files

**Recommendation:** Keep JSON files as backup even after migration

```
./data/
??? gameserver.db         (primary database)
??? gametypes/            (backup JSON files)
    ??? minecraft.json
    ??? valheim.json
    ??? ...
```

---

## Troubleshooting

### "Table already exists" error
- Delete `gameserver.db` and restart
- Or use migrations: `dotnet ef migrations add InitialCreate`

### Client generation hangs
- Verify `--no-db-init` flag in nswag.json
- Check if database file is locked
- Kill any running instances of GameServer.Docker

### Database not initializing in production
- Check logs for "Initializing database..." message
- Verify connection string is correct
- Ensure write permissions to `./data/` directory

### JSON migration not working
- Check `GameTypeStorage:DataDirectory` configuration
- Verify JSON files exist in `./data/gametypes/`
- Check logs for migration messages

---

## Best Practices

### Development
? Let database initialize automatically  
? Keep JSON files as backup  
? Use `--no-db-init` only for client generation  

### Production
? Use volume mounts for `/data` directory  
? Regular database backups  
? Monitor initialization logs  
? Consider using migrations for schema updates  

### CI/CD
? Update nswag.json with `--no-db-init` flag  
? Test client generation in CI pipeline  
? Verify database initialization in integration tests  

---

## Summary

? **Database initialization now runs at startup**  
? **NSwag client generation unaffected** (uses `--no-db-init` flag)  
? **Automatic JSON migration** (if database empty)  
? **Zero configuration for normal operation**  
? **Simple flag for special cases**  

The solution is production-ready and maintains compatibility with the existing client generation workflow!
