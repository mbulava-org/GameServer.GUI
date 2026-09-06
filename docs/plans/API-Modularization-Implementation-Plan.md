# GameServer.API Modularization — Implementation Plan

> Status: **Phases 1–6 completed; §7 host promotion delivered (Orchestration.Host, Monitoring.Host); §7 follow-up items tracked below**
> Last updated: 2026-09-05
> Related docs: [ARCHITECTURE.md](../ARCHITECTURE.md), [CURRENT-FEATURES.md](../CURRENT-FEATURES.md), [samples/docker-stack.modular.yml](../samples/docker-stack.modular.yml)

## 1. Goal

`GameServer.API` has grown to contain several distinct areas of responsibility
(agent orchestration, deployment, catalog/persistence, monitoring). This plan
decomposes it into focused class-library modules while keeping
**`GameServer.API` as the single access point that `GameServer.Web` talks to**.

This is a *modular monolith* decomposition — not microservices. Each module
becomes a cheap candidate for promotion to its own deployable service later
(see §7) without another refactor.

## 2. Target Project Layout

```
src/
  GameServer.API              (thin host: Controllers, Hubs, Program.cs, DI wiring)
  GameServer.Contracts        (NEW: interfaces, DTOs, models, ServiceLabels constants)
  GameServer.Catalog          (NEW: EF Core DbContext, entities, V2 repositories, setup detection)
  GameServer.Orchestration    (NEW: node agent discovery/registry/client, terminal sessions)
  GameServer.Deployment       (NEW: spec builder, service operations, port allocation, commands)
  GameServer.Monitoring       (NEW: resource monitor, query/aggregation services)
  GameServer.API.Client       (unchanged — generated client used by Web)
  GameServer.Web              (unchanged — still only references GameServer.API.Client)
  GameServer.Docker.Agent     (unchanged)
  GameServer.DB.PostgreSql    (unchanged; referenced by GameServer.Catalog)
```

### Dependency rules

```
API ──▶ Orchestration ──▶ Contracts
API ──▶ Deployment    ──▶ Contracts, Orchestration, Catalog
API ──▶ Catalog       ──▶ Contracts
API ──▶ Monitoring    ──▶ Contracts, Orchestration
Web ──▶ API.Client only (never module projects)
```

- Modules never reference each other except as shown above.
- Modules never reference `GameServer.API`.
- Hubs remain in `GameServer.API` but depend only on module interfaces
  (`INodeAgentDiscovery`, etc.) — per ARCHITECTURE.md, hubs never touch
  `IDockerClient` directly.

## 3. Module Responsibilities

### GameServer.Contracts
Shared, dependency-free types:
- `Interfaces/` — `INodeAgentDiscovery`, `IAgentRegistry`, `IServiceOperations`, repository interfaces
- `Dtos/V2/` — all API DTOs (GameTypes, GameServers, etc.)
- `Models/` — `NodeAgentModels` and other shared models
- `Constants/ServiceLabels.cs` — keep namespace `GameServer.Docker.Constants` to avoid breaking existing usage

### GameServer.Catalog
Persistence and metadata:
- `Data/V2/` — `GameServerV2DbContext`, entities, EF migrations (auto-apply on startup per project rules)
- `Repositories/V2/` — `GameTypeRepository`, `GameServerRepository`, `MountTypeConfigRepository`
- `Services/V2/Detection/GameTypeSetupDetectionService`
- Mount-type default seeding

### GameServer.Orchestration
Node agent connectivity and realtime plumbing:
- `NodeAgentClient`, `NodeAgentDiscoveryService`
- `AgentRegistryService`, `UdpAgentRegistryService`
- `TerminalSessionManager`

### GameServer.Deployment
Swarm service lifecycle:
- `GameServerSpecBuilder`, `ServiceOperationsViaAgent`
- `GameServerCommandService`, `GameServerValidationService`
- `PortAllocator` (backend port/protocol validation across instances, per project rules)

### GameServer.Monitoring
Read-only aggregation:
- `ServerResourceMonitor`, `GameServerQueryService`

### GameServer.API (after)
- Controllers (V1 + V2), SignalR Hubs, `Program.cs`
- One `AddGameServerModules()`-style DI extension per module, composed at startup
- OpenAPI generation unchanged so `GameServer.API.Client` regeneration is unaffected

## 4. Implementation Phases

Each phase must end with a green build and passing test suites
(`GameServer.API.Tests`, `GameServer.API.Client.Tests`, `GameServer.Integration.Tests`).

### Phase 1 — Extract `GameServer.Contracts` (lowest risk, breaks the coupling knot)
1. Create the project (net10.0 class library).
2. Move `Interfaces/`, `Dtos/`, `Models/`, `Constants/ServiceLabels.cs` — **keep original namespaces** so this is a file move, not a rename.
3. Reference Contracts from API; fix compile errors.
4. Build + run all tests.

### Phase 2 — Extract `GameServer.Catalog`
1. Create project; reference Contracts and EF Core / PostgreSQL packages.
2. Move `Data/V2/`, `Repositories/V2/`, EF migrations, setup detection service.
3. Add `AddCatalogModule(IServiceCollection, IConfiguration)` DI extension; keep startup migration-check behavior identical.
4. Build + tests; verify migrations still apply on startup.

### Phase 3 — Extract `GameServer.Orchestration`
1. Create project; move agent client/discovery/registry services and `TerminalSessionManager`.
2. Hubs stay in API and consume `INodeAgentDiscovery` from Contracts.
3. Add `AddOrchestrationModule(...)` DI extension (including hosted services for UDP registry).
4. Build + tests, including agent registration integration tests.

### Phase 4 — Extract `GameServer.Deployment`
1. Create project referencing Contracts, Orchestration, Catalog.
2. Move spec builder, service operations, command/validation services, `PortAllocator`.
3. Add `AddDeploymentModule(...)` DI extension.
4. Build + tests, including deploy/update integration tests.

### Phase 5 — Extract `GameServer.Monitoring`
1. Create project referencing Contracts, Orchestration.
2. Move `ServerResourceMonitor`, `GameServerQueryService`; add DI extension.
3. Build + tests.

### Phase 6 — Cleanup & verification
1. Remove now-empty folders from API; API should contain only Controllers, Hubs, Program.cs, DI composition.
2. Regenerate `GameServer.API.Client` and diff — output must be unchanged (route/DTO shapes stable).
3. Run full test matrix; update `ARCHITECTURE.md` and `CURRENT-FEATURES.md` with the new module map.

## 5. Rules & Constraints

- **No public route or DTO shape changes** — client generation must produce an identical surface.
- **Keep namespaces stable during moves**; renaming (if desired) is a separate follow-up.
- `ServiceLabels` constants remain the only source of Docker label strings.
- Hubs continue to use `INodeAgentDiscovery` for container operations; `DockerServiceHelper` for service management.
- EF rule: any entity change requires a migration; startup applies pending migrations automatically.
- One phase per PR/commit; never leave the solution in a non-building state.

## 6. Risks

| Risk | Mitigation |
|---|---|
| Hidden circular dependencies between areas | Contracts-first extraction; compiler enforces the dependency graph |
| Client regeneration drift | Diff generated client after each phase; DTOs move without shape changes |
| DI lifetime regressions (hosted services, singletons) | Copy registrations verbatim into module extensions; integration tests cover agent registration + deploy paths |
| EF migration discovery after assembly move | Set `MigrationsAssembly` explicitly in Catalog DI extension |

## 7. Future: promoting modules to services (delivered, with follow-up)

Promote only when a concrete pressure exists:
- **Monitoring** first if stats polling adds load — read-only, cleanest boundary. **Delivered as `GameServer.Monitoring.Host`** exposing `/hubs/resources`, `/hubs/serverlogs`, `/hubs/attach` (route names match `GameServer.API` so clients can swap base URIs). It consumes `IAgentRegistry` and `INodeAgentDiscovery` via `AddOrchestrationHttpClientModule` against `GameServer.Orchestration.Host` — no in-process orchestration co-hosting.
- **Deployment** if long-running deploys need queued/worker-based reliability. *(Not yet promoted.)*
- **Orchestration hubs** only if SignalR connection counts require a dedicated realtime service with a backplane. **Delivered as `GameServer.Orchestration.Host`** exposing `/hubs/agentregistration`. The interactive terminal hub (`/hubs/terminal`) still lives in `GameServer.API`; see follow-up.
- **Catalog stays in-process** — splitting the persistence layer adds latency and transaction complexity for little gain. `Monitoring.Host` therefore references `AddCatalogModule(..., skipDbInit: true)` so schema migrations remain owned exclusively by `GameServer.API`.

### API host-mode toggle

`GameServer.API` reads `ModularHostingOptions` (`ModularHosting` section):

| Setting | Default | Modular stack | Effect |
|---|---|---|---|
| `HostHubsInApi` | `true` | `false` | Maps `/hubs/attach`, `/hubs/terminal`, `/hubs/serverlogs`, `/hubs/resources`, `/hubs/agentregistration` in-process when `true`; leaves them to the standalone hosts when `false`. |
| `UseHttpAgentRegistry` | `false` | `true` | When `true`, replaces `AddOrchestrationModule` with `AddOrchestrationHttpClientModule`, so `IAgentRegistry`/`INodeAgentDiscovery` are HTTP clients against `OrchestrationService:BaseUrl`. |

Both defaults preserve the single-host `docker-stack.yml` deployment. The modular sample stack (`docker-stack.modular.yml`) sets both to the modular values.

### Follow-up

1. **`ContainerConsoleHub` + `SignalRTerminalSessionNotifier` relocated to `GameServer.Orchestration`** so `/hubs/terminal` runs in `GameServer.Orchestration.Host` alongside `/hubs/agentregistration`. `GameServer.API` still hosts them in-process when `HostHubsInApi=true`.
2. **API acts as a SignalR gateway** — `GameServer.Web` continues to use a single `GameServerDockerApi__BaseUri`. When modular hosting is enabled, `GameServer.API` uses YARP (`Yarp.ReverseProxy`) to relay `/hubs/*` traffic to `OrchestrationService:BaseUrl` and `MonitoringService:BaseUrl`. The Web UI is unchanged and unaware of the split.
3. **Optional Deployment host** — only if deployment throughput demands it.

