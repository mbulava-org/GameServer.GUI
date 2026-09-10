# GameType UI Extensions — Implementation Plan

> **Status:** Approved for execution
> **Design reference:** v3 (with §9.3 revision) — see conversation transcript / design review doc
> **Scope:** `GameServer.Web` (Blazor) + `GameServer.API` (V2 `GameTypeRevision` model)
> **First concrete extension:** Palworld REST API

This document is the auditable implementation plan. Work executed against this plan should reference the step IDs below (`step-1` … `step-12`). Deviations should be recorded here (append a **Change Log** entry at the bottom).

---

## Summary

Implement a data-driven, per-`GameTypeRevision` Blazor UI extension mechanism in `GameServerDetailsV2`, plus the first concrete extension: the Palworld REST API tab.

---

## Key design points (locked)

- Descriptors stored on **`GameTypeRevision.UiExtensionsJson`** (new dedicated column, EF migration — additive, nullable, auto-applied on startup).
- Resolver in `GameServer.Web` enforces an **`appsettings` assembly whitelist** (default `["GameServer.Web"]`).
- `GameServerDetail` DTO exposes **both** raw `UiExtensionsJson` **and** typed `UiExtensions`; the resolver still runs client-side to enforce the whitelist.
- Extension tabs render **strictly after** built-in tabs via `<DynamicComponent>`.
- Missing/invalid components resolve to a **`NotYetImplementedTab`** with a copy-descriptor UX.
- Marker interface **`IGameTypeExtensionComponent`** defined now (empty, reserved for future capability negotiation).
- **Protocol clients live in `GameServer.Web`** (server-side); the browser never sees credentials.
- Palworld client reaches the container via swarm service DNS: `http://{server.ServiceName}:{REST_API_PORT}`.
- **Node Agent HTTP-proxy fallback** is designed but stubbed in v1 (returns a clear "not implemented" error); the real proxy is a follow-up PR.
- **RCON extension** is a follow-up PR, not part of this work.

---

## Files to add / modify

**Backend**
- `src/GameServer.API/Models/V2/GameType.cs` — add `GameTypeRevision.UiExtensionsJson`.
- `src/GameServer.API/Data/V2/GameServerV2DbContext.cs` — column mapping.
- `src/GameServer.API/Data/V2/Migrations/…` — new migration `AddGameTypeRevisionUiExtensions`.
- `src/GameServer.API/Dtos/V2/GameTypesDtos.cs` — descriptor DTO + revision DTO fields + `GameServerDetail` DTO fields.
- `src/GameServer.API/Services/V2/GameServerQueryService.cs` (and revision query service) — populate raw + typed on read.

**GUI framework**
- `src/GameServer.Web/Configurations/GameTypeExtensionsOptions.cs`
- `src/GameServer.Web/Services/V2/GameTypeExtensionResolver.cs` (+ interface)
- `src/GameServer.Web/Components/Server/Extensions/IGameTypeExtensionComponent.cs`
- `src/GameServer.Web/Components/Server/Extensions/NotYetImplementedTab.razor`
- `src/GameServer.Web/Models/V2/GameTypeUiExtensionDescriptor.cs`
- `src/GameServer.Web/Models/V2/GameTypeV2Models.cs` + `GameServerV2Models.cs` — new fields.
- `src/GameServer.Web/Services/V2/…` — hydrate the new fields in API client mapping.
- `src/GameServer.Web/Components/Pages/Servers/GameServerDetailsV2.razor` — dynamic extension tabs.
- `src/GameServer.Web/Components/Pages/GameTypes/GameTypeDetailsV2.razor` — wire in editor.
- `src/GameServer.Web/Components/Pages/GameTypes/Components/V2/GameTypeRevisionUiExtensionsEditor.razor` — new editor.
- `src/GameServer.Web/Program.cs` — DI registration + options binding + typed `HttpClient`.
- `appsettings.json` — `GameTypeExtensions:AllowedAssemblies`.

**Palworld extension**
- `src/GameServer.Web/Services/Extensions/IPalworldApiClient.cs` + `PalworldApiClient.cs`
- `src/GameServer.Web/Components/Server/Extensions/PalworldApiTab.razor`
- Seed descriptor for the `palworld-dedicated` current revision.

**Docs**
- `docs/guides/GameType-UI-Extensions.md`
- Update `docs/CURRENT-FEATURES.md`

**Tests**
- `tests/GameServer.Web.Tests/…` — resolver, DTO round-trip, tab rendering, Palworld client URL/auth, disabled-state rendering.

---

## Non-goals in this PR set

- Node Agent proxy endpoint (stub only).
- RCON client + tab.
- "Test Resolve" preview endpoint.
- Attribute-based auto-registration.

---

## Risks / watch-outs

- EF migration must be additive & auto-applied on startup (project rule). Nullable column, no backfill.
- Resolver must never load new assemblies; only `AppDomain.CurrentDomain.GetAssemblies()`.
- Redact `Authorization` header + known secret setting keys (`ADMIN_PASSWORD`, `RCON_PASSWORD`) in all extension-client logs.
- `<DynamicComponent>` parameter dictionary keys must match component `[Parameter]` names exactly (`Server`, `ExtensionParameters`).
- Cap descriptors at 10 per revision (save-time validation).
- Palworld tab must self-disable when `REST_API_ENABLED` is not truthy.

---

## Steps

### step-1 — GameTypeRevision model + EF migration
Add `UiExtensionsJson` to `GameTypeRevision` model and EF mapping. Modify `src/GameServer.API/Models/V2/GameType.cs` and `src/GameServer.API/Data/V2/GameServerV2DbContext.cs`; generate EF migration `AddGameTypeRevisionUiExtensions`; verify startup auto-apply.

### step-2 — V2 API DTOs
Add `UiExtensionsJson` (raw) and `UiExtensions` (typed `List<GameTypeUiExtensionDescriptorDto>`) to `GameTypeRevisionDto` and `GameServerDetail` DTO in `src/GameServer.API/Dtos/V2/GameTypesDtos.cs`. Populate on read in `src/GameServer.API/Services/V2/GameServerQueryService.cs` and the revision query service. Ensure save/update endpoints persist the JSON.

### step-3 — Mirror descriptor + DTO types in `GameServer.Web`
Add `src/GameServer.Web/Models/V2/GameTypeUiExtensionDescriptor.cs` and extend `GameServerV2Models.cs` / `GameTypeV2Models.cs` with the new fields. Update the V2 API service client mapping in `src/GameServer.Web/Services/V2/…` to hydrate them.

### step-4 — Extension framework primitives in `GameServer.Web`
Create `IGameTypeExtensionComponent`, `GameTypeExtensionsOptions`, `IGameTypeExtensionResolver` + `GameTypeExtensionResolver` (whitelist enforcement, no dynamic assembly loading, fallback on any failure with `FailureReason`), and the fallback `NotYetImplementedTab.razor`. Register options + resolver in `src/GameServer.Web/Program.cs`. Add `GameTypeExtensions:AllowedAssemblies` to `appsettings.json` (default `["GameServer.Web"]`).

### step-5 — Wire extension tabs into `GameServerDetailsV2.razor`
Inject `IGameTypeExtensionResolver`, resolve on server load, render tabs after all built-ins using `<DynamicComponent>` with parameters `{ "Server": server, "ExtensionParameters": ext.Parameters }`. No changes to existing tab ordering.

### step-6 — Revision UI-extensions editor
Add `src/GameServer.Web/Components/Pages/GameTypes/Components/V2/GameTypeRevisionUiExtensionsEditor.razor` (Radzen data grid: Title, ComponentTypeName, AssemblyName, Icon, Order, Parameters key/value grid). Enforce required fields and the 10-descriptor cap. Wire into `GameTypeDetailsV2.razor` following the existing master-detail pattern; cross-tab validation errors surface outside the tabs.

### step-7 — Palworld protocol client
Add `src/GameServer.Web/Services/Extensions/IPalworldApiClient.cs` + `PalworldApiClient.cs` using typed `HttpClient` (registered with `AddHttpClient`). Compute URL from `Server.ServiceName` + `Server.Settings["REST_API_PORT"]`. Basic auth from `Server.Settings["ADMIN_PASSWORD"]` per request. Redact secrets in logs. Typed methods (Info, Players, Kick, Ban, Save, Shutdown, Broadcast) + generic `SendAsync`. Leave a `TODO` fallback branch calling a not-yet-implemented `INodeAgentExtensionProxy` that throws a clear `NotImplementedException`.

### step-8 — Palworld tab component
Add `src/GameServer.Web/Components/Server/Extensions/PalworldApiTab.razor` implementing `IGameTypeExtensionComponent`, accepting `[Parameter] GameServerDetail Server` and `[Parameter] IReadOnlyDictionary<string,string>? ExtensionParameters`. Render disabled/informational panel when `REST_API_ENABLED` is falsy; otherwise render status card, shortcut action forms, and generic API explorer, all through `IPalworldApiClient`.

### step-9 — Seed the Palworld descriptor
Update the `palworld-dedicated` current-revision seed / data-migration path so `UiExtensionsJson` contains:
```json
[{ "ComponentTypeName": "GameServer.Web.Components.Server.Extensions.PalworldApiTab",
   "Title": "Palworld API", "Icon": "cloud", "Order": 10 }]
```
If no seed pathway exists, document how to set it via the new editor and skip auto-seed.

### step-10 — Tests
Add coverage under `tests/GameServer.Web.Tests/…`: resolver (valid, unknown type, non-component, disallowed assembly, malformed JSON, empty/null, ordering, ambiguity), descriptor JSON round-trip, options binding, `PalworldApiClient` URL + Basic-auth header composition + log redaction, `NotYetImplementedTab` render, `GameServerDetailsV2` renders N extension tabs after built-ins with stubbed resolver, `PalworldApiTab` disabled-state rendering. Defer full GUI look-and-feel tests per project rule.

### step-11 — Documentation
Add `docs/guides/GameType-UI-Extensions.md` (authoring guide, descriptor schema, whitelist config incl. multi-assembly sample, protocol-client pattern, security notes, fallback design) and update `docs/CURRENT-FEATURES.md`.

### step-12 — Manual QA + build
Run `dotnet build` on the solution. Smoke-test in Visual Studio: open a Palworld server, verify tab appears, disabled state when `REST_API_ENABLED=false`, action forms round-trip when enabled, bogus descriptor yields `NotYetImplementedTab` with reason.

---

## Change Log

| Date | Step(s) | Change | Notes |
|------|---------|--------|-------|
| _initial_ | all | Plan created | v3 design approved |
