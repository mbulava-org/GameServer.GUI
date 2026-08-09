# Commit Summary: Mount-Type Options Redesign, NFS Prep Move, Blazor Hosting Hardening

Date: 2026-08-01

## 1. Mount-Type Schema Redesign (Options Dictionary)

Replaced the fixed mount-type columns with a flexible, JSON-backed key/value options bag so each
mount type can declare its own initialization options without further schema churn.

- `src/GameServer.Docker/Models/V2/MountTypeConfig.cs` — domain model now keeps only `Key`,
  `DisplayName`, `Description`, `IsActive`, `CreatedAt`, `UpdatedAt`, plus a new
  `Options : Dictionary<string, string>?` and a `GetOption(string key)` helper. Removed `Driver`,
  `DriverOptionsJson`, `SourcePathTemplate`, `DefaultReadOnly`, `DefaultEnsureNfsPathExists`,
  `DefaultOwnerUid`, `DefaultOwnerGid`, `DefaultPermissions`.
- `src/GameServer.Docker/Dtos/V2/MountTypeConfigDto.cs` — mirrors the same reduced shape with
  `Options`.
- `src/GameServer.Web/Models/V2/MountTypeConfig.cs` — Blazor/web-side model mirrors the same shape.
- `src/GameServer.Docker/Data/V2/Entities.cs` — `MountTypeConfigEntity` now persists `OptionsJson`
  instead of the old typed columns.
- `src/GameServer.Docker/Data/V2/GameServerV2DbContext.cs` — updated entity mapping and built-in
  seed data (`volume`, `nfs`) to store well-known option keys as JSON string values inside
  `OptionsJson`.
- `src/GameServer.Docker/Repositories/V2/MountTypeConfigRepository.cs` — serializes/deserializes
  `Options` to/from `OptionsJson` on save and load.
- `src/GameServer.Docker/Controllers/V2/MountTypeConfigController.cs` — maps DTO ↔ domain model
  through `Options` instead of individual typed properties.
- `src/GameServer.Docker/Services/V2/VolumeSetupResolver.cs` — reads `Driver`, `DriverOptionsJson`,
  `SourcePathTemplate`, `DefaultOwnerUid`, `DefaultOwnerGid`, `DefaultPermissions` via
  `config.GetOption(...)`, parsing ints/bools as needed. Token substitution
  (`{Source}`, `{serverId}`, `{gameTypeKey}`, `{Target}`) is unchanged.
- `src/GameServer.Docker/Repositories/V2/GameTypeRepository.cs` — raw-SQL table creation
  statements (SQLite and MySQL) and default mount-type seeding rewritten for the new `OptionsJson`
  column.
- `src/GameServer.Web/Components/Pages/Settings/MountTypeConfigEditor.razor` — replaced the fixed
  form fields with a fully generic key/value options grid (add/remove/edit rows), synced back to
  `Options` on save.
- `src/GameServer.Web/Components/Pages/GameTypes/GameTypeDetailsV2.razor` — parses mount-type
  default values (`DefaultReadOnly`, `DefaultEnsureNfsPathExists`, `DefaultOwnerUid`,
  `DefaultOwnerGid`, `DefaultPermissions`) from `Options` when prefilling the volume editor.
- `src/GameServer.Web/Components/Pages/GameTypes/Components/V2/GameTypeDetailsV2EditorModels.cs`
  — `VolumeMountTypeOption` and numeric variable option helper models used by the volume editor.
- `src/GameServer.Docker.Client/GameServer.Docker.Client.v1.g.cs` — regenerated NSwag client DTO
  updated to the new `Options` shape.

### New EF Core Migrations
- `Data/V2/Migrations/SqliteMigrations/20260801043012_RemoveContainerPathTemplateAddVolumeName.*`
- `Data/V2/Migrations/MySqlMigrations/20260801043023_RemoveContainerPathTemplateAddVolumeName.*`
- `Data/V2/Migrations/SqliteMigrations/20260801045023_MountTypeConfigOptionsJson.*`
- `Data/V2/Migrations/MySqlMigrations/20260801045033_MountTypeConfigOptionsJson.*`
- Updated model snapshots: `SqliteGameServerV2DbContextModelSnapshot.cs`,
  `MySqlGameServerV2DbContextModelSnapshot.cs`.

Both providers verified with `dotnet ef migrations has-pending-model-changes` — no pending model
changes remain.

### Tests Updated
- `tests/GameServer.Docker.Tests/Repositories/V2/MountTypeConfigRepositoryTests.cs`
- `tests/GameServer.Docker.Tests/Controllers/V2/MountTypeConfigControllerTests.cs`
- `tests/GameServer.Docker.Tests/Services/V2/VolumeSetupResolverTests.cs`

## 2. Volume Templating, VolumeName, and NFS Preparation Move

Moved NFS target-folder preparation (create + chown/chmod) from the Agent into the primary API
service, ahead of calling the Agent to create/update a service, and introduced `VolumeName` as an
explicit concept distinct from `ContainerPath`/`Source`.

- `src/GameServer.Docker/Dtos/V2/GameServersDtos.cs`,
  `src/GameServer.Docker/Dtos/V2/GameTypesDtos.cs`,
  `src/GameServer.Docker/Dtos/V2/GameTypePortableDtos.cs` — added `VolumeName` to
  `GameServerVolume`/related DTOs.
- `src/GameServer.Docker/Services/V2/NfsVolumePreparationService.cs` *(new)* — API-side service
  that builds the target path under `/data` from `VolumeName` and prepares
  ownership/permissions before deployment.
- `src/GameServer.Docker/Configurations/NfsPreparationOptions.cs` *(new)* — configuration options
  for the new preparation service.
- `src/GameServer.Docker/Services/V2/GameServerDeploymentService.cs` — calls
  `NfsVolumePreparationService` before create/update.
- `src/GameServer.Docker/Services/V2/GameServerValidationService.cs`,
  `src/GameServer.Docker/Services/V2/GameServerQueryService.cs`,
  `src/GameServer.Docker/Services/V2/GameTypeCommandService.cs`,
  `src/GameServer.Docker/Services/V2/GameTypeQueryService.cs` — updated for `VolumeName` and the
  simplified `InitMode` model.
- `src/GameServer.Docker/Models/V2/GameType.cs`,
  `src/GameServer.Web/Models/V2/GameTypeV2Models.cs`,
  `src/GameServer.Web/Components/Pages/GameTypes/Components/V2/GameTypeRevisionVolumesEditor.razor`
  — `InitMode` simplified to `EnsureNfsPathExists` / `DefaultEnsureNfsPathExists`; added UID/GID
  variable linking so owner values can bind to a numeric setting/variable instead of a literal.
- `src/GameServer.Docker.Agent/Controllers/ServicesController.cs`,
  `src/GameServer.Docker.Agent/Models/ServiceModels.cs` — Agent no longer performs filesystem
  preparation; it now expects the target folder to already exist/be owned correctly.
- `src/GameServer.Docker/Services/ServiceOperationsViaAgent.cs`,
  `src/GameServer.Docker/Repositories/V2/GameServerRepository.cs` — pass through `VolumeName` and
  related fields to the Agent calls and persistence layer.
- `src/GameServer.Web/Components/Pages/Servers/GameServerManagerV2.razor` — server list items are
  now clickable to open the manage/edit view.

## 3. Blazor Server Hosting Behind Reverse Proxy / Docker

- `src/GameServer.Docker/Program.cs`, `src/GameServer.Web/Program.cs` — added forwarded headers
  handling, WebSocket support, and conditional HTTPS redirection (skipped when already terminated
  upstream); `DetailedErrors` is now gated to `Development` only.
- `docs/guides/Reverse-Proxy-Blazor-Server.md` *(new)* — guide covering the required
  configuration and an example Traefik snippet for hosting Blazor Server interactivity behind a
  reverse proxy.

## Validation

- Full solution build: 0 errors.
- `dotnet ef migrations has-pending-model-changes` for both `SqliteGameServerV2DbContext` and
  `MySqlGameServerV2DbContext`: no pending changes.
- `MountTypeConfigControllerTests`: all passing.
- `MountTypeConfigRepositoryTests`: fail locally only due to a pre-existing environment
  restriction (SQLitePCLRaw native library blocked by an Application Control policy), unrelated to
  this change.
