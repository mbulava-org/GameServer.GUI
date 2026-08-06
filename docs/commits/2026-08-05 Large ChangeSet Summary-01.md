# Large ChangeSet Summary — 2026-08-05

A consolidated record of the work completed in this change set, covering GameType setting ergonomics, a new server-variable data type, the deployment preview, live port validation, editor testability, EF migration standardization, and a full documentation refresh.

---

## 1. Enum Setting Editor Rework

**Problem:** `AllowedValuesJson` and `ValueMappingsJson` were hand-edited raw JSON, which was error-prone and gave no feedback about type consistency.

**Change:** Enum settings are now edited as a structured list of value/display pairs.

- Added an **underlying type** concept (`string` / `numeric`) via `EnumUnderlyingType`.
- The type is inferred automatically: if every supplied value parses as a number, the setting is `numeric`, otherwise `string`.
- On save, the value/display list is serialized into `AllowedValuesJson` and `ValueMappingsJson`.
- On load, both columns are parsed back into the editable pair list — the JSON is never touched by hand.
- `GameServerSettingFieldV2` renders the dropdown from the mapping, falling back to the raw value as its own label.
- `GameServerValidationService` rejects any submitted value not present in `AllowedValuesJson`.

**Files:** `GameTypeRevisionSettingsEditor.razor`, `GameTypeDetailsV2.razor`, `GameServerSettingFieldV2.razor`, `GameServerValidationService.cs`

---

## 2. Testing Log 0.1.0.309 Fixes

Addressed the issues recorded in the testing log:

- Create Game Server now applies a sensible **default GameType/revision selection**.
- **ServiceName** is computed from the server name rather than entered manually.
- New servers generate a **`ServerId`** in the editor rather than relying on the backend.
- Token substitution behavior in the create flow was corrected.

Web test suite passed 51/51 after these fixes.

---

## 3. New Data Type: `servervariable` ("Server Variable (Optional)")

**Goal:** Allow a setting value to embed `GameServer` properties using `{Token}` syntax, with a per-server on/off switch.

**Implementation:** `ServerVariableExpander` (`src/GameServer.Docker/Services/V2/ServerVariableExpander.cs`)

Supported tokens:
`{ServerId}` · `{Name}` · `{ServiceName}` · `{Description}` · `{Status}` · `{GameTypeKey}` · `{RevisionVersionTag}` · `{RevisionImageReference}`

The toggle state is encoded into the single stored string column, requiring no schema change:

| Stored value | Meaning |
|---|---|
| `@vars:Welcome to {Name}` | Expansion **enabled** |
| `@literal:@vars:...` | Expansion **disabled**, escaping a literal marker |
| `Welcome to {Name}` | Expansion **disabled** (plain literal) |

`Decode()` splits stored text into `(ExpandVariables, RawValue)`; `Encode()` recombines them. Both `GameServerDeploymentService` and `GameServerSpecBuilder` call the same expander, so **preview and deployment always produce identical values**.

The Create/Edit Server UI renders a text box plus an expansion switch for these settings.

---

## 4. Deployment Preview Tab

**Goal:** Validate the real, fully-calculated Swarm service spec in the editor instead of debugging after deployment.

**Backend:** `GameServerSpecBuilder` → `GameServerDeploymentPreviewDto`
**Endpoint:** `POST /api/v2/gameservers/preview`
**UI:** Deployment Preview tab in `GameServerEditorV2.razor` (`GameServerDeploymentPreviewV2`)

The preview is a **dry run** — it constructs the same `ServiceCreateParameters` the deployment would send, but never contacts the Docker daemon.

Preview contents:

| Section | Contents |
|---|---|
| Service | Service name, server id, game type key, image reference, version tag, TTY flag |
| Labels | Full `gameserver.docker.*` label set from `ServiceLabels` |
| Networks | Attached networks with driver and purpose |
| Environment Variables | Key, **post-calculation value**, raw value, data type, category |
| Ports | Container port, published port, protocol, publish mode, description |
| Volumes | Name, container path, source, mount type, driver, driver options, ownership, permissions |
| Issues / Notices | Blocking problems and non-blocking notes |
| Raw Spec | Indented JSON of the exact `ServiceCreateParameters` |

Each environment variable reports `IsExpanded` (token expansion changed the value) and `UsesDefault` (value came from the revision default), making unresolved tokens and un-overridden settings immediately visible.

Builder and component tests were added.

---

## 5. Live Port Validation & Mapping Synchronization

**Endpoint:** `POST /api/v2/gameservers/ports/availability`

Ports remain **fixed by the revision** — rows cannot be added or removed. Editing is bidirectional:

1. Changing a **published port** updates the `port` setting that is its **primary direct mapping**.
2. Changing a **port setting value** updates the matching published port row.

A published port of `0` defaults to the container port. Related offset/multiplier mappings derive from the primary direct mapping and are never entered manually.

Availability behavior:

- Port edits are debounced, then checked against **all managed GameServer services**.
- Passing `serverId` excludes the server's own currently published ports, so editing an existing server does not conflict with itself.
- `ValidateRelatedPortsAvailability` extends checking to offset/multiplier-derived ports.
- `AutoAllocatePort` settings can be assigned a free published port automatically.

**Save gating:** `CanSave` is false while a required setting is empty, an availability check is in flight, or any port reported `IsAvailable = false`. Blocking issues render **outside** the tab set so they remain visible on any tab.

Key members: `EnsurePortsInitialized()`, `FindPrimarySettingForPort()`, `SyncPortSettingsFromPublishedPorts()`, `OnPublishedPortChanged()`, `CheckPortAvailabilityAsync()`, `CanSave`.

Backend port-availability tests: 6/6 passing. Port-setting bUnit tests: 8/8 passing.

---

## 6. Editor Service Interface Extraction (Testability)

Concrete web API client classes were placed behind interfaces so Blazor components can be tested with mocks:

- `IGameServerV2ApiService` — `GetListAsync`, `GetByServerIdAsync`, `ValidateAsync`, `PreviewAsync`, `CheckPortAvailabilityAsync`, `CreateAsync`, `UpdateAsync`
- `IGameTypeV2ApiService` — GameType CRUD, export/import, revision operations, `DetectSetupAsync` overloads, `CompareSetupAsync`
- `IMountTypeConfigApiService` — `GetAllAsync`, `GetAsync`, `SaveAsync`, `DeleteAsync`

Consumers updated: `GameServerEditorV2`, `GameServerManagerV2`, `GameServerDetailsV2`, `GameTypeManagerV2`, `GameTypeDetailsV2`, `MountTypeConfigEditor`.

`GameServerEditorV2Tests` was added. Web test suite: **57/57 passing**.

---

## 7. Standardization on Built-In EF Core Migrations

**Problem:** `GameTypeRepository` carried a large body of hand-rolled schema management — baseline reconciliation, legacy schema repair, synthetic migration-history insertion, and runtime mount-type seeding. This diverged from EF's migration history and caused legacy-schema test failures.

**Change:** Schema management is now owned **entirely** by provider-specific EF Core migrations.

- `GameTypeRepository.cs` reduced from **1398 → ~662 lines**.
- `MigrateRelationalDatabaseAsync()` now does only:

  ```csharp
  var pendingMigrations = (await context.Database.GetPendingMigrationsAsync()).ToList();
  if (pendingMigrations.Count == 0) { /* log and return */ }
  await context.Database.MigrateAsync();
  ```

- All custom repair, baseline, and runtime-seeding code was deleted.
- Mount-type defaults are delivered through `HasData` in the model and applied by migrations.
- PostgreSQL retains an explicit schema-existence check (`PostgreSqlTableExistsAsync`) and throws with `pgPacTool` deployment guidance, since its schema is deployed externally.

**Provider contexts** (each anchoring its own migration set):

| Context | Provider | Migrations folder |
|---|---|---|
| `GameServerV2DbContext` | base — owns the model and seed data | *(none)* |
| `SqliteGameServerV2DbContext` | SQLite | `Data/V2/Migrations/SqliteMigrations` |
| `MySqlGameServerV2DbContext` | MySQL | `Data/V2/Migrations/MySqlMigrations` |

`Program.cs` registers the concrete context for the configured provider and aliases `GameServerV2DbContext` to it, so repositories depending on the base type resolve the correct instance.

**Tests updated:** the two obsolete legacy-schema tests were replaced with migration/idempotency tests:

- `InitializeDatabaseAsync_ShouldApplyAllMigrationsAndSeedMountTypes()` (SQLite)
- `InitializeDatabaseAsync_ShouldApplyAllMigrationsAndBeIdempotent()` (MySQL)

Docker test suite: **128/128 passing, 1 skipped**.

---

## 8. Configuration Cleanup

The orphaned legacy `GameServerDb` connection string was removed from:

- `src/GameServer.Docker/appsettings.json`
- `src/GameServer.Docker/appsettings.Development.json`

Only the V2 connection strings (`GameServerV2Db`, `GameServerV2MySqlDb`, `GameServerV2PostgresDb`) and the `V2Database` section remain.

---

## 9. Documentation Refresh

### Rewritten

**`docs/guides/DATABASE-INITIALIZATION.md`** — replaced the obsolete NSwag/JSON-migration document with a current V2 database guide covering provider-specific DbContexts, `V2Database` configuration and defaults, startup migration behavior, how to add migrations for **both** SQLite and MySQL, the design-time factory's offline behavior, NSwag `--no-db-init`, and troubleshooting.

### Created

- **`docs/guides/V2-GameType-Settings-And-Metadata.md`** — the current setting model (`GameTypeSettingDefinition` / `GameTypeSettingMetadata` / `GameTypeSettingPortMapping`), all live data types (`string`, `number`, `boolean`, `yesno`, `enum`, `port`, `servervariable`), enum value/display serialization, and server-variable encoding.
- **`docs/guides/V2-Deployment-Preview-And-Port-Validation.md`** — the preview tab contents, environment resolution flags, live port synchronization, availability checking, save gating, and the validate/preview/save comparison.

### Removed (obsolete V1 material)

- `docs/guides/Port-Mapping-Integration-Guide.md`
- `docs/guides/GameType-Metadata-Complete-Guide.md`
- `docs/guides/GameType-Editor-Complete-Functionality-Guide.md`
- `docs/reference/SQLite-GameType-Database-Schema.md`

These described the retired V1 `DefaultSettings`, `SettingsMetadata`, `ExtendedMetadata`, `PortValidation`, and `PortRelationships` models, none of which exist.

### Updated

- `docs/README.md` — link hub, feature list, and database description realigned to the current guide set.
- `docs/MASTER_ROADMAP.md` — stale V1 guide links and `--seed-database` references replaced; four broken links repaired; documentation-cleanup item marked done.
- `docs/ARCHITECTURE.md` — persistence section corrected to describe provider-specific EF migrations, `HasData` seeding, and PostgreSQL as externally deployed and verified.
- `docs/CURRENT-FEATURES.md` — database persistence status updated; `servervariable`, enum mappings, deployment preview, and live port validation added.
- `docs/QUICK-START.md` — legacy seeding instructions replaced with the current migration behavior.
- `docs/guides/V2-GameServer-Lifecycle.md` — live port validation and deployment preview documented.
- `docs/guides/V2-Ports-And-WebHosts.md` — link retargeted to the new preview guide.

**Verification:** every relative link across all non-archive docs was checked programmatically — **all links resolve**.

---

## Validation Summary

| Check | Result |
|---|---|
| `dotnet build GameServer.GUI.slnx` | ✅ 0 errors, 0 warnings |
| `tests/GameServer.Web.Tests` | ✅ 57/57 passing |
| `tests/GameServer.Docker.Tests` | ✅ 128/128 passing, 1 skipped |
| Backend port-availability tests | ✅ 6/6 passing |
| Port-setting bUnit tests | ✅ 8/8 passing |
| Documentation link integrity | ✅ All relative links resolve |

---

## Migration Notes for Contributors

1. **Any change to an EF-mapped entity requires a migration for both providers.** SQLite and MySQL own separate migration sets:

   ```powershell
   cd src\GameServer.Docker

   dotnet ef migrations add MyChangeName `
	 --context SqliteGameServerV2DbContext `
	 --output-dir Data/V2/Migrations/SqliteMigrations -- --provider sqlite

   dotnet ef migrations add MyChangeName `
	 --context MySqlGameServerV2DbContext `
	 --output-dir Data/V2/Migrations/MySqlMigrations -- --provider mysql
   ```

2. **Do not add runtime schema patching.** Pending migrations are applied automatically at startup and the operation is idempotent.
3. **Write migrations defensively** — prefer add + copy + drop over destructive rebuilds; SQLite rebuilds tables for many `ALTER` operations.
4. **Seed data belongs in `HasData`**, not in repository startup code.
5. **New editor API clients should ship with an interface** so components stay testable.
