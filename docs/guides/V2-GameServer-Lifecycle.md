# V2 GameServer Lifecycle Guide

This guide describes the current V2 server management flow. The V2 UI is the active path in the navigation menu; legacy `/servers` routes still exist but are no longer promoted.

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
7. Click **Create**. The frontend validates first via `POST /api/v2/gameservers/validate`, then creates the server via `POST /api/v2/gameservers`.

Port mappings and volumes are not configured per server; they are derived from the selected `GameTypeRevision` at deployment time.

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

Port and volume configuration remains derived from the selected revision. If you change the revision, ports and volumes are recalculated on the next deployment.

## Deleting a Server

V2 servers use soft delete. After deletion, the server remains in the database with `IsDeleted = true`. Use the **include deleted** filter on the list page to see deleted servers. Hard/permanent deletion is not yet implemented in the V2 API.

## API Endpoints

| Method | Endpoint | Purpose |
|---|---|---|
| GET | `/api/v2/gameservers?includeDeleted=false` | List servers |
| GET | `/api/v2/gameservers/{serverId}` | Get one server |
| POST | `/api/v2/gameservers/validate` | Validate a create request |
| POST | `/api/v2/gameservers` | Create server |
| PUT | `/api/v2/gameservers/{serverId}` | Update server |

## Known Limitations

- Volumes are derived from the revision; per-server volume source or driver overrides are not yet available in the create/edit flow.
- Soft delete is used; there is no V2 hard-delete endpoint yet.
- Server start/stop actions are currently surfaced through service-level operations in the background, not as explicit V2 endpoints.

## Related Documentation

- [Architecture Overview](../ARCHITECTURE.md)
- [Current Features](../CURRENT-FEATURES.md)
- [Testing Quick Reference](../TESTING-QUICK-REFERENCE.md)
