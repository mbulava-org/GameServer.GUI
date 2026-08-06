# Deployment Preview & Live Port Validation

How the Create/Edit Server page shows exactly what will be deployed, and how port conflicts are caught before you save.

---

## Why This Exists

Deployment failures used to only surface *after* a Swarm service was created, which meant debugging through orchestrator logs. The **Deployment Preview** tab renders the fully-calculated service specification from the same code path used by the real deployment, so configuration errors are visible in the editor.

---

## Deployment Preview

**Endpoint:** `POST /api/v2/gameservers/preview`
**Backend:** `GameServerSpecBuilder` → `GameServerDeploymentPreviewDto`
**UI:** Deployment Preview tab in `GameServerEditorV2.razor`

The preview is a **dry run**. It builds the same `ServiceCreateParameters` the deployment service would send to Docker, but never contacts the Docker daemon.

### What the preview shows

| Section | Contents |
|---|---|
| **Service** | Service name, server id, game type key, image reference, version tag, TTY flag |
| **Labels** | The full `gameserver.docker.*` label set from `ServiceLabels` |
| **Networks** | Each attached network with its driver and purpose |
| **Environment Variables** | Key, **post-calculation value**, raw stored value, data type, category |
| **Ports** | Container port, published port, protocol, publish mode, description |
| **Volumes** | Volume name, container path, source, mount type, driver, driver options, ownership, permissions |
| **Issues** | Blocking validation problems |
| **Notices** | Non-blocking notes explaining gaps in the generated spec |
| **Raw Spec** | Indented JSON of the exact `ServiceCreateParameters` |

### Environment variable resolution

Each variable reports both `RawValue` and the resolved `Value`, plus two flags:

- `IsExpanded` — server-variable token expansion changed the value
- `UsesDefault` — the value came from the revision default rather than a per-server override

This makes it obvious whether `{Name}` style tokens actually resolved, and which settings you have not overridden. Because `GameServerSpecBuilder` and `GameServerDeploymentService` share `ServerVariableExpander`, the preview value is guaranteed to match what the container receives.

---

## Live Port Validation

Port settings are validated continuously while you edit, not only on save.

### Port setting synchronization

Ports are **fixed by the revision** — you cannot add or remove port rows in the editor. Editing works in two linked directions:

1. Changing a **published port** field updates the `port` setting that is its **primary direct mapping**.
2. Changing a **port setting value** updates the corresponding published port row.

If a published port is `0`, it defaults to the container port. Related offset/multiplier mappings derive their port from the primary direct mapping, so you never enter those values manually.

### Availability checking

**Endpoint:** `POST /api/v2/gameservers/ports/availability`

The editor debounces port edits and posts the current published port/protocol set:

```json
{
  "serverId": "mc-01",
  "ports": [ { "portId": 12, "port": 25565, "protocol": "tcp" } ]
}
```

The response returns per-port results:

```json
{
  "ports": [
	{ "portId": 12, "port": 25565, "protocol": "tcp", "isAvailable": false,
	  "reason": "Published port 25565/tcp is already used by service gs_survival." }
  ]
}
```

Key behaviors:

- `serverId` causes the server's **own** currently published ports to be ignored, so editing an existing server does not report a conflict with itself.
- Conflicts are detected across **all** managed GameServer services, not just the one being edited.
- When a setting has `ValidateRelatedPortsAvailability` enabled, offset/multiplier-derived ports are checked too.
- `AutoAllocatePort` settings can be assigned a free published port automatically.

### Save gating

The editor's `CanSave` state is false while any of the following hold:

- a required setting is empty
- an availability check is in flight
- any checked port reported `IsAvailable = false`

Blocking issues are rendered outside the tab set so they are visible regardless of the active tab.

---

## Validation vs. Preview vs. Save

| Step | Endpoint | Purpose |
|---|---|---|
| Validate | `POST /api/v2/gameservers/validate` | Field-level and setting-level rule checks |
| Port availability | `POST /api/v2/gameservers/ports/availability` | Published port conflict detection |
| Preview | `POST /api/v2/gameservers/preview` | Full dry-run service spec |
| Save | `POST` / `PUT /api/v2/gameservers` | Persist the server |

Save always runs validation server-side, so the client-side gating is a UX improvement rather than the security boundary.

---

## Related Documentation

- **[V2 GameServer Lifecycle](V2-GameServer-Lifecycle.md)** - Create, edit, and view V2 servers
- **[V2 Ports & Web Hosts](V2-Ports-And-WebHosts.md)** - Defining revision ports and mappings
- **[V2 GameType Settings & Metadata](V2-GameType-Settings-And-Metadata.md)** - Setting data types
- **[V2 Volume Setup](V2-Volume-Setup.md)** - How volumes in the preview are resolved
