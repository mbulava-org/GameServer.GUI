# V2 Ports, Port Mappings, and Web Hosts Guide

This guide covers the V2 GameType revision editor screens for defining ports, port-to-setting mappings, and web host (reverse-proxy) rules.

## Where to Find It

- **V2 GameType list:** `/gametypes-v2`
- **Edit a GameType/revision:** `/gametypes-v2/{key}`
- **Create a new GameType:** `/gametypes-v2/new`

The relevant tabs are:

- **Ports** - define container ports exposed by the revision
- **Settings** - define settings and how they map to revision ports
- **Web Hosts** - define reverse-proxy path rules and the ports they route to

---

## Ports Tab

**Component:** `GameTypeRevisionPortsEditor.razor`

Each revision port defines an exposed container port/protocol pair:

| Field | Purpose |
|---|---|
| **Container Port** | The port the container listens on (1-65535) |
| **Protocol** | `tcp` or `udp` |
| **Advertised** | The primary connection port; shown to users and used in connection strings |
| **Description** | Human-readable explanation |

Exactly one port should be marked as **Advertised**. If you remove the advertised port, the first remaining port becomes advertised automatically.

---

## Settings Tab - Port Mappings

**Component:** `GameTypeRevisionSettingsEditor.razor`

Settings support a `port` DataType. When a setting is a port setting, it must define **one direct primary mapping** to an existing revision port. You can add additional **related** mappings that use offset or multiplier relationships.

| Mapping Field | Purpose |
|---|---|
| **Role** | `Primary` (direct mapping) or `Related` (offset/multiplier) |
| **Relation** | `Direct`, `Offset`, or `Multiplier` |
| **Target Container Port** | The existing revision port this mapping points to |
| **Target Protocol** | `tcp` or `udp`, matching the target port |
| **Calculation Value** | Offset amount or multiplier factor, depending on Relation |
| **Is Required** | Whether the mapping must be present for the setting to validate |

Rules enforced by the editor:

- One primary direct mapping is required per port setting.
- Related mappings can only point to existing revision ports.
- Non-primary mappings must be offset or multiplier relationships.
- Related mappings derive their calculated published port from the primary setting value selected at server creation time.

---

## Web Hosts Tab

**Component:** `GameTypeRevisionWebHostsEditor.razor`

Web host rules define how the Primary Service's load balancer (currently Traefik) exposes in-container endpoints via path segments. They are derived from the revision at deployment time but can be enabled/disabled per GameServer instance.

| Field | Purpose |
|---|---|
| **Name** | Human-readable name of the web host |
| **Path Segment** | Relative path segment used in the reverse-proxy rule (e.g., `admin/{serverId}`) |
| **Static Port** | Fixed container port when no Port Variable is selected |
| **Port Variable** | A revision setting with a numeric default port; when selected, it overrides Static Port |
| **Description** | Human-readable explanation |
| **Enabled When** | Optional conditional expression controlling per-server enablement |

Supported runtime placeholders in path segments:

- `{serverId}`
- `{name}`
- `{serviceName}`
- `{gameType}`

Use the **From Name** helper to auto-generate a lowercase, hyphenated path segment from the Name field.

---

## Validation

The editor validates:

- Path segments are relative, lowercase, and use only allowed characters plus supported placeholders.
- Web host ports are either a fixed value or derived from a compatible numeric setting.
- Port mappings adhere to the primary/related rules described above.

Cross-tab validation errors are shown on the **Review** tab and must be resolved before publishing the revision.

---

## Relationship to Server Deployment

When a GameServer is created from a published revision:

1. `GameServerPorts` are derived from the revision ports plus the selected setting values.
2. `GameServerSettings` store the user-provided values, including the primary port setting.
3. Web host rules are derived from the revision but enabled per instance via environment variables or other server-specific configuration.

Neither ports, volumes, nor web hosts are persisted inside the `GameServer` V2 entity; they are always computed from the selected `GameTypeRevision`.

---

## Related Documentation

- [V2 GameType Editor Components](V2-GameType-Editor-Components.md)
- [Port Mapping Integration Guide](Port-Mapping-Integration-Guide.md)
- [V2 GameServer Lifecycle](V2-GameServer-Lifecycle.md)
- [Architecture Overview](../ARCHITECTURE.md)
