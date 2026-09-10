# GameType UI Extensions

GameType UI Extensions let a `GameTypeRevision` declare additional Blazor
component tabs that appear on the server details page. This is how
protocol-specific administration UIs (Palworld REST API, future RCON tabs,
etc.) plug into the GUI without editing the built-in tabs.

## Concepts

- **Descriptor** — a lightweight record stored on `GameTypeRevision.UiExtensionsJson`
  describing the component to render.
- **Resolver** (`IGameTypeExtensionResolver`) — resolves descriptors to real
  `Type` instances at runtime with a whitelist and strict validation.
- **Fallback** (`NotYetImplementedTab`) — rendered when a descriptor cannot be
  resolved (unknown type, non-component type, disallowed assembly, etc.).
- **Extension component** — a normal Blazor component that implements
  `IGameTypeExtensionComponent` and accepts `GameServerDetail Server` and
  `IReadOnlyDictionary<string, string>? ExtensionParameters`.

## Descriptor Schema

`UiExtensionsJson` is a JSON array of descriptor objects:

```json
[
  {
	"componentTypeName": "GameServer.Web.Components.Server.Extensions.PalworldApiTab",
	"assemblyName": "GameServer.Web",
	"title": "Palworld API",
	"icon": "cloud",
	"order": 10,
	"parameters": {
	  "docsUrl": "https://tech.palworldgame.com/dedicated-server-rest-api"
	}
  }
]
```

| Field               | Required | Notes                                                                    |
| ------------------- | -------- | ------------------------------------------------------------------------ |
| `componentTypeName` | Yes      | Fully qualified type name. Must implement `IComponent`.                  |
| `assemblyName`      | No       | If omitted, all whitelisted assemblies are searched.                     |
| `title`             | Yes      | Tab label.                                                               |
| `icon`              | No       | Radzen/Material icon name. Defaults to `extension`.                      |
| `order`             | No       | Sort order (ascending). Ties break by `title`.                           |
| `parameters`        | No       | String-to-string map surfaced as `ExtensionParameters` to the component. |

Maximum of **10 descriptors per revision** (enforced in the editor).

## Whitelist Configuration

The resolver only searches assemblies listed in
`GameTypeExtensions:AllowedAssemblies` in `appsettings.json`. The default is
`GameServer.Web`:

```json
{
  "GameTypeExtensions": {
	"AllowedAssemblies": [ "GameServer.Web", "MyCompany.GameExtensions" ]
  }
}
```

Any additional assemblies must already be loaded into the host process (e.g.
project-referenced or ship in the same output). The resolver does **not**
dynamically load assemblies from disk based on database content — this is a
deliberate security decision.

## Authoring an Extension Tab

1. Add a Blazor component under
   `src/GameServer.Web/Components/Server/Extensions/` (or another whitelisted
   assembly).
2. Implement `IGameTypeExtensionComponent` (marker interface).
3. Declare parameters:

   ```csharp
   [Parameter] public GameServerDetail? Server { get; set; }
   [Parameter] public IReadOnlyDictionary<string, string>? ExtensionParameters { get; set; }
   ```

4. Read server-side settings from `Server.Settings` (never from browser input)
   for anything sensitive such as admin passwords.
5. Register any protocol-specific client (`IHttpClientFactory`-backed) in
   `Program.cs` and inject it into the component.
6. Add the descriptor to the target `GameTypeRevision` — either through the
   **UI Extensions** tab in the GameType editor or in a portable import JSON
   (`docs/samples/gametype-imports/palworld-dedicated.portable.json` is the
   canonical example).

## Protocol Client Pattern

The Palworld tab is the reference implementation:

- `IPalworldApiClient` — interface with per-request `PalworldRequestContext`
  (service name, port, admin password).
- `PalworldApiClient` — server-side implementation using
  `IHttpClientFactory`, Basic authentication, and structured logging with
  credentials redacted.
- `INodeAgentExtensionProxy` — fallback route for setups where the GUI cannot
  reach the game service directly. Currently `NotImplementedNodeAgentExtensionProxy`;
  swap in a real implementation as node-agent HTTP forwarding lands.

New protocol clients (RCON, other REST APIs) should follow the same shape:
per-request context, server-side only, no browser-exposed secrets.

## RCON Extension

`RconTab` is a generic Source-RCON (Valve protocol) console usable by any
GameType exposing an RCON port (Palworld, Minecraft Java, Source engine games):

- `IRconClient` / `RconClient` — raw TCP client with per-command
  connect/auth/execute/disconnect, connect and read timeouts, and structured
  `RconCommandResult` errors. The password is never logged.
- Settings resolved from `Server.Settings`: `RCON_ENABLED` (truthy gate),
  `RCON_PORT`, and `RCON_PASSWORD` (falls back to `ADMIN_PASSWORD`).
- Optional descriptor parameter `presetCommands` — comma-separated list
  rendered as one-click buttons (e.g. `"Info, ShowPlayers, Save"`).

Example descriptor:

```json
{
  "componentTypeName": "GameServer.Web.Components.Server.Extensions.RconTab",
  "assemblyName": "GameServer.Web",
  "title": "RCON",
  "icon": "terminal",
  "order": 20,
  "parameters": { "presetCommands": "Info, ShowPlayers, Save" }
}
```


## Security Notes

- **Assembly whitelist is authoritative.** DB-supplied `assemblyName` values
  outside the whitelist are always rejected with a fallback tab.
- **No reflection into disk.** The resolver only considers
  `AppDomain.CurrentDomain.GetAssemblies()`.
- **`IComponent` is required.** Non-component types are rejected.
- **Secrets stay server-side.** Admin passwords are read from `Server.Settings`
  and used to build outbound HTTP requests inside `GameServer.Web`. They are
  never sent to the browser.
- **Descriptors are user-editable metadata.** Treat them as untrusted input;
  never `eval` or otherwise interpret their values beyond the documented schema.

## Fallback Design

When a descriptor cannot be resolved, `NotYetImplementedTab` renders in its
place with:

- The descriptor's title/icon,
- A human-readable failure reason from `GameTypeExtensionResolution.FailureReason`,
- A copy-to-clipboard action for the raw descriptor JSON to help operators
  diagnose typos or misconfigured whitelists.

This lets a GameType ship extension descriptors before their implementing
component is deployed, without breaking the server details page.

## Testing

Automated coverage lives in `tests/GameServer.Web.Tests`:

- `Services/V2/GameTypeExtensionResolverTests.cs` — whitelist enforcement,
  unknown-type failures, non-component rejection, ordering, empty inputs.
- `Services/Extensions/PalworldApiClientTests.cs` — URL composition,
  Basic-auth header, JSON body serialization, node-agent fallback on transport
  failure.
- `Services/Extensions/RconClientTests.cs` — Source-RCON packet framing
  (encode/decode round-trip, UTF-8 bodies) and request validation.

Component-level rendering tests are deferred until the extension UIs stabilize,
consistent with the project guideline of deferring GUI tests during
look-and-feel iteration.
