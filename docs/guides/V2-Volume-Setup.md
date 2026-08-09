# V2 Volume Setup & Machine Configuration

## Overview

The V2 volume system stores configuration for game server volumes so the orchestrator can deterministically create and update Docker Swarm mounts. It separates **mount-type configuration** (`MountTypeConfig`) from **revision volume templates** (`GameTypeVolume`) and **immutable per-server snapshots** (`GameServerVolume`).

## Key files

| File | Purpose |
|---|---|
| `src/GameServer.Docker/Data/V2/Entities.cs` | `MountTypeConfigEntity`, `GameTypeVolumeEntity` (revision template), and `GameServerVolumeEntity` (per-server snapshot) |
| `src/GameServer.Docker/Data/V2/GameServerV2DbContext.cs` | Seeded known mount-type defaults |
| `src/GameServer.Docker/Models/V2/MountTypeConfig.cs` | Mount-type domain model |
| `src/GameServer.Docker/Models/V2/GameType.cs` | Domain records and `VolumeInitMode` enum |
| `src/GameServer.Docker/Repositories/V2/MountTypeConfigRepository.cs` | CRUD for mount-type configs |
| `src/GameServer.Docker/Controllers/V2/MountTypeConfigController.cs` | `api/v2/mounttypeconfigs` endpoints |
| `src/GameServer.Docker/Services/V2/VolumeSetupResolver.cs` | Resolves revision volumes + mount-type config + server layout into `GameServerVolume` snapshots |
| `src/GameServer.Docker/Services/V2/GameServerDeploymentService.cs` | Builds Swarm create/update parameters from snapshots and routes through `IServiceOperations` |
| `src/GameServer.Docker.Agent/Models/ServiceModels.cs` | Agent `MountConfig` and `UpdateServiceRequest.Mounts` contracts |
| `src/GameServer.Docker.Agent/Controllers/ServicesController.cs` | Applies mounts on create/update and prepares bind-mount host paths/permissions |

## Resolution order

When a server is created, the following data is merged in order of precedence (later wins):

1. **Mount-type configuration** — `MountTypeConfig` (keyed by `GameTypeVolume.MountType`): driver, source/container path templates, default driver options, default ownership/permissions.
2. **GameType revision volume template** — `GameTypeVolume` fields: mount type, read-only, optional uid/gid/permissions override.
3. **Per-server layout** — `VolumeBindingLayout` (`standard` or `local`) selected in the server editor. In `local` layout, driver options are ignored.

The resolved concrete values are written as `GameServerVolume` rows in the database.

## Snapshot semantics

- Volume setup is **not versioned**. Editing a GameType revision's volumes updates the setup in place.
- Existing servers keep their existing `GameServerVolume` snapshots. Changes to the GameType revision or `MountTypeConfig` do not retroactively rewrite deployed servers.
- When updating a server, only volumes for container paths **not already snapshotted** are resolved and added. Existing rows are reused verbatim.
- `GameServerVolume` is the immutable record of the exact mount used by the server. It contains everything needed to recreate the mount even if the mount-type config or revision template changes later.

## Entity fields

### `MountTypeConfig` / `MountTypeConfigEntity`

| Field | Description |
|---|---|
| `Key` | String primary key (e.g. `volume`, `bind`, `tmpfs`, `nfs`). Referenced by `GameTypeVolume.MountType`. |
| `DisplayName` | Human-readable label. |
| `Description` | Optional notes. |
| `Driver` | Docker named-volume driver (e.g. `local`, `vieux/sshfs`). |
| `SourcePathTemplate` | Template for the host source path. Tokens: `{gameTypeKey}`, `{serverId}`, `{Source}`. |
| `ContainerPathTemplate` | Template for the container target path. Token: `{Source}`. |
| `DriverOptionsJson` | Optional default JSON driver options applied in `standard` layout. |
| `DefaultReadOnly` | Whether mounts of this type are read-only by default. |
| `DefaultInitMode` | Default initialization behavior. Persisted onto `GameServerVolume` as-is at resolution time. |
| `DefaultOwnerUid` / `DefaultOwnerGid` / `DefaultPermissions` | Defaults applied when the revision template does not specify them. |
| `IsActive` | Whether this mount type is usable. |

### `GameTypeVolume` / `GameTypeVolumeEntity` (template only)

| Field | Description |
|---|---|
| `Source` | Container path where the mount is attached; also the `{Source}` token value. |
| `Usage` | Logical usage label (e.g. `worlds`, `config`, `mods`). |
| `MountType` | Code referencing `MountTypeConfig.Key`. Soft FK; not enforced on existing data. |
| `ReadOnly` | True if the container must not write to the mount. |
| `OwnerUid` / `OwnerGid` | Optional owner override. |
| `Permissions` | Optional octal permission string, e.g. `0755`. |
| `Required` | Whether the server must define this volume. |

The following fields have been removed from the `GameTypeVolume` revision template and now live on the mount type configuration or the resolved `GameServerVolume` snapshot:

- `Driver`
- `DriverOptionsJson`
- `SubPathOverride`
- `InitMode`
- `SeedSourcePath`

Because `GameServerVolume` snapshots are immutable, changing `MountTypeConfig` or a revision's volumes later does not alter existing servers. Those fields are frozen at the moment the snapshot is created so the volume can be recreated exactly.

### `GameServerVolume` / `GameServerVolumeEntity` (immutable snapshot)

| Field | Description |
|---|---|
| `ContainerPath` | The resolved target path inside the container. |
| `Source` | The resolved host source path or volume name. |
| `MountType` | The mount-type code used at create time. |
| `ReadOnly` / `Driver` / `Permissions` / `InitMode` | Concrete values used by the server. |
| `DriverOptionsJson` | Snapshot of the resolved driver options. |
| `IsProvisioned` | Marks whether the orchestrator has created/updated the Swarm service with this mount. |

## Agent contract

`UpdateServiceRequest` includes an optional `Mounts` list. When provided, the agent replaces the service's mount spec and, for `bind` mounts whose snapshot `InitMode` is not `none`:

1. Creates the host directory if it does not exist.
2. Applies `chmod` for `Permissions` (Linux hosts only).
3. Applies `chown` for `OwnerUid` / `OwnerGid` (Linux hosts only).

For `volume` mounts, the Docker volume driver options are applied through the Swarm `VolumeOptions.DriverConfig`.

## Validation

`GameServerValidationService` validates:

- `VolumeBindingLayout` is `standard` or `local`.
- Each GameType volume references a supported `MountType` (`volume`, `bind`, `tmpfs`, etc.).
- `Permissions` is a 3 or 4 digit octal value when provided.
- All container paths within a revision are unique.

A warning is issued when changing mounts on an existing service because Swarm will restart the service tasks.

## UI behavior

- Mount type editor (`MountTypeConfigEditor.razor`) edits the per-key configurations at `/settings/mount-types`.
- GameType editor (`GameTypeRevisionVolumesEditor.razor`) edits the revision volume template (container path, mount type, read-only, optional ownership/permissions).
- Server editor (`GameServerEditorV2.razor`) selects only the binding layout. The resolved mounts preview is shown on the **Validation Preview** tab.
- Wizard Step 4 Volumes placeholder is retired; the configuration now flows from the GameType revision setup plus `MountTypeConfig`.

## Remaining work

- Deploy action wiring: add a controller endpoint that invokes `GameServerDeploymentService.DeployAsync` and `UpdateDeploymentAsync`.
- Migrate any still-existing legacy `DockerVolumeOptions` read paths once V2 servers are no longer pre-release.
- GUI component tests will be added after the volumes tab UX settles.
