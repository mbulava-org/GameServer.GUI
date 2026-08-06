# Database Setup & Migrations

How the V2 persistence layer is configured, initialized, and evolved.

---

## Overview

**V2 is the only persistence layer.** There is no legacy JSON store and no legacy database — game types, revisions, servers, and mount type configuration all live in the V2 relational schema.

Schema management is owned **entirely by Entity Framework Core migrations**. The application does not perform any hand-rolled schema creation, patching, or repair at runtime.

| Provider | Status | Schema managed by |
|----------|--------|-------------------|
| **SQLite** | Default | EF Core migrations (`SqliteMigrations`) |
| **MySQL** | Supported | EF Core migrations (`MySqlMigrations`) |
| **PostgreSQL** | Experimental | External `pgPacTool` database project |

---

## Provider-Specific DbContexts

Each relational provider has its own `DbContext` subclass so that EF Core can keep a **separate, provider-correct migration set** for each. This is what makes `dotnet ef migrations add` and `MigrateAsync()` work cleanly across providers.

| Context | Provider | Migrations folder |
|---------|----------|-------------------|
| `GameServerV2DbContext` | base — owns the model (`OnModelCreating`) and seed data | *(none)* |
| `SqliteGameServerV2DbContext` | SQLite | `Data/V2/Migrations/SqliteMigrations` |
| `MySqlGameServerV2DbContext` | MySQL | `Data/V2/Migrations/MySqlMigrations` |

The base context defines the model **once**. The subclasses inherit that model and exist purely to anchor a provider-specific migration assembly, so the two providers never share generated SQL.

At startup, `Program.cs` registers the concrete context for the configured provider and aliases `GameServerV2DbContext` to it, so repositories (which depend on the base type) resolve the correct instance:

```csharp
case "mysql":
	builder.Services.AddDbContext<MySqlGameServerV2DbContext>(...);
	builder.Services.AddScoped<GameServerV2DbContext>(sp => sp.GetRequiredService<MySqlGameServerV2DbContext>());
	break;

default: // sqlite
	builder.Services.AddDbContext<SqliteGameServerV2DbContext>(...);
	builder.Services.AddScoped<GameServerV2DbContext>(sp => sp.GetRequiredService<SqliteGameServerV2DbContext>());
	break;
```

---

## Configuration

Provider selection is driven by the `V2Database` section plus a matching connection string.

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

**`V2Database:Provider`** accepts `Sqlite` (default), `MySql`, or `PostgreSql` (experimental).

**`V2Database:ConnectionStringName`** is optional. When omitted, the connection string is chosen by provider:

| Provider | Default connection string name |
|----------|-------------------------------|
| `Sqlite` | `GameServerV2Db` |
| `MySql` | `GameServerV2MySqlDb` |
| `PostgreSql` | `GameServerV2PostgresDb` |

**Docker:** mount `/data` as a volume so the SQLite database survives container restarts.

---

## Startup Initialization

On startup, `GameTypeRepository.InitializeDatabaseAsync()` runs. For SQLite and MySQL it does exactly one thing:

```csharp
var pendingMigrations = (await context.Database.GetPendingMigrationsAsync()).ToList();
if (pendingMigrations.Count == 0)
{
	logger.LogInformation("No pending V2 database migrations to apply.");
	return;
}

await context.Database.MigrateAsync();
```

That means:

- A **new** database is created in full by the migrations.
- An **existing** database has only genuinely new migrations applied.
- Startup is **idempotent** — running it repeatedly is a no-op once history is current.
- Seed data (for example the built-in `volume` and `nfs` mount types) is delivered through `HasData` in the model and applied by the migrations, not by runtime seeding code.

For PostgreSQL, EF migrations are not used. The repository instead verifies the schema was already deployed by the `pgPacTool` project and throws with deployment guidance if it was not:

```powershell
.\scripts\Deploy-V2PostgresDatabase.ps1 -TargetConnectionString "Host=localhost;Database=gameserver-v2;Username=postgres;Password=postgres"
```

---

## Adding a Migration

**Whenever you change an EF-mapped entity, you must add a migration** — and you must add it for **both** SQLite and MySQL, because each provider owns its own migration set.

```powershell
cd src\GameServer.Docker

# SQLite
dotnet ef migrations add MyChangeName `
  --context SqliteGameServerV2DbContext `
  --output-dir Data/V2/Migrations/SqliteMigrations `
  -- --provider sqlite

# MySQL
dotnet ef migrations add MyChangeName `
  --context MySqlGameServerV2DbContext `
  --output-dir Data/V2/Migrations/MySqlMigrations `
  -- --provider mysql
```

The `--` arguments are consumed by `GameServerV2DbContextFactory`, the design-time factory. It deliberately **never requires a live database connection**, so migrations can be generated offline.

### Migration Guidelines

- Write migrations to be **as safe as possible** — avoid operations that drop or lose existing data.
- When renaming a column, prefer add + copy + drop over a destructive rebuild.
- Review the generated SQL for both providers; SQLite in particular rebuilds tables for many `ALTER` operations.
- Pending migrations are applied automatically on the next application start.

---

## NSwag Client Generation

NSwag starts the API to discover endpoints, which would otherwise trigger database initialization. Pass `--no-db-init` to skip it:

```json
{
  "documentGenerator": {
	"aspNetCoreToOpenApi": {
	  "project": "../GameServer.Docker/GameServer.Docker.csproj",
	  "arguments": [ "--no-db-init" ],
	  "documentName": "v1",
	  "targetFramework": "net10.0"
	}
  }
}
```

Regenerate the client manually with:

```powershell
cd src\GameServer.Docker.Client
dotnet nswag run
```

---

## Troubleshooting

**"Pending model changes" warning at startup**
An entity was modified without adding a migration. Add migrations for both SQLite and MySQL as shown above.

**Table or column missing at runtime**
The database is behind the code. Confirm the migration exists for your active provider and that startup logs show it being applied.

**Client generation hangs**
Verify `--no-db-init` is present in `nswag.json`, and kill any running `GameServer.Docker` instance holding the database file.

**Database not initializing in production**
Check logs for `Initializing V2 database...`, verify the connection string resolves, and ensure the process can write to `./data/`.

---

## Related Documentation

- **[V2 Database Diagram](../reference/V2-Database-Diagram.md)** - Entity relationships
- **[V2 GameServer Lifecycle](V2-GameServer-Lifecycle.md)** - How servers are created and updated
- **[V2 Volume Setup](V2-Volume-Setup.md)** - Mount type configuration
