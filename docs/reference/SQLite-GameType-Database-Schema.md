# Legacy SQLite Database Schema for GameType Management

> **This schema is obsolete.** The primary service now uses the V2 database schema (`GameServerV2DbContext`). This document is preserved only as historical reference for the original SQLite implementation.

## Schema Overview

```
GameTypes
    ??? Ports (1:N)
    ??? Volumes (1:N)
    ??? DefaultSettings (1:N)
    ?       ??? SettingsMetadata (0:1) ? Optional metadata for this setting
    ?               ??? PortRelationships (1:N)
    ?               ??? PortValidation (1:1)
    ??? ExtendedMetadata (1:1) ? General game type metadata (like EnableTTY)
```

## Table Definitions

### GameTypes Table
```sql
CREATE TABLE GameTypes (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Key TEXT NOT NULL UNIQUE,              -- minecraft, valheim, etc.
    DisplayName TEXT NOT NULL,
    Description TEXT,
    Image TEXT NOT NULL,                   -- Docker image
    ThumbnailUrl TEXT,
    DocumentationUrl TEXT,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    IsActive BOOLEAN DEFAULT 1,
    
    CONSTRAINT UK_GameTypes_Key UNIQUE (Key)
);

CREATE INDEX IX_GameTypes_Key ON GameTypes(Key);
CREATE INDEX IX_GameTypes_IsActive ON GameTypes(IsActive);
```

### Ports Table
```sql
CREATE TABLE Ports (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    GameTypeId INTEGER NOT NULL,
    Port INTEGER NOT NULL,
    Protocol TEXT NOT NULL,                -- tcp, udp
    IsDefaultPort BOOLEAN DEFAULT 0,       -- Primary connection port
    Description TEXT,
    DisplayOrder INTEGER DEFAULT 0,
    
    FOREIGN KEY (GameTypeId) REFERENCES GameTypes(Id) ON DELETE CASCADE,
    CONSTRAINT CK_Ports_Protocol CHECK (Protocol IN ('tcp', 'udp', 'tcp/udp')),
    CONSTRAINT CK_Ports_Range CHECK (Port >= 1 AND Port <= 65535)
);

CREATE INDEX IX_Ports_GameTypeId ON Ports(GameTypeId);
CREATE INDEX IX_Ports_IsDefaultPort ON Ports(IsDefaultPort);
```

### Volumes Table
```sql
CREATE TABLE Volumes (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    GameTypeId INTEGER NOT NULL,
    Source TEXT NOT NULL,                  -- Host path
    Target TEXT NOT NULL,                  -- Container path
    ReadOnly BOOLEAN DEFAULT 0,
    Description TEXT,
    DisplayOrder INTEGER DEFAULT 0,
    
    FOREIGN KEY (GameTypeId) REFERENCES GameTypes(Id) ON DELETE CASCADE
);

CREATE INDEX IX_Volumes_GameTypeId ON Volumes(GameTypeId);
```

### DefaultSettings Table
```sql
CREATE TABLE DefaultSettings (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    GameTypeId INTEGER NOT NULL,
    SettingKey TEXT NOT NULL,
    SettingValue TEXT,
    Description TEXT,
    DisplayOrder INTEGER DEFAULT 0,
    
    FOREIGN KEY (GameTypeId) REFERENCES GameTypes(Id) ON DELETE CASCADE,
    CONSTRAINT UK_DefaultSettings_GameTypeKey UNIQUE (GameTypeId, SettingKey)
);

CREATE INDEX IX_DefaultSettings_GameTypeId ON DefaultSettings(GameTypeId);
CREATE INDEX IX_DefaultSettings_Key ON DefaultSettings(SettingKey);
```

**Note:** Each DefaultSetting can have 0 or 1 SettingsMetadata record (defined below) that describes how to present and validate this setting in the UI.

### ExtendedMetadata Table
```sql
CREATE TABLE ExtendedMetadata (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    GameTypeId INTEGER NOT NULL UNIQUE,    -- 1:1 relationship
    EnableTTY BOOLEAN DEFAULT 0,
    CustomPropertiesJson TEXT,             -- JSON blob for extensibility
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (GameTypeId) REFERENCES GameTypes(Id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX IX_ExtendedMetadata_GameTypeId ON ExtendedMetadata(GameTypeId);
```

**Note:** This stores game-type-level metadata (like TTY support). Setting-specific metadata is stored in SettingsMetadata table (linked to DefaultSettings).

### SettingsMetadata Table
```sql
CREATE TABLE SettingsMetadata (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    DefaultSettingId INTEGER NOT NULL UNIQUE,  -- 1:1 relationship with DefaultSetting
    Description TEXT,                          -- UI description (can override DefaultSettings.Description)
    IsRequired BOOLEAN DEFAULT 0,
    CannotBeEmpty BOOLEAN DEFAULT 0,
    DataType TEXT,                             -- string, number, boolean, enum, list, port
    Category TEXT,
    DisplayOrder INTEGER DEFAULT 0,
    Placeholder TEXT,
    ValidationPattern TEXT,
    ValidationMessage TEXT,
    
    -- Port-specific fields
    MapsToContainerPort BOOLEAN DEFAULT 0,
    LinkedContainerPort INTEGER,
    PortProtocol TEXT DEFAULT 'tcp',
    SynchronizedWithSetting TEXT,
    AutoAllocatePort BOOLEAN DEFAULT 0,
    ValidateRelatedPortsAvailability BOOLEAN DEFAULT 1,
    
    -- List-specific fields
    ListDelimiter TEXT DEFAULT ',',
    
    -- Enum-specific fields
    AllowedValuesJson TEXT,                    -- JSON array
    ValueMappingsJson TEXT,                    -- JSON object
    
    FOREIGN KEY (DefaultSettingId) REFERENCES DefaultSettings(Id) ON DELETE CASCADE,
    CONSTRAINT CK_SettingsMetadata_DataType CHECK (
        DataType IS NULL OR 
        DataType IN ('string', 'number', 'boolean', 'enum', 'list', 'port')
    )
);

CREATE UNIQUE INDEX IX_SettingsMetadata_DefaultSettingId ON SettingsMetadata(DefaultSettingId);
CREATE INDEX IX_SettingsMetadata_Category ON SettingsMetadata(Category);
CREATE INDEX IX_SettingsMetadata_MapsToContainerPort ON SettingsMetadata(MapsToContainerPort);
```

**Note:** This creates a 1:1 optional relationship. A DefaultSetting exists with or without metadata. If SettingsMetadata exists, it describes HOW to present and validate the setting in the UI.

### PortValidation Table
```sql
CREATE TABLE PortValidation (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    SettingMetadataId INTEGER NOT NULL UNIQUE, -- 1:1 relationship
    MinPort INTEGER DEFAULT 1024,
    MaxPort INTEGER DEFAULT 65535,
    ReservedPortsJson TEXT,                -- JSON array
    CheckAvailability BOOLEAN DEFAULT 1,
    IsUserEditable BOOLEAN DEFAULT 1,
    SuggestedPortsJson TEXT,               -- JSON array
    ValidationMessage TEXT,
    
    FOREIGN KEY (SettingMetadataId) REFERENCES SettingsMetadata(Id) ON DELETE CASCADE,
    CONSTRAINT CK_PortValidation_Range CHECK (MinPort >= 1 AND MinPort <= MaxPort AND MaxPort <= 65535)
);

CREATE UNIQUE INDEX IX_PortValidation_SettingMetadataId ON PortValidation(SettingMetadataId);
```

### PortRelationships Table
```sql
CREATE TABLE PortRelationships (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    SettingMetadataId INTEGER NOT NULL,
    RelationType INTEGER NOT NULL,         -- 0=Offset, 1=Fixed, 2=Multiplier
    TargetContainerPort INTEGER NOT NULL,
    TargetProtocol TEXT DEFAULT 'udp',
    OffsetValue INTEGER DEFAULT 0,
    FixedValue INTEGER,
    Description TEXT,
    IsRequired BOOLEAN DEFAULT 1,
    DisplayOrder INTEGER DEFAULT 0,
    
    FOREIGN KEY (SettingMetadataId) REFERENCES SettingsMetadata(Id) ON DELETE CASCADE,
    CONSTRAINT CK_PortRelationships_Type CHECK (RelationType IN (0, 1, 2)),
    CONSTRAINT CK_PortRelationships_Protocol CHECK (TargetProtocol IN ('tcp', 'udp'))
);

CREATE INDEX IX_PortRelationships_SettingMetadataId ON PortRelationships(SettingMetadataId);
```

## Views for Easy Querying

### Complete GameType View
```sql
CREATE VIEW vw_GameTypesComplete AS
SELECT 
    gt.Id,
    gt.Key,
    gt.DisplayName,
    gt.Description,
    gt.Image,
    gt.ThumbnailUrl,
    gt.DocumentationUrl,
    gt.IsActive,
    gt.CreatedAt,
    gt.UpdatedAt,
    COUNT(DISTINCT p.Id) as PortCount,
    COUNT(DISTINCT v.Id) as VolumeCount,
    COUNT(DISTINCT ds.Id) as DefaultSettingsCount,
    em.EnableTTY,
    COUNT(DISTINCT sm.Id) as SettingsMetadataCount
FROM GameTypes gt
LEFT JOIN Ports p ON gt.Id = p.GameTypeId
LEFT JOIN Volumes v ON gt.Id = v.GameTypeId
LEFT JOIN DefaultSettings ds ON gt.Id = ds.GameTypeId
LEFT JOIN ExtendedMetadata em ON gt.Id = em.GameTypeId
LEFT JOIN SettingsMetadata sm ON em.Id = sm.ExtendedMetadataId
GROUP BY gt.Id;
```

### Port Mapping Settings View
```sql
CREATE VIEW vw_PortMappingSettings AS
SELECT 
    gt.Key as GameTypeKey,
    gt.DisplayName as GameTypeName,
    sm.SettingKey,
    sm.Description,
    sm.LinkedContainerPort,
    sm.PortProtocol,
    pv.MinPort,
    pv.MaxPort,
    COUNT(pr.Id) as RelatedPortsCount
FROM SettingsMetadata sm
INNER JOIN ExtendedMetadata em ON sm.ExtendedMetadataId = em.Id
INNER JOIN GameTypes gt ON em.GameTypeId = gt.Id
LEFT JOIN PortValidation pv ON sm.Id = pv.SettingMetadataId
LEFT JOIN PortRelationships pr ON sm.Id = pr.SettingMetadataId
WHERE sm.MapsToContainerPort = 1
GROUP BY sm.Id;
```

## Sample Queries

### Get All Game Types with Port Info
```sql
SELECT * FROM vw_GameTypesComplete 
WHERE IsActive = 1 
ORDER BY DisplayName;
```

### Get Game Type with All Related Data
```sql
-- Game type basic info
SELECT * FROM GameTypes WHERE Key = 'minecraft';

-- Ports
SELECT * FROM Ports WHERE GameTypeId = (SELECT Id FROM GameTypes WHERE Key = 'minecraft');

-- Volumes
SELECT * FROM Volumes WHERE GameTypeId = (SELECT Id FROM GameTypes WHERE Key = 'minecraft');

-- Default settings
SELECT * FROM DefaultSettings WHERE GameTypeId = (SELECT Id FROM GameTypes WHERE Key = 'minecraft');

-- Extended metadata with settings
SELECT 
    em.*,
    sm.SettingKey,
    sm.Description,
    sm.DataType,
    sm.IsRequired
FROM ExtendedMetadata em
LEFT JOIN SettingsMetadata sm ON em.Id = sm.ExtendedMetadataId
WHERE em.GameTypeId = (SELECT Id FROM GameTypes WHERE Key = 'minecraft');
```

### Find All Game Types with TTY Enabled
```sql
SELECT gt.*
FROM GameTypes gt
INNER JOIN ExtendedMetadata em ON gt.Id = em.GameTypeId
WHERE em.EnableTTY = 1;
```

### Get Port Mapping Configuration for a Setting
```sql
SELECT 
    sm.SettingKey,
    sm.LinkedContainerPort,
    sm.PortProtocol,
    pv.MinPort,
    pv.MaxPort,
    pr.RelationType,
    pr.TargetContainerPort,
    pr.TargetProtocol,
    pr.OffsetValue,
    pr.Description as RelationshipDescription
FROM SettingsMetadata sm
LEFT JOIN PortValidation pv ON sm.Id = pv.SettingMetadataId
LEFT JOIN PortRelationships pr ON sm.Id = pr.SettingMetadataId
WHERE sm.SettingKey = 'SERVER_PORT'
  AND sm.ExtendedMetadataId = (
      SELECT em.Id 
      FROM ExtendedMetadata em 
      INNER JOIN GameTypes gt ON em.GameTypeId = gt.Id 
      WHERE gt.Key = 'minecraft'
  );
```

## Benefits Over File Storage

### 1. Data Integrity
```sql
-- Enforce port range
CONSTRAINT CK_Ports_Range CHECK (Port >= 1 AND Port <= 65535)

-- Prevent duplicate keys
CONSTRAINT UK_GameTypes_Key UNIQUE (Key)

-- Cascade deletes (delete game type ? deletes all related data)
FOREIGN KEY (GameTypeId) REFERENCES GameTypes(Id) ON DELETE CASCADE
```

### 2. Complex Queries
```sql
-- Find all game types using a specific port
SELECT DISTINCT gt.DisplayName 
FROM GameTypes gt
INNER JOIN Ports p ON gt.Id = p.GameTypeId
WHERE p.Port = 25565;

-- Find settings with validation rules
SELECT gt.Key, sm.SettingKey, pv.MinPort, pv.MaxPort
FROM SettingsMetadata sm
INNER JOIN PortValidation pv ON sm.Id = pv.SettingMetadataId
INNER JOIN ExtendedMetadata em ON sm.ExtendedMetadataId = em.Id
INNER JOIN GameTypes gt ON em.GameTypeId = gt.Id
WHERE pv.CheckAvailability = 1;
```

### 3. Performance
```sql
-- Indexed queries are fast
CREATE INDEX IX_GameTypes_Key ON GameTypes(Key);
CREATE INDEX IX_Ports_GameTypeId ON Ports(GameTypeId);

-- Query with index
SELECT * FROM GameTypes WHERE Key = 'minecraft';  -- Uses index
```

### 4. Transactions
```csharp
// Atomic operations
using var transaction = dbContext.Database.BeginTransaction();
try
{
    // Create game type
    var gameType = new GameType { Key = "minecraft", ... };
    dbContext.GameTypes.Add(gameType);
    await dbContext.SaveChangesAsync();
    
    // Add ports
    gameType.Ports.Add(new Port { Port = 25565, Protocol = "tcp" });
    await dbContext.SaveChangesAsync();
    
    // Commit if everything succeeded
    await transaction.CommitAsync();
}
catch
{
    // Rollback if anything failed
    await transaction.RollbackAsync();
    throw;
}
```

## Migration Strategy

### Phase 1: Add SQLite Support
1. Keep existing file storage
2. Add SQLite database
3. Dual-write to both systems

### Phase 2: Migrate Data
1. Read from files
2. Write to database
3. Verify data integrity

### Phase 3: Switch Over
1. Read from database
2. Remove file storage code
3. Archive old JSON files

---

**Recommendation:** Switch to SQLite for better data management, query capabilities, and scalability! ??
