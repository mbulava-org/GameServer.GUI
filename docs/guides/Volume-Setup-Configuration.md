# Mount Type Configuration Guide

The **Mount Type** page (`/settings/mount-types`) controls how the orchestrator turns the `Volumes` defined on a `GameTypeRevision` into concrete per-server mount instructions. Each mount type (`volume`, `bind`, `tmpfs`, etc.) is a keyed configuration that owns its driver, path templates, and default driver options.

## Where the page lives

- Blazor route: `/settings/mount-types`
- API route: `api/v2/mounttypeconfigs`
- Storage: `MountTypeConfigs` table in the V2 database (SQLite by default, PostgreSQL supported)
- Key implementation files:
  - `src/GameServer.Docker/Data/V2/Entities.cs` — `MountTypeConfigEntity`
  - `src/GameServer.Docker/Data/V2/GameServerV2DbContext.cs` — seeded known defaults
  - `src/GameServer.Docker/Models/V2/MountTypeConfig.cs` — domain model
  - `src/GameServer.Docker/Repositories/V2/MountTypeConfigRepository.cs`
  - `src/GameServer.Docker/Controllers/V2/MountTypeConfigController.cs`

> Note: the old `VolumeDriverConfigOptions`/`VolumeSetupConfig` classes and the legacy `api/v2/volumesetupconfig` endpoint have been removed. Mount type configuration is now per-key, not a single global row.

## Mount type fields

| Field | Description | Example |
|---|---|---|
| **Key** | Unique string code for the mount type. This is referenced by `GameTypeVolume.MountType`. | `volume`, `bind`, `tmpfs`, `nfs` |
| **Display Name** | Human-readable label shown in the UI. | `Docker volume` |
| **Driver** | Docker named-volume driver used when resolving volumes of this type. | `local` |
| **Source Path Template** | Template for the host source path or volume name. | `{gameTypeKey}_{serverId}_{Source}` |
| **Container Path Template** | Template for the container target path. | `{Source}` |
| **Driver Options (JSON)** | Default driver options applied in `standard` layout. | `{"type":"nfs","device":":/exported/gameservers","o":"addr=host.docker.internal,rw"}` |
| **Default Read Only** | Whether mounts of this type are read-only by default. | `false` |
| **Active** | Whether the mount type is available for use. | `true` |

### Path template tokens

| Token | Substituted with |
|---|---|
| `{gameTypeKey}` | The `Key` of the game type. |
| `{serverId}` | The server's unique `ServerId`. |
| `{Source}` | The `Source` value from the revision volume definition (usually the container path). |

For example, with:

- Mount type key = `volume`
- `SourcePathTemplate` = `{gameTypeKey}_{serverId}_{Source}`
- Game type key = `minecraft`
- Server id = `srv1`
- Revision volume `Source` = `/data/worlds`

the resolved mount source path is:

```text
/minecraft_srv1_/data/worlds
```

And the resolved container path is:

```text
/data/worlds
```

## Seeded defaults

The V2 database seeds a small set of known mount types automatically:

| Key | Display Name | Driver | Source Path Template |
|---|---|---|---|
| `volume` | Docker volume | `local` | `{gameTypeKey}_{serverId}_{Source}` |
| `bind` | Bind mount | `local` | `/host/gameservers/{gameTypeKey}/{serverId}/{Source}` |
| `tmpfs` | tmpfs | `local` | *(empty; tmpfs has no source)* |
| `nfs` | NFS volume | `vieux/sshfs` | `{gameTypeKey}_{serverId}_{Source}` |

> If you previously used a `MountType` that does not exist in `MountTypeConfigs`, server creation will fail until you add the matching configuration through the UI or API. This is intentional: `GameTypeVolume.MountType` is a soft foreign key to `MountTypeConfig.Key`.

## Driver options behavior

Driver options describe the raw mount options passed to Docker when a volume is resolved. They are taken from the mount type configuration and:

- Only applied when the server uses the `standard` volume binding layout.
- Ignored for `local` layout.
- `tmpfs` and `bind` mounts typically have no driver options.

A common NFS example:

```json
{
  "type": "nfs",
  "device": ":/volume1/gameservers",
  "o": "addr=192.168.1.10,rw,nfsvers=4"
}
```

## Volume binding layouts

When you create or edit a V2 game server, you choose one of two layouts. The layout is independent of the mount type; it only controls whether driver options are applied.

### Standard layout

- Applies the mount type's `Driver` and `DriverOptionsJson`.
- Intended for persistent, shared storage across Swarm nodes (e.g., NFS-backed named volumes).

### Local layout

- Ignores driver options; useful for development or single-node installs.

## Revision-level volume fields

A `GameTypeRevision` volume is now a template only. It references a `MountType` by key. Driver, path templates, and driver options are inherited from the `MountTypeConfig` at resolution time.

| Field | Effect |
|---|---|
| `Source` | Container path where the mount is attached and also the `{Source}` token value. |
| `Usage` | Logical label such as `worlds`, `config`, `mods`. |
| `MountType` | Code that must match a row in `MountTypeConfigs`. |
| `ReadOnly` | Mounts the volume read-only. |
| `OwnerUid` / `OwnerGid` / `Permissions` | Optional ownership/permissions for bind-mount directory initialization. |
| `Required` | Whether the server must define this volume. |

The following fields have been removed from the revision template and now live on the mount type configuration or the resolved `GameServerVolume` snapshot:

- `Driver`
- `DriverOptionsJson`
- `SubPathOverride`
- `InitMode`
- `SeedSourcePath`

Because `GameServerVolume` snapshots are immutable, changing `MountTypeConfig` or the `GameTypeRevision` volumes after a server exists does not alter that server's stored mount points. Existing snapshots keep these values so the mount can be recreated exactly as originally resolved.

## Changing settings after servers exist

Changing a `MountTypeConfig` does **not** modify already deployed servers or already persisted `GameServerVolume` snapshots. It only affects:

- validation previews,
- new server creates,
- new volumes added to an existing server during an update.

Existing snapshots remain untouched so that deployed services keep their current mounts, even if the mount type definition is later edited or removed.

## Deployment flow

```text
MountTypeConfigs  ←── seeded defaults / user-defined config
	   │
GameTypeRevision.Volumes
	   │
	   ▼
Server request (layout + settings)
	   │
	   ▼
VolumeSetupResolver.ResolveForCreate / ResolveForUpdate
	   │
	   ▼
GameServerVolume snapshots stored per server
	   │
	   ▼
GameServerDeploymentService.BuildDockerMounts
	   │
	   ▼
Agent receives MountConfig and applies mount + ownership/permissions
```

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| "Mount type configuration for 'x' was not found" | The `GameTypeVolume.MountType` value does not match a configured mount type. Open `/settings/mount-types` and add a configuration with the matching key. |
| Resolved volumes point to back-slash paths on Windows | `VolumeSetupResolver` normalizes to forward slashes; ensure your `SourcePathTemplate` does not end with a backslash. |
| NFS mount fails in Docker | Verify `addr=<host>` is present and reachable from Swarm worker nodes for the configured `DriverOptionsJson`. Check that the NFS share is exported with correct permissions. |
| Local layout still uses driver options | Driver options are intentionally omitted for local layout; ensure the server is saved with `VolumeBindingLayout = local`. |
| Changes not reflected immediately | `MountTypeConfig` values are cached per resolver scope. New scopes pick up the latest values. |

## See also

- [V2 Volume Setup Deep Dive](V2-Volume-Setup.md)
- [V2 GameType Assembly Instructions](V2-GameType-Assembly-Instructions.md)
