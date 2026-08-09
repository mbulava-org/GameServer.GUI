# V2 GameServer Lifecycle Guide

This guide describes the current V2 server management flow. The V2 UI is the active path in the navigation menu.

## Pages & Routes

| Page | Route | Component |
|---|---|---|
| Server List | `/gameservers-v2` | `GameServerManagerV2.razor` |
| Server Details | `/gameservers-v2/{serverId}` | `GameServerDetailsV2.razor` |
| Create Server | `/gameservers-v2/new` | `GameServerEditorV2.razor` |
| Edit Server | `/gameservers-v2/{serverId}/edit` | `GameServerEditorV2.razor` |

## Creating a V2 Server

1. Open `/gameservers-v2`.
2. Click **New V2 Server**.
3. Select a GameType that has a published revision.
4. Pick the published revision. Only revisions that are published can be selected.
5. Enter the server name and an optional description. The service name is derived from the name.
6. Provide per-server setting overrides in `GameServerSettingFieldV2`. Required settings are marked.
7. Adjust published ports if needed. Availability is checked live and the **Create** button stays disabled while a conflict is outstanding.
8. Open the **Deployment Preview** tab to review the fully-calculated service spec before committing.
9. Click **Create**. The frontend validates first via `POST /api/v2/gameservers/validate`, then creates the server via `POST /api/v2/gameservers`.

Volumes are not configured per server; they are derived from the selected `GameTypeRevision` at deployment time. Port rows are fixed by the revision — you can change published port numbers but cannot add or remove mappings.

See [Deployment Preview & Live Port Validation](V2-Deployment-Preview-And-Port-Validation.md) for details on both surfaces.

## Viewing Servers

The server list shows:

- Name, service name, and status badge
- Selected revision (version tag and image reference)
- Soft-delete status (`IsDeleted`)
- An **include deleted** filter

Click a row to open the details page.

## Server Details

The details page displays:

- Server identity (id, name, service name)
- Lifecycle timestamps
- Selected revision summary
- Soft-delete flag

Live container interactions (logs, terminal, console, resource monitoring) are available through the server's container context. Logs and terminal connect to the SignalR hubs running in `GameServer.Docker` and are routed through Node Agents; they do not require the Primary Service to connect to the Docker daemon.

## Editing a Server

From the list or details page you can navigate to the editor. The editor lets you change:

- Name
- Selected published revision
- Per-server setting overrides

Port configuration is derived from the selected revision. Volumes are resolved from the revision's volume templates plus the matching `MountTypeConfig` entry at create/update time. If you change the revision, ports are recalculated and any new volumes are resolved on the next deployment; existing `GameServerVolume` snapshots remain unchanged.

## Deleting a Server

V2 servers use soft delete. After deletion, the server remains in the database with `IsDeleted = true`. Use the **include deleted** filter on the list page to see deleted servers. Hard/permanent deletion is not yet implemented in the V2 API.

## API Endpoints

| Method | Endpoint | Purpose |
|---|---|---|
| GET | `/api/v2/gameservers?includeDeleted=false` | List servers |
| GET | `/api/v2/gameservers/{serverId}` | Get one server |
| POST | `/api/v2/gameservers/validate` | Validate a create request |
| POST | `/api/v2/gameservers/preview` | Dry-run the Swarm service spec |
| POST | `/api/v2/gameservers/ports/availability` | Check published port conflicts |
| POST | `/api/v2/gameservers` | Create server |
| PUT | `/api/v2/gameservers/{serverId}` | Update server |

## Known Limitations

- Volumes are resolved from revision templates plus `MountTypeConfig`; per-server snapshot overrides are not available in the create/edit flow.
- Soft delete is used; there is no V2 hard-delete endpoint yet.
- Server start/stop actions are currently surfaced through service-level operations in the background, not as explicit V2 endpoints.

## Related Documentation

- [Deployment Preview & Live Port Validation](V2-Deployment-Preview-And-Port-Validation.md)
- [V2 GameType Settings & Metadata](V2-GameType-Settings-And-Metadata.md)
- [V2 Ports & Web Hosts](V2-Ports-And-WebHosts.md)
- [Architecture Overview](../ARCHITECTURE.md)
- [Current Features](../CURRENT-FEATURES.md)
- [Testing Quick Reference](../TESTING-QUICK-REFERENCE.md)
