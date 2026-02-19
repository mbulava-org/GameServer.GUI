# SQLite Implementation Guide for GameType Data

## Overview

This guide shows how to migrate from file-based JSON storage to SQLite database for better data management, query capabilities, and scalability.

## Benefits Summary

| Feature | File Storage | SQLite Database |
|---------|--------------|-----------------|
| **ACID Transactions** | ? No | ? Yes |
| **Complex Queries** | ? Difficult | ? Easy (SQL/LINQ) |
| **Relationships** | ? Manual | ? Foreign keys |
| **Concurrent Access** | ? Manual locking | ? Built-in |
| **Data Validation** | ? Application only | ? Database constraints |
| **Performance** | ?? Slow for large data | ? Fast with indexes |
| **Backup** | ? Copy files | ? Copy single file |
| **Human Readable** | ? Yes (JSON) | ? No (binary) |

---

## Step-by-Step Implementation

### Step 1: Install NuGet Packages

```xml
<!-- In GameServer.Docker.csproj -->
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="10.0.0" />
```

### Step 2: Configure in Program.cs

```csharp
using GameServer.Docker.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add SQLite Database
var connectionString = builder.Configuration.GetConnectionString("GameServerDb") 
    ?? "Data Source=gameserver.db";
    
builder.Services.AddDbContext<GameServerDbContext>(options =>
    options.UseSqlite(connectionString));

// Optional: Add repository pattern
builder.Services.AddScoped<IGameTypeRepository, GameTypeRepository>();

var app = builder.Build();

// Ensure database is created (development only)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<GameServerDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
    // OR use migrations:
    // await dbContext.Database.MigrateAsync();
}

app.Run();
```

### Step 3: Create Initial Migration

```bash
# Navigate to Docker project
cd src/GameServer.Docker

# Create initial migration
dotnet ef migrations add InitialCreate --output-dir Data/Migrations

# Apply migration
dotnet ef database update
```

### Step 4: Create Repository Interface

```csharp
// src/GameServer.Docker/Repositories/IGameTypeRepository.cs
using GameServer.Docker.Models;

namespace GameServer.Docker.Repositories
{
    public interface IGameTypeRepository
    {
        // Query methods
        Task<List<GameTypeDefinition>> GetAllAsync(bool includeInactive = false);
        Task<GameTypeDefinition?> GetByKeyAsync(string key);
        Task<GameTypeDefinition?> GetByIdAsync(int id);
        Task<List<GameTypeDefinition>> SearchAsync(string searchTerm);
        Task<List<GameTypeDefinition>> GetWithTTYEnabledAsync();
        
        // CRUD operations
        Task<GameTypeDefinition> CreateAsync(GameTypeDefinition gameType);
        Task<GameTypeDefinition> UpdateAsync(GameTypeDefinition gameType);
        Task DeleteAsync(string key);
        Task<bool> ExistsAsync(string key);
        
        // Extended metadata operations
        Task<GameTypeExtendedMetadata?> GetExtendedMetadataAsync(string gameTypeKey);
        Task<GameTypeExtendedMetadata> SaveExtendedMetadataAsync(GameTypeExtendedMetadata metadata);
        
        // Port operations
        Task<List<PortDefinition>> GetPortsAsync(string gameTypeKey);
        Task UpdatePortsAsync(string gameTypeKey, List<PortDefinition> ports);
        
        // Setting operations
        Task<SettingMetadata?> GetSettingMetadataAsync(string gameTypeKey, string settingKey);
        Task UpdateSettingMetadataAsync(string gameTypeKey, string settingKey, SettingMetadata metadata);
    }
}
```

### Step 5: Implement Repository

```csharp
// src/GameServer.Docker/Repositories/GameTypeRepository.cs
using GameServer.Docker.Data;
using GameServer.Docker.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GameServer.Docker.Repositories
{
    public class GameTypeRepository : IGameTypeRepository
    {
        private readonly GameServerDbContext _context;

        public GameTypeRepository(GameServerDbContext context)
        {
            _context = context;
        }

        public async Task<List<GameTypeDefinition>> GetAllAsync(bool includeInactive = false)
        {
            var query = _context.GameTypes
                .Include(gt => gt.Ports)
                .Include(gt => gt.Volumes)
                .Include(gt => gt.DefaultSettings)
                .Include(gt => gt.ExtendedMetadata)
                    .ThenInclude(em => em!.SettingsMetadata)
                        .ThenInclude(sm => sm.PortValidation)
                .Include(gt => gt.ExtendedMetadata)
                    .ThenInclude(em => em!.SettingsMetadata)
                        .ThenInclude(sm => sm.PortRelationships)
                .AsQueryable();

            if (!includeInactive)
            {
                query = query.Where(gt => gt.IsActive);
            }

            var entities = await query.ToListAsync();
            return entities.Select(MapToModel).ToList();
        }

        public async Task<GameTypeDefinition?> GetByKeyAsync(string key)
        {
            var entity = await _context.GameTypes
                .Include(gt => gt.Ports)
                .Include(gt => gt.Volumes)
                .Include(gt => gt.DefaultSettings)
                .Include(gt => gt.ExtendedMetadata)
                .FirstOrDefaultAsync(gt => gt.Key == key);

            return entity == null ? null : MapToModel(entity);
        }

        public async Task<GameTypeDefinition> CreateAsync(GameTypeDefinition gameType)
        {
            var entity = MapToEntity(gameType);
            _context.GameTypes.Add(entity);
            await _context.SaveChangesAsync();
            return MapToModel(entity);
        }

        public async Task<GameTypeDefinition> UpdateAsync(GameTypeDefinition gameType)
        {
            var entity = await _context.GameTypes
                .Include(gt => gt.Ports)
                .Include(gt => gt.Volumes)
                .Include(gt => gt.DefaultSettings)
                .FirstOrDefaultAsync(gt => gt.Key == gameType.Key);

            if (entity == null)
            {
                throw new KeyNotFoundException($"GameType with key '{gameType.Key}' not found");
            }

            UpdateEntity(entity, gameType);
            await _context.SaveChangesAsync();
            return MapToModel(entity);
        }

        public async Task DeleteAsync(string key)
        {
            var entity = await _context.GameTypes.FirstOrDefaultAsync(gt => gt.Key == key);
            if (entity != null)
            {
                _context.GameTypes.Remove(entity);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> ExistsAsync(string key)
        {
            return await _context.GameTypes.AnyAsync(gt => gt.Key == key);
        }

        public async Task<List<GameTypeDefinition>> SearchAsync(string searchTerm)
        {
            var query = _context.GameTypes
                .Include(gt => gt.Ports)
                .Include(gt => gt.Volumes)
                .Include(gt => gt.DefaultSettings)
                .Where(gt => gt.IsActive &&
                    (gt.Key.Contains(searchTerm) ||
                     gt.DisplayName.Contains(searchTerm) ||
                     (gt.Description != null && gt.Description.Contains(searchTerm))));

            var entities = await query.ToListAsync();
            return entities.Select(MapToModel).ToList();
        }

        public async Task<List<GameTypeDefinition>> GetWithTTYEnabledAsync()
        {
            var query = _context.GameTypes
                .Include(gt => gt.Ports)
                .Include(gt => gt.Volumes)
                .Include(gt => gt.DefaultSettings)
                .Include(gt => gt.ExtendedMetadata)
                .Where(gt => gt.IsActive && gt.ExtendedMetadata != null && gt.ExtendedMetadata.EnableTTY);

            var entities = await query.ToListAsync();
            return entities.Select(MapToModel).ToList();
        }

        // Mapping methods
        private GameTypeDefinition MapToModel(GameTypeEntity entity)
        {
            return new GameTypeDefinition
            {
                Key = entity.Key,
                DisplayName = entity.DisplayName,
                Description = entity.Description,
                Image = entity.Image,
                ThumbnailUrl = entity.ThumbnailUrl,
                DocumentationUrl = entity.DocumentationUrl,
                Ports = entity.Ports.Select(p => new PortDefinition
                {
                    Port = p.Port,
                    Protocol = p.Protocol,
                    IsDefaultPort = p.IsDefaultPort
                }).ToList(),
                Volumes = entity.Volumes.Select(v => new VolumeDefinition
                {
                    Source = v.Source,
                    Target = v.Target,
                    ReadOnly = v.ReadOnly
                }).ToList(),
                DefaultSettings = entity.DefaultSettings.ToDictionary(
                    ds => ds.SettingKey,
                    ds => ds.SettingValue ?? string.Empty
                )
            };
        }

        private GameTypeEntity MapToEntity(GameTypeDefinition model)
        {
            return new GameTypeEntity
            {
                Key = model.Key,
                DisplayName = model.DisplayName,
                Description = model.Description,
                Image = model.Image,
                ThumbnailUrl = model.ThumbnailUrl,
                DocumentationUrl = model.DocumentationUrl,
                IsActive = true,
                Ports = model.Ports?.Select(p => new PortEntity
                {
                    Port = p.Port,
                    Protocol = p.Protocol,
                    IsDefaultPort = p.IsDefaultPort
                }).ToList() ?? new List<PortEntity>(),
                Volumes = model.Volumes?.Select(v => new VolumeEntity
                {
                    Source = v.Source,
                    Target = v.Target,
                    ReadOnly = v.ReadOnly
                }).ToList() ?? new List<VolumeEntity>(),
                DefaultSettings = model.DefaultSettings?.Select(ds => new DefaultSettingEntity
                {
                    SettingKey = ds.Key,
                    SettingValue = ds.Value
                }).ToList() ?? new List<DefaultSettingEntity>()
            };
        }

        private void UpdateEntity(GameTypeEntity entity, GameTypeDefinition model)
        {
            entity.DisplayName = model.DisplayName;
            entity.Description = model.Description;
            entity.Image = model.Image;
            entity.ThumbnailUrl = model.ThumbnailUrl;
            entity.DocumentationUrl = model.DocumentationUrl;

            // Update ports
            entity.Ports.Clear();
            if (model.Ports != null)
            {
                foreach (var port in model.Ports)
                {
                    entity.Ports.Add(new PortEntity
                    {
                        Port = port.Port,
                        Protocol = port.Protocol,
                        IsDefaultPort = port.IsDefaultPort
                    });
                }
            }

            // Update volumes
            entity.Volumes.Clear();
            if (model.Volumes != null)
            {
                foreach (var volume in model.Volumes)
                {
                    entity.Volumes.Add(new VolumeEntity
                    {
                        Source = volume.Source,
                        Target = volume.Target,
                        ReadOnly = volume.ReadOnly
                    });
                }
            }

            // Update settings
            entity.DefaultSettings.Clear();
            if (model.DefaultSettings != null)
            {
                foreach (var setting in model.DefaultSettings)
                {
                    entity.DefaultSettings.Add(new DefaultSettingEntity
                    {
                        SettingKey = setting.Key,
                        SettingValue = setting.Value
                    });
                }
            }
        }
    }
}
```

### Step 6: Update Controllers

```csharp
// Update GameTypeController to use repository
[ApiController]
[Route("api/[controller]")]
public class GameTypeController : ControllerBase
{
    private readonly IGameTypeRepository _repository;
    private readonly ILogger<GameTypeController> _logger;

    public GameTypeController(IGameTypeRepository repository, ILogger<GameTypeController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<GameTypeDefinition>>> GetAll()
    {
        var gameTypes = await _repository.GetAllAsync();
        return Ok(gameTypes);
    }

    [HttpGet("{key}")]
    public async Task<ActionResult<GameTypeDefinition>> GetByKey(string key)
    {
        var gameType = await _repository.GetByKeyAsync(key);
        if (gameType == null)
        {
            return NotFound();
        }
        return Ok(gameType);
    }

    [HttpPost]
    public async Task<ActionResult<GameTypeDefinition>> Create([FromBody] GameTypeDefinition gameType)
    {
        if (await _repository.ExistsAsync(gameType.Key))
        {
            return Conflict($"GameType with key '{gameType.Key}' already exists");
        }

        var created = await _repository.CreateAsync(gameType);
        return CreatedAtAction(nameof(GetByKey), new { key = created.Key }, created);
    }

    [HttpPut("{key}")]
    public async Task<ActionResult<GameTypeDefinition>> Update(string key, [FromBody] GameTypeDefinition gameType)
    {
        if (key != gameType.Key)
        {
            return BadRequest("Key mismatch");
        }

        try
        {
            var updated = await _repository.UpdateAsync(gameType);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{key}")]
    public async Task<IActionResult> Delete(string key)
    {
        await _repository.DeleteAsync(key);
        return NoContent();
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<GameTypeDefinition>>> Search([FromQuery] string q)
    {
        var results = await _repository.SearchAsync(q);
        return Ok(results);
    }

    [HttpGet("with-tty")]
    public async Task<ActionResult<List<GameTypeDefinition>>> GetWithTTY()
    {
        var results = await _repository.GetWithTTYEnabledAsync();
        return Ok(results);
    }
}
```

---

## Migration Strategy

### Phase 1: Dual Write (Safe Migration)

```csharp
public class HybridGameTypeRepository : IGameTypeRepository
{
    private readonly GameTypeRepository _sqliteRepository;
    private readonly FileBasedGameTypeRepository _fileRepository;

    public async Task<GameTypeDefinition> CreateAsync(GameTypeDefinition gameType)
    {
        // Write to both
        var sqliteResult = await _sqliteRepository.CreateAsync(gameType);
        await _fileRepository.CreateAsync(gameType);
        return sqliteResult;
    }

    public async Task<GameTypeDefinition?> GetByKeyAsync(string key)
    {
        // Read from SQLite
        var result = await _sqliteRepository.GetByKeyAsync(key);
        
        // Fallback to files if not found
        if (result == null)
        {
            result = await _fileRepository.GetByKeyAsync(key);
            if (result != null)
            {
                // Migrate to SQLite
                await _sqliteRepository.CreateAsync(result);
            }
        }
        
        return result;
    }
}
```

### Phase 2: Data Migration Script

```csharp
public class DataMigrationService
{
    private readonly IGameTypeRepository _sqliteRepo;
    private readonly IConfiguration _configuration;

    public async Task MigrateFromFilesAsync(string filesDirectory)
    {
        var files = Directory.GetFiles(filesDirectory, "*.json");
        
        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var gameType = JsonSerializer.Deserialize<GameTypeDefinition>(json);
                
                if (gameType != null && !await _sqliteRepo.ExistsAsync(gameType.Key))
                {
                    await _sqliteRepo.CreateAsync(gameType);
                    Console.WriteLine($"Migrated: {gameType.Key}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error migrating {file}: {ex.Message}");
            }
        }
    }
}
```

### Phase 3: Switchover

```csharp
// In Program.cs
if (useSqlite)
{
    builder.Services.AddScoped<IGameTypeRepository, GameTypeRepository>();
}
else
{
    builder.Services.AddScoped<IGameTypeRepository, FileBasedGameTypeRepository>();
}
```

---

## Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "GameServerDb": "Data Source=gameserver.db"
  },
  "GameTypeStorage": {
    "Type": "SQLite",  // or "Files"
    "FilesDirectory": "./data/gametypes",
    "BackupEnabled": true,
    "BackupDirectory": "./backups"
  }
}
```

---

## Performance Comparisons

### Query Performance

**File-based:**
```csharp
// Read ALL files, parse JSON, filter in memory
var allGameTypes = await fileRepo.GetAllAsync();
var withTTY = allGameTypes.Where(gt => gt.ExtendedMetadata?.EnableTTY == true);
// O(n) - reads all files
```

**SQLite:**
```csharp
// Query with WHERE clause, only returns matching rows
var withTTY = await sqliteRepo.GetWithTTYEnabledAsync();
// O(log n) with index - very fast
```

### Concurrent Access

**File-based:**
```csharp
// Manual file locking required
lock (_fileLock)
{
    File.WriteAllText(path, json);
}
```

**SQLite:**
```csharp
// Built-in locking and transactions
await _context.SaveChangesAsync(); // Thread-safe
```

---

## Backup Strategy

### Automated Backup

```csharp
public class DatabaseBackupService : IHostedService
{
    public async Task BackupDatabase()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var backupPath = $"./backups/gameserver_{timestamp}.db";
        
        File.Copy("gameserver.db", backupPath);
        
        // Keep only last 30 backups
        CleanupOldBackups();
    }
}
```

---

## Summary

### Pros of SQLite for GameType Data

? **Better data integrity** - Foreign keys, constraints  
? **Faster queries** - Indexes, SQL optimization  
? **Concurrent access** - Built-in locking  
? **Relationships** - Proper modeling  
? **Transactions** - ACID compliance  
? **Scalability** - Handles thousands of records easily  
? **Single file** - Easy backup/restore  
? **EF Core support** - LINQ queries, migrations  

### When to Use Files

- Very simple data (< 10 game types)
- Human editing required
- No relationships needed
- No concurrent access

### When to Use SQLite

- Complex relationships (ports, settings, metadata)
- Many game types (> 10)
- Concurrent access needed
- Complex queries required
- **Recommended for your use case!**

---

**Recommendation:** Implement SQLite database for production use! ??
